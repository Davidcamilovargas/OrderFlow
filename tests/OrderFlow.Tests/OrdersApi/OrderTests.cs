using Orders.Api.Domain;

namespace OrderFlow.Tests.OrdersApi;

public class OrderTests
{
    private static Order NewPendingOrder() =>
        Order.Create("Ana", "abc-01", 2, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Confirm_es_idempotente_si_llega_el_mismo_resultado_dos_veces()
    {
        var order = NewPendingOrder();

        var primeraVez = order.Confirm();
        var segundaVez = order.Confirm();

        Assert.True(primeraVez);
        Assert.False(segundaVez);
        Assert.Equal(OrderStatus.Confirmed, order.Estado);
    }

    [Fact]
    public void Reject_sobre_un_pedido_confirmado_lanza_excepcion()
    {
        var order = NewPendingOrder();
        order.Confirm();

        Assert.Throws<InvalidOperationException>(() => order.Reject("Sin stock"));
        Assert.Equal(OrderStatus.Confirmed, order.Estado);
    }
}