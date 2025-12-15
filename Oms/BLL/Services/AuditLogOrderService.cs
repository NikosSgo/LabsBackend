using AutoMapper;
using Oms.BLL.Models;
using Oms.DAL;
using Oms.DAL.Interfaces;
using Oms.DAL.Models;

namespace Oms.BLL.Services;

public class AuditLogOrderService(
    UnitOfWork unitOfWork,
    IAuditLogOrderRepository auditLogOrderRepository,
    IMapper mapper
)
{

    public async Task<AuditLogOrder[]> BatchInsert(AuditLogOrder[] orderUnits, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await unitOfWork.BeginTransactionAsync(token);
        var insertedLogOrders = Array.Empty<V1AuditLogOrderDal>();

        try
        {
            // гарантируем валидные временные метки, чтобы не уехать в Infinity в UI
            Array.ForEach(orderUnits, o =>
            {
                o.CreatedAt = now;
                o.UpdatedAt = now;
            });

            insertedLogOrders = await auditLogOrderRepository.BulkInsert(
                mapper.Map<V1AuditLogOrderDal[]>(orderUnits),
                token
                );

            await transaction.CommitAsync(token);

            return mapper.Map<AuditLogOrder[]>(insertedLogOrders);
        }
        catch
        {
            await transaction.RollbackAsync(token);
            throw;
        }
    }

}
