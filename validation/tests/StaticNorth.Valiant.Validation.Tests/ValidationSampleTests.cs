using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Valiant.Validation.Samples.AttributeValidation;
using Valiant.Validation.Samples.Basic;
using Valiant.Validation.Samples.Catalog;
using SampleAddressPropertyValidator = Valiant.Validation.Samples.Catalog.AddressPropertyValidator;

namespace Valiant.Validation.Tests;

public sealed class ValidationSampleTests
{
    [Test]
    public async Task Basic_Sample_Validates_Valid_And_Invalid_Models()
    {
        var validator = new CreateUserValidator();

        var valid = validator.Validate(new CreateUser("Ada"));
        var invalid = validator.Validate(new CreateUser(""));

        await Assert.That(valid.IsValid).IsTrue();
        await Assert.That(invalid.IsValid).IsFalse();
        await Assert.That(invalid.Errors[0].PropertyName).IsEqualTo("Name");
        await Assert.That(invalid.Errors[0].ErrorCode).IsEqualTo("Required");
        await Assert.That(invalid.Errors[0].ErrorMessage).IsEqualTo("Name is required.");
    }

    [Test]
    public async Task Attribute_Sample_Uses_Generated_Sync_And_Async_Validation()
    {
        var validator = new CreateUserRequestValidator();
        var valid = new CreateUserRequest
        {
            UserName = "ada",
            DisplayName = "Ada Lovelace"
        };
        var invalid = new CreateUserRequest
        {
            UserName = "",
            DisplayName = "This display name is intentionally too long for validation.",
            Age = 121
        };

        var syncResult = validator.Validate(invalid);
        var asyncResult = await validator.ValidateAsync(invalid);

        await Assert.That(validator.IsValid(valid)).IsTrue();
        await Assert.That(await validator.IsValidAsync(valid)).IsTrue();
        await Assert.That(syncResult.Errors.Select(static failure => failure.PropertyName).Distinct())
            .IsEquivalentTo(["UserName", "DisplayName", "Age"]);
        await Assert.That(asyncResult.Failures).IsEquivalentTo(syncResult.Failures);
    }

    [Test]
    public async Task DependencyInjection_Sample_Registers_Default_Concrete_Property_And_Metadata_Services()
    {
        var services = new ServiceCollection();
        SampleServiceRegistration.AddValidators(services, options =>
        {
            options.Mode = ValiantValidationMode.Fast;
            options.Lifetime = ValiantValidatorLifetime.Scoped;
            options.FailureBehavior = ValiantFailureBehavior.ValidateAll;
        });

        await Assert.That(services.Single(static descriptor =>
                descriptor.ServiceType == typeof(IValidator<CatalogItem>)).Lifetime)
            .IsEqualTo(ServiceLifetime.Scoped);
        await Assert.That(services.Any(static descriptor =>
            descriptor.ServiceType == typeof(CatalogItemRulesValidator))).IsTrue();
        await Assert.That(services.Any(static descriptor =>
            descriptor.ServiceType == typeof(IValidator<CatalogItem>) &&
            descriptor.ImplementationType == typeof(CatalogItemRulesValidator))).IsFalse();
        await Assert.That(services.Any(static descriptor =>
            descriptor.ServiceType == typeof(IPropertyValidator<Address>))).IsTrue();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<ValiantValidationOptions>();
        var metadata = scope.ServiceProvider.GetServices<IValiantValidationMetadataProvider>()
            .Single(provider => provider.ModelType == typeof(CatalogItem));

        await Assert.That(options.Mode).IsEqualTo(ValiantValidationMode.Fast);
        await Assert.That(options.Lifetime).IsEqualTo(ValiantValidatorLifetime.Scoped);
        await Assert.That(options.FailureBehavior).IsEqualTo(ValiantFailureBehavior.ValidateAll);
        await Assert.That(scope.ServiceProvider.GetRequiredService<IValidator<CatalogItem>>())
            .IsTypeOf<CatalogItemValidator>();
        await Assert.That(scope.ServiceProvider.GetRequiredService<CatalogItemRulesValidator>())
            .IsNotNull();
        await Assert.That(scope.ServiceProvider.GetRequiredService<SampleAddressPropertyValidator>())
            .IsNotNull();
        await Assert.That(metadata.Properties).IsNotEmpty();
    }

