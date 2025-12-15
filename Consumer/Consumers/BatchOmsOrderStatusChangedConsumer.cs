using Consumer.Base;
using Consumer.Clients;
using Consumer.Config;
using Consumer.Consumers.Base;
using Messages;
using Microsoft.Extensions.Options;
using Models.Dto.Common;
using Models.Dto.V1.Requests;

namespace Consumer.Consumers;

public class BatchOmsOrderStatusChangedConsumer(
    IOptions<RabbitMqSettings> rabbitMqSettings,
    IServiceProvider serviceProvider)
    : BaseBatchMessageConsumer<OmsOrderStatusChangedMessage>(rabbitMqSettings.Value, s => s.OrderStatusChanged)
{
    protected override async Task ProcessMessages(OmsOrderStatusChangedMessage[] messages)
    {
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<OmsClient>();

        await client.LogOrder(new V1AuditLogOrderRequest
        {
            Orders = messages.Select(order =>
                new AuditLogOrder
                {
                    OrderId = order.OrderId,
                    OrderItemId = order.OrderItemId,
                    CustomerId = order.CustomerId,
                    OrderStatus = order.OrderStatus
                }).ToArray()
        }, CancellationToken.None);
    }
}
