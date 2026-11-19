namespace Orders_API.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<T>(string routingKey, T message, CancellationToken ct=default);
    }
}
