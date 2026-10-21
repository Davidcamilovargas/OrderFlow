using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;
using Orders.Api.Application;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Api.Infrastructure.RabbitMq;

/// <summary>
/// Consume StockReserved y StockRejected (routing key "stock.*") y actualiza el estado del pedido.
/// </summary>
public class StockResultConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<StockResultConsumer> logger) : BackgroundService
{
    public const string QueueName = "orders.stock-results";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private IConnection? _connection;
    private IChannel? _channel;

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

        _connection = await factory.CreateConnectionAsync("orders-api-stock-results", ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(Messaging.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await _channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await _channel.QueueBindAsync(QueueName, Messaging.Exchange, "stock.*", cancellationToken: ct);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;
        await _channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, ct);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs delivery)
    {
        var channel = _channel!;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<StockResultHandler>();

            switch (delivery.RoutingKey)
            {
                case Messaging.RoutingKeys.StockReserved:
                    await handler.HandleAsync(Deserialize<StockReserved>(delivery), delivery.CancellationToken);
                    break;
                case Messaging.RoutingKeys.StockRejected:
                    await handler.HandleAsync(Deserialize<StockRejected>(delivery), delivery.CancellationToken);
                    break;
                default:
                    logger.LogWarning("Routing key desconocida {RoutingKey}: mensaje descartado.", delivery.RoutingKey);
                    break;
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Mensaje inválido o transición imposible (p. ej. Confirmed -> Rejected): reintentar no lo arreglaría.
            logger.LogError(ex, "Mensaje descartado ({RoutingKey}): {Body}",
                delivery.RoutingKey, Encoding.UTF8.GetString(delivery.Body.Span));
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false);
        }
        catch (Exception ex)
        {
            // Error transitorio (p. ej. base de datos caída): se devuelve a la cola para reintentar.
            logger.LogWarning("Error procesando {RoutingKey}: {Error}. Se reintentará en {Seconds}s.",
                delivery.RoutingKey, ex.Message, RetryDelay.TotalSeconds);
            await Task.Delay(RetryDelay);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private static T Deserialize<T>(BasicDeliverEventArgs delivery) =>
        JsonSerializer.Deserialize<T>(delivery.Body.Span, Messaging.JsonOptions)
        ?? throw new JsonException($"El mensaje no es un {typeof(T).Name} válido.");

    private async Task CloseAsync()
    {
        try
        {
            if (_channel is not null) await _channel.DisposeAsync();
            if (_connection is not null) await _connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error al cerrar la conexión con RabbitMQ.");
        }
        finally
        {
            _channel = null;
            _connection = null;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseAsync();
    }
}