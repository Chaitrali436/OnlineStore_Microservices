using Products_Api.Data;
using Products_Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Products_Api.Messaging
{
    public class OrderPlacedConsumer(IServiceScopeFactory scopeFactory,
        IConfiguration configuration, ILogger<OrderPlacedConsumer> logger):BackgroundService
    {
        private const string Exchange = "orders.exchange";
        private const string InQueue = "products.order-placed";
        private const string ResultQueue = "orders.stock-result";

        private IConnection? _connection;
        private IChannel? _channel;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"],
                Port = int.Parse(configuration["RabbitMQ:Port"]),
                UserName = configuration["RabbitMQ:Username"],
                Password = configuration["RabbitMQ:Password"]
            };
            // Keep trying until RabbitMQ is available (Products may start first)

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _connection=await factory.CreateConnectionAsync(stoppingToken);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("RabbitMQ not reachable ({Msg}). Retrying in 5s...", ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            if (_connection is null) return;

            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            // Declare everything (declaring is idempotent)
            await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(InQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(InQueue, Exchange, "order.placed", cancellationToken: stoppingToken);

            // Result queue, so results are kept even if Orders is not running yet
            await _channel.QueueDeclareAsync(ResultQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(ResultQueue, Exchange, "stock.reserved", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(ResultQueue, Exchange, "stock.rejected", cancellationToken: stoppingToken);

            // One unacked message at a time
            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<OrderPlaced>(ea.Body.Span)!;
                    await HandleAsync(message, stoppingToken);

                    // Ack ONLY after the work is saved
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process message, requeueing");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);   // avoid a hot loop
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, stoppingToken);
                    
                }
            };
            // autoAck: false means manual acknowledgement
            await _channel.BasicConsumeAsync(InQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            logger.LogInformation("Consuming from {Queue}", InQueue);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        private async Task HandleAsync(OrderPlaced msg, CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();

            // Idempotency: was this order already processed?
            var done = await db.ProcessedMessages.FindAsync([msg.OrderId], ct);
            if (done is not null)
            {
                logger.LogWarning("Order {OrderId} already processed, skipping stock change", msg.OrderId);
                await PublishResultAsync(msg.OrderId, done.Succeeded, done.Reason, ct);  // re-send the stored result
                return;
            }

            var product = await db.Products.FindAsync([msg.ProductId], ct);
            string? reason = null;
            if (product is null) reason = "Product not found.";
            else if (product.Stock < msg.Quantity) reason = "Not enough stock.";
            else product.Stock -= msg.Quantity;

            // Stock change and the "processed" record are saved in ONE transaction
            db.ProcessedMessages.Add(new ProcessedMessage
            {
                OrderId = msg.OrderId,
                Succeeded = reason is null,
                Reason = reason
            });
            await db.SaveChangesAsync(ct);

            await PublishResultAsync(msg.OrderId, reason is null, reason, ct);
        }

        private async Task PublishResultAsync(int orderId, bool succeeded, string? reason, CancellationToken ct)
        {
            var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

            object payload = succeeded
                ? new StockReserved(orderId, DateTime.UtcNow)
                : new StockRejected(orderId, reason ?? "Rejected", DateTime.UtcNow);
            var key = succeeded ? "stock.reserved" : "stock.rejected";

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, payload.GetType()));
            await _channel!.BasicPublishAsync(Exchange, key, mandatory: false, basicProperties: props, body: body, cancellationToken: ct);
            logger.LogInformation("Published {Key} for order {OrderId}", key, orderId);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null) await _channel.CloseAsync(cancellationToken);
            if (_connection is not null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
