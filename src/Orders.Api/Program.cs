using Microsoft.EntityFrameworkCore;
using Orders.Api.Infrastructure;
using System.Text.Json.Serialization;
using Orders.Api.Endpoints;
using Orders.Api.Infrastructure.RabbitMq;
using Orders.Api.Infrastructure.Outbox;
using Orders.Api.Application;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'ConnectionStrings:OrdersDb'.");

builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddScoped<StockResultHandler>();
builder.Services.AddHostedService<StockResultConsumer>();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Aplica las migraciones pendientes al arrancar (incluye el seed del catálogo).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapOrderEndpoints();
app.MapHealthChecks("/health");

app.Run();