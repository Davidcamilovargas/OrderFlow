using Microsoft.EntityFrameworkCore;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;
/// <summary> Crea la tabla de pedidos y el catálogo de productos (seed) si no existen.
/// </summary>

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.Property(o => o.ClienteNombre).HasMaxLength(200).IsRequired();
            order.Property(o => o.Sku).HasMaxLength(50).IsRequired();
            order.Property(o => o.Estado).HasConversion<string>().HasMaxLength(20);
            order.Property(o => o.MotivoRechazo).HasMaxLength(500);
            order.HasIndex(o => o.CreadoEn);
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.HasKey(p => p.Sku);
            product.Property(p => p.Sku).HasMaxLength(50);
            product.Property(p => p.Nombre).HasMaxLength(200).IsRequired();

            // Seed del catálogo: los mismos SKUs que siembra Inventory con su stock.
            product.HasData(
                new Product("ABC-01", "Teclado mecánico"),
                new Product("ABC-02", "Mouse inalámbrico"),
                new Product("ABC-03", "Monitor 27 pulgadas"));
        });
    }
}