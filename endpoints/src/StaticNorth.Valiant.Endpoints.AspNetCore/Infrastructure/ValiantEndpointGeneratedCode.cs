using System.ComponentModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

/// <summary>
/// Provides the cross-assembly runtime bridge used by Valiant-generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ValiantEndpointGeneratedCode
{
    /// <summary>
    /// Creates generated endpoint group registration metadata.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static EndpointGroupRegistration CreateEndpointGroupRegistration<TGroup>(int order)
        where TGroup : IEndpointGroup =>
        ValiantEndpointRouteBuilderExtensions.CreateEndpointGroupRegistration<TGroup>(order);

    /// <summary>
    /// Creates generated endpoint registration metadata.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static EndpointRegistration CreateEndpointRegistration<TEndpoint>(
        IEndpointRouteBuilder builder,
        EndpointGroupRegistration? group,
        int order,
        bool isBindingGenerated,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
        where TEndpoint : IEndpoint =>
        ValiantEndpointRouteBuilderExtensions.CreateEndpointRegistration<TEndpoint>(
            builder,
            group,
            order,
            isBindingGenerated,
            supportedArrayFormats,
            supportedObjectFormats,
            enableJsonQueryValues);

    /// <summary>
    /// Evaluates whether a generated endpoint or group should be mapped.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool ShouldMap<T>(IEndpointRouteBuilder builder) =>
        ValiantEndpointRouteBuilderExtensions.ShouldMap<T>(builder);

    /// <summary>
    /// Evaluates whether a generated endpoint group should be mapped.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool ShouldMapEndpointGroup<TGroup>(IEndpointRouteBuilder builder)
        where TGroup : IEndpointGroup =>
        ValiantEndpointRouteBuilderExtensions.ShouldMapEndpointGroup<TGroup>(builder);

    /// <summary>
    /// Evaluates whether generated endpoint group registration metadata should be mapped.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool ShouldMapEndpointGroup<TGroup>(
        IEndpointRouteBuilder builder,
        EndpointGroupRegistration registration)
        where TGroup : IEndpointGroup =>
        ValiantEndpointRouteBuilderExtensions.ShouldMapEndpointGroup<TGroup>(
            builder,
            registration);

    /// <summary>
    /// Evaluates whether a generated endpoint should be mapped.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool ShouldMapEndpoint<TEndpoint>(IEndpointRouteBuilder builder)
        where TEndpoint : IEndpoint =>
        ValiantEndpointRouteBuilderExtensions.ShouldMapEndpoint<TEndpoint>(builder);

    /// <summary>
    /// Evaluates whether generated endpoint registration metadata should be mapped.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool ShouldMapEndpoint<TEndpoint>(
        IEndpointRouteBuilder builder,
        EndpointRegistration registration)
        where TEndpoint : IEndpoint =>
        ValiantEndpointRouteBuilderExtensions.ShouldMapEndpoint<TEndpoint>(
            builder,
            registration);

    /// <summary>
    /// Creates a route group for generated endpoint mapping.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RouteGroupBuilder MapEndpointGroup<TGroup>(IEndpointRouteBuilder builder)
        where TGroup : IEndpointGroup =>
        ValiantEndpointRouteBuilderExtensions.MapEndpointGroup<TGroup>(builder);

    /// <summary>
    /// Creates a route group using generated registration metadata.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RouteGroupBuilder MapEndpointGroup<TGroup>(
        IEndpointRouteBuilder builder,
        EndpointGroupRegistration registration)
        where TGroup : IEndpointGroup =>
        ValiantEndpointRouteBuilderExtensions.MapEndpointGroup<TGroup>(
            builder,
            registration);

    /// <summary>
    /// Maps an endpoint from generated registration code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IEndpointConventionBuilder MapEndpoint<TEndpoint>(IEndpointRouteBuilder builder)
        where TEndpoint : IEndpoint =>
        ValiantEndpointRouteBuilderExtensions.MapEndpoint<TEndpoint>(builder);

    /// <summary>
    /// Maps an endpoint using generated registration metadata.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IEndpointConventionBuilder MapEndpoint<TEndpoint>(
        IEndpointRouteBuilder builder,
        EndpointRegistration registration)
        where TEndpoint : IEndpoint =>
        ValiantEndpointRouteBuilderExtensions.MapEndpoint<TEndpoint>(
            builder,
            registration);

    /// <summary>
    /// Applies source-generated query parsing overrides to a mapped endpoint.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ApplyQueryParsingOverrides(
        IEndpointConventionBuilder endpoint,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var arrayFormatsSnapshot = supportedArrayFormats is null ? null : supportedArrayFormats.ToArray();
        var objectFormatsSnapshot = supportedObjectFormats is null ? null : supportedObjectFormats.ToArray();
        endpoint.Add(endpointBuilder => endpointBuilder.Metadata.Add(
            new EndpointQueryParsingOverridesMetadata(
                arrayFormatsSnapshot,
                objectFormatsSnapshot,
                enableJsonQueryValues)));
    }

    /// <summary>
    /// Preserves the post-mapping entry point emitted by previously generated registrations.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static RouteGroupBuilder ConfigureEndpointGroupAfterMapping<TGroup>(RouteGroupBuilder group)
        where TGroup : IEndpointGroup
    {
        ArgumentNullException.ThrowIfNull(group);
        return group;
    }

    /// <summary>
    /// Resolves query parsing options from generated binding code.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static QueryParsingOptions ResolveQueryParsingOptions(HttpContext context) =>
        QueryParsingOptionsResolver.Resolve(context);

}
