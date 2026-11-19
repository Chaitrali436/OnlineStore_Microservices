using Microsoft.EntityFrameworkCore;
using Orders_API.Models;

namespace Orders_API.Data
{
    public class OrdersDbContext(DbContextOptions<OrdersDbContext> options):DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().Property(o => o.TotalPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Status).HasConversion<string>();
        }
    }
}
