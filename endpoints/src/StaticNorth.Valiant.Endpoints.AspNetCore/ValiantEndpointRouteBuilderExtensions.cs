using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Extension methods for mapping endpoint-per-class Minimal APIs.
/// </summary>
public static class ValiantEndpointRouteBuilderExtensions
{
    private static readonly ConditionalWeakTable<IEndpointRouteBuilder, EndpointGroupRegistration> EndpointGroupRegistrations = new();

    internal static EndpointGroupRegistration CreateEndpointGroupRegistration<TGroup>(int order)
        where TGroup : IEndpointGroup =>
        new(typeof(TGroup), order);

    internal static EndpointRegistration CreateEndpointRegistration<TEndpoint>(
        this IEndpointRouteBuilder builder,
        EndpointGroupRegistration? group,
        int order,
        bool isBindingGenerated,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
        where TEndpoint : IEndpoint
    {
        ArgumentNullException.ThrowIfNull(builder);

        return GetOptions(builder).CreateEndpointRegistration(
            typeof(TEndpoint),
            group,
            order,
            isBindingGenerated,
            supportedArrayFormats,
            supportedObjectFormats,
            enableJsonQueryValues);
    }

    /// <summary>
    /// Creates and configures an endpoint group.
    /// </summary>
    /// <typeparam name="TGroup">The endpoint group type.</typeparam>
    /// <param name="builder">The route builder to map the group on.</param>
    /// <returns>The configured route group builder.</returns>
    internal static RouteGroupBuilder MapEndpointGroup<TGroup>(this IEndpointRouteBuilder builder)
        where TGroup : IEndpointGroup
    {
        return builder.MapEndpointGroup<TGroup>(CreateEndpointGroupRegistration<TGroup>(0));
    }

    internal static RouteGroupBuilder MapEndpointGroup<TGroup>(
        this IEndpointRouteBuilder builder,
        EndpointGroupRegistration registration)
        where TGroup : IEndpointGroup
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registration);

        var groupType = typeof(TGroup);
        if (registration.GroupType != groupType)
        {
            throw new ArgumentException(
                $"Endpoint group registration type '{registration.GroupType}' does not match '{groupType}'.",
                nameof(registration));
        }

        var options = GetOptions(builder);
        var group = TGroup.CreateGroup(builder);

