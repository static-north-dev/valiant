using StaticNorth.Valiant.Validation;
using Validly;

namespace StaticNorth.Valiant.Validation.Benchmarks;

public sealed class ValiantValidationModel
{
    public string? DisplayName { get; init; }

    public string? Username { get; init; }

    public string? CountryCode { get; init; }

    public int Age { get; init; }

    public string? Nickname { get; init; }
}

[ValiantValidator]
public sealed partial class BenchmarkValidator : ValidatorDefinition<ValiantValidationModel>
{
    public override void Configure(ValidationContext<ValiantValidationModel> context)
    {
        context.Property(model => model.DisplayName).Required();
        context.Property(model => model.Username).MinLength(5);
        context.Property(model => model.CountryCode).MaxLength(2);
        context.Property(model => model.Age).InclusiveBetween(18, 99);
        context.Property(model => model.Nickname).NotEmpty();
    }
}

[Validatable(NoAutoValidators = true)]
public sealed partial class ValidlyValidationModel
{
    [Validly.Extensions.Validators.Common.Required]
    public string? DisplayName { get; init; }

    [Validly.Extensions.Validators.Strings.MinLength(5)]
    public string? Username { get; init; }

    [Validly.Extensions.Validators.Strings.MaxLength(2)]
    public string? CountryCode { get; init; }

    [Validly.Extensions.Validators.Numbers.Between(18, 99)]
    public int Age { get; init; }

    [Validly.Extensions.Validators.Common.NotEmpty]
    public string? Nickname { get; init; }
}

public enum ValidationScenario
{
    Valid,
    OneFailure,
    AllFailures,
}
