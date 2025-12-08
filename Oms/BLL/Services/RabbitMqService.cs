using System.Text;
using System.Threading;
using Common;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using WebApi.Config;

namespace WebApi;

public class RabbitMqService(IOptions<RabbitMqSettings> settings) : IAsyncDisposable
{
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

    public async Task Publish<T>(IEnumerable<T> enumerable, string queue, CancellationToken token)
    {
        await _channelLock.WaitAsync(token);
        try
        {
            var channel = await GetOrCreateChannelAsync(token);

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: token);

            foreach (var message in enumerable)
            {
                var messageStr = message.ToJson();
                var body = Encoding.UTF8.GetBytes(messageStr);
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queue,
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
