namespace OrderFlow.Contracts;

/// <summary> Publicado por Orders API cuando se crea un pedido 
/// </summary>
public record OrderCreatedEvent(
    Guid EventId,
    Guid OrderId,
    string Sku,
    int Cantidad,
    DateTimeOffset OcurridoEn
);

/// <summary>Publicado por Inventory cuando pudo descontar el stock.
/// </summary>
public record StockReserved(
    Guid EventId,
    Guid OrderId,
    DateTimeOffset OcurridoEn
);
