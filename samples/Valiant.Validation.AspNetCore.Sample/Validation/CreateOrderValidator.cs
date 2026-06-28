using Valiant.Validation.AspNetCore.Sample.Models;

namespace Valiant.Validation.AspNetCore.Sample.Validation;

[ValiantValidator]
public sealed partial class CreateOrderValidator : IValidatorDefinition<CreateOrder>
{
    public static void Configure(ValidationContext<CreateOrder> context)
    {
        context.Property(order => order.CustomerEmail)
            .Required(message: "Customer email is required.")
            .MaxLength(80);

        context.Property(order => order.Lines)
            .MinCount(1)
            .MaxCount(20);

        context.Property(order => order.Total)
            .GreaterThanOrEqualTo(0.01m)
            .LessThanOrEqualTo(100000m);

        context.Property(order => order.Lines).ForEach(line =>
        {
            line.Property(item => item.Sku)
                .Required();

            line.Property(item => item.Quantity)
                .InclusiveBetween(1, 999);
        });

        if (context.Instance.IsVip)
        {
            context.Property(order => order.VipCode)
                .Required();
        }
        else
        {
            context.Property(order => order.VipCode)
                .Empty();
        }
    }
}
