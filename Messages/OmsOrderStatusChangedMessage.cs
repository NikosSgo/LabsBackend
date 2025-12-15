namespace Messages;

public class OmsOrderStatusChangedMessage: BaseMessage
{
    public override string RoutingKey => "order.status.changed";
    public long OrderId { get; set; }
    public long OrderItemId { get; set; }
    public long CustomerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string OrderStatus { get; set; }
}
