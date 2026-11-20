namespace Orders_API.Messaging
{
    //This is the event: “an order was placed.” It carries only what Products needs.
    //It stays inside Orders_Api,
    //and Products will define its own copy later (no shared project).
    public record OrderPlaced(int OrderId, int ProductId, int Quantity, DateTime OccurredAt);
}
