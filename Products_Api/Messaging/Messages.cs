namespace Products_Api.Messaging
{
    
    // Products keeps its OWN copy of the contracts (no project reference to Orders)
    public record OrderPlaced(int OrderId, int ProductId, int Quantity, DateTime OccurredAt);
    public record StockReserved(int OrderId, DateTime OccurredAt);
    public record StockRejected(int OrderId, string Reason, DateTime OccurredAt);
   
}
