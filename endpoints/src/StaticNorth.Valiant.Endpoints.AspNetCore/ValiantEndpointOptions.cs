using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace StaticNorth.Valiant.Endpoints.AspNetCore;

/// <summary>
/// Configures mapping policies and conventions for mapped Valiant endpoints.
/// </summary>
public sealed class ValiantEndpointOptions
{
    private readonly List<EndpointConfigurationRule> _endpointConfigurationRules = [];

    /// <summary>
    /// Gets or sets the predicate that determines whether source-generated endpoint groups should be mapped.
    /// </summary>
    /// <remarks>
    /// The predicate is evaluated while <c>MapValiantEndpoints</c> maps generated registrations during application startup.
    /// Returning <see langword="false" /> skips the group and its generated endpoints.
    /// </remarks>
    public Func<EndpointGroupRegistration, bool> MapEndpointGroupsWhen
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static _ => true;

    /// <summary>
    /// Gets or sets the predicate that determines whether source-generated endpoints should be mapped.
    /// </summary>
    /// <remarks>
    /// The predicate is evaluated for grouped and standalone endpoints while <c>MapValiantEndpoints</c> maps generated
    /// registrations during application startup. It is not evaluated for endpoints in a group rejected by
    /// <see cref="MapEndpointGroupsWhen" />.
    /// </remarks>
    public Func<EndpointRegistration, bool> MapEndpointWhen
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static _ => true;

    /// <summary>
    /// Adds conditional registration-level configuration for generated endpoints.
    /// </summary>
    /// <param name="predicate">Selects endpoint registrations to configure.</param>
    /// <param name="configure">Configures parsing options for each selected endpoint.</param>
    /// <remarks>
    /// Rule predicates observe the same initial pre-mapping registration snapshot. Matching configuration actions run
    /// in registration order before <see cref="MapEndpointWhen" /> is evaluated.
    /// </remarks>
    public void ConfigureEndpointWhen(
        Func<EndpointRegistration, bool> predicate,
        Action<EndpointConfigurationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(configure);
        _endpointConfigurationRules.Add(new EndpointConfigurationRule(predicate, configure));
    }

    internal EndpointRegistration CreateEndpointRegistration(
        Type endpointType,
        EndpointGroupRegistration? group,
        int order,
        bool isBindingGenerated,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
    {
        var configuration = new EndpointConfigurationOptions(QueryParameters.Parsing);
        var initialRegistration = CreateEndpointRegistration(
            endpointType,
            group,
            order,
            isBindingGenerated,
            configuration,
            supportedArrayFormats,
            supportedObjectFormats,
            enableJsonQueryValues);
        var matchingConfigurations = _endpointConfigurationRules
            .Where(rule => rule.Predicate(initialRegistration))
            .Select(static rule => rule.Configure)
            .ToArray();

        if (matchingConfigurations.Length > 0)
        {
            configuration.MarkConditional();
        }

        foreach (var configure in matchingConfigurations)
        {
            configure(configuration);
        }

        return CreateEndpointRegistration(
            endpointType,
            group,
            order,
            isBindingGenerated,
            configuration,
            supportedArrayFormats,
            supportedObjectFormats,
            enableJsonQueryValues);
    }

    /// <summary>
    /// Configures each endpoint group after it is created and before generated endpoints are mapped into it.
    /// </summary>
    public Action<RouteGroupBuilder, EndpointGroupRegistration> ConfigureEndpointGroup
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static (_, _) => { };

    /// <summary>
    /// Configures each mapped endpoint after Valiant conventions have been applied.
    /// </summary>
    public Action<IEndpointConventionBuilder, EndpointRegistration> ConfigureEndpoint
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = static (_, _) => { };

    /// <summary>
    /// Gets or sets configurable query parameter behavior for Valiant endpoint helpers.
    /// </summary>
    public QueryParameterOptions QueryParameters
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new();

    private EndpointRegistration CreateEndpointRegistration(
        Type endpointType,
        EndpointGroupRegistration? group,
        int order,
        bool isBindingGenerated,
        EndpointConfigurationOptions configuration,
        QueryArrayFormat[]? supportedArrayFormats,
        QueryObjectFormat[]? supportedObjectFormats,
        bool? enableJsonQueryValues)
    {
        var preMappingParsingOptions = QueryParsingOptionsSnapshot.ApplyOverrides(
            configuration.QueryParameters.Parsing,
            supportedArrayFormats,
            supportedObjectFormats,
            enableJsonQueryValues);
        var queryParameters = new EndpointQueryParameterRegistration(
            isBindingGenerated,
            isBindingGenerated && QueryParameters.EnableBinding,
            preMappingParsingOptions);
        return new EndpointRegistration(
            endpointType,
            group,
            order,
            queryParameters,
            configuration);
    }

    private sealed record EndpointConfigurationRule(
        Func<EndpointRegistration, bool> Predicate,
        Action<EndpointConfigurationOptions> Configure);
}
