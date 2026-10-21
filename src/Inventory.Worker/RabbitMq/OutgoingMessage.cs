using System.Text.Json;
using OrderFlow.Contracts;

namespace Inventory.Worker.RabbitMq;

/// <summary>Mensaje listo para publicar en RabbitMQ.</summary>
public record OutgoingMessage(Guid MessageId, string Type, string RoutingKey, string Payload)
{
    public static OutgoingMessage Create<TEvent>(Guid eventId, TEvent @event, string routingKey) =>
        new(eventId, typeof(TEvent).Name, routingKey, JsonSerializer.Serialize(@event, Messaging.JsonOptions));
}