using Microsoft.EntityFrameworkCore;
using Orders_API.Clients;
using Orders_API.Data;
using Orders_API.Models;

namespace Orders_API.Services
{
    public record OrderResult(Order? Order, string? Error, int StatusCode);
    public class OrderService(OrdersDbContext db, IProductsClient products)
    {

        public async Task<OrderResult> PlaceOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
        {
            if (request.Quantity <= 0)
                return new OrderResult(null, "Quantity must be greater than zero.", 400);

            try
            {
                var product = await products.GetByIdAsync(request.ProductId, ct);
                if (product is null)
                    return new OrderResult(null, "Product not found.", 404);

                var reduced = await products.ReduceStockAsync(request.ProductId, request.Quantity, ct);
                if (reduced == ReduceStockResult.InsufficientStock)
                    return new OrderResult(null, "Not enough stock.", 409);
                if (reduced == ReduceStockResult.NotFound)
                    return new OrderResult(null, "Product not found.", 404);

                var order = new Order
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = request.Quantity,
                    TotalPrice = product.Price * request.Quantity
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);

                return new OrderResult(order, null, 201);
            }
            catch (ProductServiceUnavailableException)
            {
                return new OrderResult(null, "Product service is currently unavailable. Please try again later.", 503);
            }
        }

        public Task<List<Order>> GetAllAsync() => db.Orders.ToListAsync();
        public async Task<Order?> GetByIdAsync(int id) => await db.Orders.FindAsync(id);
    }
}