        EndpointGroupRegistrations.Remove(group);
        EndpointGroupRegistrations.Add(group, registration);
        options.ConfigureEndpointGroup(group, registration);
        return group;
    }

    /// <summary>
    /// Maps a single endpoint.
    /// </summary>
    /// <typeparam name="TEndpoint">The endpoint type.</typeparam>
    /// <param name="builder">The route builder to map the endpoint on.</param>
    /// <returns>The mapped endpoint convention builder.</returns>
    internal static IEndpointConventionBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder builder)
        where TEndpoint : IEndpoint
    {
        var registration = builder.CreateEndpointRegistration<TEndpoint>(
            GetEndpointGroupRegistration(builder),
            order: 0,
            isBindingGenerated: GetOptions(builder).QueryParameters.EnableBinding,
            supportedArrayFormats: null,
            supportedObjectFormats: null,
            enableJsonQueryValues: null);
        return builder.MapEndpoint<TEndpoint>(registration);
    }

    internal static IEndpointConventionBuilder MapEndpoint<TEndpoint>(
        this IEndpointRouteBuilder builder,
        EndpointRegistration registration)
        where TEndpoint : IEndpoint
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registration);

        if (registration.EndpointType != typeof(TEndpoint))
        {
            throw new ArgumentException(
                $"Endpoint registration type '{registration.EndpointType}' does not match '{typeof(TEndpoint)}'.",
                nameof(registration));
        }

        var options = GetOptions(builder);
        var endpoint = TEndpoint.Map(builder);
        if (registration.QueryParameters.IsRuntimeBindingEnabled)
        {
            endpoint.AddValiantQueryBinding();
        }

        if (registration.Configuration.IsConditional)
        {
            endpoint.AddRegistrationQueryParsingOptions(
                registration.Configuration.QueryParameters.Parsing);
        }
        options.ConfigureEndpoint(endpoint, registration);

        return endpoint;
    }

    /// <summary>
    /// Replaces the globally configured query parsing options for a mapped endpoint.
    /// </summary>
    /// <param name="endpoint">The mapped endpoint convention builder.</param>
    /// <param name="options">The query parsing options to use for the endpoint.</param>
    /// <returns>The same endpoint convention builder so calls can be chained.</returns>
    /// <remarks>
    /// When called more than once, the last configured options take precedence.
    /// Explicit properties on <see cref="ValiantQueryParsingAttribute" /> are applied after these options.
    /// </remarks>
    public static IEndpointConventionBuilder WithQueryParsingOptions(
        this IEndpointConventionBuilder endpoint,
        QueryParsingOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(options);

        endpoint.Add(endpointBuilder =>
            endpointBuilder.Metadata.Add(new EndpointQueryParsingOptionsMetadata(options)));
        return endpoint;
    }

    /// <summary>
    /// Determines whether a source-generated endpoint group should be mapped.
    /// </summary>
    /// <typeparam name="TGroup">The endpoint group type being considered for generated mapping.</typeparam>
    /// <param name="builder">The route builder used for generated mapping.</param>
    /// <returns><see langword="true" /> when the configured group mapping policy allows <typeparamref name="TGroup" /> to be mapped.</returns>
    internal static bool ShouldMapEndpointGroup<TGroup>(this IEndpointRouteBuilder builder)
        where TGroup : IEndpointGroup
    {
        return builder.ShouldMapEndpointGroup<TGroup>(
            CreateEndpointGroupRegistration<TGroup>(0));
    }

    internal static bool ShouldMapEndpointGroup<TGroup>(
        this IEndpointRouteBuilder builder,
        EndpointGroupRegistration registration)
        where TGroup : IEndpointGroup
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registration);

        return GetOptions(builder).MapEndpointGroupsWhen(registration);
    }

    /// <summary>
    /// Determines whether a source-generated endpoint should be mapped.
    /// </summary>
    /// <typeparam name="TEndpoint">The endpoint type being considered for generated mapping.</typeparam>
    /// <param name="builder">The route builder used for generated mapping.</param>
    /// <returns><see langword="true" /> when the configured endpoint mapping policy allows <typeparamref name="TEndpoint" /> to be mapped.</returns>
    internal static bool ShouldMapEndpoint<TEndpoint>(this IEndpointRouteBuilder builder)
        where TEndpoint : IEndpoint
    {
        var registration = builder.CreateEndpointRegistration<TEndpoint>(
            GetEndpointGroupRegistration(builder),
            order: 0,
            isBindingGenerated: GetOptions(builder).QueryParameters.EnableBinding,
            supportedArrayFormats: null,
            supportedObjectFormats: null,
            enableJsonQueryValues: null);
        return builder.ShouldMapEndpoint<TEndpoint>(registration);
    }

    internal static bool ShouldMapEndpoint<TEndpoint>(
        this IEndpointRouteBuilder builder,
        EndpointRegistration registration)
        where TEndpoint : IEndpoint
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registration);

        return GetOptions(builder).MapEndpointWhen(registration);
    }

    /// <summary>
    /// Preserves the mapping-policy entry point used by previously generated endpoint registrations.
    /// </summary>
    internal static bool ShouldMap<T>(this IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var type = typeof(T);
        if (typeof(IEndpointGroup).IsAssignableFrom(type))
        {
            var registration = new EndpointGroupRegistration(type, order: 0);
            return GetOptions(builder).MapEndpointGroupsWhen(registration);
        }

        if (typeof(IEndpoint).IsAssignableFrom(type))
        {
            var registration = GetOptions(builder).CreateEndpointRegistration(
                type,
                GetEndpointGroupRegistration(builder),
                order: 0,
                isBindingGenerated: GetOptions(builder).QueryParameters.EnableBinding,
                supportedArrayFormats: null,
                supportedObjectFormats: null,
                enableJsonQueryValues: null);
            return GetOptions(builder).MapEndpointWhen(registration);
        }

        throw new InvalidOperationException(
            $"Generated endpoint type '{type}' must implement {nameof(IEndpoint)} or {nameof(IEndpointGroup)}.");
    }

    /// <summary>
    /// Maps all Valiant endpoints discovered by the source generator and registered in dependency injection.
    /// </summary>
    /// <param name="builder">The route builder to map endpoints on.</param>
    /// <returns>The same route builder instance so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapValiantEndpoints(this IEndpointRouteBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var registrar in builder.ServiceProvider.GetServices<IValiantEndpointRouteRegistrar>())
        {
            registrar.MapEndpoints(builder);
        }

        return builder;
    }

    private static ValiantEndpointOptions GetOptions(IEndpointRouteBuilder builder)
    {
        return builder.ServiceProvider.GetService<IOptions<ValiantEndpointOptions>>()?.Value ?? new ValiantEndpointOptions();
    }

    private static EndpointGroupRegistration? GetEndpointGroupRegistration(
        IEndpointRouteBuilder builder)
    {
        return EndpointGroupRegistrations.TryGetValue(builder, out var registration)
            ? registration
            : null;
    }

    private static void AddRegistrationQueryParsingOptions(
        this IEndpointConventionBuilder endpoint,
        QueryParsingOptions options)
    {
        endpoint.Add(endpointBuilder =>
            endpointBuilder.Metadata.Add(
                new EndpointRegistrationQueryParsingOptionsMetadata(options)));
    }

    private static void AddValiantQueryBinding(this IEndpointConventionBuilder endpoint)
    {
        endpoint.Add(static endpointBuilder =>
        {
            endpointBuilder.FilterFactories.Add(CreateValiantQueryBindingFilter);
        });
    }

    private static EndpointFilterDelegate CreateValiantQueryBindingFilter(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        var parameters = context.MethodInfo.GetParameters();
        var asParametersCandidates = GetAsParametersQueryBindingCandidates(parameters);
        if (asParametersCandidates.Length == 0)
        {
            return next;
        }

        return async invocationContext =>
        {
            var options = QueryParsingOptionsResolver.Resolve(invocationContext.HttpContext);

            foreach (var candidate in asParametersCandidates)
            {
                if (candidate.ArgumentIndex >= invocationContext.Arguments.Count)
                {
                    continue;
                }

                var parsed = QueryStringParser.Parse(
                    invocationContext.HttpContext.Request.QueryString.Value,
                    candidate.RequestType,
                    options);

                if (!parsed.IsSuccess)
                {
                    return Results.BadRequest(parsed.Errors);
                }

                if (parsed.Value is null)
                {
                    continue;
                }

                var target = invocationContext.Arguments[candidate.ArgumentIndex] ??
                    Activator.CreateInstance(candidate.RequestType);
                if (target is null)
                {
                    continue;
                }

                foreach (var property in candidate.QueryProperties)
                {
                    property.SetValue(target, property.GetValue(parsed.Value));
                }

                invocationContext.Arguments[candidate.ArgumentIndex] = target;
            }

            return await next(invocationContext).ConfigureAwait(false);
        };
    }

    private static AsParametersQueryBindingCandidate[] GetAsParametersQueryBindingCandidates(ParameterInfo[] parameters)
    {
        List<AsParametersQueryBindingCandidate>? candidates = null;
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter.GetCustomAttribute<AsParametersAttribute>() is null)
            {
                continue;
            }

            var queryProperties = GetFromQueryProperties(parameter.ParameterType);
            if (queryProperties.Length == 0)
            {
                continue;
            }

            candidates ??= [];
            candidates.Add(new AsParametersQueryBindingCandidate(i, parameter.ParameterType, queryProperties));
        }

        return candidates?.ToArray() ?? [];
    }

    private static PropertyInfo[] GetFromQueryProperties(Type requestType)
    {
        return requestType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetCustomAttribute<FromQueryAttribute>() is not null &&
                property.GetMethod is not null &&
                property.SetMethod is not null &&
                property.GetIndexParameters().Length == 0)
            .ToArray();
    }

    private sealed record AsParametersQueryBindingCandidate(
        int ArgumentIndex,
        Type RequestType,
        PropertyInfo[] QueryProperties);

}
