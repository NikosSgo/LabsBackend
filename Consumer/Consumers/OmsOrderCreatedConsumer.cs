using System.Text;
using Common;
using Consumer.Clients;
using Consumer.Config;
using Messages;
using Microsoft.Extensions.Options;
using Models.Dto.Common;
using Models.Dto.V1.Requests;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumer.Consumers;

public class OmsOrderCreatedConsumer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<RabbitMqSettings> _rabbitMqSettings;
    private readonly ConnectionFactory _factory;
    private IConnection _connection;
    private IChannel _channel;
    private AsyncEventingBasicConsumer _consumer;

    public OmsOrderCreatedConsumer(IOptions<RabbitMqSettings> rabbitMqSettings, IServiceProvider serviceProvider)
    {
        _rabbitMqSettings = rabbitMqSettings;
        _serviceProvider = serviceProvider;
        _factory = new ConnectionFactory 
        { 
            HostName = rabbitMqSettings.Value.HostName, 
            Port = rabbitMqSettings.Value.Port,
            UserName = rabbitMqSettings.Value.UserName,
            Password = rabbitMqSettings.Value.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            TopologyRecoveryEnabled = true
        };
    }

    private async Task SetupChannelAndConsumer(CancellationToken cancellationToken)
    {
        if (_channel != null && _channel.IsOpen)
        {
            return;
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: _rabbitMqSettings.Value.OrderCreatedQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        _consumer = new AsyncEventingBasicConsumer(_channel);
        _consumer.ReceivedAsync += async (sender, args) =>
        {
            try
            {
                var body = args.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var order = message.FromJson<OmsOrderCreatedMessage>();

                Console.WriteLine($"Received order: {order.Id}, CustomerId: {order.CustomerId}, OrderItems count: {order.OrderItems?.Length ?? 0}");

                if (order.OrderItems == null || order.OrderItems.Length == 0)
                {
                    Console.WriteLine($"Warning: Order {order.Id} has no OrderItems. Skipping audit log creation.");
                    await _channel.BasicAckAsync(args.DeliveryTag, false);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<OmsClient>();
                
                var auditLogRequest = new V1AuditLogOrderRequest
                {
                    Orders = order.OrderItems.Select(x =>
                        new AuditLogOrder
                        {
                            OrderId = order.Id,
                            OrderItemId = x.Id,
                            CustomerId = order.CustomerId,
                            OrderStatus = nameof(OrderStatus.Created)
                        }).ToArray()
                };

                Console.WriteLine($"Sending {auditLogRequest.Orders.Length} audit log entries to OMS for order {order.Id}");
                var response = await client.LogOrder(auditLogRequest, CancellationToken.None);
                Console.WriteLine($"Successfully processed audit log for order: {order.Id} (created {auditLogRequest.Orders.Length} audit log entries)");
                
                await _channel.BasicAckAsync(args.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                try
                {
                    if (_channel != null && _channel.IsOpen)
                    {
                        await _channel.BasicNackAsync(args.DeliveryTag, false, false);
                    }
                }
                catch (Exception nackEx)
                {
                    Console.WriteLine($"Error sending NACK: {nackEx.Message}");
                }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _rabbitMqSettings.Value.OrderCreatedQueue,
            autoAck: false,
            consumer: _consumer,
            cancellationToken: cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _factory.CreateConnectionAsync(cancellationToken);
        Console.WriteLine("Connected to RabbitMQ");

        await SetupChannelAndConsumer(cancellationToken);
        Console.WriteLine($"Queue '{_rabbitMqSettings.Value.OrderCreatedQueue}' declared. Waiting for messages...");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        _connection?.Dispose();
        _channel?.Dispose();
    }
}
