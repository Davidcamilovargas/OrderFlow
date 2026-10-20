namespace Orders.Api.Endpoints;

public static class CreateOrderValidator
{
    public const int CantidadMinima = 1;
    public const int CantidadMaxima = 100;

    public static Dictionary<string, string[]> Validate(CreateOrderRequest request, bool skuExists)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ClienteNombre))
            errors["clienteNombre"] = ["El nombre del cliente es obligatorio."];
        else if (request.ClienteNombre.Trim().Length > 200)
            errors["clienteNombre"] = ["El nombre del cliente no puede superar 200 caracteres."];

        if (string.IsNullOrWhiteSpace(request.Sku))
            errors["sku"] = ["El SKU es obligatorio."];
        else if (!skuExists)
            errors["sku"] = [$"El SKU '{request.Sku.Trim()}' no existe en el catálogo."];

        if (request.Cantidad is < CantidadMinima or > CantidadMaxima)
            errors["cantidad"] = [$"La cantidad debe estar entre {CantidadMinima} y {CantidadMaxima}."];

        return errors;
    }
}