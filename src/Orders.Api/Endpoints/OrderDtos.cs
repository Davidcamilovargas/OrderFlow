using Orders.Api.Domain;

namespace Orders.Api.Endpoints;
/// <summary>
/// DTOs para la API de Orders. Se usan para exponer la información de los pedidos y del catálogo de productos.
/// </summary>
/// <summary>
/// Record (Representación de datos inmutable) para crear un pedido. Se recibe desde el cliente.
/// </summary>
public record CreateOrderRequest(string? ClienteNombre, string? Sku, int Cantidad);

public record OrderResponse(
    Guid Id,
    string ClienteNombre,
    string Sku,
    int Cantidad,
    OrderStatus Estado,
    string? MotivoRechazo,
    DateTimeOffset CreadoEn)
{
    public static OrderResponse From(Order order) => new(
        order.Id, order.ClienteNombre, order.Sku, order.Cantidad,
        order.Estado, order.MotivoRechazo, order.CreadoEn);
}

public record ProductResponse(string Sku, string Nombre);