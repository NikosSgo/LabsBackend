using Models.Dto.Common;

namespace Models.Dto.V1.Requests;

public class V1AuditLogOrderRequest
{
    public AuditLogOrder[] Orders { get; set; }
}
