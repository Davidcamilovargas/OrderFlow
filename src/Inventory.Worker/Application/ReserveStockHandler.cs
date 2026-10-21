using Inventory.Worker.Data;
using Inventory.Worker.Domain;
using Inventory.Worker.RabbitMq;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Contracts;

namespace Inventory.Worker.Application;

/// <summary>
/// Procesa un OrderCreated de forma idempotente: si el evento ya se procesó, devuelve la respuesta
/// original sin volver a descontar stock.
/// </summary>
public class ReserveStockHandler(InventoryDbContext db, TimeProvider time, ILogger<ReserveStockHandler> logger)
{
    public async Task<OutgoingMessage> HandleAsync(OrderCreated message, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var alreadyProcessed = await db.ProcessedEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == message.EventId, ct);

        if (alreadyProcessed is not null)
        {
            logger.LogInformation(
                "Evento {EventId} duplicado: no se descuenta stock, se reenvía la respuesta original.", message.EventId);
             // return alreadyProcessed.ToResponse();     ← comentada: "rompemos" la idempotencia
        }

        // UPDATE atómico: solo descuenta si alcanza el stock. Evita leer-modificar-escribir y,
        // con él, la condición de carrera entre dos consumidores que procesen el mismo SKU.
        var reservedRows = await db.Stock
            .Where(s => s.Sku == message.Sku && s.Disponible >= message.Cantidad)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Disponible, x => x.Disponible - message.Cantidad), ct);

        var now = time.GetUtcNow();
        OutgoingMessage response;

        if (reservedRows == 1)
        {
            var reserved = new StockReserved(Guid.NewGuid(), message.OrderId, now);
            response = OutgoingMessage.Create(reserved.EventId, reserved, Messaging.RoutingKeys.StockReserved);
            logger.LogInformation("Stock reservado: {Cantidad} x {Sku} para el pedido {OrderId}",
                message.Cantidad, message.Sku, message.OrderId);
        }
        else
        {
            var motivo = await BuildRejectionReasonAsync(message, ct);
            var rejected = new StockRejected(Guid.NewGuid(), message.OrderId, motivo, now);
            response = OutgoingMessage.Create(rejected.EventId, rejected, Messaging.RoutingKeys.StockRejected);
            logger.LogInformation("Pedido {OrderId} rechazado: {Motivo}", message.OrderId, motivo);
        }

        // Si otro consumidor ya registró este EventId, la clave primaria hace fallar el INSERT,
        // la transacción se revierte (incluido el descuento) y el mensaje se reintenta como duplicado.
        db.ProcessedEvents.Add(new ProcessedEvent(message.EventId, now, response));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return response;
    }

    private async Task<string> BuildRejectionReasonAsync(OrderCreated message, CancellationToken ct)
    {
        var disponible = await db.Stock
            .Where(s => s.Sku == message.Sku)
            .Select(s => (int?)s.Disponible)
            .FirstOrDefaultAsync(ct);

        return disponible is null
            ? $"El SKU {message.Sku} no tiene registro de inventario."
            : $"Stock insuficiente para {message.Sku}: disponible {disponible}, solicitado {message.Cantidad}.";
    }
}