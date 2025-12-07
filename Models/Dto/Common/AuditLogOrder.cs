namespace Models.Dto.Common;

public class AuditLogOrder
{
    public long OrderId { get; set; }

    public long OrderItemId { get; set; }

    public long CustomerId { get; set; }

    public string OrderStatus { get; set; }
}

public enum OrderStatus { Created, Pending, Rejected }
