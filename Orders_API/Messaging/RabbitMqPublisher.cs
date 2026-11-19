using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Orders_API.Messaging
{
    public sealed class RabbitMqPublisher(IConfiguration config,ILogger<RabbitMqPublisher> logger):
        IEventPublisher,IAsyncDisposable
    {
        public const string ExchangeName = "orders.exchange";
        public const string OrderPlacedQueue = "products.order-placed";
        public const string OrderPlacedRoutingKey = "order.placed";

        private readonly SemaphoreSlim _lock = new(1, 1);
        private IConnection? _connection;
        private IChannel? _channel;

        private async Task<IChannel> GetChannelAsync(CancellationToken ct)
        {
            if (_channel is { IsOpen: true }) return _channel;

            await _lock.WaitAsync(ct);
            try
            {
                if (_channel is { IsOpen: true }) return _channel;

                var factory = new ConnectionFactory
                {
                    HostName = config["RabbitMQ:Host"]!,
                    Port = int.Parse(config["RabbitMQ:Port"]!),
                    UserName = config["RabbitMQ:Username"]!,
                    Password = config["RabbitMQ:Password"]!
                };

                _connection = await factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

                // Declare BEFORE publishing, so the message is never dropped
                await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Direct,
                    durable: true, cancellationToken: ct);
                await _channel.QueueDeclareAsync(OrderPlacedQueue,
                    durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
                await _channel.QueueBindAsync(OrderPlacedQueue, ExchangeName,
                    OrderPlacedRoutingKey, cancellationToken: ct);

                logger.LogInformation("Connected to RabbitMQ at {Host}", factory.HostName);
                return _channel;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task PublishAsync<T>(string routingKey, T message, CancellationToken ct = default)
        {
            var channel = await GetChannelAsync(ct);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var props = new BasicProperties
            {
                Persistent = true,                 // survive a broker restart
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(ExchangeName, routingKey, mandatory: false,
                basicProperties: props, body: body, cancellationToken: ct);

            logger.LogInformation("Published {Type} with key {Key}", typeof(T).Name, routingKey);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel is not null) await _channel.CloseAsync();
            if (_connection is not null) await _connection.CloseAsync();
        }
    }
}
