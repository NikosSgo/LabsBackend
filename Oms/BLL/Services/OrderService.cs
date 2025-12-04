using Messages;
using Microsoft.Extensions.Options;
using WebApi;
using WebApi.BLL.Models;
using WebApi.DAL;
using WebApi.DAL.Interfaces;
using WebApi.DAL.Models;
using WebApi.Config;

namespace WebApi.BLL.Services;

public class OrderService(
    UnitOfWork unitOfWork,
    IOrderRepository orderRepository,
    IOrderItemRepository orderItemRepository,
    RabbitMqService _rabbitMqService,
    IOptions<RabbitMqSettings> settings)
{
    /// <summary>
    /// Метод создания заказов
    /// </summary>
    public async Task<OrderUnit[]> BatchInsert(OrderUnit[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);
        var insertedOrders = Array.Empty<V1OrderDal>();
        var insertedItems = Array.Empty<V1OrderItemDal>();

        try
        {

            var orderDals = orderUnits.Select(o => new V1OrderDal
            {
                CustomerId = o.CustomerId,
                DeliveryAddress = o.DeliveryAddress,
                TotalPriceCents = o.TotalPriceCents,
                TotalPriceCurrency = o.TotalPriceCurrency,
                CreatedAt = now,
                UpdatedAt = now
            }).ToArray();

            insertedOrders = await orderRepository.BulkInsert(orderDals, token);

            var allOrderItems = orderUnits.SelectMany((order, index) =>
            {
                var orderId = insertedOrders[index].Id;
                return order.OrderItems?.Select(item => new V1OrderItemDal
                {
                    OrderId = orderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    ProductTitle = item.ProductTitle,
                    ProductUrl = item.ProductUrl,
                    PriceCents = item.PriceCents,
                    PriceCurrency = item.PriceCurrency,
                    CreatedAt = now,
                    UpdatedAt = now
                }) ?? [];
            }).ToArray();

            if (allOrderItems.Length > 0)
            {
                insertedItems = await orderItemRepository.BulkInsert(allOrderItems, token);
            }

            await transaction.CommitAsync(token);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }

        var orderItemLookup = insertedItems.ToLookup(x => x.OrderId);
        var messages = insertedOrders.Select(order => new OmsOrderCreatedMessage
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            DeliveryAddress = order.DeliveryAddress,
            TotalPriceCents = order.TotalPriceCents,
            TotalPriceCurrency = order.TotalPriceCurrency,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            OrderItems = orderItemLookup[order.Id].Select(item => new OmsOrderCreatedMessage.OrderItemUnit
            {
                Id = item.Id,
                OrderId = item.OrderId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ProductTitle = item.ProductTitle,
                ProductUrl = item.ProductUrl,
                PriceCents = item.PriceCents,
                PriceCurrency = item.PriceCurrency,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }).ToArray()
        }).ToArray();

        if (messages.Length > 0)
        {
            await _rabbitMqService.Publish(messages, settings.Value.OrderCreatedQueue, token);
        }

        return Map(insertedOrders);
    }
    
    /// <summary>
    /// Метод получения заказов
    /// </summary>
    public async Task<OrderUnit[]> GetOrders(QueryOrderItemsModel model, CancellationToken token)
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
        return orders.Select(x => new OrderUnit
        {
            Id = x.Id,
            CustomerId = x.CustomerId,
            DeliveryAddress = x.DeliveryAddress,
            TotalPriceCents = x.TotalPriceCents,
            TotalPriceCurrency = x.TotalPriceCurrency,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            OrderItems = orderItemLookup?[x.Id].Select(o => new OrderItemUnit
            {
                Id = o.Id,
                OrderId = o.OrderId,
                ProductId = o.ProductId,
                Quantity = o.Quantity,
                ProductTitle = o.ProductTitle,
                ProductUrl = o.ProductUrl,
                PriceCents = o.PriceCents,
                PriceCurrency = o.PriceCurrency,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            }).ToArray() ?? []
        }).ToArray();
    }
}
