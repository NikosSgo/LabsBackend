using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using Oms.BLL.Models;
using Oms.BLL.Services;
using Oms.Validators;

[Route("api/v1/order")]
public class OrderController(
    OrderService orderService,
    ValidatorFactory validatorFactory,
    IMapper mapper) : ControllerBase
{
    [HttpPost("batch-update-status")]
    public async Task<ActionResult<V1UpdateOrdersStatusResponse>> V1BatchUpdateStatus([FromBody] V1UpdateOrdersStatusRequest request,
        CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1UpdateOrdersStatusRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        var updateModel = mapper.Map<UpdateOrdersStatusModel>(request);

        try
        {
            var res = await orderService.BatchUpdateStatus(new[] { updateModel }, token);

            return Ok(new V1UpdateOrdersStatusResponse
            {
                Orders = mapper.Map<Models.Dto.Common.OrderUnit[]>(res)
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("batch-create")]
    public async Task<ActionResult<V1CreateOrderResponse>> V1BatchCreate([FromBody] V1CreateOrderRequest request, CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1CreateOrderRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }
        
        var res = await orderService.BatchInsert(
            mapper.Map<OrderUnit[]>(request.Orders),
            token
            );

        return Ok(new V1CreateOrderResponse
        {
            Orders = mapper.Map<Models.Dto.Common.OrderUnit[]>(res)
        });
    }

    [HttpPost("query")]
    public async Task<ActionResult<V1QueryOrdersResponse>> V1QueryOrders([FromBody] V1QueryOrdersRequest request, CancellationToken token)
    {
        var validationResult = await validatorFactory.GetValidator<V1QueryOrdersRequest>().ValidateAsync(request, token);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        var res = await orderService.GetOrders(
            mapper.Map<QueryOrdersModel>(request),
            token
            );
        
        return Ok(new V1QueryOrdersResponse
        {
            Orders = mapper.Map<Models.Dto.Common.OrderUnit[]>(res)
        });
    }
}
