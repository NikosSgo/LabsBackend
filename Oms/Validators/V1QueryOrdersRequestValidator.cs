namespace Oms.Validators;

using FluentValidation;
using Models.Dto.V1.Requests;


public class V1QueryOrdersRequestValidator : AbstractValidator<V1QueryOrdersRequest>
{
    public V1QueryOrdersRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.Ids != null && x.Ids.Length > 0) || (x.CustomerIds != null && x.CustomerIds.Length > 0));

        // Если переданы Ids — они должны быть положительными
        RuleForEach(x => x.Ids)
            .GreaterThan(0)
            .When(x => x.Ids is not null && x.Ids.Length > 0);

        // Если переданы CustomerIds — они тоже должны быть положительными
        RuleForEach(x => x.CustomerIds)
            .GreaterThan(0)
            .When(x => x.CustomerIds is not null && x.CustomerIds.Length > 0);

        // Page и PageSize не должны быть отрицательными
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(0);

        // Если PageSize задан, он должен быть в разумных пределах (например, до 100)
        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(100)
            .When(x => x.PageSize > 0);

        // Ничего не проверяем для IncludeOrderItems — он просто bool
    }
}
