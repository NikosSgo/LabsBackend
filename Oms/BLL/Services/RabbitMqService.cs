using System.Text;
using System.Threading;
using Common;
using Messages;
using Microsoft.Extensions.Options;
using Oms.Config;
using RabbitMQ.Client;


namespace Oms.BLL.Services;

public class RabbitMqService(IOptions<RabbitMqSettings> settings) : IAsyncDisposable
{
    private readonly RabbitMqSettings _settings = settings.Value;

    private readonly ConnectionFactory _factory = new()
    {
        HostName = settings.Value.HostName,
        Port = settings.Value.Port,
        UserName = settings.Value.UserName,
        Password = settings.Value.Password
    };

    private IConnection _connection;
    private IChannel _channel;
    private readonly SemaphoreSlim _channelLock = new(1, 1);

    public async Task Publish<T>(IEnumerable<T> enumerable, CancellationToken token)
        where T : BaseMessage
    {
        await _channelLock.WaitAsync(token);
        try
        {
            var channel = await GetOrCreateChannelAsync(token);

            foreach (var message in enumerable)
            {
                var messageStr = message.ToJson();
                var body = Encoding.UTF8.GetBytes(messageStr);
                await channel.BasicPublishAsync(
                    exchange: _settings.Exchange,
                    routingKey: message.RoutingKey,
                    body: body,
                    cancellationToken: token);
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken token)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_connection is null || !_connection.IsOpen)
        {
            _connection = await _factory.CreateConnectionAsync(token);
        }

        _channel = await _connection.CreateChannelAsync(cancellationToken: token);
        await _channel.ExchangeDeclareAsync(_settings.Exchange, ExchangeType.Topic, cancellationToken: token);

        if (_settings.ExchangeMappings is { Length: > 0 })
        {
            foreach (var mapping in settings.Value.ExchangeMappings)
            {
                var args = mapping.DeadLetter is null ? null : new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", mapping.DeadLetter.Dlx },
                    { "x-dead-letter-routing-key", mapping.DeadLetter.RoutingKey }
                };

                await _channel.QueueDeclareAsync(
                    queue: mapping.Queue,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: args,
                    cancellationToken: token);

                await _channel.QueueBindAsync(
                    queue: mapping.Queue,
                    exchange: settings.Value.Exchange,
                    routingKey: mapping.RoutingKeyPattern,
                    cancellationToken: token);
            }
        }
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _channelLock.Dispose();
        await Task.CompletedTask;
    }
}
