using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Valiant.Mapping;
using Valiant.Mapping.SourceGeneration;
using Valiant.SourceGeneration.Testing;

namespace Valiant.Mapping.Tests;

public sealed class SourceGenerationTests
{
    [Test]
    public async Task Trusted_Platform_References_Exclude_Local_Generated_Assemblies()
    {
        var referenceFileNames = GetTrustedPlatformReferences()
            .Select(static reference => Path.GetFileName(reference.Display));

        await Assert.That(referenceFileNames).DoesNotContain(Path.GetFileName(typeof(SourceGenerationTests).Assembly.Location));
        await Assert.That(referenceFileNames).DoesNotContain(Path.GetFileName(typeof(global::Valiant.Mapping.Samples.Basic.Person).Assembly.Location));
    }

    [Test]
    public async Task Mapping_Definition_Generates_Map_Method()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("public global::PersonDto Map(global::Person source)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("Name = source.Name", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("        var target = new global::PersonDto", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("        return target;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("BeforeMap", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("AfterMap", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("MapWithServiceProvider", StringComparison.Ordinal)).IsFalse();
        await Assert.That(generated.Contains("_valiantMapperCache", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Generated_Mapper_Contains_Fast_Debuggable_And_Auto_Dispatch_Paths()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name);
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static source =>
            source.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal))
            .SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains(
            "if (GetEffectiveMappingMode() == global::Valiant.Mapping.ValiantMappingMode.Debuggable)");
        await Assert.That(generated).Contains("return MapRuntime(source);");
        await Assert.That(generated).Contains("Name = source.Name");
        await Assert.That(generated).Contains(
            "var mode = _valiantMappingOptions?.Mode ?? global::Valiant.Mapping.ValiantMappingMode.Auto;");
        await Assert.That(generated).Contains("#if DEBUG");
        await Assert.That(generated).Contains(
            "return global::Valiant.Mapping.ValiantMappingMode.Debuggable;");
        await Assert.That(generated).Contains("#else");
        await Assert.That(generated).Contains(
            "return global::Valiant.Mapping.ValiantMappingMode.Fast;");
        await Assert.That(generated).Contains("Configure(context);");
    }

