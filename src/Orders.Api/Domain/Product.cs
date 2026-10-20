namespace Orders.Api.Domain;

/// <summary>Producto del catálogo. Define qué SKUs se pueden pedir (el stock vive en Inventory).</summary>
public class Product
{
    public string Sku { get; private set; } = default!;
    public string Nombre { get; private set; } = default!;

    private Product() { }

    public Product(string sku, string nombre)
    {
        Sku = sku;
        Nombre = nombre;
    }
}