namespace WebApi.DAL.Models;

public class V1AuditLogOrderDal
{
    public long OrderId { get; set; }
    public long OrderItemId { get; set; }
    public long CustomerId { get; set; }
    public string OrderStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
