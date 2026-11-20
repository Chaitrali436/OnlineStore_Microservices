using Microsoft.EntityFrameworkCore;
using Products_Api.Models;

namespace Products_Api.Data
{
    public class ProductsDbContext(DbContextOptions<ProductsDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<ProcessedMessage>(e =>
            {
                e.HasKey(o => o.OrderId);
                e.Property(m => m.OrderId).ValueGeneratedNever();
            });

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Laptop", Price = 800, Stock = 10 },
                new Product { Id = 2, Name = "Mouse", Price = 25, Stock = 100 });
        }
    }
}
