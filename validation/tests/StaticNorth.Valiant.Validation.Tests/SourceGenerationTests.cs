using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StaticNorth.Valiant.SourceGeneration.Testing;
using StaticNorth.Valiant.Validation.SourceGeneration;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class SourceGenerationTests
{
    [Test]
    [Arguments("context.Property(x => x.Name).Required().MinLength(3);", true)]
    [Arguments("context.Property(x => x.Name).Required(); return;", false)]
    [Arguments("context.Property(x => x.Name).Required(message: BuildMessage());", false)]
    [Arguments("context.Property(x => x.Name).MinLength(Threshold);", false)]
    [Arguments("context.Property(x => x.Name).MinLength(3, \"custom\");", false)]
    [Arguments("context.Property(x => x.Name).When(static _ => true).Required();", false)]
    [Arguments("context.Property(x => x.Name).MaxLength(3);", false)]
    [Arguments("context.Property(x => x.Name).Required(); context.Property(x => x.Name);", false)]
    public async Task Simple_Fluent_Emission_Requires_Bound_Built_In_Checks_And_Safe_Declarations(string rules, bool generated)
    {
        var result = RunGenerator(
            $$"""
            using StaticNorth.Valiant.Validation;

            public static class CustomRules
            {
                public static PropertyValidation<Person, string?> MaxLength(this PropertyValidation<Person, string?> property, int length) =>
                    property.Required(message: length.ToString());
            }

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>
            {
                private static string BuildMessage() => "custom";
                private const int Threshold = 3;

                public override void Configure(ValidationContext<Person> context)
                {
                    {{rules}}
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var source = result.GeneratedSources.Single(static item =>
            item.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(source.Contains("var value0 = instance.Name;", StringComparison.Ordinal)).IsEqualTo(generated);
        await Assert.That(source).Contains("Configure(context);");
        await Assert.That(source.Contains("_valiantValidationOptions?.Mode == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable", StringComparison.Ordinal))
            .IsEqualTo(generated);
        if (generated)
        {
            await Assert.That(source).Contains("return ConfigureValidation(instance).ToResult();");
            await Assert.That(source).Contains("return await ConfigureValidation(instance).ToResultAsync(cancellationToken).ConfigureAwait(false);");
            await Assert.That(source).Contains("instance) => Validate(instance).IsValid;");
            await Assert.That(source).Contains("await ValidateAsync(instance, cancellationToken).ConfigureAwait(false)");
        }
    }

    [Test]
    [Arguments("_child", true)]
    [Arguments("GetChild()", false)]
    public async Task Simple_Fluent_Child_Reuse_Only_Skips_Configure_For_A_Readonly_Dependency(string dependency, bool generated)
    {
        var source = $$"""
            using StaticNorth.Valiant.Validation;

            public sealed record Parent(Child? Child);
            public sealed record Child(string? Name);

            [ValiantValidator]
            public sealed partial class ParentValidator(IValidator<Child> child) : ValidatorDefinition<Parent>
            {
                private readonly IValidator<Child> _child = child;

                public override void Configure(ValidationContext<Parent> context)
                {
                    context.Property(x => x.Child).Children(nested => nested.UseValidator({{dependency}}));
                }

                private IValidator<Child> GetChild() => _child;
            }
            """;

        var result = RunGenerator(source);
        var emitted = result.GeneratedSources.Single(static item =>
            item.HintName.EndsWith("ParentValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(RunCompilation(source).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(emitted).Contains("Configure(context);");
        await Assert.That(emitted.Contains("Mode == global::StaticNorth.Valiant.Validation.ValiantValidationMode.Debuggable", StringComparison.Ordinal))
            .IsEqualTo(generated);
        if (generated)
        {
            await Assert.That(emitted).Contains("childValidator0.Validate(child0)");
            await Assert.That(emitted).Contains("await childValidator0.ValidateAsync(child0, cancellationToken)");
        }
    }

    [Test]
    public async Task DI_Status_Only_Configures_Parent_And_Child_In_Explicit_Debuggable_Mode()
    {
        const string source = """
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using StaticNorth.Valiant.Validation;
            using StaticNorth.Valiant.Validation.DependencyInjection;

            public static class StatusProbe
            {
                public static async Task<(bool Valid, int ParentCalls, int ChildCalls)> Run(
                    ValiantValidationMode mode, bool useAsync, bool validChild)
                {
                    var services = new ServiceCollection();
                    ValiantServiceCollectionExtensions.AddValiantValidators(services, options => options.Mode = mode);
                    using var provider = services.BuildServiceProvider();
                    var validator = provider.GetRequiredService<IValidator<Parent>>();
                    var child = new Child(validChild ? "valid" : "");
                    var parent = new Parent("valid", child);
                    var valid = useAsync ? await validator.IsValidAsync(parent) : validator.IsValid(parent);
                    return (valid, parent.ConfigureCalls, child.ConfigureCalls);
                }
            }

            public sealed record Parent(string Title, Child Child)
            {
                public int ConfigureCalls { get; set; }
            }

            public sealed record Child(string Name)
            {
                public int ConfigureCalls { get; set; }
            }

            [ValiantValidator]
            public sealed partial class ParentValidator(IValidator<Child> child) : ValidatorDefinition<Parent>
            {
                private readonly IValidator<Child> _child = child;

                public override void Configure(ValidationContext<Parent> context)
                {
            #if COUNT_CONFIGURE_CALLS
                    context.Instance.ConfigureCalls++;
            #endif
                    context.Property(x => x.Title).Required();
                    context.Property(x => x.Child).Children(nested => nested.UseValidator(_child));
                }
            }

            [ValiantValidator]
            public sealed partial class ChildValidator : ValidatorDefinition<Child>
            {
                public override void Configure(ValidationContext<Child> context)
                {
            #if COUNT_CONFIGURE_CALLS
                    context.Instance.ConfigureCalls++;
            #endif
                    context.Property(x => x.Name).Required();
                }
            }
            """;

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = GeneratorTestHarness.CreateCompilation(source, parseOptions, GetTrustedPlatformReferences(),
            "StaticNorth.Valiant.Validation.GeneratedStatusProbe");
#if !DEBUG
        compilation = compilation.WithOptions(compilation.Options.WithOptimizationLevel(OptimizationLevel.Release));
#endif
        var generated = GeneratorTestHarness.Run(new ValiantGenerator(), compilation, parseOptions);
        await Assert.That(generated.GeneratorDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();

        // Enable the counters after generation so Configure still qualifies for generated validation.
        var originalTree = compilation.SyntaxTrees.Single();
        var instrumentedTree = CSharpSyntaxTree.ParseText(source, parseOptions.WithPreprocessorSymbols("COUNT_CONFIGURE_CALLS"), originalTree.FilePath);
        var instrumented = generated.OutputCompilation.ReplaceSyntaxTree(originalTree, instrumentedTree);
        using var stream = new MemoryStream();
        var emit = instrumented.Emit(stream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        }

        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var run = assembly.GetType("StatusProbe")!.GetMethod("Run")!
            .CreateDelegate<Func<ValiantValidationMode, bool, bool, Task<(bool Valid, int ParentCalls, int ChildCalls)>>>();

        foreach (var mode in new[] { ValiantValidationMode.Auto, ValiantValidationMode.Fast, ValiantValidationMode.Debuggable })
        {
            var expectedCalls = mode == ValiantValidationMode.Debuggable ? 1 : 0;
            foreach (var useAsync in new[] { false, true })
            {
                foreach (var validChild in new[] { false, true })
                {
                    var result = await run(mode, useAsync, validChild);
                    var scenario = $"{mode}, {(useAsync ? "IsValidAsync" : "IsValid")}, valid child: {validChild}";
                    await Assert.That(result).IsEqualTo((validChild, expectedCalls, expectedCalls)).Because(scenario);
                }
            }
        }
    }

    [Test]
    [Arguments("[Required]", true, 1)]
    [Arguments("[Required]", true, 6)]
    [Arguments("[Required]", false, 7)]
    [Arguments("[Required(Message = \"custom\")]", false, 1)]
    [Arguments("[Required(Code = \"custom\")]", false, 1)]
    [Arguments("[Required(Severity = ValiantSeverity.Warning)]", false, 1)]
    public async Task Attribute_Failure_Reuse_Is_Bounded_And_Uses_Default_Diagnostics(string attributes, bool expectedCache, int checkCount)
    {
        var properties = string.Join(Environment.NewLine, Enumerable.Range(0, checkCount)
            .Select(index => attributes + " public string? Name" + index + " { get; init; }"));
        var source = $$"""
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                {{properties}}
            }
            """;
        var result = RunGenerator(source);
        var generated = result.GeneratedSources.Single(static source =>
            source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(generated.Contains("__valiantFixedFailureCache", StringComparison.Ordinal)).IsEqualTo(expectedCache);
        await Assert.That(RunCompilation(source).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Marker_Attribute_With_Interface_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
        await Assert.That(generated.Contains("/// Validates instances using the rules declared for this validator.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// Validates the specified instance.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// Determines whether the specified instance is valid.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// Asynchronously validates the specified instance.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("void global::StaticNorth.Valiant.Validation.IValidatorDefinition<global::Person>.Configure(global::StaticNorth.Valiant.Validation.ValidationContext<global::Person> context)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("((global::StaticNorth.Valiant.Validation.IValidatorDefinition<global::Person>)this).Configure(context);", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Fluent_Validator_Uses_One_Execution_Path_In_Every_Mode()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name)
                        .Required()
                        .SatisfiesAsync(static (_, _) => new System.Threading.Tasks.ValueTask<bool>(true));
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static source =>
            source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("This validator contains asynchronous validations. Call ValidateAsync instead.");
        await Assert.That(generated).Contains("return await ConfigureValidation(instance).ToResultAsync(cancellationToken).ConfigureAwait(false);");
        await Assert.That(generated).Contains("instance) => Validate(instance).IsValid;");
        await Assert.That(generated).Contains("return await ConfigureValidation(instance).ToStatusAsync(cancellationToken).ConfigureAwait(false);");
        await Assert.That(generated.Contains("GetEffectiveValidationMode", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("var value0 = instance.Name;", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Attribute_Validator_Precomputes_Only_Culture_Invariant_Integer_Literal_Messages()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [InclusiveBetween(18, 99)]
                public int Age { get; init; }

                [GreaterThan(-10)]
                public int Score { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static source =>
            source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();

        await Assert.That(generated).Contains("\"'Age' must be between 18 and 99.\"");
        await Assert.That(generated).Contains("\" must be greater than \" + (-10) + \".\"");
    }

    [Test]
    public async Task Base_Validator_Without_Configure_Uses_Validation_Attributes()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [Required]
                [MaxLength(64)]
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLT104")).IsFalse();
        await Assert.That(generated.Contains("public global::StaticNorth.Valiant.Validation.ValidationResult Validate(global::Person instance)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("public override ", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("private global::StaticNorth.Valiant.Validation.ValiantFailureBehavior GetEffectiveFailureBehavior()", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("var value0 = instance.Name;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("instance.instance", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("global::System.String.IsNullOrWhiteSpace(value0)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("global::StaticNorth.Valiant.Validation.ValidationChecks.HasMaxLength", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Base_Validator_Without_Configure_And_Without_Validation_Attributes_Reports_Warning()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT104");
    }

    [Test]
    public async Task Base_Validator_With_Configure_And_Validation_Attributes_Reports_Error()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>
            {
                public override void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Age).GreaterThan(0);
                }
            }

            public sealed class Person
            {
                [Required]
                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLT105" && x.Severity == DiagnosticSeverity.Error)).IsTrue();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Duplicate_Default_Validator_Model_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class UserValidator : IValidatorDefinition<User>
            {
                public static void Configure(ValidationContext<User> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            [ValiantValidator]
            public sealed partial class UserValidator2 : IValidatorDefinition<User>
            {
                public static void Configure(ValidationContext<User> context)
                {
                    context.Property(x => x.Name).MaxLength(64);
                }
            }

            public sealed class User
            {
                public string? Name { get; init; }
            }
            """);

        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "VLT106");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("UserValidator2");
        await Assert.That(diagnostic.GetMessage()).Contains("UserValidator");
        await Assert.That(diagnostic.GetMessage()).Contains("global::User");
    }

    [Test]
    public async Task Non_Default_Validator_Does_Not_Report_Duplicate_Model_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator(RegisterAsDefault = false)]
            public sealed partial class UserNameValidator : IValidatorDefinition<User>
            {
                public static void Configure(ValidationContext<User> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            [ValiantValidator]
            public sealed partial class UserValidator : IValidatorDefinition<User>
            {
                private readonly UserNameValidator _userNameValidator;

                public UserValidator(UserNameValidator userNameValidator)
                {
                    _userNameValidator = userNameValidator;
                }

                public void Configure(ValidationContext<User> context)
                {
                    context.UseValidator(_userNameValidator);
                    context.Property(x => x.Name).MaxLength(64);
                }
            }

            public sealed class User
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Base_Validator_With_Incompatible_Validation_Attribute_Reports_Error()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [MaxLength(10)]
                public int Age { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static x =>
            x.Id == "VLT007" &&
            x.Severity == DiagnosticSeverity.Error &&
            x.GetMessage().Contains("MaxLength", StringComparison.Ordinal) &&
            x.GetMessage().Contains("Age", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Base_Validator_With_Compatible_Collection_Validation_Attribute_Does_Not_Report_Type_Error()
    {
        var result = RunGenerator(
            """
            using System.Collections;
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [MinLength(2)]
                public ICollection? Tags { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLT007")).IsFalse();
        await Assert.That(generated.Contains("global::StaticNorth.Valiant.Validation.ValidationChecks.HasMinLength", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Base_Validator_Attributes_Support_Metadata_And_Condition()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [Required("NameRequired", "Name is required.", ValiantSeverity.Warning, ConditionMethod = nameof(ShouldValidateName))]
                public string? Name { get; init; }

                public static bool ShouldValidateName(Person person) => person.Name != "skip";
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("((global::System.Func<global::Person, bool>)(global::Person.ShouldValidateName))(instance)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("(\"NameRequired\" ?? \"Required\")", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("(\"Name is required.\" ?? \"'Name' is required.\")", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("global::StaticNorth.Valiant.Validation.ValiantSeverity.Warning", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Generic_Attribute_Compatibility_Form_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator<Person>]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Typeof_Attribute_Compatibility_Form_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator(typeof(Person))]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Nested_Validator_In_Partial_Containing_Type_Generates_Validator()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            public partial class ValidatorContainer
            {
                [ValiantValidator]
                public sealed partial class PersonValidator : IValidatorDefinition<Person>
                {
                    public static void Configure(ValidationContext<Person> context)
                    {
                        context.Property(x => x.Name).Required();
                    }
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("ValidatorContainer_PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("public partial class ValidatorContainer", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("public sealed partial class PersonValidator : global::StaticNorth.Valiant.Validation.IValidator<global::Person>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("internal sealed class __ValiantValidatorContainer_PersonValidatorMetadataProvider", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Nested_Validator_In_Non_Partial_Containing_Type_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            public class ValidatorContainer
            {
                [ValiantValidator]
                public sealed partial class PersonValidator : IValidatorDefinition<Person>
                {
                    public static void Configure(ValidationContext<Person> context)
                    {
                        context.Property(x => x.Name).Required();
                    }
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT002");
    }

    [Test]
    public async Task Missing_Partial_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT001");
    }

    [Test]
    public async Task Missing_Definition_Interface_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT005");
    }

    [Test]
    public async Task Attribute_And_Interface_Model_Mismatch_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator(typeof(OtherPerson))]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }

            public sealed class OtherPerson
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT006");
    }

    [Test]
    public async Task Missing_Configure_Method_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT003");
    }

    [Test]
    public async Task Non_Public_Configure_Method_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    [Test]
    public async Task Unsupported_Rule_Syntax_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    throw new System.InvalidOperationException();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    [Test]
    public async Task Bare_Property_Declaration_Is_A_No_Op()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Age);
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public int? Age { get; init; }

                public string? Name { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Missing_Property_Selector_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Missing).GreaterThan(1);
                }
            }

            public sealed class Person
            {
                public int Age { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    [Test]
    public async Task Analyzer_Suggestions_Report_Redundant_Duplicate_And_Overridden_Checks()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name)
                        .FailFast()
                        .ValidateAll()
                        .Required()
                        .NotNull()
                        .Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var warningIds = result.Diagnostics
            .Where(static x => x.Severity == DiagnosticSeverity.Warning)
            .Select(static x => x.Id)
            .ToArray();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(warningIds).Contains("VLT101");
        await Assert.That(warningIds).Contains("VLT102");
        await Assert.That(warningIds).Contains("VLT103");
    }

    [Test]
    public async Task If_Else_Rules_Have_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    if (context.Instance.RequiresName)
                    {
                        context.Property(x => x.Name).Required();
                    }
                    else
                    {
                        context.Property(x => x.Age).GreaterThanOrEqualTo(18);
                    }
                }
            }

            public sealed class Person
            {
                public bool RequiresName { get; init; }

                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Name\", typeof(string), true");
        await Assert.That(generated).Contains("\"Age\", typeof(int), true");
    }

    [Test]
    public async Task When_Rule_Has_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Age)
                        .GreaterThanOrEqualTo(18)
                        .When(request => request.RequiresAge);
                }
            }

            public sealed class Person
            {
                public bool RequiresAge { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Age\", typeof(int), true");
    }

    [Test]
    public async Task When_Nullable_Rule_Has_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class ProductValidator : IValidatorDefinition<Product>
            {
                public static void Configure(ValidationContext<Product> context)
                {
                    context.Property(request => request.PriceMin)
                        .LessThanOrEqualTo(100m)
                        .When(request => request.PriceMax.HasValue);
                }
            }

            public sealed class Product
            {
                public decimal PriceMin { get; init; }

                public decimal? PriceMax { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("ProductValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"PriceMin\", typeof(decimal), true");
    }

    [Test]
    public async Task Child_Property_Rule_Metadata_Uses_Full_Path()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Address.Line1).Required();
                }
            }

            public sealed class Person
            {
                public Address? Address { get; init; }
            }

            public sealed class Address
            {
                public string? Line1 { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Address.Line1\", typeof(string), false");
    }

    [Test]
    public async Task Children_Rules_Have_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Address).Children((child, wholeModel) =>
                    {
                        child.Property(x => x.Line1).Required();
                        if (wholeModel.Address is not null)
                        {
                            child.Property(x => x.Zip).Required();
                        }
                    });
                }
            }

            public sealed class Person
            {
                public Address? Address { get; init; }
            }

            public sealed class Address
            {
                public string? Line1 { get; init; }

                public string? Zip { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Line1\", typeof(string), true");
        await Assert.That(generated).Contains("\"Zip\", typeof(string), true");
    }

    [Test]
    public async Task Child_When_Rule_Has_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Address)
                        .Children(child =>
                        {
                            child.Property(x => x.Line1)
                                .Required()
                                .When(address => address.RequiresLine1);
                        });
                }
            }

            public sealed class Person
            {
                public Address? Address { get; init; }
            }

            public sealed class Address
            {
                public bool RequiresLine1 { get; init; }

                public string? Line1 { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Line1\", typeof(string), true");
    }

    [Test]
    public async Task Switch_Rules_Have_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    switch (context.Instance.Kind)
                    {
                        case PersonKind.Child:
                            context.Property(x => x.Age).LessThan(18);
                            break;
                        case PersonKind.Adult when context.Instance.RequiresName:
                            context.Property(x => x.Name).Required();
                            break;
                        default:
                            context.Property(x => x.Age).GreaterThanOrEqualTo(0);
                            break;
                    }
                }
            }

            public enum PersonKind
            {
                Child,
                Adult
            }

            public sealed class Person
            {
                public PersonKind Kind { get; init; }

                public bool RequiresName { get; init; }

                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Name\", typeof(string), true");
    }

    [Test]
    public async Task Generated_Validator_Preserves_User_Usings_For_Rewritten_Expressions()
    {
        var diagnostics = RunCompilation(
            """
            namespace Models
            {
                public enum PersonKind
                {
                    Child,
                    Adult
                }

                public sealed class Person
                {
                    public PersonKind Kind { get; init; }

                    public string? Name { get; init; }
                }
            }

            namespace Validators
            {
                using Models;
                using StaticNorth.Valiant.Validation;

                [ValiantValidator]
                public sealed partial class PersonValidator : IValidatorDefinition<Person>
                {
                    public static void Configure(ValidationContext<Person> context)
                    {
                        switch (context.Instance.Kind)
                        {
                            case PersonKind.Adult:
                                context.Property(x => x.Name).Required(severity: ValiantSeverity.Warning);
                                break;
                        }
                    }
                }
            }
            """);

        await Assert.That(diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Loop_Rules_Have_Conditional_Metadata()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    foreach (var value in context.Instance.Values)
                    {
                        if (value < 0)
                        {
                            context.Property(x => x.Name).Required();
                        }
                    }

                    for (var index = 0; index < context.Instance.Values.Count; index++)
                    {
                        if (context.Instance.Values[index] == 0)
                        {
                            continue;
                        }

                        context.Property(x => x.Name).Required();
                    }

                    while (context.Instance.RunWhile)
                    {
                        context.Property(x => x.Name).Required();
                        break;
                    }

                    var runDo = context.Instance.RunDo;
                    do
                    {
                        if (runDo)
                        {
                            context.Property(x => x.Name).Required();
                        }

                        runDo = false;
                    }
                    while (runDo);
                }
            }

            public sealed class Person
            {
                public List<int> Values { get; init; } = new();

                public string? Name { get; init; }

                public bool RunWhile { get; init; }

                public bool RunDo { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Name\", typeof(string), true");
    }

    [Test]
    public async Task Collection_Iteration_And_Predicates_Generate_Metadata()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Values)
                        .Count(2)
                        .All(value => value > 0)
                        .Any(value => value == 7)
                        .None(value => value < 0);

                    context.Property(x => x.Children).ForEach((child, children) =>
                    {
                        if (children.Count > 0)
                        {
                            child.Property(x => x.Name).Required();
                        }
                    });
                }
            }

            public sealed class Person
            {
                public List<int> Values { get; init; } = new();

                public List<Child> Children { get; init; } = new();
            }

            public sealed class Child
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Count\"");
        await Assert.That(generated).Contains("\"All\"");
        await Assert.That(generated).Contains("\"Any\"");
        await Assert.That(generated).Contains("\"None\"");
        await Assert.That(generated).Contains("\"Name\", typeof(string), true");
    }

    [Test]
    public async Task Native_Foreach_Item_Metadata_Is_Conditional()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    var index = 0;
                    foreach (var child in context.Instance.Children)
                    {
                        context.Item(child, x => x.Children, index)
                            .Property(x => x.Name)
                            .Required();
                        index++;
                    }
                }
            }

            public sealed class Person
            {
                public List<Child> Children { get; init; } = new();
            }

            public sealed class Child
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"Name\", typeof(string), true");
    }

    [Test]
    public async Task Check_Metadata_Generates_Code_Message_And_Severity_On_Check()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                private const ValiantSeverity Advisory = ValiantSeverity.Warning;

                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required(
                        code: "NameRequired",
                        message: "Name is required.",
                        severity: Advisory);
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("\"NameRequired\", \"Name is required.\", \"Warning\"");
    }

    [Test]
    public async Task Validation_Metadata_Provider_Generates_Static_Rules_And_Conditional_Flags()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required(
                        code: "NameRequired",
                        message: "Name is required.",
                        severity: ValiantSeverity.Warning);

                    if (context.Instance.RequiresAge)
                    {
                        context.Property(x => x.Age).GreaterThanOrEqualTo(18);
                    }
                }
            }

            public sealed class Person
            {
                public bool RequiresAge { get; init; }

                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("internal sealed class __ValiantPersonValidatorMetadataProvider", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("new global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata(\"Name\", typeof(string), false", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("new global::StaticNorth.Valiant.Validation.ValiantValidationCheckMetadata(\"Required\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("\"NameRequired\", \"Name is required.\", \"Warning\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("new global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata(\"Age\", typeof(int), true", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Validation_Metadata_Provider_Marks_When_Rules_As_Conditional()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Age)
                        .GreaterThanOrEqualTo(18)
                        .When(request => request.RequiresAge);
                }
            }

            public sealed class Person
            {
                public bool RequiresAge { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("new global::StaticNorth.Valiant.Validation.ValiantPropertyValidationMetadata(\"Age\", typeof(int), true", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task UseValidator_Generates_Root_Validator_Dependency()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator(RegisterAsDefault = false)]
            public sealed partial class RequiredNameValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                private readonly RequiredNameValidator _requiredNameValidator;

                public PersonValidator(RequiredNameValidator requiredNameValidator)
                {
                    _requiredNameValidator = requiredNameValidator;
                }

                public void Configure(ValidationContext<Person> context)
                {
                    context.UseValidator(_requiredNameValidator);
                    context.Property(x => x.Age).GreaterThanOrEqualTo(18);
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains(".Configure(context);");
    }

    [Test]
    public async Task Property_UseValidator_Generates_Property_Validator_Dependency_And_Di_Registration()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed class AddressPropertyValidator : PropertyValidatorDefinition<Address>
            {
                public override ValidationResult Validate(Address value) => ValidationResult.Valid;
            }

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                private readonly AddressPropertyValidator _addressPropertyValidator;

                public PersonValidator(AddressPropertyValidator addressPropertyValidator)
                {
                    _addressPropertyValidator = addressPropertyValidator;
                }

                public void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Address).UseValidator(_addressPropertyValidator);
                }
            }

            public sealed class Person
            {
                public Address? Address { get; init; }
            }

            public sealed class Address
            {
                public string? Line1 { get; init; }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(System.Type serviceType, object implementationInstance) => new();

                    public static ServiceDescriptor Singleton(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Singleton<TService, TImplementation>() => new();

                    public static ServiceDescriptor Scoped(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Scoped<TService, TImplementation>() => new();

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Transient<TService, TImplementation>() => new();
                }

                namespace Extensions
                {
                    public static class ServiceCollectionDescriptorExtensions
                    {
                        public static void TryAdd(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }

                        public static void TryAddEnumerable(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }
                    }
                }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();
        var dependencyInjection = result.GeneratedSources.Single(static x => x.HintName == "StaticNorth.Valiant.Validation.DependencyInjection.g.cs").SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains(".Configure(context);");
        await Assert.That(dependencyInjection.Contains("CreateValidatorDescriptor<global::AddressPropertyValidator, global::AddressPropertyValidator>(valiantValidationOptions.Lifetime)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("CreateValidatorDescriptor<global::StaticNorth.Valiant.Validation.IPropertyValidator<global::Address>, global::AddressPropertyValidator>(valiantValidationOptions.Lifetime)", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Attribute_UseValidator_Generates_Property_Validator_Reuse()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed class AddressPropertyValidator : PropertyValidatorDefinition<Address>
            {
                public override System.Threading.Tasks.ValueTask<ValidationResult> ValidateAsync(Address value, System.Threading.CancellationToken cancellationToken = default) => new(ValidationResult.Valid);
            }

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [UseValidator<AddressPropertyValidator>]
                public Address? Address { get; init; }
            }

            public sealed class Address
            {
                [Required]
                public string? Line1 { get; init; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("new global::AddressPropertyValidator()", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains(".Validate(value0)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains(".ValidateAsync(value0, cancellationToken).ConfigureAwait(false)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("CombinePropertyPath(\"Address\", propertyValidatorFailure0.PropertyName)", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Attribute_UseValidator_With_Incompatible_Validator_Reports_Error()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed class AddressPropertyValidator : PropertyValidatorDefinition<Address>
            {
                public override ValidationResult Validate(Address value) => ValidationResult.Valid;
            }

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [UseValidator<AddressPropertyValidator>]
                public string? Address { get; init; }
            }

            public sealed class Address
            {
                [Required]
                public string? Line1 { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static x =>
            x.Id == "VLT007" &&
            x.Severity == DiagnosticSeverity.Error &&
            x.GetMessage().Contains("UseValidator", StringComparison.Ordinal))).IsTrue();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task DependencyInjection_Extension_Uses_Configured_Validator_Lifetime()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(System.Type serviceType, object implementationInstance) => new();

                    public static ServiceDescriptor Singleton(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Singleton<TService, TImplementation>() => new();

                    public static ServiceDescriptor Scoped(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Scoped<TService, TImplementation>() => new();

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Transient<TService, TImplementation>() => new();
                }

                namespace Extensions
                {
                    public static class ServiceCollectionDescriptorExtensions
                    {
                        public static void TryAdd(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }

                        public static void TryAddEnumerable(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }
                    }
                }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName == "StaticNorth.Valiant.Validation.DependencyInjection.g.cs").SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("CreateValidatorDescriptor<global::PersonValidator, global::PersonValidator>(valiantValidationOptions.Lifetime)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("ServiceDescriptor.Singleton<global::StaticNorth.Valiant.Validation.Infrastructure.IValiantValidatorInvoker, global::StaticNorth.Valiant.Validation.Infrastructure.ValiantValidatorInvoker<global::Person>>()", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("case global::StaticNorth.Valiant.Validation.ValiantValidatorLifetime.Scoped:", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("return global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Transient<TService, TImplementation>();", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task DependencyInjection_Extension_Is_Generated_When_No_Validators_Are_Discovered()
    {
        var result = RunGenerator(
            """
            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(System.Type serviceType, object implementationInstance) => new();

                    public static ServiceDescriptor Singleton(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Singleton<TService, TImplementation>() => new();

                    public static ServiceDescriptor Scoped(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Scoped<TService, TImplementation>() => new();

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Transient<TService, TImplementation>() => new();
                }

                namespace Extensions
                {
                    public static class ServiceCollectionDescriptorExtensions
                    {
                        public static void TryAdd(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }

                        public static void TryAddEnumerable(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }
                    }
                }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName == "StaticNorth.Valiant.Validation.DependencyInjection.g.cs").SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("/// Provides dependency injection registration helpers", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// Registers all Valiant validators discovered in the current compilation.", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// <param name=\"services\">The service collection to add validators to.</param>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("/// <returns>The same service collection instance so calls can be chained.</returns>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("AddValiantValidators", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("AddValiantValidations", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("ValiantValidationOptions", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("TryAdd(services, CreateValidatorDescriptor<", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("IValiantValidatorInvoker", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Unrelated_NonValidator_Edit_Does_Not_Change_Generated_Validator_Source()
    {
        const string before = """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }

            public sealed class Unrelated
            {
                public int Value { get; init; }
            }
            """;

        const string after = """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }

            public sealed class Unrelated
            {
                public int Value { get; init; }

                public string? Description { get; init; }
            }
            """;

        var beforeSource = RunGenerator(before).GeneratedSources
            .Single(static source => source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();
        var afterSource = RunGenerator(after).GeneratedSources
            .Single(static source => source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();

        await Assert.That(afterSource).IsEqualTo(beforeSource);
    }

    [Test]
    [Arguments("IValidatorDefinition<Person>", "static")]
    [Arguments("ValidatorDefinition<Person>", "override")]
    public async Task Unrelated_Edit_Leaves_Tracked_Validator_Step_Cached_Or_Unchanged(string definition, string modifier)
    {
        var validator = $$"""
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : {{definition}}
            {
                public {{modifier}} void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """;

        var reasons = RunTrackedGeneratorEdit(
            validator + "\npublic sealed class Unrelated { public int Value { get; init; } }",
            validator + "\npublic sealed class Unrelated { public int Value { get; init; } public string? Text { get; init; } }",
            "Validation.NonGenericValidator");

        await Assert.That(reasons).IsNotEmpty();
        await Assert.That(reasons.All(static reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).IsTrue();
    }

    [Test]
    public async Task Labeled_Jump_Tracking_Changes_When_The_Target_Changes()
    {
        const string imports = "using StaticNorth.Valiant.Validation;";
        const string validator = """
            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>
            {
                public override void Configure(ValidationContext<Person> context)
                {
                    outer: for (var row = 0; row < 2; row++)
                    {
                        inner: for (var column = 0; column < 2; column++)
                        {
                            if (row == 0)
                            {
                                break outer;
                            }

                            if (column == 0)
                            {
                                continue inner;
                            }

                            context.Property(person => person.Name).Required();
                        }

                        continue outer;
                    }
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """;
        const string stepName = "Validation.NonGenericValidator";
        var source = imports + "\n" + validator;

        var retargeted = RunTrackedGeneratorEdit(
            source,
            source.Replace("break outer;", "break inner;", StringComparison.Ordinal),
            stepName);
        var originalSource = RunGenerator(source).GeneratedSources
            .Single(static generated => generated.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();
        var shiftedSource = RunGenerator(imports + "\npublic sealed class Unrelated { }\n" + validator).GeneratedSources
            .Single(static generated => generated.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();

        await Assert.That(retargeted).Contains(IncrementalStepRunReason.Modified);
        await Assert.That(shiftedSource).IsEqualTo(originalSource);
    }

    [Test]
    public async Task Diagnostic_Location_Preserves_Line_Mapping_Without_Retaining_Syntax_Tree()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            #line 200 "mapped-validator.cs"
            [ValiantValidator]
            public sealed class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context) { }
            }
            #line default

            public sealed class Person;
            """);

        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "VLT001");
        var lineSpan = diagnostic.Location.GetLineSpan();

        await Assert.That(lineSpan.Path).IsEqualTo("mapped-validator.cs");
        await Assert.That(lineSpan.StartLinePosition.Line).IsEqualTo(200);
        await Assert.That(diagnostic.Location.SourceTree).IsNull();
    }

    [Test]
    public async Task Diagnostic_Location_Updates_When_Validator_File_Moves()
    {
        const string source = """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context) { }
            }

            public sealed class Person;
            """;
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);
        var beforeCompilation = CSharpCompilation.Create(
            "StaticNorth.Valiant.Validation.FileMove.Tests",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "/tests/Before.cs")],
            references,
            options);
        var afterCompilation = CSharpCompilation.Create(
            "StaticNorth.Valiant.Validation.FileMove.Tests",
            [CSharpSyntaxTree.ParseText(source, parseOptions, path: "/tests/After.cs")],
            references,
            options);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ValiantGenerator())
            .WithUpdatedParseOptions(parseOptions);

        driver = driver.RunGenerators(beforeCompilation);
        driver = driver.RunGenerators(afterCompilation);

        var diagnostic = driver.GetRunResult().Results.Single().Diagnostics
            .Single(static diagnostic => diagnostic.Id == "VLT001");
        await Assert.That(diagnostic.Location.GetLineSpan().Path).IsEqualTo("/tests/After.cs");
    }

    [Test]
    public async Task Generated_Result_Builder_Uses_Minimal_Capacity_For_FailFast()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : ValidatorDefinition<Person>;

            public sealed class Person
            {
                [Required(Message = "required")]
                [MaxLength(100)]
                public string? Name { get; init; }
            }
            """);
        var generated = result.GeneratedSources
            .Single(static source => source.HintName.EndsWith("PersonValidator.Validator.g.cs", StringComparison.Ordinal))
            .SourceText
            .ToString();

        await Assert.That(generated).Contains("validationFailureBehavior == global::StaticNorth.Valiant.Validation.ValiantFailureBehavior.FailFast ? 1 : 2");
    }

    [Test]
    public async Task Rule_Body_Edit_Does_Not_Change_DependencyInjection_Source()
    {
        const string dependencyInjectionTypes = """

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(System.Type serviceType, object implementationInstance) => new();

                    public static ServiceDescriptor Singleton(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Singleton<TService, TImplementation>() => new();

                    public static ServiceDescriptor Scoped(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Scoped<TService, TImplementation>() => new();

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Transient<TService, TImplementation>() => new();
                }

                namespace Extensions
                {
                    public static class ServiceCollectionDescriptorExtensions
                    {
                        public static void TryAdd(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }

                        public static void TryAddEnumerable(IServiceCollection services, ServiceDescriptor descriptor)
                        {
                        }
                    }
                }
            }
            """;

        var before = """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).Required();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """ + dependencyInjectionTypes;

        var after = """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).NotEmpty();
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }
            }
            """ + dependencyInjectionTypes;

        var beforeSource = RunGenerator(before).GeneratedSources
            .Single(static source => source.HintName == "StaticNorth.Valiant.Validation.DependencyInjection.g.cs")
            .SourceText
            .ToString();
        var afterSource = RunGenerator(after).GeneratedSources
            .Single(static source => source.HintName == "StaticNorth.Valiant.Validation.DependencyInjection.g.cs")
            .SourceText
            .ToString();

        await Assert.That(afterSource).IsEqualTo(beforeSource);
    }

    [Test]
    public async Task Type_Specific_Checks_Are_Not_Available_On_Wrong_Property_Types()
    {
        var diagnostics = RunCompilation(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Name).DefinedEnum();
                    context.Property(x => x.Age).NotNull();
                    context.Property(x => x.Age).MaxLength(10);
                    context.Property(x => x.Age).HasKey("id");
                }
            }

            public sealed class Person
            {
                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        var errorIds = diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error).Select(static x => x.Id).ToArray();
        await Assert.That(errorIds.Contains("CS1061") || errorIds.Contains("CS0452")).IsTrue();
    }

    [Test]
    public async Task String_Length_Checks_Are_Available_On_Non_Nullable_String_Properties()
    {
        var diagnostics = RunCompilation(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class RequestValidator : IValidatorDefinition<Request>
            {
                public static void Configure(ValidationContext<Request> context)
                {
                    context.Property(x => x.CredentialJson)
                        .NotEmpty()
                        .MaxLength(5000);
                }
            }

            public sealed record Request(string CredentialJson, bool RememberMe);
            """);

        await Assert.That(diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Nullable_Comparable_Checks_Compile()
    {
        var diagnostics = RunCompilation(
            """
            using System;
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Bool).GreaterThan(false);
                    context.Property(x => x.Byte).GreaterThan((byte)0);
                    context.Property(x => x.SByte).GreaterThan((sbyte)0);
                    context.Property(x => x.Short).GreaterThan((short)0);
                    context.Property(x => x.UShort).GreaterThan((ushort)0);
                    context.Property(x => x.Int).GreaterThan(0);
                    context.Property(x => x.UInt).GreaterThan(0u);
                    context.Property(x => x.Long).GreaterThan(0L);
                    context.Property(x => x.ULong).GreaterThan(0UL);
                    context.Property(x => x.Float).GreaterThan(0f);
                    context.Property(x => x.Double).GreaterThan(0d);
                    context.Property(x => x.Amount).GreaterThan(0m);
                    context.Property(x => x.Char).GreaterThan('a');
                    context.Property(x => x.DateTime).GreaterThan(new DateTime(2000, 1, 1));
                    context.Property(x => x.TimeSpan).GreaterThan(TimeSpan.Zero);
                    context.Property(x => x.Guid).GreaterThan(Guid.Empty);
                }
            }

            public sealed class Person
            {
                public bool? Bool { get; init; }

                public byte? Byte { get; init; }

                public sbyte? SByte { get; init; }

                public short? Short { get; init; }

                public ushort? UShort { get; init; }

                public int? Int { get; init; }

                public uint? UInt { get; init; }

                public long? Long { get; init; }

                public ulong? ULong { get; init; }

                public float? Float { get; init; }

                public double? Double { get; init; }

                public decimal? Amount { get; init; }

                public char? Char { get; init; }

                public DateTime? DateTime { get; init; }

                public TimeSpan? TimeSpan { get; init; }

                public Guid? Guid { get; init; }
            }
            """);

        await Assert.That(diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
    }

    [Test]
    public async Task Generic_Enum_Checks_Report_Diagnostic_On_Wrong_Property_Types()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Validation;

            [ValiantValidator]
            public sealed partial class PersonValidator : IValidatorDefinition<Person>
            {
                public static void Configure(ValidationContext<Person> context)
                {
                    context.Property(x => x.Age).EnumName<PersonKind>();
                    context.Property(x => x.Name).EnumValue<PersonKind>();
                }
            }

            public enum PersonKind
            {
                Unknown,
                Known
            }

            public sealed class Person
            {
                public string? Name { get; init; }

                public int Age { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLT004");
    }

    private static SourceGenerationResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));

        var compilation = GeneratorTestHarness.CreateCompilation(
            source,
            parseOptions,
            references,
            "StaticNorth.Valiant.Validation.SourceGeneration.Tests");
        var execution = GeneratorTestHarness.Run(new ValiantGenerator(), compilation, parseOptions);

        var diagnostics = execution.OutputCompilation.GetDiagnostics()
            .Concat(execution.GeneratorDiagnostics)
            .Where(static diagnostic => diagnostic.Id.StartsWith("VLT", StringComparison.Ordinal))
            .ToImmutableArray();

        return new SourceGenerationResult(
            diagnostics,
            execution.GeneratedSources);
    }

    private static ImmutableArray<IncrementalStepRunReason> RunTrackedGeneratorEdit(
        string before,
        string after,
        string stepName)
    {
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));
        return GeneratorTestHarness.RunTrackedGeneratorEdit(
            new ValiantGenerator(),
            before,
            after,
            stepName,
            references,
            "StaticNorth.Valiant.Validation.Tracked.Tests");
    }

    private static ImmutableArray<Diagnostic> RunCompilation(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "/tests/GeneratedInput.cs");

        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IValidator<>).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "StaticNorth.Valiant.Validation.SourceGeneration.Tests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ValiantGenerator())
            .WithUpdatedParseOptions(parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        return outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .ToImmutableArray();
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        return GeneratorTestHarness.GetTrustedPlatformReferences();
    }

    private sealed record SourceGenerationResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
