using WebApi.BLL.Models;
using WebApi.DAL;
using WebApi.DAL.Interfaces;
using WebApi.DAL.Models;

namespace WebApi.BLL.Services;

public class AuditLogOrderService(
    UnitOfWork unitOfWork,
    IAuditLogOrderRepository auditLogOrderRepository
)
{

    public async Task<AuditLogOrder[]> BatchInsert(AuditLogOrder[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);
        var insertedLogOrders = Array.Empty<V1AuditLogOrderDal>();

        try
        {

            var orderDals = orderUnits.Select(o => new V1AuditLogOrderDal
            {
                OrderId =  o.OrderId,
                OrderItemId =  o.OrderItemId,
                OrderStatus =   o.OrderStatus,
                CustomerId = o.CustomerId,
                CreatedAt = now,
                UpdatedAt = now
            }).ToArray();

            insertedLogOrders = await auditLogOrderRepository.BulkInsert(orderDals, token);


            await transaction.CommitAsync(token);

            return Map(insertedLogOrders);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

    private AuditLogOrder[] Map(V1AuditLogOrderDal[] auditLogOrderDals)
    {
        return auditLogOrderDals.Select(x => new AuditLogOrder
        {
            OrderId =  x.OrderId,
            OrderItemId =  x.OrderItemId,
            OrderStatus =  x.OrderStatus,
            CustomerId = x.CustomerId,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToArray();
    }
}
