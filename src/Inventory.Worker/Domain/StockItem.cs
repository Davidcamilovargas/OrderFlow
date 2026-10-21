namespace Inventory.Worker.Domain;

/// <summary>Stock disponible de un producto.</summary>
public class StockItem
{
    public string Sku { get; private set; } = default!;
    public int Disponible { get; private set; }

    private StockItem() { }

    public StockItem(string sku, int disponible)
    {
        Sku = sku;
        Disponible = disponible;
    }
}