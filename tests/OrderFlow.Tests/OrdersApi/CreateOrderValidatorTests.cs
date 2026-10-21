using Orders.Api.Endpoints;

namespace OrderFlow.Tests.OrdersApi;

public class CreateOrderValidatorTests
{
    [Fact]
    public void Devuelve_todos_los_errores_juntos_cuando_todos_los_campos_son_invalidos()
    {
        var request = new CreateOrderRequest(ClienteNombre: " ", Sku: "XYZ-99", Cantidad: 0);

        var errors = CreateOrderValidator.Validate(request, skuExists: false);

        Assert.Equal(["cantidad", "clienteNombre", "sku"], errors.Keys.Order());
    }

}