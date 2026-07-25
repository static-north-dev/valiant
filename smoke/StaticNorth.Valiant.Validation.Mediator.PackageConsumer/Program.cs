using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Valiant.Validation;
using Valiant.Validation.DependencyInjection;
using Valiant.Validation.Mediator;

var services = new ServiceCollection();
services.AddValiantValidators();
services.AddValiantMediatorValidation();

if (services.All(static descriptor =>
        descriptor.ServiceType != typeof(IPipelineBehavior<,>) ||
        descriptor.ImplementationType != typeof(ValiantValidationBehavior<,>)))
{
    throw new InvalidOperationException("The packaged Mediator build-transitive registration was not applied.");
}

Console.WriteLine("Valiant.Validation.Mediator package consumer passed.");

public sealed record MediatorSmokeRequest(string? Name) : IRequest<string>;

[ValiantValidator]
public sealed partial class MediatorSmokeValidator : ValidatorDefinition<MediatorSmokeRequest>
{
    public override void Configure(ValidationContext<MediatorSmokeRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}
