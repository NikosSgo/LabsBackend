namespace Oms.BLL.Models;

public class UpdateOrdersStatusModel
{
    public long[] OrderIds { get; set; }
    public string NewStatus { get; set; }
}
