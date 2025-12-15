using System.Text;
using Common;
using Consumer.Config;
using Consumer.Consumers.Base;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consumer.Base;

public abstract class BaseBatchMessageConsumer<T>(RabbitMqSettings rabbitMqSettings, Func<RabbitMqSettings, RabbitMqSettings.TopicSettingsUnit> rabbitFunc): IHostedService
    where T : class
{
    private IConnection _connection;
    private IChannel _channel;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = rabbitMqSettings.HostName,
        Port = rabbitMqSettings.Port,
        UserName = rabbitMqSettings.UserName,
        Password =  rabbitMqSettings.Password
    };
    private List<MessageInfo> _messageBuffer;
    private Timer _batchTimer;
    private SemaphoreSlim _processingSemaphore;
    private readonly RabbitMqSettings.TopicSettingsUnit _topicSettings = rabbitFunc(rabbitMqSettings);

    protected abstract Task ProcessMessages(T[] messages);

    public async Task StartAsync(CancellationToken token)
    {
        _connection = await _factory.CreateConnectionAsync(token);
        _channel = await _connection.CreateChannelAsync(cancellationToken: token);

        _messageBuffer = new List<MessageInfo>();
        _processingSemaphore = new SemaphoreSlim(1, 1);

        // Настройка prefetch для батчевой обработки
        await _channel.BasicQosAsync(0, (ushort)(_topicSettings.BatchSize * 2), false, token);

        var batchTimeout = TimeSpan.FromSeconds(_topicSettings.BatchTimeoutSeconds);
        // Таймер для принудительной обработки по времени
        _batchTimer = new Timer(ProcessBatchByTimeout, null, batchTimeout, batchTimeout);

        await _channel.ExchangeDeclareAsync(
            exchange: _topicSettings.DeadLetter.Dlx,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: token);

        await _channel.QueueDeclareAsync(
            queue: _topicSettings.DeadLetter.Dlq,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: token);

        await _channel.QueueBindAsync(
            queue: _topicSettings.DeadLetter.Dlq,
            exchange: _topicSettings.DeadLetter.Dlx,
            routingKey: _topicSettings.DeadLetter.RoutingKey,
            cancellationToken: token);

        var queueArgs = new Dictionary<string, object>
        {
            {"x-dead-letter-exchange", _topicSettings.DeadLetter.Dlx},
            {"x-dead-letter-routing-key", _topicSettings.DeadLetter.RoutingKey}
        };

        await _channel.QueueDeclareAsync(
            queue: _topicSettings.Queue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: token);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceived;

        await _channel.BasicConsumeAsync(queue: _topicSettings.Queue, autoAck: false, consumer: consumer, cancellationToken: token);
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs ea)
    {
        await _processingSemaphore.WaitAsync();

        try
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            _messageBuffer.Add(new MessageInfo
            {
                Message = message,
                DeliveryTag = ea.DeliveryTag,
                ReceivedAt = DateTimeOffset.UtcNow
            });

            // Если достигли лимита батча - обрабатываем
            if (_messageBuffer.Count >= _topicSettings.BatchSize)
            {
                await ProcessBatch();
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async void ProcessBatchByTimeout(object state)
    {
        await _processingSemaphore.WaitAsync();

        try
        {
            if (_messageBuffer.Count > 0)
            {
                await ProcessBatch();
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async Task ProcessBatch()
    {
        if (_messageBuffer.Count == 0) return;

        var currentBatch = _messageBuffer.ToList();
        _messageBuffer.Clear();

        try
        {
            var messages = currentBatch.Select(x => x.Message.FromJson<T>()).ToArray();

            // Ваша логика обработки батча
            await ProcessMessages(messages);

            // ACK всех сообщений в батче (multiple = true для последнего)
            var lastDeliveryTag = currentBatch.Max(x => x.DeliveryTag);
            await _channel.BasicAckAsync(lastDeliveryTag, multiple: true);

            Console.WriteLine($"Successfully processed batch of {currentBatch.Count} messages");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process batch: {ex.Message}");

            // NACK всех сообщений в батче для повторной обработки
            var lastDeliveryTag = currentBatch.Max(x => x.DeliveryTag);
            await _channel.BasicNackAsync(lastDeliveryTag, multiple: true, requeue: false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _batchTimer?.Dispose();
        _channel?.Dispose();
        _connection?.Dispose();
        _processingSemaphore?.Dispose();
        return Task.CompletedTask;
    }
}
