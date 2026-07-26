using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
using StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

namespace StaticNorth.Valiant.Endpoints.Tests.ApiSurface;

public sealed class PublicApiSurfaceTests
{
    [Test]
    public async Task Endpoint_Authoring_Assembly_Exports_Only_Authoring_Attributes()
    {
        var visibleTypes = GetVisibleExportedTypeNames(typeof(ValiantEndpointAttribute).Assembly);

        await Assert.That(visibleTypes).IsEquivalentTo([
            "StaticNorth.Valiant.Endpoints.ValiantEndpointAttribute",
            "StaticNorth.Valiant.Endpoints.ValiantEndpointAttribute`1",
            "StaticNorth.Valiant.Endpoints.ValiantEndpointGroupAttribute"
        ]);
    }

    [Test]
    public async Task AspNetCore_Assembly_Exports_Only_EndUser_Types_Outside_Hidden_Infrastructure()
    {
        var visibleTypes = GetVisibleExportedTypeNames(typeof(IEndpoint).Assembly);

        await Assert.That(visibleTypes).IsEquivalentTo([
            "StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection.ValiantEndpointServiceCollectionExtensions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.EndpointConfigurationOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.EndpointGroupRegistration",
            "StaticNorth.Valiant.Endpoints.AspNetCore.EndpointQueryParameterConfigurationOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.EndpointQueryParameterRegistration",
            "StaticNorth.Valiant.Endpoints.AspNetCore.EndpointRegistration",
            "StaticNorth.Valiant.Endpoints.AspNetCore.IEndpoint",
            "StaticNorth.Valiant.Endpoints.AspNetCore.IEndpointGroup",
            "StaticNorth.Valiant.Endpoints.AspNetCore.JsonQueryValues",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryArrayFormat",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryError",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryErrorKind",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryObjectFormat",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryParameterOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryParseResult",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryParseResult`1",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryParsingOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QuerySerializationOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QuerySerializationResult",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryStringParser",
            "StaticNorth.Valiant.Endpoints.AspNetCore.QueryStringSerializer",
            "StaticNorth.Valiant.Endpoints.AspNetCore.ValiantEndpointOptions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.ValiantEndpointRouteBuilderExtensions",
            "StaticNorth.Valiant.Endpoints.AspNetCore.ValiantQueryParsingAttribute"
        ]);
    }

    [Test]
    public async Task Endpoint_Assemblies_Expose_Only_StaticNorth_Valiant_Namespaces()
    {
        Assembly[] assemblies = [
            typeof(ValiantEndpointAttribute).Assembly,
            typeof(IEndpoint).Assembly,
            typeof(StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration.ValiantEndpointGenerator).Assembly
        ];

        foreach (var assembly in assemblies)
        {
            var unexpectedNamespaces = assembly
                .GetExportedTypes()
                .Select(static type => type.Namespace)
                .Where(static @namespace => @namespace is null || !@namespace.StartsWith("StaticNorth.Valiant.", StringComparison.Ordinal))
                .ToArray();

            await Assert.That(unexpectedNamespaces).IsEmpty();
        }
    }

    [Test]
    public async Task Query_Parsing_Attribute_Has_A_Narrow_Endpoint_Authoring_Shape()
    {
        var usage = typeof(ValiantQueryParsingAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        var attribute = new ValiantQueryParsingAttribute();

        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Class);
        await Assert.That(usage.AllowMultiple).IsFalse();
        await Assert.That(usage.Inherited).IsFalse();
        await Assert.That(attribute.SupportedArrayFormats).IsNull();
        await Assert.That(attribute.SupportedObjectFormats).IsNull();
        await Assert.That(attribute.JsonValues).IsEqualTo(JsonQueryValues.Enabled);
        await Assert.That(typeof(ValiantQueryParsingAttribute)
            .GetProperty(nameof(ValiantQueryParsingAttribute.SupportedArrayFormats))!
            .PropertyType).IsEqualTo(typeof(QueryArrayFormat[]));
        await Assert.That(typeof(ValiantQueryParsingAttribute)
            .GetProperty(nameof(ValiantQueryParsingAttribute.SupportedObjectFormats))!
            .PropertyType).IsEqualTo(typeof(QueryObjectFormat[]));
        await Assert.That(Enum.GetNames<JsonQueryValues>()).IsEquivalentTo([
            nameof(JsonQueryValues.Disabled),
            nameof(JsonQueryValues.Enabled)
        ]);
    }

