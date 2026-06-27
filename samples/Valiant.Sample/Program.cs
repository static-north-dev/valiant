using Microsoft.Extensions.DependencyInjection;
using Valiant;
using Valiant.DependencyInjection;

var services = new ServiceCollection();
services.AddValiantValidators();

using var provider = services.BuildServiceProvider();
var validator = provider.GetRequiredService<IValidator<CreateUser>>();
var tagValidator = provider.GetRequiredService<IValidator<CreateTag>>();

var invalid = await validator.ValidateAsync(new CreateUser
{
    Name = "",
    Age = 42
});

if (invalid.IsValid || invalid.Errors.Count != 2)
{
    throw new InvalidOperationException("Generated validator did not report the expected failures.");
}

var validUser = new CreateUser
{
    Name = "Ada",
    Age = 37
};

if (!await validator.IsValidAsync(validUser))
{
    throw new InvalidOperationException("Generated validator rejected a valid model.");
}

try
{
    _ = validator.IsValid(validUser);
    throw new InvalidOperationException("Synchronous IsValid should reject validators with asynchronous rules.");
}
catch (InvalidOperationException)
{
}

if (!tagValidator.IsValid(new CreateTag { Value = "release", DoThis = true }))
{
    throw new InvalidOperationException("Generated synchronous IsValid rejected a valid model.");
}

if (!tagValidator.IsValid(new CreateTag { Value = "" }))
{
    throw new InvalidOperationException("Generated synchronous IsValid rejected a valid conditional model.");
}

if (tagValidator.IsValid(new CreateTag { Value = "release" }))
{
    throw new InvalidOperationException("Generated synchronous IsValid accepted an invalid model.");
}

if (tagValidator.IsValid(new CreateTag { Value = "", DoThis = true }))
{
    throw new InvalidOperationException("Generated synchronous IsValid accepted an invalid model.");
}

if (!(await tagValidator.ValidateAsync(new CreateTag { Value = "docs", DoThis = true })).IsValid)
{
    throw new InvalidOperationException("Generated ValidateAsync rejected a valid sync-only model.");
}

Console.WriteLine("Valiant sample passed.");

public sealed class CreateUser
{
    public string? Name { get; init; }

    public int Age { get; init; }
}

public sealed class CreateTag
{
    public string? Value { get; init; }
    
    public bool DoThis { get; set; }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    public static ValidationContext<CreateUser> Configure(ValidationContext<CreateUser> context)
    {
        context.RuleFor(user => user.Name)
            .NotEmpty()
            .MaximumLength(64)
            .Must(static name => name is null || !name.Contains('!'))
            .WithMessage("Name must not contain exclamation marks.");

        context.RuleFor(user => user.Age)
            .GreaterThanOrEqualTo(18)
            .MustAsync(static (age, _) => new ValueTask<bool>(age != 42))
            .WithMessage("Age 42 is reserved.");

        return context;
    }
}

[ValiantValidator]
public sealed partial class CreateTagValidator : IValidatorDefinition<CreateTag>
{
    private readonly IValidator<CreateUser> _userValidator;

    public CreateTagValidator(IValidator<CreateUser> userValidator)
    {
        _userValidator = userValidator;
    }

    public static ValidationContext<CreateTag> Configure(ValidationContext<CreateTag> context)
    {
        if (!context.Instance.DoThis)
        {
            context.RuleFor(tag => tag.Value)
                .Empty()
                .WithMessage("Value must be empty when DoThis is false.");
        }
        else
        {
            context.RuleFor(tag => tag.Value)
                .NotEmpty()
                .Must(static value =>
                {
                    return value is null || value.Length <= 64;
                });
        }
        
        return context;
    }
}
