using System.Collections.Immutable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration;
using StaticNorth.Valiant.SourceGeneration.Testing;

namespace StaticNorth.Valiant.Endpoints.Tests.SourceGeneration;

public sealed class SourceGenerationTests
{
    [Test]
    public async Task Standalone_Endpoint_Maps_At_Root()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("[assembly: global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointRouteRegistrarAttribute(");
        await Assert.That(generated).Contains("internal sealed class ValiantEndpointRouteRegistrar : global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.IValiantEndpointRouteRegistrar");
        await Assert.That(generated).Contains("public ValiantEndpointRouteRegistrar()");
        await Assert.That(generated).Contains("MapEndpoint<global::HealthEndpoint>(builder, endpointRegistration0);");
        await Assert.That(generated).DoesNotContain("MapEndpointGroup");
    }

    [Test]
    public async Task Generated_Registration_Guards_Groups_And_Endpoints_With_Separate_Policies()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint<TodosEndpointGroup>]
            public sealed class ListTodosEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpointGroup<global::TodosEndpointGroup>(builder, groupRegistration0))");
        await Assert.That(generated).Contains("if (group0 is not null)");
        await Assert.That(generated).Contains("CreateEndpointRegistration<global::ListTodosEndpoint>(group0, groupRegistration0, 0, true, null, null, null)");
        await Assert.That(generated).Contains("if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpoint<global::ListTodosEndpoint>(group0, endpointRegistration2))");
        await Assert.That(generated).Contains("if (global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ShouldMapEndpoint<global::HealthEndpoint>(builder, endpointRegistration1))");
        await Assert.That(generated).DoesNotContain("ValiantEndpointGeneratedCode.ShouldMap<");
        await Assert.That(generated).DoesNotContain("ConfigureEndpointGroupAfterMapping");
    }

    [Test]
    public async Task Grouped_Endpoint_Maps_Inside_Group_Without_Manual_Group_Reference()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }

            [ValiantEndpoint(typeof(TodosEndpointGroup))]
            public sealed class ListTodosEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("group0 = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpointGroup<global::TodosEndpointGroup>(builder, groupRegistration0);");
        await Assert.That(generated).Contains("global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpoint<global::ListTodosEndpoint>(group0, endpointRegistration1);");
        await Assert.That(generated).DoesNotContain("ConfigureEndpointGroupAfterMapping");
        await Assert.That(generated).DoesNotContain("MapEndpoint<global::ListTodosEndpoint>(builder,");
    }

    [Test]
    public async Task Generic_Grouped_Endpoint_Maps_Inside_Group()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }

            [ValiantEndpoint<TodosEndpointGroup>]
            public sealed class ListTodosEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("group0 = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpointGroup<global::TodosEndpointGroup>(builder, groupRegistration0);");
        await Assert.That(generated).Contains("global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpoint<global::ListTodosEndpoint>(group0, endpointRegistration1);");
        await Assert.That(generated).DoesNotContain("ConfigureEndpointGroupAfterMapping");
        await Assert.That(generated).DoesNotContain("MapEndpoint<global::ListTodosEndpoint>(builder,");
    }

    [Test]
    public async Task Endpoint_Group_Must_Implement_CreateGroup()
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = GeneratorTestHarness.CreateCompilation(
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup;
            """,
            parseOptions,
            GetTrustedPlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(ValiantEndpointAttribute).Assembly.Location))
                .Add(MetadataReference.CreateFromFile(typeof(IEndpointGroup).Assembly.Location))
                .Add(MetadataReference.CreateFromFile(typeof(IEndpointRouteBuilder).Assembly.Location)),
            "StaticNorth.Valiant.Endpoints.RequiredGroupFactory.Tests");

        var diagnostic = compilation.GetDiagnostics().Single(static diagnostic => diagnostic.Id == "CS0535");

        await Assert.That(diagnostic.GetMessage()).Contains("IEndpointGroup.CreateGroup");
    }

    [Test]
    public async Task Group_And_Children_Are_Mapped_Once()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }

            [ValiantEndpoint(typeof(TodosEndpointGroup))]
            public sealed class ListTodosEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(CountOccurrences(
            generated,
            "ValiantEndpointGeneratedCode.MapEndpointGroup<global::TodosEndpointGroup>")).IsEqualTo(1);
        await Assert.That(CountOccurrences(
            generated,
            "ValiantEndpointGeneratedCode.MapEndpoint<global::ListTodosEndpoint>")).IsEqualTo(1);
    }

    [Test]
    public async Task Endpoint_Group_Attribute_Maps_Group_Without_Endpoints()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder)
                {
                    throw null!;
                }
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("group0 = global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.MapEndpointGroup<global::TodosEndpointGroup>(builder, groupRegistration0);");
        await Assert.That(generated).DoesNotContain("ConfigureEndpointGroupAfterMapping");
        await Assert.That(generated).DoesNotContain("MapEndpoint<");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Object_TryParse_Shim_For_AsParameters_Request_Property()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public FacetRequest? Facets { get; init; }
            }

            public sealed partial record FacetRequest
            {
                public string? Category { get; init; }
                public int? Limit { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "FacetRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static bool TryParse(string value, out global::FacetRequest? result)");
        await Assert.That(generated).Contains("Compatibility shim for ASP.NET Core [AsParameters] property binding.");
        await Assert.That(generated).Contains("result = default;");
        await Assert.That(generated).Contains("return true;");
        await Assert.That(generated).DoesNotContain("JsonSerializer.Deserialize");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Record_Struct_Query_Object_Declaration()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public FacetRequest? Facets { get; init; }
            }

            public partial record struct FacetRequest
            {
                public int? Limit { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "FacetRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("partial record struct FacetRequest");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Object_TryParse_For_FromQuery_Request()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([FromQuery] SearchRequest request) => request);
                }
            }

            public sealed partial record SearchRequest
            {
                public string? Term { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "SearchRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static bool TryParse(string value, out global::SearchRequest result)");
        await Assert.That(generated).Contains("global::System.Text.Json.JsonSerializer.Deserialize<global::SearchRequest>(value, __valiantQueryObjectJsonSerializerOptions)!;");
    }

    [Test]
    public async Task Valiant_Query_Parsing_Attribute_Disables_Json_In_Generated_TryParse()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static ([FromQuery] SearchRequest request) => request);
            }

            public sealed partial record SearchRequest
            {
                public string? Term { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "SearchRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("result = default!;");
        await Assert.That(generated).Contains("throw new global::Microsoft.AspNetCore.Http.BadHttpRequestException");
        await Assert.That(generated).DoesNotContain("JsonSerializer");
    }

    [Test]
    public async Task Parameterless_Valiant_Query_Parsing_Attribute_Preserves_Json()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static ([FromQuery] SearchRequest request) => request);
            }

            public sealed partial record SearchRequest;
            """);

        var generated = GetGeneratedSource(result, "SearchRequest.QueryObject.g.cs");
        var registration = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("JsonSerializer.Deserialize<global::SearchRequest>");
        await Assert.That(registration).DoesNotContain("ApplyQueryParsingOverrides");
    }

    [Test]
    public async Task Valiant_Query_Parsing_Attribute_Emits_Explicit_Full_Query_Overrides()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(
                SupportedArrayFormats =
                [
                    QueryArrayFormat.RepeatedKeys,
                    QueryArrayFormat.CommaSeparated
                ],
                SupportedObjectFormats =
                [
                    QueryObjectFormat.Flat,
                    QueryObjectFormat.DotNotation
                ],
                JsonValues = JsonQueryValues.Disabled)]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
            }

            public sealed partial record SearchRequest
            {
                [FromQuery]
                public SearchFilter? Filter { get; init; }
            }

            public sealed partial record SearchFilter;
            """);

        var registration = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(registration).Contains("ApplyQueryParsingOverrides(");
        await Assert.That(registration).Contains(
            "new global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat[] { " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat)0, " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat)1 }");
        await Assert.That(registration).Contains(
            "new global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat[] { " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat)0, " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat)1 }");
        await Assert.That(registration).Contains(", false);");
    }

    [Test]
    public async Task Valiant_Query_Parsing_Array_And_Object_Overrides_Do_Not_Change_Direct_Query_Json()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(
                SupportedArrayFormats = [],
                SupportedObjectFormats = [])]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static ([FromQuery] SearchRequest request) => request);
            }

            public sealed partial record SearchRequest;
            """);

        var directParser = GetGeneratedSource(result, "SearchRequest.QueryObject.g.cs");
        var registration = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(directParser).Contains("JsonSerializer.Deserialize<global::SearchRequest>");
        await Assert.That(registration).Contains("ApplyQueryParsingOverrides(");
    }

    [Test]
    public async Task Valiant_Query_Parsing_Null_Overrides_Inherit_Without_Emitting_Metadata()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(
                SupportedArrayFormats = null,
                SupportedObjectFormats = null)]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static () => "ok");
            }
            """);

        var registration = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(registration).DoesNotContain("ApplyQueryParsingOverrides");
    }

    [Test]
    public async Task Valiant_Query_Parsing_Preserves_Empty_Duplicate_And_Invalid_Enum_Overrides()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(
                SupportedArrayFormats = [],
                SupportedObjectFormats =
                [
                    QueryObjectFormat.DotNotation,
                    (QueryObjectFormat)999,
                    QueryObjectFormat.DotNotation
                ])]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static () => "ok");
            }
            """);

        var registration = GetGeneratedSource(result);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(registration).Contains(
            "new global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat[] {  }");
        await Assert.That(registration).Contains(
            "new global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat[] { " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat)1, " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat)999, " +
            "(global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat)1 }");
    }

    [Test]
    public async Task Conflicting_Direct_Query_Json_Policies_Report_VLE011()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class EnabledEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/enabled", static ([FromQuery] SharedRequest request) => request);
            }

            [ValiantEndpoint]
            [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
            public sealed class DisabledEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/disabled", static ([FromQuery] SharedRequest request) => request);
            }

            public sealed partial record SharedRequest;
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE011");
        await Assert.That(result.GeneratedSources.Any(static source =>
            source.HintName == "SharedRequest.QueryObject.g.cs")).IsFalse();
    }

    [Test]
    public async Task Collection_And_Dictionary_Reuse_Reports_Conflicting_Direct_Query_Json_Policies()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class EnabledEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/enabled", static ([FromQuery] SharedRequest[] request) => request);
            }

            [ValiantEndpoint]
            [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
            public sealed class DisabledEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/disabled", static ([FromQuery] Dictionary<string, SharedRequest> request) => request);
            }

            public sealed partial record SharedRequest;
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE011");
    }

    [Test]
    public async Task Direct_Query_Parser_Takes_Precedence_Over_AsParameters_Shim_Without_Conflict()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    builder.MapGet("/direct", static ([FromQuery] SharedRequest request) => request);
                    return builder.MapGet("/wrapped", static ([AsParameters] Wrapper request) => request);
                }
            }

            public sealed record Wrapper
            {
                [FromQuery]
                public SharedRequest? Request { get; init; }
            }

            public sealed partial record SharedRequest;
            """);

        var generated = GetGeneratedSource(result, "SharedRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLE011");
        await Assert.That(generated).Contains("throw new global::Microsoft.AspNetCore.Http.BadHttpRequestException");
        await Assert.That(generated).DoesNotContain("Compatibility shim");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Request_BindAsync_For_Unannotated_Request_With_FromQuery_Properties()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed partial class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static (SearchRequest request) => request);
                }

                public sealed partial record SearchRequest
                {
                    [FromQuery]
                    public FacetRequest? Facets { get; init; }
                }

                public sealed record FacetRequest
                {
                    public string? Category { get; init; }
                    public int? Limit { get; init; }
                }
            }
            """);

        var generated = GetGeneratedSource(result, "SearchEndpoint_SearchRequest.QueryRequest.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static global::System.Threading.Tasks.ValueTask<global::SearchEndpoint.SearchRequest> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context, global::System.Reflection.ParameterInfo parameter)");
        await Assert.That(generated).Contains("global::StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure.ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(context)");
        await Assert.That(generated).Contains("global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryStringParser.Parse<global::SearchEndpoint.SearchRequest>");
    }

    [Test]
    public async Task Query_Binding_Is_Not_Generated_Without_Explicit_OptIn()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static (SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public SearchFilter? Filter { get; init; }
            }

            public sealed record SearchFilter
            {
                public int Id { get; init; }
            }
            """,
            enableQueryBinding: false);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLE010");
        await Assert.That(result.GeneratedSources.Any(static source =>
            source.HintName.Contains("QueryRequest", StringComparison.Ordinal) ||
            source.HintName.Contains("QueryObject", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Query_Binding_Is_Not_Generated_For_False_Or_Unrelated_EnableBinding_Assignments()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;
            using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

            public sealed class UnrelatedOptions
            {
                public bool EnableBinding { get; set; }
            }

            public static class Configuration
            {
                public static void Configure(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddValiantEndpoints(static options =>
                    {
                        options.QueryParameters.EnableBinding = false;
                    });
                    new UnrelatedOptions().EnableBinding = true;
                }
            }

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static (SearchRequest request) => request);
                }
            }

            public sealed partial record SearchRequest
            {
                [FromQuery]
                public SearchFilter? Filter { get; init; }
            }

            public sealed partial record SearchFilter;
            """,
            enableQueryBinding: false);

        await Assert.That(result.GeneratedSources.Any(static source =>
            source.HintName.Contains("QueryRequest", StringComparison.Ordinal) ||
            source.HintName.Contains("QueryObject", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Valiant_Query_Parsing_Attribute_Is_Ignored_When_Query_Binding_Is_Disabled()
    {
        const string source = """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;
            using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

            public static class Configuration
            {
                public static void Configure(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddValiantEndpoints(static options =>
                    {
                        options.QueryParameters.EnableBinding = false;
                    });
                }
            }

            [ValiantEndpoint]
            ATTRIBUTE
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static ([FromQuery] SearchRequest request) => request);
            }

            public sealed partial record SearchRequest;
            """;
        var attributed = RunGenerator(
            source.Replace(
                "ATTRIBUTE",
                """
                [ValiantQueryParsing(
                    SupportedArrayFormats = [QueryArrayFormat.CommaSeparated],
                    SupportedObjectFormats = [QueryObjectFormat.DotNotation],
                    JsonValues = JsonQueryValues.Disabled)]
                """,
                StringComparison.Ordinal),
            enableQueryBinding: false);
        var plain = RunGenerator(
            source.Replace("ATTRIBUTE", string.Empty, StringComparison.Ordinal),
            enableQueryBinding: false);

        await Assert.That(attributed.Diagnostics.Select(static diagnostic => diagnostic.Id))
            .DoesNotContain("VLE010")
            .And.DoesNotContain("VLE011");
        await Assert.That(attributed.GeneratedSources.Any(static generated =>
            generated.HintName.Contains("QueryRequest", StringComparison.Ordinal) ||
            generated.HintName.Contains("QueryObject", StringComparison.Ordinal))).IsFalse();
        await Assert.That(attributed.GeneratedSources.Any(static source =>
            source.HintName == "SearchRequest.QueryObject.g.cs")).IsFalse();
        await Assert.That(GetGeneratedSource(attributed)).DoesNotContain("ApplyQueryParsingOverrides");
        await Assert.That(attributed.GeneratedSources.Select(static generated => generated.HintName))
            .IsEquivalentTo(plain.GeneratedSources.Select(static generated => generated.HintName));
        await Assert.That(GetGeneratedSource(attributed)).IsEqualTo(GetGeneratedSource(plain));
    }

    [Test]
    public async Task Query_Binding_OptIn_Supports_Expression_Bodied_Setup_Lambda()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;
            using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

            public static class Configuration
            {
                public static void Configure(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddValiantEndpoints(options => options.QueryParameters.EnableBinding = true);
                }
            }

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static (SearchRequest request) => request);
                }
            }

            public sealed partial record SearchRequest
            {
                [FromQuery]
                public SearchFilter? Filter { get; init; }
            }

            public sealed partial record SearchFilter;
            """,
            enableQueryBinding: false);

        await Assert.That(result.GeneratedSources.Any(static source =>
            source.HintName.Contains("QueryRequest", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Changing_Query_Binding_OptIn_From_True_To_False_Removes_Query_Sources()
    {
        const string source = """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;
            using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

            public static class Configuration
            {
                public static void Configure(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddValiantEndpoints(options => options.QueryParameters.EnableBinding = ENABLED);
                }
            }

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                    builder.MapGet("/search", static (SearchRequest request) => request);
            }

            public sealed partial record SearchRequest
            {
                [FromQuery]
                public SearchFilter? Filter { get; init; }
            }

            public sealed partial record SearchFilter;
            """;

        var enabled = RunGenerator(source.Replace("ENABLED", "true", StringComparison.Ordinal), enableQueryBinding: false);
        var disabled = RunGenerator(source.Replace("ENABLED", "false", StringComparison.Ordinal), enableQueryBinding: false);

        await Assert.That(enabled.GeneratedSources.Any(static generated =>
            generated.HintName.Contains("QueryRequest", StringComparison.Ordinal))).IsTrue();
        await Assert.That(disabled.GeneratedSources.Any(static generated =>
            generated.HintName.Contains("QueryRequest", StringComparison.Ordinal) ||
            generated.HintName.Contains("QueryObject", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Request_BindAsync_For_Generic_Endpoint_Group_Request()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class CarsEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint<CarsEndpointGroup>]
            public sealed partial class ListCarsEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/cars", static (Request request) => request);
                }

                public sealed partial record Request
                {
                    [FromQuery]
                    public Filter? Filter { get; init; }
                }

                public sealed record Filter
                {
                    public int Id { get; init; }
                }
            }
            """);

        var generated = GetGeneratedSource(result, "ListCarsEndpoint_Request.QueryRequest.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static global::System.Threading.Tasks.ValueTask<global::ListCarsEndpoint.Request> BindAsync(global::Microsoft.AspNetCore.Http.HttpContext context, global::System.Reflection.ParameterInfo parameter)");
        await Assert.That(generated).Contains("global::StaticNorth.Valiant.Endpoints.AspNetCore.QueryStringParser.Parse<global::ListCarsEndpoint.Request>");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Object_TryParse_For_Query_Array_Element()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public FacetRequest[]? Facets { get; init; }
            }

            public sealed partial record FacetRequest
            {
                public string? Category { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "FacetRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static bool TryParse(string value, out global::FacetRequest? result)");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Object_TryParse_For_Query_List_Element()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public List<FacetRequest>? Facets { get; init; }
            }

            public sealed partial record FacetRequest
            {
                public string? Category { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "FacetRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static bool TryParse(string value, out global::FacetRequest? result)");
    }

    [Test]
    public async Task Valiant_Endpoint_Generates_Query_Object_TryParse_For_Query_Dictionary_Value()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public Dictionary<string, FacetRequest>? Facets { get; init; }
            }

            public sealed partial record FacetRequest
            {
                public string? Category { get; init; }
            }
            """);

        var generated = GetGeneratedSource(result, "FacetRequest.QueryObject.g.cs");

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(generated).Contains("public static bool TryParse(string value, out global::FacetRequest? result)");
    }

    [Test]
    public async Task Valiant_Endpoint_Query_Object_Binding_Skips_Primitive_Query_List()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public List<int>? Ids { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Select(static source => source.HintName)).DoesNotContain("Int32.QueryObject.g.cs");
    }

    [Test]
    public async Task Valiant_Endpoint_Query_Object_Binding_Reports_Diagnostic_When_Query_Object_Is_Not_Partial()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public FacetRequest? Facets { get; init; }
            }

            public sealed record FacetRequest
            {
                public string? Category { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE010");
        await Assert.That(result.GeneratedSources.Select(static source => source.HintName)).DoesNotContain("FacetRequest.QueryObject.g.cs");
    }

    [Test]
    public async Task Valiant_Endpoint_Query_Object_Binding_Skips_Type_With_Existing_TryParse()
    {
        var result = RunGenerator(
            """
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static ([AsParameters] SearchRequest request) => request);
                }
            }

            public sealed record SearchRequest
            {
                [FromQuery]
                public FacetRequest? Facets { get; init; }
            }

            public sealed record FacetRequest
            {
                public static bool TryParse(string value, out FacetRequest? result)
                {
                    result = new FacetRequest();
                    return true;
                }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Select(static source => source.HintName)).DoesNotContain("FacetRequest.QueryObject.g.cs");
    }

    [Test]
    public async Task Valiant_Endpoint_Query_Object_Binding_Does_Not_Scan_Handler_Lambda_Body()
    {
        var result = RunGenerator(
            """
            using System.Linq;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class SearchEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    return builder.MapGet("/search", static () =>
                    {
                        return new[] { new FacetRequest() }
                            .Select(static ([FromQuery] FacetRequest request) => request)
                            .ToArray();
                    });
                }
            }

            public sealed record FacetRequest
            {
                public string? Category { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Select(static source => source.HintName)).DoesNotContain("FacetRequest.QueryObject.g.cs");
    }

    [Test]
    public async Task Generated_Output_Is_Deterministically_Ordered()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class RootB : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint]
            public sealed class RootA : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint(typeof(GroupB))]
            public sealed class EndpointB : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint(typeof(GroupA))]
            public sealed class EndpointA : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup]
            public sealed class GroupB : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup]
            public sealed class GroupA : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(generated.IndexOf("MapEndpointGroup<global::GroupA>", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpointGroup<global::GroupB>", StringComparison.Ordinal));
        await Assert.That(generated.IndexOf("MapEndpoint<global::EndpointA>(group0,", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpoint<global::EndpointB>(group1,", StringComparison.Ordinal));
        await Assert.That(generated.IndexOf("MapEndpoint<global::RootA>(builder,", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpoint<global::RootB>(builder,", StringComparison.Ordinal));
    }

    [Test]
    public async Task Generated_Output_Respects_Endpoint_Order()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint(Order = 20)]
            public sealed class RootA : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint(Order = 10)]
            public sealed class RootB : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup(Order = 20)]
            public sealed class GroupA : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup(Order = 10)]
            public sealed class GroupB : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint(typeof(GroupA), Order = 20)]
            public sealed class EndpointA : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpoint(typeof(GroupB), Order = 10)]
            public sealed class EndpointB : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        var generated = GetGeneratedSource(result);

        await Assert.That(generated).Contains("CreateEndpointGroupRegistration<global::GroupB>(10)");
        await Assert.That(generated).Contains("CreateEndpointGroupRegistration<global::GroupA>(20)");
        await Assert.That(generated).Contains("CreateEndpointRegistration<global::EndpointB>(group0, groupRegistration0, 10,");
        await Assert.That(generated).Contains("CreateEndpointRegistration<global::EndpointA>(group1, groupRegistration1, 20,");
        await Assert.That(generated.IndexOf("MapEndpointGroup<global::GroupB>", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpointGroup<global::GroupA>", StringComparison.Ordinal));
        await Assert.That(generated.IndexOf("MapEndpoint<global::EndpointB>(group0,", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpoint<global::EndpointA>(group1,", StringComparison.Ordinal));
        await Assert.That(generated.IndexOf("MapEndpoint<global::RootB>(builder,", StringComparison.Ordinal)).IsLessThan(generated.IndexOf("MapEndpoint<global::RootA>(builder,", StringComparison.Ordinal));
    }

    [Test]
    public async Task Unsupported_Endpoint_Shape_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;

            [ValiantEndpoint]
            public sealed class NotAnEndpoint;
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE001");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Implementing_Both_Shapes_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class AmbiguousEndpoint : IEndpoint, IEndpointGroup
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE002");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Group_With_Group_Type_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint(typeof(ParentGroup))]
            public sealed class ChildGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup]
            public sealed class ParentGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE003");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_With_Invalid_Group_Type_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint(typeof(NotAGroup))]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            public sealed class NotAGroup;
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE004");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Generic_Endpoint_With_Invalid_Group_Type_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint<NotAGroup>]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            public sealed class NotAGroup;
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE004");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_With_Duplicate_Endpoint_Attributes_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint(typeof(TodosEndpointGroup))]
            [ValiantEndpoint<TodosEndpointGroup>]
            public sealed class ListTodosEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }

            [ValiantEndpointGroup]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE009");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_With_Endpoint_Group_Attribute_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpointGroup]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE008");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Group_With_Endpoint_Attribute_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE003");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Without_Endpoint_Attribute_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE006");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Group_Without_Endpoint_Group_Attribute_Reports_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            public sealed class TodosEndpointGroup : IEndpointGroup
            {
                public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("VLE007");
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    [Test]
    public async Task Missing_Marker_Analyzer_Intentionally_Ignores_Aliased_Endpoint_Interface()
    {
        var result = RunGenerator(
            """
            using EndpointContract = StaticNorth.Valiant.Endpoints.AspNetCore.IEndpoint;

            public sealed class HealthEndpoint : EndpointContract
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """);

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).DoesNotContain("VLE006");
    }

    [Test]
    public async Task Query_Binding_Discovery_Ignores_Unrelated_Lambdas_Inside_Map()
    {
        var result = RunGenerator(
            """
            using System;
            using Microsoft.AspNetCore.Mvc;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
                {
                    Helpers.Use(([FromQuery] QueryFilter filter) => { });
                    return builder.MapGet("/health", () => "ok");
                }
            }

            public static class Helpers
            {
                public static void Use(Action<QueryFilter> action) { }
            }

            public sealed partial record QueryFilter
            {
                public int Id { get; init; }
            }
            """);

        await Assert.That(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEmpty();
        await Assert.That(result.GeneratedSources.Any(static source =>
            source.HintName.Contains("QueryFilter.QueryObject", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task Query_Parsing_Attribute_Edit_Invalidates_Tracked_Endpoint_Step()
    {
        const string source = """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            [ValiantQueryParsing(SupportedArrayFormats = [ARRAY_FORMAT])]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """;

        var reasons = RunTrackedGeneratorEdit(
            source.Replace("ARRAY_FORMAT", "QueryArrayFormat.RepeatedKeys", StringComparison.Ordinal),
            source.Replace("ARRAY_FORMAT", "QueryArrayFormat.CommaSeparated", StringComparison.Ordinal),
            "Endpoints.Endpoint");

        await Assert.That(reasons).Contains(IncrementalStepRunReason.Modified);
    }

    [Test]
    public async Task Unrelated_Edit_Leaves_Tracked_Endpoint_Step_Cached_Or_Unchanged()
    {
        const string endpoint = """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """;

        var reasons = RunTrackedGeneratorEdit(
            endpoint + "\npublic sealed class Unrelated { public int Value { get; init; } }",
            endpoint + "\npublic sealed class Unrelated { public int Value { get; init; } public string? Text { get; init; } }",
            "Endpoints.Endpoint");

        await Assert.That(reasons).IsNotEmpty();
        await Assert.That(reasons.All(static reason => reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged)).IsTrue();
    }

    [Test]
    public async Task Unrelated_Edit_Leaves_Registration_Output_Cached_Or_Unchanged()
    {
        const string endpoint = """
            using StaticNorth.Valiant.Endpoints;
            using StaticNorth.Valiant.Endpoints.AspNetCore;

            [ValiantEndpoint]
            public sealed class HealthEndpoint : IEndpoint
            {
                public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => throw null!;
            }
            """;

        var reasons = RunTrackedGeneratorEdit(
            endpoint + "\npublic sealed class Unrelated { public int Value { get; init; } }",
            endpoint + "\npublic sealed class Unrelated { public int Value { get; init; } public string? Text { get; init; } }",
            "Endpoints.Registrations");

        await Assert.That(reasons).IsNotEmpty();
        await Assert.That(reasons.All(static reason =>
                reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged))
            .IsTrue();
    }

    private static string GetGeneratedSource(SourceGenerationResult result)
    {
        if (result.GeneratedSources.IsEmpty && !result.Diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return result.GeneratedSources.Single(static source =>
            source.HintName.StartsWith("StaticNorth.Valiant.Endpoints.AspNetCore.", StringComparison.Ordinal) &&
            source.HintName.EndsWith(".g.cs", StringComparison.Ordinal)).SourceText.ToString();
    }

    private static string GetGeneratedSource(SourceGenerationResult result, string hintName)
    {
        if (result.GeneratedSources.IsEmpty && !result.Diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }

        return result.GeneratedSources.Single(source => source.HintName == hintName).SourceText.ToString();
    }

    private static SourceGenerationResult RunGenerator(string source, bool enableQueryBinding = true)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var sourceWithUsings =
            """
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Routing;

            """ + source + (enableQueryBinding
                ? """

                  internal static class ValiantQueryBindingTestConfiguration
                  {
                      public static void Configure(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                      {
                          global::StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection.ValiantEndpointServiceCollectionExtensions.AddValiantEndpoints(
                              services,
                              static options =>
                              {
                                  options.QueryParameters.EnableBinding = true;
                              });
                      }
                  }

                  """
                : string.Empty);
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(ValiantEndpointAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IEndpoint).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(AsParametersAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(FromQueryAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IEndpointRouteBuilder).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IEndpointConventionBuilder).Assembly.Location));

        var compilation = GeneratorTestHarness.CreateCompilation(
            sourceWithUsings,
            parseOptions,
            references,
            "StaticNorth.Valiant.Endpoints.SourceGeneration.Tests");
        var execution = GeneratorTestHarness.Run(new ValiantEndpointGenerator(), compilation, parseOptions);
        var analyzerDiagnostics = execution.OutputCompilation
            .WithAnalyzers([new ValiantEndpointDeclarationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        var diagnostics = execution.OutputCompilation.GetDiagnostics()
            .Concat(execution.GeneratorDiagnostics)
            .Concat(analyzerDiagnostics)
            .Where(static diagnostic => diagnostic.Id.StartsWith("VLE", StringComparison.Ordinal) ||
                diagnostic.Id is "CS8784" or "CS8785")
            .ToImmutableArray();

        return new SourceGenerationResult(
            diagnostics,
            execution.GeneratedSources);
    }

    private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
    {
        return GeneratorTestHarness.GetTrustedPlatformReferences();
    }

    private static ImmutableArray<IncrementalStepRunReason> RunTrackedGeneratorEdit(
        string before,
        string after,
        string stepName)
    {
        var references = GetTrustedPlatformReferences()
            .Add(MetadataReference.CreateFromFile(typeof(ValiantEndpointAttribute).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IEndpoint).Assembly.Location))
            .Add(MetadataReference.CreateFromFile(typeof(IEndpointRouteBuilder).Assembly.Location));
        return GeneratorTestHarness.RunTrackedGeneratorEdit(
            new ValiantEndpointGenerator(),
            before,
            after,
            stepName,
            references,
            "StaticNorth.Valiant.Endpoints.Tracked.Tests");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed record SourceGenerationResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
