using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.Valiant.Validation;
using StaticNorth.Valiant.Validation.DependencyInjection;
using StaticNorth.Valiant.Validation.MediatR;

var services = new ServiceCollection();
services.AddValiantValidators();
services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(typeof(Program).Assembly);
    configuration.AddValiantValidation();
});

using var provider = services.BuildServiceProvider();
if (provider.GetServices<IPipelineBehavior<MediatRSmokeRequest, string>>()
    .All(static behavior => behavior is not ValiantValidationBehavior<MediatRSmokeRequest, string>))
{
    throw new InvalidOperationException("The packaged MediatR build-transitive registration was not applied.");
}

Console.WriteLine("StaticNorth.Valiant.Validation.MediatR package consumer passed.");

public sealed record MediatRSmokeRequest(string? Name) : IRequest<string>;

[ValiantValidator]
public sealed partial class MediatRSmokeValidator : ValidatorDefinition<MediatRSmokeRequest>
{
    public override void Configure(ValidationContext<MediatRSmokeRequest> context)
    {
        context.Property(static request => request.Name).Required();
    }
}

public sealed class MediatRSmokeHandler : IRequestHandler<MediatRSmokeRequest, string>
{
    public Task<string> Handle(MediatRSmokeRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Name ?? "");
    }
}
