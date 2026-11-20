namespace Products_Api.Models
{
    public class ProcessedMessage
    {
        public int OrderId { get; set; }          // primary key: one row per order
        public bool Succeeded { get; set; }
        public string? Reason { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}
