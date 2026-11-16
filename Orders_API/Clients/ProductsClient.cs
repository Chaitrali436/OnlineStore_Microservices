using System.Net;

namespace Orders_API.Clients
{
    public class ProductsClient(HttpClient http, ILogger<ProductsClient> logger) : IProductsClient
    {
        public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var response = await http.GetAsync($"api/products/{id}", ct);

                if (response.StatusCode == HttpStatusCode.NotFound) return null;

                response.EnsureSuccessStatusCode();   // throws on 500, 503, etc.
                return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogError(ex, "Product service call failed (GetById {ProductId})", id);
                throw new ProductServiceUnavailableException("Product service is unavailable.", ex);
            }
        }

        public async Task<ReduceStockResult> ReduceStockAsync(int id, int quantity, CancellationToken ct = default)
        {
            try
            {
                var response = await http.PostAsJsonAsync(
                    $"api/products/{id}/reduce-stock", new ReduceStockRequest(quantity), ct);

                if (response.StatusCode == HttpStatusCode.NotFound) return ReduceStockResult.NotFound;
                if (response.StatusCode == HttpStatusCode.Conflict) return ReduceStockResult.InsufficientStock;

                response.EnsureSuccessStatusCode();
                return ReduceStockResult.Success;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogError(ex, "Product service call failed (ReduceStock {ProductId})", id);
                throw new ProductServiceUnavailableException("Product service is unavailable.", ex);
            }
        }
    }
}