    [Test]
    public async Task Endpoint_Options_Expose_Separate_Group_And_Endpoint_Mapping_Policies()
    {
        var methods = typeof(ValiantEndpointOptions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(methods.Select(static method => method.Name))
            .IsEquivalentTo(["ConfigureEndpointWhen"]);

        var configureEndpointWhen = methods.Single();
        await Assert.That(configureEndpointWhen.ReturnType).IsEqualTo(typeof(void));
        await Assert.That(configureEndpointWhen.GetParameters()
            .Select(static parameter => parameter.ParameterType))
            .IsEquivalentTo([
                typeof(Func<EndpointRegistration, bool>),
                typeof(Action<EndpointConfigurationOptions>)
            ]);

        await Assert.That(typeof(ValiantEndpointOptions).GetProperty("ShouldMap")).IsNull();
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty(nameof(ValiantEndpointOptions.MapEndpointGroupsWhen))!
            .PropertyType).IsEqualTo(typeof(Func<EndpointGroupRegistration, bool>));
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty(nameof(ValiantEndpointOptions.MapEndpointWhen))!
            .PropertyType).IsEqualTo(typeof(Func<EndpointRegistration, bool>));
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty(nameof(ValiantEndpointOptions.ConfigureEndpointGroup))!
            .PropertyType).IsEqualTo(
                typeof(Action<RouteGroupBuilder, EndpointGroupRegistration>));
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty(nameof(ValiantEndpointOptions.ConfigureEndpoint))!
            .PropertyType).IsEqualTo(
                typeof(Action<IEndpointConventionBuilder, EndpointRegistration>));
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty("ConfigureAllEndpointGroupsBeforeMapping")).IsNull();
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty("ConfigureAllEndpointGroupsAfterMapping")).IsNull();
        await Assert.That(typeof(ValiantEndpointOptions)
            .GetProperty("ConfigureAllEndpointsAfterMapping")).IsNull();
    }

    [Test]
    public async Task EndUser_Extension_Classes_Expose_Only_The_Supported_Workflow()
    {
        var routeMethods = GetPublicDeclaredMethods(typeof(ValiantEndpointRouteBuilderExtensions));
        var serviceMethods = GetPublicDeclaredMethods(typeof(ValiantEndpointServiceCollectionExtensions));

        await Assert.That(routeMethods).IsEquivalentTo([
            "MapValiantEndpoints",
            "WithQueryParsingOptions"
        ]);
        await Assert.That(serviceMethods).IsEquivalentTo(Enumerable.Repeat("AddValiantEndpoints", 6));
    }

    [Test]
    public async Task CrossAssembly_Infrastructure_Is_Hidden_From_IntelliSense()
    {
        Type[] infrastructureTypes = [
            typeof(IValiantEndpointRouteRegistrar),
            typeof(ValiantEndpointRouteRegistrarAttribute),
            typeof(ValiantEndpointGeneratedCode)
        ];

        foreach (var type in infrastructureTypes)
        {
            await Assert.That(IsEditorBrowsableNever(type)).IsTrue();

            var publicMembers = type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(static member => member is ConstructorInfo or PropertyInfo ||
                    member is MethodInfo { IsSpecialName: false });

            foreach (var member in publicMembers)
            {
                await Assert.That(IsEditorBrowsableNever(member)).IsTrue();
            }
        }
    }

    [Test]
    public async Task RuntimeOnly_Query_Types_And_Generated_Registrar_Are_Not_Exported()
    {
        var runtimeAssembly = typeof(IEndpoint).Assembly;
        var metadataType = runtimeAssembly.GetType("StaticNorth.Valiant.Endpoints.AspNetCore.EndpointQueryParsingOptionsMetadata");
        var overridesMetadataType = runtimeAssembly.GetType("StaticNorth.Valiant.Endpoints.AspNetCore.EndpointQueryParsingOverridesMetadata");
        var resolverType = runtimeAssembly.GetType("StaticNorth.Valiant.Endpoints.AspNetCore.QueryParsingOptionsResolver");
        var registrarType = typeof(PublicApiSurfaceTests).Assembly
            .GetType("StaticNorth.Valiant.Endpoints.AspNetCore.Generated.ValiantEndpointRouteRegistrar");

        await Assert.That(metadataType).IsNotNull();
        await Assert.That(metadataType!.IsNotPublic).IsTrue();
        await Assert.That(overridesMetadataType).IsNotNull();
        await Assert.That(overridesMetadataType!.IsNotPublic).IsTrue();
        await Assert.That(resolverType).IsNotNull();
        await Assert.That(resolverType!.IsNotPublic).IsTrue();
        await Assert.That(registrarType).IsNotNull();
        await Assert.That(registrarType!.IsNotPublic).IsTrue();
        await Assert.That(registrarType.GetConstructor(Type.EmptyTypes)).IsNotNull();
    }

    private static string[] GetVisibleExportedTypeNames(Assembly assembly) =>
        assembly
            .GetExportedTypes()
            .Where(static type => !IsEditorBrowsableNever(type))
            .Select(static type => type.FullName!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static string[] GetPublicDeclaredMethods(Type type) =>
        type
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static bool IsEditorBrowsableNever(MemberInfo member) =>
        member.GetCustomAttribute<EditorBrowsableAttribute>()?.State == EditorBrowsableState.Never;
}
