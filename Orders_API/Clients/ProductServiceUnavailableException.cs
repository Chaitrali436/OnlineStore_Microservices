namespace Orders_API.Clients
{
    public record ProductDto(int Id, string Name, decimal Price, int Stock);
    public record ReduceStockRequest(int Quantity);

    public enum ReduceStockResult { Success, NotFound, InsufficientStock }

    public class ProductServiceUnavailableException(string message, Exception inner)
    : Exception(message, inner);

    public interface IProductsClient
    {
        Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ReduceStockResult> ReduceStockAsync(int id, int quantity, CancellationToken ct = default);
    }
}
