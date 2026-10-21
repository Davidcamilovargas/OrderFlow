using Inventory.Worker.RabbitMq;

namespace Inventory.Worker.Domain;

/// <summary>
/// Registro de un evento ya procesado. Su clave primaria (EventId) garantiza que cada evento
/// descuente stock una sola vez. Guarda también la respuesta enviada, para reenviarla si llega un duplicado.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public Guid ResponseMessageId { get; private set; }
    public string ResponseType { get; private set; } = default!;
    public string ResponseRoutingKey { get; private set; } = default!;
    public string ResponsePayload { get; private set; } = default!;

    private ProcessedEvent() { }

    public ProcessedEvent(Guid eventId, DateTimeOffset processedAt, OutgoingMessage response)
    {
        EventId = eventId;
        ProcessedAt = processedAt;
        ResponseMessageId = response.MessageId;
        ResponseType = response.Type;
        ResponseRoutingKey = response.RoutingKey;
        ResponsePayload = response.Payload;
    }

    public OutgoingMessage ToResponse() =>
        new(ResponseMessageId, ResponseType, ResponseRoutingKey, ResponsePayload);
}