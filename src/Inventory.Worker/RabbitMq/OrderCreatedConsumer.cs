using System.Text;
using System.Text.Json;
using Inventory.Worker.Application;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Inventory.Worker.RabbitMq;

/// <summary>
/// Consume OrderCreated desde RabbitMQ, reserva el stock y publica StockReserved o StockRejected.
/// El mensaje se confirma (ack) solo después de guardar en la base y publicar la respuesta.
/// </summary>
public class OrderCreatedConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    public const string QueueName = "inventory.order-created";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private IConnection? _connection;
    private IChannel? _consumeChannel;
    private IChannel? _publishChannel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reintenta la conexión inicial: el broker puede tardar más que este servicio en arrancar.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(stoppingToken);
                logger.LogInformation("Escuchando la cola {Queue}", QueueName);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("No se pudo conectar a RabbitMQ: {Error}. Reintento en {Seconds}s.",
                    ex.Message, RetryDelay.TotalSeconds);
                await CloseAsync();
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task StartConsumingAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.User,
            Password = settings.Password
        };

        // Si el broker se reinicia, la librería reconecta y vuelve a registrar el consumidor sola.
        _connection = await factory.CreateConnectionAsync("inventory-worker", ct);

        _publishChannel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        _consumeChannel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _consumeChannel.ExchangeDeclareAsync(Messaging.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await _consumeChannel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _consumeChannel.QueueBindAsync(QueueName, Messaging.Exchange, Messaging.RoutingKeys.OrderCreated, cancellationToken: ct);

        // Procesa de a un mensaje por vez: el siguiente no llega hasta confirmar el actual.
        await _consumeChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;
        await _consumeChannel.BasicConsumeAsync(QueueName, autoAck: false, consumer, ct);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs delivery)
    {
        var channel = _consumeChannel!;

        OrderCreated? message;
        try
        {
            message = JsonSerializer.Deserialize<OrderCreated>(delivery.Body.Span, Messaging.JsonOptions);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (message is null)
        {
            // Un mensaje mal formado nunca se va a poder procesar: reintentarlo bloquearía la cola.
            logger.LogError("Mensaje inválido descartado: {Body}", Encoding.UTF8.GetString(delivery.Body.Span));
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ReserveStockHandler>();

            var response = await handler.HandleAsync(message, delivery.CancellationToken);
            await PublishAsync(response, delivery.CancellationToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            // No se confirma: RabbitMQ lo vuelve a entregar. Como el handler es idempotente,
            // el reintento no descuenta stock de nuevo; solo reenvía la respuesta.
            logger.LogWarning("Error procesando el evento {EventId}: {Error}. Se reintentará en {Seconds}s.",
                message.EventId, ex.Message, RetryDelay.TotalSeconds);
            await Task.Delay(RetryDelay);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task PublishAsync(OutgoingMessage message, CancellationToken ct)
    {
        var properties = new BasicProperties
        {
            MessageId = message.MessageId.ToString(),
            Type = message.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _publishChannel!.BasicPublishAsync(Messaging.Exchange, message.RoutingKey, mandatory: true,
            properties, Encoding.UTF8.GetBytes(message.Payload), ct);
    }

    private async Task CloseAsync()
    {
        try
        {
            if (_consumeChannel is not null) await _consumeChannel.DisposeAsync();
            if (_publishChannel is not null) await _publishChannel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error al cerrar la conexión con RabbitMQ.");
        }
        finally
        {
            _consumeChannel = null;
            _publishChannel = null;
            _connection = null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseAsync();
    }
}