using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using Oms.BLL.Models;
using Oms.BLL.Services;
using Oms.Validators;

namespace Oms.Controllers.V1;

[ApiController]
[Route("api/v1/audit/")]
public class AuditLogOrderController(
    AuditLogOrderService auditLogOrderService,
    ValidatorFactory validatorFactory,
    ILogger<AuditLogOrderController> logger,
    IMapper mapper) : ControllerBase
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

            var mappedOrders = mapper.Map<AuditLogOrder[]>(request.Orders);
            var res = await auditLogOrderService.BatchInsert(mappedOrders, token);

            return Ok(new V1AuditLogOrderResponse
            {
                Orders = mapper.Map<Models.Dto.Common.AuditLogOrder[]>(res)
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid request data for audit log");
            return BadRequest("Invalid request data");  // 400
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process audit log order request");
            return StatusCode(500, "Internal server error");  // 500
        }
    }


}
