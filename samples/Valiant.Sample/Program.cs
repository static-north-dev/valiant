using Microsoft.Extensions.DependencyInjection;
using Valiant;
using Valiant.DependencyInjection;

var services = new ServiceCollection();
services.AddValiantValidations(options =>
{
    options.Mode = ValiantValidationMode.Auto;
    options.Lifetime = ValiantValidatorLifetime.Transient;
});

using var provider = services.BuildServiceProvider();
var validator = provider.GetRequiredService<IValidator<CreateUser>>();
var tagValidator = provider.GetRequiredService<CreateTagValidator>();

var invalid = await validator.ValidateAsync(new CreateUser
{
    Name = "",
    Age = 42,
    Tags = ["sample"]
});

if (invalid.IsValid || invalid.Errors.Count != 2)
{
    throw new InvalidOperationException("Generated validator did not report the expected failures.");
}

var validUser = new CreateUser
{
    Name = "Ada",
    Age = 37,
    Tags = ["sample"]
};

if (!await validator.IsValidAsync(validUser))
{
    throw new InvalidOperationException("Generated validator rejected a valid model.");
}

try
{
    _ = validator.IsValid(validUser);
    throw new InvalidOperationException("Synchronous IsValid should reject validators with asynchronous validations.");
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

if (tagValidator.IsValid(new CreateTag { Value = new string('x', 65), DoThis = true }))
{
    throw new InvalidOperationException("Generated synchronous IsValid accepted a tag rejected by the reused validator.");
}

if (!(await tagValidator.ValidateAsync(new CreateTag { Value = "docs", DoThis = true })).IsValid)
{
    throw new InvalidOperationException("Generated ValidateAsync rejected a valid sync-only model.");
}

Console.WriteLine("Valiant sample passed.");

public enum UserRole
{
    Admin,
    User,
    Guest
}

public sealed class CreateUser
{
    public string? Name { get; init; }

    public int Age { get; init; }
    
    public UserRole Role { get; init; }
    
    public string? RoleString { get; init; }
    
    public List<string>? Tags { get; init; }
    
    public IEnumerable<string>? TagCollection { get; init; }
    
    public Dictionary<string, string>? TagDictionary { get; init; }
}

public sealed class CreateTag
{
    public string? Value { get; init; }
    
    public bool DoThis { get; set; }
}

[ValiantValidator]
public sealed partial class CreateUserValidator : IValidatorDefinition<CreateUser>
{
    public static void Configure(ValidationContext<CreateUser> context)
    {
        context.Property(p => p.Role)
            .DefinedEnum();

        context.Property(p => p.RoleString)
            .EnumName<UserRole>();

        // foreach (var v in context.Instance.TagCollection)
        // {
        //     context.Item(context.Instance.TagCollection, v)
        //         .Required();
        // }

        context.Property(p => p.Tags)
            .NotEmpty();
        
        context.Property(user => user.Name)
            .Required()
            .MaxLength(64)
            .Satisfies(
                static name => name is null || !name.Contains('!'),
                code: "NameContainsBang",
                message: "Name must not contain exclamation marks.");

        context.Property(user => user.Age)
            .GreaterThanOrEqualTo(18)
            .SatisfiesAsync(
                static (age, _) => new ValueTask<bool>(age != 42),
                code: "ReservedAge",
                message: "Age 42 is reserved.");
    }
}

[ValiantValidator]
public sealed partial class CreateTagValueValidator : IValidatorDefinition<CreateTag>
{
    public static void Configure(ValidationContext<CreateTag> context)
    {
        context.Property(tag => tag.Value).MaxLength(64);
    }
}

[ValiantValidator]
public sealed partial class CreateTagValidator(CreateTagValueValidator valueValidator) : IValidatorDefinition<CreateTag>
{
    public void Configure(ValidationContext<CreateTag> context)
    {
        context.UseValidator(valueValidator);

        if (!context.Instance.DoThis)
        {
            context.Property(tag => tag.Value)
                .Empty(message: "Value must be empty when DoThis is false.");
        }
        else
        {
            context.Property(tag => tag.Value)
                .Required()
                .Satisfies(static (_, prop) =>
                {
                    return prop is null || prop.Length <= 64;
                });
        }
    }
}
