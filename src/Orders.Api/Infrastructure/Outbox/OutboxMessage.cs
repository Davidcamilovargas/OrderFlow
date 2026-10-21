using System.Text.Json;
using OrderFlow.Contracts;

namespace Orders.Api.Infrastructure.Outbox;

/// <summary>Evento pendiente de publicar. Se guarda en la misma transacción que el cambio que lo origina.</summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string RoutingKey { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create<TEvent>(Guid eventId, TEvent @event, string routingKey, DateTimeOffset occurredAt) =>
        new()
        {
            Id = eventId,
            Type = typeof(TEvent).Name,
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(@event, Messaging.JsonOptions),
            OccurredAt = occurredAt
        };

    public void MarkAsProcessed(DateTimeOffset now) => ProcessedAt = now;

    public void MarkAsFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 1000 ? error[..1000] : error;
    }
}