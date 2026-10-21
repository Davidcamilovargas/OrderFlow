using System.Text.Json;

namespace OrderFlow.Contracts;

/// <summary>Topología de RabbitMQ y formato de serialización compartidos por todos los servicios.</summary>
public static class Messaging
{
    /// <summary>Exchange de tipo topic donde se publican todos los eventos de integración.</summary>
    public const string Exchange = "orderflow.events";

    public static class RoutingKeys
    {
        public const string OrderCreated = "order.created";
        public const string StockReserved = "stock.reserved";
        public const string StockRejected = "stock.rejected";
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}