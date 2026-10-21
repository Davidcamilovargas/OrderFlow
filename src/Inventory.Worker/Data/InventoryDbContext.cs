using Inventory.Worker.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Worker.Data;

/// <summary>Mapea las entidades de Inventory (stock y eventos procesados) a PostgreSQL.</summary>
public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockItem> Stock => Set<StockItem>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockItem>(stock =>
        {
            stock.ToTable("Stock");
            stock.HasKey(s => s.Sku);
            stock.Property(s => s.Sku).HasMaxLength(50);
            stock.ToTable(t => t.HasCheckConstraint("CK_Stock_Disponible_NoNegativo", "\"Disponible\" >= 0"));

            // Seed: los mismos SKUs del catálogo de Orders. ABC-03 tiene poco stock para poder probar rechazos.
            stock.HasData(
                new StockItem("ABC-01", 100),
                new StockItem("ABC-02", 50),
                new StockItem("ABC-03", 3));
        });

        modelBuilder.Entity<ProcessedEvent>(processed =>
        {
            processed.HasKey(e => e.EventId);
            processed.Property(e => e.ResponseType).HasMaxLength(100).IsRequired();
            processed.Property(e => e.ResponseRoutingKey).HasMaxLength(100).IsRequired();
            processed.Property(e => e.ResponsePayload).HasColumnType("jsonb").IsRequired();
        });
    }
}