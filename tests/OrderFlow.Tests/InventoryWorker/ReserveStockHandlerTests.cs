using Inventory.Worker.Application;
using Inventory.Worker.Data;
using Inventory.Worker.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts;
using Testcontainers.PostgreSql;

namespace OrderFlow.Tests.InventoryWorker;

/// <summary>
/// Tests de integración contra un PostgreSQL real en un contenedor temporal:
/// el UPDATE atómico y las transacciones no se pueden probar con una base en memoria.
/// </summary>
public class ReserveStockHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync(); // crea las tablas y el seed (ABC-01 = 100, ABC-03 = 3)
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task El_mismo_evento_procesado_dos_veces_descuenta_stock_una_sola_vez()
    {
        var evento = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-01", 2, DateTimeOffset.UtcNow);

        var primeraRespuesta = await HandleAsync(evento);
        var segundaRespuesta = await HandleAsync(evento);

        Assert.Equal(98, await GetStockAsync("ABC-01"));
        Assert.Equal(nameof(StockReserved), primeraRespuesta.Type);
        // El duplicado reenvía la misma respuesta: mismo MessageId, para que Orders también la reconozca.
        Assert.Equal(primeraRespuesta.MessageId, segundaRespuesta.MessageId);
        Assert.Equal(primeraRespuesta.Type, segundaRespuesta.Type);
    }

    [Fact]
    public async Task Sin_stock_suficiente_rechaza_y_no_descuenta()
    {
        var evento = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-03", 5, DateTimeOffset.UtcNow);

        var respuesta = await HandleAsync(evento);

        Assert.Equal(nameof(StockRejected), respuesta.Type);
        Assert.Equal(3, await GetStockAsync("ABC-03"));
    }

    // Cada llamada usa un DbContext nuevo, como hace el consumidor real con un scope por mensaje.
    private async Task<OutgoingMessage> HandleAsync(OrderCreated evento)
    {
        await using var db = CreateDbContext();
        var handler = new ReserveStockHandler(db, TimeProvider.System, NullLogger<ReserveStockHandler>.Instance);
        return await handler.HandleAsync(evento, CancellationToken.None);
    }

    private async Task<int> GetStockAsync(string sku)
    {
        await using var db = CreateDbContext();
        return await db.Stock.Where(s => s.Sku == sku).Select(s => s.Disponible).SingleAsync();
    }

    private InventoryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);
}