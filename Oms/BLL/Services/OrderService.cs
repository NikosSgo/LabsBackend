using AutoMapper;
using Messages;
using Microsoft.Extensions.Options;
using Oms.BLL.Models;
using Oms.Config;
using Oms.DAL;
using Oms.DAL.Interfaces;
using Oms.DAL.Models;

namespace Oms.BLL.Services;

public class OrderService(
    UnitOfWork unitOfWork,
    IOrderRepository orderRepository,
    IOrderItemRepository orderItemRepository,
    RabbitMqService _rabbitMqService,
    IOptions<RabbitMqSettings> settings,
    IMapper mapper)
{
    public async Task<OrderUnit[]> BatchUpdateStatus(UpdateOrdersStatusModel[] updateOrdersModel, CancellationToken token)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);
        try
        {
            var allOrderIds = updateOrdersModel.SelectMany(m => m.OrderIds).Distinct().ToArray();
            var updatedOrders = new List<V1OrderDal>();

            foreach (var updateModel in updateOrdersModel)
            {
                var existing = await orderRepository.Query(new QueryOrdersDalModel
                {
                    Ids = updateModel.OrderIds
                }, token);

                if (existing.Length == 0)
                {
                    continue; // нет заказов — возвращаем 200 с пустым ответом
                }

                var targetStatus = Enum.Parse<OrderUnit.OrderStatus>(updateModel.NewStatus, true);
                ValidateTransitions(existing, targetStatus);

                var orders = await orderRepository.UpdateStatus(existing.Select(x => x.Id).ToArray(), updateModel.NewStatus, token);
                updatedOrders.AddRange(orders);
            }

            await transaction.CommitAsync(token);

            var orderItemLookup = await GetOrderItemsLookup(allOrderIds, token);

            var statusMessages = updatedOrders
                .SelectMany(order =>
                {
                    var items = orderItemLookup[order.Id].ToArray();

                    return items.Select(item => new OmsOrderStatusChangedMessage
                    {
                        OrderId = order.Id,
                        OrderItemId = item.Id,
                        CustomerId = order.CustomerId,
                        OrderStatus = order.Status,
                        CreatedAt = order.CreatedAt,
                        UpdatedAt = order.UpdatedAt
                    });
                })
                .ToArray();

            if (statusMessages.Length > 0)
            {
                await _rabbitMqService.Publish(statusMessages, token);
            }

            return Map(updatedOrders.ToArray(), orderItemLookup);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    private async Task<ILookup<long, V1OrderItemDal>> GetOrderItemsLookup(long[] orderIds, CancellationToken token)
    {
        if (orderIds.Length == 0)
        {
            return Array.Empty<V1OrderItemDal>().ToLookup(x => x.OrderId);
        }

        var orderItems = await orderItemRepository.Query(new QueryOrderItemsDalModel
        {
            OrderIds = orderIds
        }, token);

        return orderItems.ToLookup(x => x.OrderId);
    }

    private static void ValidateTransitions(V1OrderDal[] existingOrders, OrderUnit.OrderStatus targetStatus)
    {
        foreach (var order in existingOrders)
        {
            var current = Enum.Parse<OrderUnit.OrderStatus>(order.Status, true);
            if (current == targetStatus)
            {
                continue;
            }

            var allowed = current switch
            {
                OrderUnit.OrderStatus.Created => new[] { OrderUnit.OrderStatus.InAssembly, OrderUnit.OrderStatus.Rejected },
                OrderUnit.OrderStatus.InAssembly => new[] { OrderUnit.OrderStatus.InDelivery, OrderUnit.OrderStatus.Rejected },
                OrderUnit.OrderStatus.InDelivery => new[] { OrderUnit.OrderStatus.Completed, OrderUnit.OrderStatus.Rejected },
                OrderUnit.OrderStatus.Rejected => new[] { OrderUnit.OrderStatus.InAssembly },
                OrderUnit.OrderStatus.Completed => Array.Empty<OrderUnit.OrderStatus>(),
                _ => Array.Empty<OrderUnit.OrderStatus>()
            };

            if (!allowed.Contains(targetStatus))
            {
                throw new InvalidOperationException($"Invalid status transition from {current} to {targetStatus} for order {order.Id}");
            }
        }
    }

    /// <summary>
    /// Метод создания заказов
    /// </summary>
    public async Task<OrderUnit[]> BatchInsert(OrderUnit[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);

        try
        {

            V1OrderDal[] orderDals = mapper.Map<V1OrderDal[]>(orderUnits);
            Array.ForEach(orderDals, o =>
            {
                o.Status = o.Status ?? "Created";
                o.CreatedAt = now;
                o.UpdatedAt = now;
            });
            var orders = await orderRepository.BulkInsert(orderDals, token);

            V1OrderItemDal[] orderItemDals = orderUnits.SelectMany((o, index) =>
            {
                var items = mapper.Map<V1OrderItemDal[]>(o.OrderItems);
                Array.ForEach(items, item =>
                {
                    item.OrderId = orders[index].Id;
                    item.CreatedAt = now;
                    item.UpdatedAt = now;
                });
                return items;
            }).ToArray();

            var orderItems = await orderItemRepository.BulkInsert(orderItemDals, token);

            ILookup<long, V1OrderItemDal> orderItemLookup = orderItems.ToLookup(x => x.OrderId);

            OmsOrderCreatedMessage[] messages = orders.Select(o =>
            {
                var message = mapper.Map<OmsOrderCreatedMessage>(o);
                message.OrderItems = orderItemLookup[o.Id]
                    .Select(i => mapper.Map<OmsOrderCreatedMessage.OrderItemUnit>(i))
                    .ToArray();
                return message;
            }).ToArray();

            await _rabbitMqService.Publish(messages, token);
            await transaction.CommitAsync(token);
            return Map(orders, orderItemLookup);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    /// <summary>
    /// Метод получения заказов
    /// </summary>
    public async Task<OrderUnit[]> GetOrders(QueryOrdersModel model, CancellationToken token)
    {
        var orders = await orderRepository.Query(new QueryOrdersDalModel
        {
            Ids = model.Ids,
            CustomerIds = model.CustomerIds,
            Limit = model.PageSize,
            Offset = (model.Page - 1) * model.PageSize
        }, token);

        if (orders.Length is 0)
        {
            return [];
        }
        
        ILookup<long, V1OrderItemDal> orderItemLookup = null;
        if (model.IncludeOrderItems)
        {
            var orderItems = await orderItemRepository.Query(new QueryOrderItemsDalModel
            {
                OrderIds = orders.Select(x => x.Id).ToArray(),
            }, token);

            orderItemLookup = orderItems.ToLookup(x => x.OrderId);
        }

        return Map(orders, orderItemLookup);
    }

    private OrderUnit[] Map(V1OrderDal[] orders, ILookup<long, V1OrderItemDal> orderItemLookup = null)
    {
        var mappedOrders = mapper.Map<OrderUnit[]>(orders);

        foreach (var order in mappedOrders)
        {
            if (orderItemLookup is null)
            {
                order.OrderItems = Array.Empty<OrderItemUnit>();
                continue;
            }

            var items = orderItemLookup[order.Id].ToArray();
            order.OrderItems = mapper.Map<OrderItemUnit[]>(items);
        }

        return mappedOrders;
    }
}
