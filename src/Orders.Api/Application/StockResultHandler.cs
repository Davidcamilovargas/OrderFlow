using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts;
using Orders.Api.Infrastructure;

namespace Orders.Api.Application;

/// <summary>
/// Aplica al pedido el resultado de la reserva de stock. Es idempotente: si el pedido ya está
/// en el estado final que indica el evento, no hace nada.
/// </summary>
public class StockResultHandler(OrdersDbContext db, ILogger<StockResultHandler> logger)
{
    public Task HandleAsync(StockReserved message, CancellationToken ct) =>
        ApplyAsync(message.OrderId, message.EventId, order => order.Confirm(), ct);

    public Task HandleAsync(StockRejected message, CancellationToken ct) =>
        ApplyAsync(message.OrderId, message.EventId, order => order.Reject(message.Motivo), ct);

    private async Task ApplyAsync(Guid orderId, Guid eventId, Func<Domain.Order, bool> transition, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            logger.LogWarning("Evento {EventId} ignorado: el pedido {OrderId} no existe.", eventId, orderId);
            return;
        }

        if (!transition(order))
        {
            logger.LogInformation("Evento {EventId} duplicado: el pedido {OrderId} ya está en {Estado}.",
                eventId, orderId, order.Estado);
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Pedido {OrderId} actualizado a {Estado}", orderId, order.Estado);
    }
}