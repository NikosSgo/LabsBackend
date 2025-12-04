using Microsoft.AspNetCore.Mvc;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using WebApi.BLL.Models;
using WebApi.BLL.Services;
using WebApi.Validators;

namespace WebApi.Controllers.V1;

[ApiController]
[Route("api/v1/audit/")]
public class AuditLogOrderController(AuditLogOrderService auditLogOrderService, ValidatorFactory validatorFactory) : ControllerBase
{
    [HttpPost("log-order")]
    public async Task<ActionResult<V1AuditLogOrderResponse>> V1AuditLogOrder(
        [FromBody] V1AuditLogOrderRequest request,
        CancellationToken token)
    {
        try
        {
            var validationResult = await validatorFactory.GetValidator<V1AuditLogOrderRequest>().ValidateAsync(request, token);
            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.ToDictionary());
            }

            var mappedOrders = Map(request.Orders);
            var res = await auditLogOrderService.BatchInsert(mappedOrders, token);

            return Ok(new V1AuditLogOrderResponse
            {
                Orders = Map(res)
            });
        }
        catch (ArgumentException)
        {
            return BadRequest("Invalid request data");  // 400
        }
        catch (Exception ex)
        {
            // Логирование
            return StatusCode(500, "Internal server error");  // 500
        }
    }


    private Models.Dto.Common.AuditLogOrder[] Map(AuditLogOrder[] orders)
    {
        return orders.Select(x => new Models.Dto.Common.AuditLogOrder
        {
            OrderId =x.OrderId,
            CustomerId = x.CustomerId,
            OrderItemId = x.OrderItemId,
            OrderStatus = x.OrderStatus
        }).ToArray();
    }

    private AuditLogOrder[] Map(Models.Dto.Common.AuditLogOrder[] orders)
    {
        return orders.Select(x => new AuditLogOrder
        {
            OrderId =x.OrderId,
            CustomerId = x.CustomerId,
            OrderItemId = x.OrderItemId,
            OrderStatus = x.OrderStatus
        }).ToArray();
    }
}
