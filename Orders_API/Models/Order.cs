namespace Orders_API.Models
{
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Rejected
    }
    public class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    public record CreateOrderRequest(int ProductId, int Quantity);
}
