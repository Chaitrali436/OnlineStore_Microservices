using Orders_API.Data;
using Orders_API.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Orders_API.Messaging
{
    public record StockReserved(int OrderId,DateTime OccurredAt);
    public record StockRejected(int OrderId,string Reason,DateTime OccurredAt);   
    public class StockResultConsumer(IServiceScopeFactory factory, IConfiguration config,
    ILogger<StockResultConsumer> logger) : BackgroundService
    {
        private const string Exchange = "orders.exchange";
        private const string ResultQueue = "orders.stock-result";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private IConnection? _connection;
        private IChannel? _channel;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"],
                Port = int.Parse(config["RabbitMQ:Port"]),
                UserName = config["RabbitMQ:Username"],
                Password = config["RabbitMQ:Password"]
            };

            // Keep trying until RabbitMQ is available
            while(!stoppingToken.IsCancellationRequested)
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

            // Declare on this side too, so startup order does not matter
            await _channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(ResultQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(ResultQueue, Exchange, "stock.reserved", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(ResultQueue, Exchange, "stock.rejected", cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    await HandleAsync(ea.RoutingKey, ea.Body.ToArray(), stoppingToken);
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);   // ack AFTER save
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process result, requeueing");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(ResultQueue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
            logger.LogInformation("Consuming from {Queue}", ResultQueue);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleAsync(string routingKey, byte[] body, CancellationToken ct)
        {
            int orderId;
            OrderStatus newStatus;

            if (routingKey == "stock.reserved")
            {
                orderId = JsonSerializer.Deserialize<StockReserved>(body, JsonOptions)!.OrderId;
                newStatus = OrderStatus.Confirmed;
            }
            else if (routingKey == "stock.rejected")
            {
                orderId = JsonSerializer.Deserialize<StockRejected>(body, JsonOptions)!.OrderId;
                newStatus = OrderStatus.Rejected;
            }
            else
            {
                logger.LogWarning("Unknown routing key {Key}, ignoring", routingKey);
                return;
            }

            using var scope = factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            var order = await db.Orders.FindAsync([orderId], ct);
            if (order is null)
            {
                logger.LogWarning("Order {OrderId} not found, ignoring result", orderId);
                return;
            }

            // Idempotent: only a Pending order changes, so a duplicate result does nothing
            if (order.Status != OrderStatus.Pending)
            {
                logger.LogInformation("Order {OrderId} already {Status}, skipping", orderId, order.Status);
                return;
            }

            order.Status = newStatus;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Order {OrderId} is now {Status}", orderId, newStatus);
        }
    }


}
