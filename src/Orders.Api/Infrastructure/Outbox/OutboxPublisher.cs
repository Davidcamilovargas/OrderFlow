using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderFlow.Contracts;
using Orders.Api.Infrastructure.RabbitMq;
using RabbitMQ.Client;

namespace Orders.Api.Infrastructure.Outbox;

/// <summary>
/// Publica en RabbitMQ los eventos pendientes del outbox. Si el broker no está disponible,
/// los eventos permanecen en la base de datos y se reintentan en el siguiente ciclo.
/// </summary>
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    TimeProvider time,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = PollInterval;
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("No se pudo publicar el outbox: {Error}. Reintento en {Seconds}s.",
                    ex.Message, RetryDelay.TotalSeconds);
                await CloseChannelAsync();
                delay = RetryDelay;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var channel = await GetChannelAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                var properties = new BasicProperties
                {
                    MessageId = message.Id.ToString(),
                    Type = message.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent
                };

                // mandatory: si ninguna cola recibe el mensaje, el broker lo devuelve y se lanza una excepción
                // en vez de descartarlo en silencio. Así el evento sigue en el outbox hasta que exista un consumidor.
                await channel.BasicPublishAsync(Messaging.Exchange, message.RoutingKey, mandatory: true,
                    properties, Encoding.UTF8.GetBytes(message.Payload), ct);

                message.MarkAsProcessed(time.GetUtcNow());
                await db.SaveChangesAsync(ct);

                logger.LogInformation("Publicado {Type} {MessageId}", message.Type, message.Id);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                message.MarkAsFailed(ex.Message);
                await db.SaveChangesAsync(ct);
                throw;
            }
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await CloseChannelAsync();

        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.User,
            Password = settings.Password
        };

        _connection = await factory.CreateConnectionAsync("orders-api-outbox", ct);

        // Con confirmaciones activadas, BasicPublishAsync espera a que el broker confirme que recibió el mensaje.
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await _channel.ExchangeDeclareAsync(Messaging.Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        return _channel;
    }

    private async Task CloseChannelAsync()
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
        await CloseChannelAsync();
    }
}