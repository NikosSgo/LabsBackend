using Consumer.Base;
using Consumer.Consumers.Base;
using Consumer.Clients;
using Consumer.Config;
using Messages;
using Microsoft.Extensions.Options;
using System.Threading;
using Models.Dto.Common;
using Models.Dto.V1.Requests;
using Models.Enum;

namespace Consumer.Consumers;

public class BatchOmsOrderCreatedConsumer(
    IOptions<RabbitMqSettings> rabbitMqSettings,
    IServiceProvider serviceProvider)
    : BaseBatchMessageConsumer<OmsOrderCreatedMessage>(rabbitMqSettings.Value, s => s.OrderCreated)
{
    private static int _batchCounter;

    protected override async Task ProcessMessages(OmsOrderCreatedMessage[] messages)
    {
        var current = Interlocked.Increment(ref _batchCounter);
        if (current % 5 == 0)
        {
            throw new Exception($"Test failure on every 5th batch. Batch #{current}");
        }

        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

        await client.LogOrder(new V1AuditLogOrderRequest
        {
            Orders = messages.SelectMany(order => order.OrderItems.Select(ol =>
                new AuditLogOrder
                {
                    OrderId = order.Id,
                    OrderItemId = ol.Id,
                    CustomerId = order.CustomerId,
                    OrderStatus = nameof(OrderStatus.Created)
                })).ToArray()
        }, CancellationToken.None);
    }
}
