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

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void La_cantidad_debe_estar_entre_1_y_100(int cantidad, bool esValida)
    {
        var request = new CreateOrderRequest("Ana", "ABC-01", cantidad);

        var errors = CreateOrderValidator.Validate(request, skuExists: true);

        Assert.Equal(esValida, !errors.ContainsKey("cantidad"));
    }
}