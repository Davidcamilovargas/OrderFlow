using Inventory.Worker.Application;
using Inventory.Worker.Data;
using Inventory.Worker.RabbitMq;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("InventoryDb")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:InventoryDb'.");

builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReserveStockHandler>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<OrderCreatedConsumer>();

var host = builder.Build();

// Aplica las migraciones pendientes al arrancar (incluye el seed del stock).
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.Migrate();
}

host.Run();