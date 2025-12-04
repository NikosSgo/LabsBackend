using FluentValidation;
using Models.Dto.Common;
using Models.Dto.V1.Requests;

namespace WebApi.Validators;

public class V1AuditLogOrderRequestValidator: AbstractValidator<V1AuditLogOrderRequest>
{
    public V1AuditLogOrderRequestValidator()
    {
        RuleFor(x => x.Orders)
            .NotEmpty()
            .ForEach(x => x.SetValidator(new AuditLogOrderValidator()));

    }

    public class AuditLogOrderValidator : AbstractValidator<AuditLogOrder>
    {
        public AuditLogOrderValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0);
            RuleFor(x => x.OrderItemId).GreaterThan(0);
            RuleFor(x => x.CustomerId).GreaterThan(0);
            RuleFor(x => x.OrderStatus)
                .Must(value => Enum.GetNames(typeof(OrderStatus)).Contains(value))
                .WithMessage("Недопустимое значение OrderStatus");
        }
    }
}