    [Test]
    public async Task Generated_Mapper_Implements_Runtime_Mapper_Interface()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public int Age { get; set; }
            }

            public sealed class PersonDto
            {
                public int Age { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("global::Valiant.Mapping.IMapper<global::Person, global::PersonDto>", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Generated_Mapper_Maps_Public_Properties()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string? Name { get; set; }

                public int Age { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }

                public int Age { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("Age")!.SetValue(source, 37);

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(target)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Age")!.GetValue(target)).IsEqualTo(37);
    }

    [Test]
    public async Task Generated_Mapper_Uses_Target_Constructor()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string Name { get; set; } = "";

                public int Age { get; set; }
            }

            public sealed record PersonDto(string Name, int Age);
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Grace");
        sourceType.GetProperty("Age")!.SetValue(source, 42);

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(target)).IsEqualTo("Grace");
        await Assert.That(targetType.GetProperty("Age")!.GetValue(target)).IsEqualTo(42);
    }

    [Test]
    public async Task Configure_Map_Overrides_Convention()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.EmailAddress, target => target.Email);
                }
            }

            public sealed class Person
            {
                public string? EmailAddress { get; set; }

                public int Age { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Email { get; set; }

                public int Age { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("EmailAddress")!.SetValue(source, "ada@example.test");
        sourceType.GetProperty("Age")!.SetValue(source, 37);

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Email")!.GetValue(target)).IsEqualTo("ada@example.test");
        await Assert.That(targetType.GetProperty("Age")!.GetValue(target)).IsEqualTo(0);
    }

    [Test]
    public async Task Configure_Disables_Convention_Mapping()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM103")).IsFalse();
        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM101" && x.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(generated.Contains("Name = source.Name", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Configure_Ignore_Suppresses_Unmapped_Target_Warning()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Ignore(target => target.Name);
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM103")).IsFalse();
        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM101" && x.Severity == DiagnosticSeverity.Warning)).IsFalse();
        await Assert.That(generated.Contains("Name = source.Name", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Configure_Ignore_When_Guards_Explicit_Map()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name);
                    context.Ignore(target => target.Name)
                        .When(source => source.HideName);
                }
            }

            public sealed class Person
            {
                public bool HideName { get; set; }

                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");

        var mapper = Activator.CreateInstance(mapperType)!;
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("HideName")!.SetValue(source, true);
        var ignoredTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(mappedTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Name")!.GetValue(ignoredTarget)).IsNull();
    }

    [Test]
    public async Task Configure_Map_When_Emits_Conditional_Assignment()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.EmailAddress, target => target.Email)
                        .When(source => source.IncludeEmail);
                }
            }

            public sealed class Person
            {
                public bool IncludeEmail { get; set; }

                public string? EmailAddress { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Email { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("EmailAddress")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType)!;
        var skippedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeEmail")!.SetValue(source, true);
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Email")!.GetValue(skippedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Email")!.GetValue(mappedTarget)).IsEqualTo("ada@example.test");
    }

    [Test]
    public async Task Configure_Map_When_Reports_Unmapped_Target_Member()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.EmailAddress, target => target.Email)
                        .When(source => source.IncludeEmail);
                }
            }

            public sealed class Person
            {
                public bool IncludeEmail { get; set; }

                public string? EmailAddress { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Email { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic =>
            diagnostic.Id == "VLM101" &&
            diagnostic.Severity == DiagnosticSeverity.Warning &&
            diagnostic.GetMessage().Contains("Email", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Configure_Map_Only_Reports_All_Other_Target_Members_As_Unmapped()
    {
        var result = RunGenerator(
            """
            using System;
            using Valiant.Mapping;

            [ValiantMapper]
            public partial class Mapper : MapperDefinition<From, To>
            {
                public override void Configure(MappingContext<From, To> context)
                {
                    context.Map(source => source.Name, target => target.Name);
                }
            }

            public record From
            {
                public int Id { get; set; }

                public string Name { get; set; } = "";

                public string? Description { get; set; }

                public DateTime CreatedAt { get; set; }
            }

            public record To
            {
                public int Id { get; set; }

                public string Name { get; set; } = "";

                public string? Description { get; set; }

                public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            }
            """);

        var unmappedMessages = result.Diagnostics
            .Where(static diagnostic => diagnostic.Id == "VLM101" && diagnostic.Severity == DiagnosticSeverity.Warning)
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToImmutableArray();

        await Assert.That(unmappedMessages.Any(static message => message.Contains("'Id'", StringComparison.Ordinal))).IsTrue();
        await Assert.That(unmappedMessages.Any(static message => message.Contains("'Description'", StringComparison.Ordinal))).IsTrue();
        await Assert.That(unmappedMessages.Any(static message => message.Contains("'CreatedAt'", StringComparison.Ordinal))).IsTrue();
        await Assert.That(unmappedMessages.Any(static message => message.Contains("'Name'", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Configure_Unmapped_Target_Diagnostics_Update_Across_Incremental_Edits()
    {
        var runs = RunGeneratorIncremental(
            CreateSource(""),
            CreateSource("context.Ignore(target => target.Id);"),
            CreateSource(
                """
                context.Ignore(target => target.Id);
                context.Ignore(target => target.Description);
                """));

        var firstWarnings = GetUnmappedTargetDiagnostics(runs[0]);
        var secondWarnings = GetUnmappedTargetDiagnostics(runs[1]);
        var thirdWarnings = GetUnmappedTargetDiagnostics(runs[2]);

        await Assert.That(firstWarnings.Length).IsEqualTo(3);
        await Assert.That(secondWarnings.Length).IsEqualTo(2);
        await Assert.That(thirdWarnings.Length).IsEqualTo(1);

        await Assert.That(firstWarnings.Select(static diagnostic => diagnostic.Location.SourceSpan.Start).Distinct().Count()).IsEqualTo(1);
        await Assert.That(secondWarnings.Select(static diagnostic => diagnostic.Location.SourceSpan.Start).Distinct().Count()).IsEqualTo(1);
        await Assert.That(thirdWarnings.Select(static diagnostic => diagnostic.Location.SourceSpan.Start).Distinct().Count()).IsEqualTo(1);
        await Assert.That(GetUnmappedTargetMessages(firstWarnings).Any(static message => message.Contains("'Id'", StringComparison.Ordinal))).IsTrue();
        await Assert.That(GetUnmappedTargetMessages(secondWarnings).Any(static message => message.Contains("'Id'", StringComparison.Ordinal))).IsFalse();
        await Assert.That(GetUnmappedTargetMessages(thirdWarnings).Single().Contains("'CreatedAt'", StringComparison.Ordinal)).IsTrue();

        static string CreateSource(string ignoredMappings)
        {
            return $$"""
            using System;
            using Valiant.Mapping;

            [ValiantMapper]
            public partial class Mapper : MapperDefinition<From, To>
            {
                public override void Configure(MappingContext<From, To> context)
                {
                    context.Map(source => source.Name, target => target.Name);
                    {{ignoredMappings}}
                }
            }

            public record From
            {
                public int Id { get; set; }

                public string Name { get; set; } = "";

                public string? Description { get; set; }

                public DateTime CreatedAt { get; set; }
            }

            public record To
            {
                public int Id { get; set; }

                public string Name { get; set; } = "";

                public string? Description { get; set; }

                public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            }
            """;
        }
    }

    [Test]
    public async Task Configure_Map_When_With_Default_Does_Not_Report_Unmapped_Target_Member()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.EmailAddress, target => target.Email)
                        .When(source => source.IncludeEmail)
                        .Default("unknown@example.test");
                }
            }

            public sealed class Person
            {
                public bool IncludeEmail { get; set; }

                public string? EmailAddress { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Email { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic =>
            diagnostic.Id == "VLM101" &&
            diagnostic.GetMessage().Contains("Email", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Configure_Native_If_Else_Covering_Target_Does_Not_Report_Unmapped_Target_Member()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    if (context.Instance.UseDisplayName)
                    {
                        context.Map(source => source.DisplayName, target => target.Name);
                    }
                    else
                    {
                        context.Map(source => source.Name, target => target.Name);
                    }
                }
            }

            public sealed class Person
            {
                public bool UseDisplayName { get; set; }

                public string? DisplayName { get; set; }

                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic =>
            diagnostic.Id == "VLM101" &&
            diagnostic.GetMessage().Contains("Name", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Required_Target_Member_With_Conditional_Map_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name)
                        .When(source => source.IncludeName);
                }
            }

            public sealed class Person
            {
                public bool IncludeName { get; set; }

                public string Name { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public required string Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Any(static diagnostic =>
            diagnostic.Id == "VLM006" &&
            diagnostic.Severity == DiagnosticSeverity.Error &&
            diagnostic.GetMessage().Contains("Name", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Configure_Chained_Map_When_Guards_Whole_Expression()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Id, target => target.Id)
                        .When(source => source.IncludeId)
                        .Map(source => source.Name, target => target.Name)
                        .Map(source => source.EmailAddress, target => target.Email)
                        .When(source => source.IncludeEmail);
                }
            }

            public sealed class Person
            {
                public bool IncludeId { get; set; }

                public bool IncludeEmail { get; set; }

                public int Id { get; set; }

                public string Name { get; set; } = "";

                public string? EmailAddress { get; set; }
            }

            public sealed class PersonDto
            {
                public int Id { get; set; }

                public string? Name { get; set; }

                public string? Email { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Id")!.SetValue(source, 42);
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("EmailAddress")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType)!;
        var skippedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeId")!.SetValue(source, true);
        var partiallyMatchedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeEmail")!.SetValue(source, true);
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Id")!.GetValue(skippedTarget)).IsEqualTo(0);
        await Assert.That(targetType.GetProperty("Name")!.GetValue(skippedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Email")!.GetValue(skippedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Id")!.GetValue(partiallyMatchedTarget)).IsEqualTo(0);
        await Assert.That(targetType.GetProperty("Name")!.GetValue(partiallyMatchedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Email")!.GetValue(partiallyMatchedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Id")!.GetValue(mappedTarget)).IsEqualTo(42);
        await Assert.That(targetType.GetProperty("Name")!.GetValue(mappedTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Email")!.GetValue(mappedTarget)).IsEqualTo("ada@example.test");
    }

    [Test]
    public async Task Configure_Map_When_Targeting_Constructor_Parameter_Uses_Default_Fallback()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Id, target => target.Id)
                        .When(source => source.IncludeId);
                    context.Map(source => source.Name, target => target.Name);
                }
            }

            public sealed class Person
            {
                public bool IncludeId { get; set; }

                public int Id { get; set; }

                public string Name { get; set; } = "";
            }

            public sealed record PersonDto(int Id, string Name);
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Id")!.SetValue(source, 42);
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");

        var mapper = Activator.CreateInstance(mapperType)!;
        var defaultTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeId")!.SetValue(source, true);
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Id")!.GetValue(defaultTarget)).IsEqualTo(0);
        await Assert.That(targetType.GetProperty("Name")!.GetValue(defaultTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Id")!.GetValue(mappedTarget)).IsEqualTo(42);
        await Assert.That(targetType.GetProperty("Name")!.GetValue(mappedTarget)).IsEqualTo("Ada");
    }

    [Test]
    public async Task Configure_Native_If_Else_Emits_Conditional_Mapping()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    if (context.Source.UseEmail)
                    {
                        context.Map(source => source.Email, target => target.Display);
                    }
                    else
                    {
                        context.Map(source => source.Name, target => target.Display);
                    }
                }
            }

            public sealed class Person
            {
                public bool UseEmail { get; set; }

                public string Name { get; set; } = "";

                public string Email { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public string? Display { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("Email")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType)!;
        var nameTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("UseEmail")!.SetValue(source, true);
        var emailTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Display")!.GetValue(nameTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Display")!.GetValue(emailTarget)).IsEqualTo("ada@example.test");
    }

    [Test]
    public async Task Configure_Native_If_Else_Applies_Conditions_To_Chained_Mappings()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    if (context.Source.UseEmail)
                    {
                        context.Map(source => source.Email, target => target.Primary)
                            .Map(source => source.Name, target => target.Secondary);
                    }
                    else
                    {
                        context.Map(source => source.Name, target => target.Primary)
                            .Map(source => source.Email, target => target.Secondary);
                    }
                }
            }

            public sealed class Person
            {
                public bool UseEmail { get; set; }

                public string Name { get; set; } = "";

                public string Email { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public string? Primary { get; set; }

                public string? Secondary { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("Email")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType)!;
        var nameFirstTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("UseEmail")!.SetValue(source, true);
        var emailFirstTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Primary")!.GetValue(nameFirstTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Secondary")!.GetValue(nameFirstTarget)).IsEqualTo("ada@example.test");
        await Assert.That(targetType.GetProperty("Primary")!.GetValue(emailFirstTarget)).IsEqualTo("ada@example.test");
        await Assert.That(targetType.GetProperty("Secondary")!.GetValue(emailFirstTarget)).IsEqualTo("Ada");
    }

    [Test]
    public async Task Debuggable_Mode_Executes_Chained_Map_When_As_Whole_Expression()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name)
                        .Map(source => source.EmailAddress, target => target.Email)
                        .When(source => source.IncludeEmail);
                }
            }

            public sealed class Person
            {
                public bool IncludeEmail { get; set; }

                public string Name { get; set; } = "";

                public string? EmailAddress { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }

                public string? Email { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Debuggable };
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("EmailAddress")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType, [options])!;
        var skippedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeEmail")!.SetValue(source, true);
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(skippedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Email")!.GetValue(skippedTarget)).IsNull();
        await Assert.That(targetType.GetProperty("Name")!.GetValue(mappedTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Email")!.GetValue(mappedTarget)).IsEqualTo("ada@example.test");
    }

    [Test]
    public async Task Debuggable_Mode_Executes_Configure_At_Runtime()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    if (context.Source.UseEmail)
                    {
                        context.Map(source => source.Email, target => target.Display);
                    }
                    else
                    {
                        context.Map(source => source.Name, target => target.Display);
                    }
                }
            }

            public sealed class Person
            {
                public bool UseEmail { get; set; }

                public string Name { get; set; } = "";

                public string Email { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public string? Display { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var options = new ValiantMappingOptions { Mode = ValiantMappingMode.Debuggable };
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");
        sourceType.GetProperty("Email")!.SetValue(source, "ada@example.test");

        var mapper = Activator.CreateInstance(mapperType, [options])!;
        var nameTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("UseEmail")!.SetValue(source, true);
        var emailTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Display")!.GetValue(nameTarget)).IsEqualTo("Ada");
        await Assert.That(targetType.GetProperty("Display")!.GetValue(emailTarget)).IsEqualTo("ada@example.test");
    }

    [Test]
    public async Task Configure_UseMapper_Reuses_Injected_Mapper_For_Member_Pair()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                private readonly AddressMapper _addressMapper;

                public PersonMapper(AddressMapper addressMapper)
                {
                    _addressMapper = addressMapper;
                }

                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.UseMapper<AddressMapper>(_addressMapper);
                    context.Map(source => source.Address, target => target.Address);
                }
            }

            public sealed class Person
            {
                public Address Address { get; set; } = new();
            }

            public sealed class PersonDto
            {
                public AddressDto? Address { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var addressType = assembly.GetType("Address", throwOnError: true)!;
        var addressMapperType = assembly.GetType("AddressMapper", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var addressDtoType = assembly.GetType("AddressDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        var address = sourceType.GetProperty("Address")!.GetValue(source)!;
        addressType.GetProperty("Line1")!.SetValue(address, "Main");

        var addressMapper = Activator.CreateInstance(addressMapperType)!;
        var mapper = Activator.CreateInstance(mapperType, [addressMapper])!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        var addressDto = targetType.GetProperty("Address")!.GetValue(target)!;

        await Assert.That(addressDtoType.GetProperty("Line1")!.GetValue(addressDto)).IsEqualTo("Main");
    }

    [Test]
    public async Task Configure_Map_Allows_Custom_Source_Transform()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.FirstName + " " + source.LastName, target => target.DisplayName);
                }
            }

            public sealed class Person
            {
                public string FirstName { get; set; } = "";

                public string LastName { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public string? DisplayName { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("FirstName")!.SetValue(source, "Ada");
        sourceType.GetProperty("LastName")!.SetValue(source, "Lovelace");

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("DisplayName")!.GetValue(target)).IsEqualTo("Ada Lovelace");
    }

    [Test]
    public async Task Configure_Map_Default_Provides_Fallback_Value()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name)
                        .When(source => source.IncludeName)
                        .Default("Anonymous");
                }
            }

            public sealed class Person
            {
                public bool IncludeName { get; set; }

                public string Name { get; set; } = "";
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Name")!.SetValue(source, "Ada");

        var mapper = Activator.CreateInstance(mapperType)!;
        var fallbackTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        sourceType.GetProperty("IncludeName")!.SetValue(source, true);
        var mappedTarget = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(fallbackTarget)).IsEqualTo("Anonymous");
        await Assert.That(targetType.GetProperty("Name")!.GetValue(mappedTarget)).IsEqualTo("Ada");
    }

    [Test]
    public async Task Generated_Mapper_Can_Ignore_Null_Source_Values()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; } = "Existing";
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var options = new ValiantMappingOptions { NullHandling = ValiantNullHandling.IgnoreSourceNulls };
        var source = Activator.CreateInstance(sourceType)!;

        var mapper = Activator.CreateInstance(mapperType, [options])!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(target)).IsEqualTo("Existing");
    }

    [Test]
    public async Task Configure_MapNull_Overrides_Global_Ignore_Null_Source_Value()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name).MapNull();
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; } = "Existing";
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var options = new ValiantMappingOptions
        {
            Mode = ValiantMappingMode.Fast,
            NullHandling = ValiantNullHandling.IgnoreSourceNulls
        };
        var source = Activator.CreateInstance(sourceType)!;

        var mapper = Activator.CreateInstance(mapperType, [options])!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Name")!.GetValue(target)).IsNull();
    }

    [Test]
    public async Task Generated_Mapper_Uses_Flattening_Convention()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public Address Address { get; set; } = new();
            }

            public sealed class Address
            {
                public string? City { get; set; }
            }

            public sealed class PersonDto
            {
                public string? AddressCity { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var addressType = assembly.GetType("Address", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        var address = sourceType.GetProperty("Address")!.GetValue(source)!;
        addressType.GetProperty("City")!.SetValue(address, "Riga");

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("AddressCity")!.GetValue(target)).IsEqualTo("Riga");
    }

    [Test]
    public async Task Generated_Mapper_Uses_Unflattening_Convention()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string? AddressCity { get; set; }
            }

            public sealed class PersonDto
            {
                public AddressDto? Address { get; set; }
            }

            public sealed class AddressDto
            {
                public string? City { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var addressDtoType = assembly.GetType("AddressDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("AddressCity")!.SetValue(source, "Riga");

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        var address = targetType.GetProperty("Address")!.GetValue(target)!;

        await Assert.That(addressDtoType.GetProperty("City")!.GetValue(address)).IsEqualTo("Riga");
    }

    [Test]
    public async Task Generated_Mapper_Maps_Enums()
    {
        var assembly = CompileAndLoad(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class OrderMapper : MapperDefinition<Order, OrderDto>;

            public sealed class Order
            {
                public OrderStatus Status { get; set; }
            }

            public sealed class OrderDto
            {
                public OrderStatusDto Status { get; set; }
            }

            public enum OrderStatus
            {
                Pending = 1
            }

            public enum OrderStatusDto
            {
                Pending = 1
            }
            """);

        var sourceType = assembly.GetType("Order", throwOnError: true)!;
        var sourceEnumType = assembly.GetType("OrderStatus", throwOnError: true)!;
        var mapperType = assembly.GetType("OrderMapper", throwOnError: true)!;
        var targetType = assembly.GetType("OrderDto", throwOnError: true)!;
        var targetEnumType = assembly.GetType("OrderStatusDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        sourceType.GetProperty("Status")!.SetValue(source, Enum.Parse(sourceEnumType, "Pending"));

        var mapper = Activator.CreateInstance(mapperType)!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;

        await Assert.That(targetType.GetProperty("Status")!.GetValue(target)).IsEqualTo(Enum.Parse(targetEnumType, "Pending"));
    }

    [Test]
    public async Task Configure_UseMapper_Maps_Collections_By_Element()
    {
        var assembly = CompileAndLoad(
            """
            using System.Collections.Generic;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                private readonly AddressMapper _addressMapper;

                public PersonMapper(AddressMapper addressMapper)
                {
                    _addressMapper = addressMapper;
                }

                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.UseMapper(_addressMapper);
                    context.Map(source => source.Addresses, target => target.Addresses);
                }
            }

            public sealed class Person
            {
                public List<Address> Addresses { get; } = new();
            }

            public sealed class PersonDto
            {
                public List<AddressDto>? Addresses { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var sourceType = assembly.GetType("Person", throwOnError: true)!;
        var addressType = assembly.GetType("Address", throwOnError: true)!;
        var addressMapperType = assembly.GetType("AddressMapper", throwOnError: true)!;
        var mapperType = assembly.GetType("PersonMapper", throwOnError: true)!;
        var targetType = assembly.GetType("PersonDto", throwOnError: true)!;
        var addressDtoType = assembly.GetType("AddressDto", throwOnError: true)!;
        var source = Activator.CreateInstance(sourceType)!;
        var address = Activator.CreateInstance(addressType)!;
        addressType.GetProperty("Line1")!.SetValue(address, "Main");
        var addresses = sourceType.GetProperty("Addresses")!.GetValue(source)!;
        addresses.GetType().GetMethod("Add")!.Invoke(addresses, [address]);

        var addressMapper = Activator.CreateInstance(addressMapperType)!;
        var mapper = Activator.CreateInstance(mapperType, [addressMapper])!;
        var target = mapperType.GetMethod("Map")!.Invoke(mapper, [source])!;
        var addressDtos = targetType.GetProperty("Addresses")!.GetValue(target)!;
        var firstAddressDto = addressDtos.GetType().GetProperty("Item")!.GetValue(addressDtos, [0])!;

        await Assert.That(addressDtoType.GetProperty("Line1")!.GetValue(firstAddressDto)).IsEqualTo("Main");
    }

    [Test]
    public async Task Default_Mapping_Uses_Registered_Mapper_For_Nested_Object()
    {
        var assembly = CompileAndLoad(
            """
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static string? Run()
                {
                    var services = new ServiceCollection();
                    services.AddValiantMappers();

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<IMapper<Person, PersonDto>>();
                    var target = mapper.Map(new Person { Address = new Address { Line1 = "Main" } });
                    return target.Address?.Line1;
                }
            }

            public sealed class Person
            {
                public Address? Address { get; set; }
            }

            public sealed class PersonDto
            {
                public AddressDto? Address { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var result = assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, []);

        await Assert.That(result).IsEqualTo("Main");
    }

    [Test]
    public async Task Default_Mapping_Uses_Registered_Mapper_For_Collection_Items()
    {
        var assembly = CompileAndLoad(
            """
            using System.Collections.Generic;
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static string? Run()
                {
                    var services = new ServiceCollection();
                    services.AddValiantMappers();

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<IMapper<Person, PersonDto>>();
                    var target = mapper.Map(new Person { Addresses = [new Address { Line1 = "Main" }] });
                    return target.Addresses?[0].Line1;
                }
            }

            public sealed class Person
            {
                public List<Address> Addresses { get; set; } = [];
            }

            public sealed class PersonDto
            {
                public List<AddressDto>? Addresses { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var result = assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, []);

        await Assert.That(result).IsEqualTo("Main");
    }

    [Test]
    public async Task Default_Mapping_Uses_Registered_Mapper_For_Set_Items()
    {
        var assembly = CompileAndLoad(
            """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static string? Run()
                {
                    var services = new ServiceCollection();
                    services.AddValiantMappers();

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<IMapper<Person, PersonDto>>();
                    var target = mapper.Map(new Person { Addresses = [new Address { Line1 = "Main" }] });
                    return target.Addresses?.Single().Line1;
                }
            }

            public sealed class Person
            {
                public List<Address> Addresses { get; set; } = [];
            }

            public sealed class PersonDto
            {
                public HashSet<AddressDto>? Addresses { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var result = assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, []);

        await Assert.That(result).IsEqualTo("Main");
    }

    [Test]
    public async Task Default_Mapping_With_User_Constructor_Uses_ServiceProvider_For_Nested_Object()
    {
        var assembly = CompileAndLoad(
            """
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                private readonly Marker _marker;

                public PersonMapper(Marker marker)
                {
                    _marker = marker;
                }

            }

            public static class TestRunner
            {
                public static object?[] Run()
                {
                    var services = new ServiceCollection();
                    services.AddSingleton(new Marker("resolved"));
                    services.AddValiantMappers();

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<IMapper<Person, PersonDto>>();
                    var target = mapper.Map(new Person { Address = new Address { Line1 = "Main" } });
                    return [target.Address?.Line1];
                }
            }

            public sealed class Marker(string value)
            {
                public string Value { get; } = value;
            }

            public sealed class Person
            {
                public Address? Address { get; set; }
            }

            public sealed class PersonDto
            {
                public AddressDto? Address { get; set; }

            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var result = (object?[])assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, [])!;

        await Assert.That(result[0]).IsEqualTo("Main");
    }

    [Test]
    public async Task Generated_Nested_Mapper_Lookup_Uses_Cache_And_Depth_Guard()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public Address? Address { get; set; }
            }

            public sealed class PersonDto
            {
                public AddressDto? Address { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var generated = result.GeneratedSources.Single(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal)).SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated.Contains("_valiantMapperCache.TryGetValue", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains("MapWithDepthGuard", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Discovered_Mappers_Generate_Dependency_Injection_Registration()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
            }

            public sealed class PersonDto
            {
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

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();
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

        var dependencyInjection = result.GeneratedSources.Single(static source => source.HintName == "Valiant.Mapping.DependencyInjection.g.cs").SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(dependencyInjection.Contains("AddValiantMappers", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.ValiantMappingOptions)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("file sealed class ValiantGeneratedMapper", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("file sealed class ValiantGeneratedMapperFactory", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapper), typeof(global::Valiant.Mapping.ValiantGeneratedMapper)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapperFactory), typeof(global::Valiant.Mapping.ValiantGeneratedMapperFactory)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("MakeGenericType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(dependencyInjection.Contains("typeof(global::PersonMapper), typeof(global::PersonMapper)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapper<global::Person, global::PersonDto>), typeof(global::PersonMapper)", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Duplicate_Default_Mapper_Pair_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class UserMapper : MapperDefinition<User, UserDto>;

            [ValiantMapper]
            public sealed partial class UserMapper22 : MapperDefinition<User, UserDto>;

            public sealed class User
            {
                public string? Name { get; set; }
            }

            public sealed class UserDto
            {
                public string? Name { get; set; }
            }
            """);

        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "VLM104");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("UserMapper22");
        await Assert.That(diagnostic.GetMessage()).Contains("UserMapper");
        await Assert.That(diagnostic.GetMessage()).Contains("global::User");
        await Assert.That(diagnostic.GetMessage()).Contains("global::UserDto");
    }

    [Test]
    public async Task Mapper_Factory_CreateMapper_With_Registered_Pair_Does_Not_Report_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static IMapper<Person, PersonDto> Run(IMapperFactory factory)
                {
                    return factory.CreateMapper<Person, PersonDto>();
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM105");
    }

    [Test]
    public async Task Mapper_Factory_CreateMapper_With_Missing_Generic_Pair_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static IMapper<PersonDto, Person> Run(IMapperFactory factory)
                {
                    return factory.CreateMapper<PersonDto, Person>();
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "VLM105");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("global::PersonDto");
        await Assert.That(diagnostic.GetMessage()).Contains("global::Person");
    }

    [Test]
    public async Task Mapper_Factory_CreateMapper_With_Missing_Typeof_Pair_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static object Run(IMapperFactory factory)
                {
                    return factory.CreateMapper(typeof(PersonDto), typeof(Person));
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLM105");
    }

    [Test]
    public async Task Mapper_Factory_TryCreateMapper_With_Missing_Pair_Does_Not_Report_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static bool Run(IMapperFactory factory)
                {
                    return factory.TryCreateMapper<PersonDto, Person>(out _);
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM105");
    }

    [Test]
    public async Task Mapper_Facade_Map_With_Registered_Pair_Does_Not_Report_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static PersonDto Run(IMapper mapper)
                {
                    return mapper.Map<PersonDto>(new Person());
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM106");
    }

    [Test]
    public async Task Mapper_Facade_Map_With_Missing_Generic_Pair_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static Person Run(IMapper mapper)
                {
                    return mapper.Map<Person>(new PersonDto());
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        var diagnostic = result.Diagnostics.Single(static diagnostic => diagnostic.Id == "VLM106");

        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.GetMessage()).Contains("global::PersonDto");
        await Assert.That(diagnostic.GetMessage()).Contains("global::Person");
    }

    [Test]
    public async Task Mapper_Facade_Map_With_Missing_Typeof_Pair_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static object Run(IMapper mapper)
                {
                    return mapper.Map(new PersonDto(), typeof(Person));
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLM106");
    }

    [Test]
    public async Task Mapper_Facade_TryMap_With_Missing_Pair_Does_Not_Report_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static bool Run(IMapper mapper)
                {
                    return mapper.TryMap<Person>(new PersonDto(), out _);
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM106");
    }

    [Test]
    public async Task Mapper_Facade_Map_With_Object_Source_Does_Not_Report_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public static class TestRunner
            {
                public static Person Run(IMapper mapper, object source)
                {
                    return mapper.Map<Person>(source);
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM106");
    }

    [Test]
    public async Task Dependency_Injection_Extension_Is_Generated_Without_Discovered_Mappers()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            namespace Microsoft.Extensions.DependencyInjection
            {
                public interface IServiceCollection
                {
                }

                public sealed class ServiceDescriptor
                {
                    public static ServiceDescriptor Singleton(System.Type serviceType, object implementationInstance) => new();

                    public static ServiceDescriptor Singleton(System.Type serviceType, System.Type implementationType) => new();

                    public static ServiceDescriptor Transient(System.Type serviceType, System.Type implementationType) => new();
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

        var dependencyInjection = result.GeneratedSources.Single(static source => source.HintName == "Valiant.Mapping.DependencyInjection.g.cs").SourceText.ToString();

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(dependencyInjection.Contains("AddValiantMappers", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.ValiantMappingOptions)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("file sealed class ValiantGeneratedMapper", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("file sealed class ValiantGeneratedMapperFactory", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapper), typeof(global::Valiant.Mapping.ValiantGeneratedMapper)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapperFactory), typeof(global::Valiant.Mapping.ValiantGeneratedMapperFactory)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(dependencyInjection.Contains("MakeGenericType", StringComparison.Ordinal)).IsFalse();
        await Assert.That(dependencyInjection.Contains("typeof(global::Valiant.Mapping.IMapper<", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Generated_Dependency_Injection_Extension_Registers_All_Mappers()
    {
        var assembly = CompileAndLoad(
            """
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            [ValiantMapper]
            public sealed partial class AddressMapper : MapperDefinition<Address, AddressDto>;

            public static class TestRunner
            {
                public static object?[] Run()
                {
                    var services = new ServiceCollection();
                    services.AddValiantMappers(options =>
                    {
                        options.NullHandling = ValiantNullHandling.IgnoreSourceNulls;
                    });

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<IMapper<Person, PersonDto>>();
                    var mapperFacade = provider.GetRequiredService<Valiant.Mapping.IMapper>();
                    var mapperFactory = provider.GetRequiredService<IMapperFactory>();
                    var concrete = provider.GetRequiredService<PersonMapper>();
                    var addressMapper = provider.GetRequiredService<IMapper<Address, AddressDto>>();
                    var options = provider.GetRequiredService<ValiantMappingOptions>();
                    var target = mapper.Map(new Person { Name = "Ada" });
                    var facadeTarget = mapperFacade.Map<PersonDto>(new Person { Name = "Edsger" });
                    var typeFacadeTarget = (AddressDto)mapperFacade.Map(new Address { Line1 = "Third" }, typeof(AddressDto));
                    var factoryTarget = mapperFactory.CreateMapper<Person, PersonDto>().Map(new Person { Name = "Grace" });
                    var typeFactoryTarget = ((IMapper<Address, AddressDto>)mapperFactory.CreateMapper(typeof(Address), typeof(AddressDto))).Map(new Address { Line1 = "Second" });
                    var addressTarget = addressMapper.Map(new Address { Line1 = "Main" });
                    var hasFacadeMissingMapper = mapperFacade.TryMap<Person>(new PersonDto { Name = "Missing" }, out _);
                    var hasTypeFacadeMissingMapper = mapperFacade.TryMap(new PersonDto { Name = "Missing" }, typeof(Person), out _);
                    var hasMissingMapper = mapperFactory.TryCreateMapper(typeof(PersonDto), typeof(Person), out _);

                    return
                    [
                        target.Name,
                        concrete is not null,
                        addressTarget.Line1,
                        options.NullHandling,
                        facadeTarget.Name,
                        typeFacadeTarget.Line1,
                        factoryTarget.Name,
                        typeFactoryTarget.Line1,
                        hasFacadeMissingMapper,
                        hasTypeFacadeMissingMapper,
                        hasMissingMapper
                    ];
                }
            }

            public sealed class Person
            {
                public string? Name { get; set; }
            }

            public sealed class PersonDto
            {
                public string? Name { get; set; }
            }

            public sealed class Address
            {
                public string? Line1 { get; set; }
            }

            public sealed class AddressDto
            {
                public string? Line1 { get; set; }
            }
            """);

        var result = (object?[])assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, [])!;

        await Assert.That(result[0]).IsEqualTo("Ada");
        await Assert.That((bool)result[1]!).IsTrue();
        await Assert.That(result[2]).IsEqualTo("Main");
        await Assert.That(result[3]).IsEqualTo(ValiantNullHandling.IgnoreSourceNulls);
        await Assert.That(result[4]).IsEqualTo("Edsger");
        await Assert.That(result[5]).IsEqualTo("Third");
        await Assert.That(result[6]).IsEqualTo("Grace");
        await Assert.That(result[7]).IsEqualTo("Second");
        await Assert.That((bool)result[8]!).IsFalse();
        await Assert.That((bool)result[9]!).IsFalse();
        await Assert.That((bool)result[10]!).IsFalse();
    }

    [Test]
    public async Task Generated_Dependency_Injection_Extension_Can_Be_Called_Without_Mappers()
    {
        var assembly = CompileAndLoad(
            """
            using Microsoft.Extensions.DependencyInjection;
            using Valiant.Mapping;

            public static class TestRunner
            {
                public static object?[] Run()
                {
                    var services = new ServiceCollection();
                    services.AddValiantMappers(options =>
                    {
                        options.NullHandling = ValiantNullHandling.IgnoreSourceNulls;
                    });

                    using var provider = services.BuildServiceProvider();
                    var mapper = provider.GetRequiredService<Valiant.Mapping.IMapper>();
                    var factory = provider.GetRequiredService<IMapperFactory>();
                    return
                    [
                        provider.GetRequiredService<ValiantMappingOptions>().NullHandling,
                        mapper.TryMap("test", typeof(int), out _),
                        factory.TryCreateMapper(typeof(string), typeof(int), out _)
                    ];
                }
            }
            """);

        var result = (object?[])assembly.GetType("TestRunner", throwOnError: true)!
            .GetMethod("Run")!
            .Invoke(null, [])!;

        await Assert.That(result[0]).IsEqualTo(ValiantNullHandling.IgnoreSourceNulls);
        await Assert.That((bool)result[1]!).IsFalse();
        await Assert.That((bool)result[2]!).IsFalse();
    }

    [Test]
    public async Task Missing_Partial_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
            }

            public sealed class PersonDto
            {
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLM001");
    }

    [Test]
    public async Task Required_Target_Member_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
            }

            public sealed class PersonDto
            {
                public required string Name { get; set; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static x => x.Id)).Contains("VLM006");
    }

    [Test]
    public async Task Default_Mapping_Reports_Unmapped_Target_Member_When_Constructor_Cannot_Be_Mapped()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person
            {
                public string Name { get; set; } = "";
            }

            public sealed record PersonDto(string Name, int Age);
            """);

        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM101" && x.Severity == DiagnosticSeverity.Warning)).IsTrue();
    }

    [Test]
    public async Task Configure_Mapping_Reports_Unmapped_Target_Member_When_Constructor_Cannot_Be_Mapped()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>
            {
                public override void Configure(MappingContext<Person, PersonDto> context)
                {
                    context.Map(source => source.Name, target => target.Name);
                }
            }

            public sealed class Person
            {
                public string Name { get; set; } = "";

                public int Age { get; set; }
            }

            public sealed record PersonDto(string Name, int Age);
            """);

        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM101" && x.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(result.Diagnostics.Any(static x => x.Id == "VLM103")).IsFalse();
    }

    [Test]
    public async Task Interface_Mapper_Without_Configure_Uses_Conventions()
    {
        var result = RunGenerator(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : IMapperDefinition<Person, PersonDto>
            {
            }

            public sealed class Person
            {
            }

            public sealed class PersonDto
            {
            }
            """);

        await Assert.That(result.Diagnostics.Where(static x => x.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static x => x.HintName.EndsWith("PersonMapper.Mapper.g.cs", StringComparison.Ordinal))).IsTrue();
    }

    private static SourceGenerationResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IMapper<,>).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location));

        var compilation = GeneratorTestHarness.CreateCompilation(
            source,
            parseOptions,
            references,
            "Valiant.Mapping.SourceGeneration.Tests");
        var execution = GeneratorTestHarness.Run(new ValiantMappingGenerator(), compilation, parseOptions);
        var analyzerDiagnostics = execution.OutputCompilation
            .WithAnalyzers([new ValiantMapperInvocationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        var diagnostics = execution.OutputCompilation.GetDiagnostics()
            .Concat(execution.GeneratorDiagnostics)
            .Concat(analyzerDiagnostics)
            .Where(static diagnostic => diagnostic.Id.StartsWith("VLM", StringComparison.Ordinal))
            .ToImmutableArray();

        return new SourceGenerationResult(
            diagnostics,
            execution.GeneratedSources);
    }

    [Test]
    public async Task Dynamic_Lookup_Uses_Chains_For_Small_Sets_And_Dictionary_For_Large_Sets()
    {
        var small = RunGenerator(CreateMapperSetSource(1)).GeneratedSources
            .Single(static source => source.HintName == "Valiant.Mapping.DependencyInjection.g.cs")
            .SourceText
            .ToString();
        var largeSource = CreateMapperSetSource(9);
        var large = RunGenerator(largeSource).GeneratedSources
            .Single(static source => source.HintName == "Valiant.Mapping.DependencyInjection.g.cs")
            .SourceText
            .ToString();

        await Assert.That(small).DoesNotContain("ValiantGeneratedMapperLookup");
        await Assert.That(large).Contains("ValiantGeneratedMapperLookup");
        await Assert.That(large).Contains("IReadOnlyDictionary<(global::System.Type Source, global::System.Type Target), Entry>");

        _ = CompileAndLoad(largeSource);
    }

    [Test]
    public async Task Unrelated_Edit_Leaves_Tracked_Mapper_Step_Cached_Or_Unchanged()
    {
        const string mapper = """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person { public string? Name { get; init; } }
            public sealed class PersonDto { public string? Name { get; init; } }
            """;

        var reasons = RunTrackedGeneratorEdit(
            mapper + "\npublic sealed class Unrelated { public int Value { get; init; } }",
            mapper + "\npublic sealed class Unrelated { public int Value { get; init; } public string? Text { get; init; } }",
            "Mapping.Mapper");

        await Assert.That(reasons).IsNotEmpty();
        await Assert.That(reasons.All(static reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).IsTrue();
    }

    [Test]
    public async Task Mapper_Invocation_Analyzer_Discovers_Local_Pairs_Without_Generator_Output()
    {
        var diagnostics = RunInvocationAnalyzer(
            """
            using Valiant.Mapping;

            [ValiantMapper]
            public sealed partial class PersonMapper : MapperDefinition<Person, PersonDto>;

            public sealed class Person;
            public sealed class PersonDto;

            public static class Usage
            {
                public static void Execute(IMapperFactory factory)
                {
                    _ = factory.CreateMapper<Person, PersonDto>();
                }
            }
            """);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLM105");
    }

    private static string CreateMapperSetSource(int count)
    {
        var builder = new System.Text.StringBuilder("using Valiant.Mapping;\n");
        for (var index = 0; index < count; index++)
        {
            builder.Append("[ValiantMapper] public sealed partial class Mapper")
                .Append(index)
                .Append(" : MapperDefinition<Source")
                .Append(index)
                .Append(", Target")
                .Append(index)
                .Append(">; public sealed class Source")
                .Append(index)
                .Append(" { public int Value { get; init; } } public sealed class Target")
                .Append(index)
                .AppendLine(" { public int Value { get; init; } }");
        }

        return builder.ToString();
    }

    private static ImmutableArray<IncrementalStepRunReason> RunTrackedGeneratorEdit(
        string before,
        string after,
        string stepName)
    {
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IMapper<,>).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location));
        return GeneratorTestHarness.RunTrackedGeneratorEdit(
            new ValiantMappingGenerator(),
            before,
            after,
            stepName,
            references,
            "Valiant.Mapping.Tracked.Tests");
    }

    private static ImmutableArray<Diagnostic> RunInvocationAnalyzer(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "/tests/AnalyzerInput.cs");
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IMapper<,>).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "Valiant.Mapping.Analyzer.Tests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return [.. compilation
            .WithAnalyzers([new ValiantMapperInvocationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult()];
    }

    private static ImmutableArray<ImmutableArray<Diagnostic>> RunGeneratorIncremental(params string[] sources)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IMapper<,>).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location));
        var diagnostics = ImmutableArray.CreateBuilder<ImmutableArray<Diagnostic>>(sources.Length);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ValiantMappingGenerator())
            .WithUpdatedParseOptions(parseOptions);

        foreach (var source in sources)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(
                source,
                parseOptions,
                path: "/tests/GeneratedInput.cs");

            var compilation = CSharpCompilation.Create(
                "Valiant.Mapping.SourceGeneration.Tests",
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
            diagnostics.Add(outputCompilation.GetDiagnostics()
                .Concat(generatorDiagnostics)
                .Where(static diagnostic => diagnostic.Id.StartsWith("VLM", StringComparison.Ordinal))
                .ToImmutableArray());
        }

        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> GetUnmappedTargetDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(static diagnostic => diagnostic.Id == "VLM101" && diagnostic.Severity == DiagnosticSeverity.Warning)
            .ToImmutableArray();
    }

    private static ImmutableArray<string> GetUnmappedTargetMessages(ImmutableArray<Diagnostic> diagnostics)
    {
        return diagnostics
            .Select(static diagnostic => diagnostic.GetMessage())
            .ToImmutableArray();
    }

    private static System.Reflection.Assembly CompileAndLoad(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "/tests/GeneratedInput.cs");

        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(IMapper<,>).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(ServiceCollection).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "Valiant.Mapping.GeneratedMapper.Tests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new ValiantMappingGenerator())
            .WithUpdatedParseOptions(parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        var diagnostics = outputCompilation.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
        }

        return System.Reflection.Assembly.Load(stream.ToArray());
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        var testAssemblyFileName = Path.GetFileName(typeof(SourceGenerationTests).Assembly.Location);
        var sampleAssemblyFileName = Path.GetFileName(typeof(global::Valiant.Mapping.Samples.Basic.Person).Assembly.Location);

        return GeneratorTestHarness.GetTrustedPlatformReferences(testAssemblyFileName)
            .Where(reference => !string.Equals(
                Path.GetFileName(reference.Display),
                sampleAssemblyFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();
    }

    private sealed record SourceGenerationResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
