using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Domain;
using Orders.Api.Infrastructure;
using OrderFlow.Contracts;
using Orders.Api.Infrastructure.Outbox;

namespace Orders.Api.Endpoints;

/// <summary>
/// Endpoints de la API de Orders. Se exponen los endpoints para crear, listar y consultar pedidos, así como para listar el catálogo de productos.
/// </summary>

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapPost("/", CreateOrder);
        orders.MapGet("/", ListOrders);
        orders.MapGet("/{id:guid}", GetOrder);

        app.MapGet("/products", ListProducts).WithTags("Products");

        return app;
    }

    /// <summary>
    ///     Crea un nuevo pedido. Valida que el SKU exista en el catálogo de productos y que la cantidad sea positiva. Devuelve 201 Created con el pedido creado, o 400 Bad Request si hay errores de validación.
    /// </summary>
    private static async Task<Results<Created<OrderResponse>, ValidationProblem>> CreateOrder(
        CreateOrderRequest request, OrdersDbContext db, TimeProvider time, CancellationToken ct)
    {
        var sku = request.Sku?.Trim().ToUpperInvariant() ?? string.Empty;
        var skuExists = sku.Length > 0 && await db.Products.AnyAsync(p => p.Sku == sku, ct);

        var errors = CreateOrderValidator.Validate(request, skuExists);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

         var order = Order.Create(request.ClienteNombre!, sku, request.Cantidad, time.GetUtcNow());
        var orderCreated = new OrderCreated(Guid.NewGuid(), order.Id, order.Sku, order.Cantidad, order.CreadoEn);

        db.Orders.Add(order);
        db.OutboxMessages.Add(OutboxMessage.Create(
            orderCreated.EventId, orderCreated, Messaging.RoutingKeys.OrderCreated, orderCreated.OcurridoEn));
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/orders/{order.Id}", OrderResponse.From(order));
    }

    
    /// <summary>
    /// Lista todos los pedidos existentes, ordenados por fecha de creación descendente. Devuelve una lista vacía si no hay pedidos.
    /// </summary>

    private static async Task<Ok<List<OrderResponse>>> ListOrders(OrdersDbContext db, CancellationToken ct)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreadoEn)
            .Select(o => OrderResponse.From(o))
            .ToListAsync(ct);

        return TypedResults.Ok(orders);
    }

    /// <summary>
    /// Busca pedidio Específico por su Id. Devuelve 404 si no existe.
    /// </summary>

    private static async Task<Results<Ok<OrderResponse>, NotFound>> GetOrder(
        Guid id, OrdersDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(OrderResponse.From(order));
    }

    /// <summary>
    /// COnsulta el catálogo de productos. Devuelve la lista de productos con su SKU y nombre.
    /// </summary>
    private static async Task<Ok<List<ProductResponse>>> ListProducts(OrdersDbContext db, CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Sku)
            .Select(p => new ProductResponse(p.Sku, p.Nombre))
            .ToListAsync(ct);

        return TypedResults.Ok(products);
    }
}