    [Test]
    public async Task Sync_And_Async_Sample_Covers_Boolean_APIs_And_Sync_Guard()
    {
        using var provider = SampleServiceRegistration.CreateProvider();
        var syncValidator = provider.GetRequiredService<CatalogItemRulesValidator>();
        var asyncValidator = provider.GetRequiredService<CatalogItemValidator>();
        var valid = CatalogItemFactory.Valid();

        await Assert.That(syncValidator.IsValid(valid)).IsTrue();
        await Assert.That(await asyncValidator.IsValidAsync(valid)).IsTrue();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Task.FromResult(asyncValidator.Validate(valid)));
    }

    [Test]
    public async Task Severity_Sample_Separates_Warnings_And_Info_From_Errors()
    {
        using var provider = SampleServiceRegistration.CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var valid = CatalogItemFactory.Valid();
        var warningAndInfo = valid with
        {
            Description = "This description is intentionally longer than the warning threshold.",
            ShippingAddress = valid.ShippingAddress! with { Line2 = "Optional second line" }
        };

        var result = await validator.ValidateAsync(warningAndInfo);

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.Warnings).IsNotEmpty();
        await Assert.That(result.Infos).IsNotEmpty();
    }

    [Test]
    public async Task Composition_And_Paths_Sample_Covers_Whole_Model_Property_And_Indexed_Reuse()
    {
        using var provider = SampleServiceRegistration.CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();

        var reservedResult = await validator.ValidateAsync(CatalogItemFactory.Valid() with { Sku = "RESERVED" });
        var invalidResult = await validator.ValidateAsync(CatalogItemFactory.Invalid());
        var paths = invalidResult.Failures.Select(static failure => failure.PropertyName).ToArray();

        await Assert.That(reservedResult.Errors.Select(static failure => failure.ErrorCode))
            .Contains("ReservedSku");
        await Assert.That(paths).Contains("ShippingAddress.Line1");
        await Assert.That(paths).Contains("ShippingAddress.Zip");
        await Assert.That(paths).Contains("Variants[0].Code");
        await Assert.That(paths).Contains("AuditEntries[0].Actor");
    }

    [Test]
    public async Task Failure_Handling_Sample_Covers_Per_Chain_FailFast_And_ThrowIfFailed()
    {
        using var provider = SampleServiceRegistration.CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemValidator>();
        var result = await validator.ValidateAsync(CatalogItemFactory.Invalid());

        await Assert.That(result.Failures.Count(static failure =>
            failure.PropertyName == "Name" && failure.ErrorCode == "NameRequired")).IsEqualTo(1);
        await Assert.That(result.Failures.Any(static failure =>
            failure.PropertyName == "Name" && failure.ErrorCode == "NameTooShort")).IsFalse();

        var exception = await Assert.ThrowsAsync<ValiantValidationException>(
            () => Task.FromResult(result.ThrowIfFailed()));
        await Assert.That(exception!.Result).IsSameReferenceAs(result);
    }

    [Test]
    public async Task Control_Flow_And_Collections_Sample_Covers_Rule_Families_And_Native_Loops()
    {
        using var provider = SampleServiceRegistration.CreateProvider();
        var validator = provider.GetRequiredService<CatalogItemRulesValidator>();
        var result = validator.Validate(CatalogItemFactory.Invalid());

        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Tags" && failure.ErrorCode == "Contains")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Metadata" && failure.ErrorCode == "HasKey")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "ApprovalCode" && failure.ErrorCode == "Empty")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "ShippingAddress.Line1" && failure.ErrorCode == "Required")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Variants[0].Code" && failure.ErrorCode == "Required")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "AuditEntries[0].Actor" && failure.ErrorCode == "Required")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Sku" && failure.ErrorCode == "ForLoopSkuRequired")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Name" && failure.ErrorCode == "WhileLoopNameRequired")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "ApiToken" && failure.ErrorCode == "DoLoopApiTokenRequired")).IsTrue();
        await Assert.That(result.Errors.Any(static failure =>
            failure.PropertyName == "Variants[0].Quantity")).IsTrue();
    }
}

internal static class SampleServiceRegistration
{
    public static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        AddValidators(services, options => options.Mode = ValiantValidationMode.Fast);
        return services.BuildServiceProvider();
    }

    public static void AddValidators(
        IServiceCollection services,
        Action<ValiantValidationOptions>? configure = null)
    {
        var extensionType = typeof(CatalogItemValidator).Assembly.GetType(
            "Valiant.Validation.DependencyInjection.ValiantServiceCollectionExtensions",
            throwOnError: true)!;
        var method = extensionType.GetMethod(
            "AddValiantValidators",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [typeof(IServiceCollection), typeof(Action<ValiantValidationOptions>)],
            modifiers: null)!;

        _ = method.Invoke(null, [services, configure]);
    }
}
