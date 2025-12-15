using FluentValidation;
using Models.Dto.V1.Requests;
using Models.Enum;

namespace Oms.Validators;

public class V1UpdateOrdersStatusRequestValidator: AbstractValidator<V1UpdateOrdersStatusRequest>
{
    public V1UpdateOrdersStatusRequestValidator()
    {
        RuleFor(x => x.OrderIds).NotEmpty().WithMessage("OrderIds must not be empty");
        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("NewStatus must not be empty")
            .Must(value => Enum.TryParse<OrderStatus>(value, true, out _))
            .WithMessage("Unsupported value NewStatus");
    }
}
