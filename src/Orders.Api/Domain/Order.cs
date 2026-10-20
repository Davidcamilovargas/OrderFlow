namespace Orders.Api.Domain;

/// <summary>Representa el estado de un pedido.</summary>
public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected
}

/// <summary>Representa un pedido de un cliente.</summary>
public class Order
{
    public Guid Id { get; private set; }
    public string ClienteNombre { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public int Cantidad { get; private set; }
    public OrderStatus Estado { get; private set; }
    public string? MotivoRechazo { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }

     // Constructor vacío requerido por EF Core para materializar desde la base de datos.
    private Order() { }

    /// <summary>Crea un nuevo pedido válido.</summary>
    public static Order Create(string clienteNombre, string sku, int cantidad, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClienteNombre = clienteNombre.Trim(),
            Sku = sku.Trim().ToUpperInvariant(),
            Cantidad = cantidad,
            Estado = OrderStatus.Pending,
            CreadoEn = now
        };
    /// <summary>Confirma el pedido.</summary>
    /// <returns>true si el estado cambió; false si ya estaba confirmado (evento duplicado).</returns>
      public bool Confirm() => TransitionTo(OrderStatus.Confirmed);

    /// <summary>Rechaza el pedido.</summary>
    /// <param name="motivo">El motivo del rechazo.</param>
    /// <returns>true si el estado cambió; false si ya estaba rechazado (evento duplicado).</returns>
    public bool Reject(string motivo)
    {
        var changed = TransitionTo(OrderStatus.Rejected);
        if (changed) MotivoRechazo = motivo;
        return changed;
    }

    /// <summary>
    /// Intenta cambiar el estado del pedido al estado objetivo. Lanza InvalidOperationException si la transición no es válida. 
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private bool TransitionTo(OrderStatus target)
    {
        if (Estado == target)
            return false;

        if (Estado != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Transición inválida: el pedido {Id} está en {Estado} y no puede pasar a {target}.");

        Estado = target;
        return true;
    }
        
}

