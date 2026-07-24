using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Valiant.Endpoints.AspNetCore;
using Valiant.Endpoints.AspNetCore.DependencyInjection;
using Valiant.Endpoints.AspNetCore.Infrastructure;

namespace Valiant.Endpoints.Tests.ApiSurface;

public sealed class PublicApiSurfaceTests
{
    [Test]
    public async Task Endpoint_Authoring_Assembly_Exports_Only_Authoring_Attributes()
    {
        var visibleTypes = GetVisibleExportedTypeNames(typeof(ValiantEndpointAttribute).Assembly);

        await Assert.That(visibleTypes).IsEquivalentTo([
            "Valiant.Endpoints.ValiantEndpointAttribute",
            "Valiant.Endpoints.ValiantEndpointAttribute`1",
            "Valiant.Endpoints.ValiantEndpointGroupAttribute"
        ]);
    }

    [Test]
    public async Task AspNetCore_Assembly_Exports_Only_EndUser_Types_Outside_Hidden_Infrastructure()
    {
        var visibleTypes = GetVisibleExportedTypeNames(typeof(IEndpoint).Assembly);

        await Assert.That(visibleTypes).IsEquivalentTo([
            "Valiant.Endpoints.AspNetCore.DependencyInjection.ValiantEndpointServiceCollectionExtensions",
            "Valiant.Endpoints.AspNetCore.EndpointConfigurationOptions",
            "Valiant.Endpoints.AspNetCore.EndpointGroupRegistration",
            "Valiant.Endpoints.AspNetCore.EndpointQueryParameterConfigurationOptions",
            "Valiant.Endpoints.AspNetCore.EndpointQueryParameterRegistration",
            "Valiant.Endpoints.AspNetCore.EndpointRegistration",
            "Valiant.Endpoints.AspNetCore.IEndpoint",
            "Valiant.Endpoints.AspNetCore.IEndpointGroup",
            "Valiant.Endpoints.AspNetCore.JsonQueryValues",
            "Valiant.Endpoints.AspNetCore.QueryArrayFormat",
            "Valiant.Endpoints.AspNetCore.QueryError",
            "Valiant.Endpoints.AspNetCore.QueryErrorKind",
            "Valiant.Endpoints.AspNetCore.QueryObjectFormat",
            "Valiant.Endpoints.AspNetCore.QueryParameterOptions",
            "Valiant.Endpoints.AspNetCore.QueryParseResult",
            "Valiant.Endpoints.AspNetCore.QueryParseResult`1",
            "Valiant.Endpoints.AspNetCore.QueryParsingOptions",
            "Valiant.Endpoints.AspNetCore.QuerySerializationOptions",
            "Valiant.Endpoints.AspNetCore.QuerySerializationResult",
            "Valiant.Endpoints.AspNetCore.QueryStringParser",
            "Valiant.Endpoints.AspNetCore.QueryStringSerializer",
            "Valiant.Endpoints.AspNetCore.ValiantEndpointOptions",
            "Valiant.Endpoints.AspNetCore.ValiantEndpointRouteBuilderExtensions",
            "Valiant.Endpoints.AspNetCore.ValiantQueryParsingAttribute"
        ]);
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
        var metadataType = runtimeAssembly.GetType("Valiant.Endpoints.AspNetCore.EndpointQueryParsingOptionsMetadata");
        var overridesMetadataType = runtimeAssembly.GetType("Valiant.Endpoints.AspNetCore.EndpointQueryParsingOverridesMetadata");
        var resolverType = runtimeAssembly.GetType("Valiant.Endpoints.AspNetCore.QueryParsingOptionsResolver");
        var registrarType = typeof(PublicApiSurfaceTests).Assembly
            .GetType("Valiant.Endpoints.AspNetCore.Generated.ValiantEndpointRouteRegistrar");

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
