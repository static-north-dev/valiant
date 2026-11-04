using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using StaticNorth.Valiant.Endpoints.AspNetCore.Infrastructure;

namespace StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;

/// <summary>
/// Provides dependency injection registration helpers for Valiant endpoints.
/// </summary>
public static class ValiantEndpointServiceCollectionExtensions
{
    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the calling and entry assemblies.
    /// </summary>
    /// <remarks>For Native AOT, use an overload that accepts endpoint assemblies explicitly.</remarks>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    [RequiresDynamicCode("Calling assembly discovery is unavailable in Native AOT. Use an AddValiantEndpoints overload with explicit assemblies.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services)
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var assemblies = GetDefaultRegistrationAssemblies(callingAssembly);

        return AddValiantEndpoints(services, assemblies);
    }

    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the calling and entry assemblies and configures endpoint options.
    /// </summary>
    /// <remarks>For Native AOT, use an overload that accepts endpoint assemblies explicitly.</remarks>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="setup">The endpoint setup action.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    [RequiresDynamicCode("Calling assembly discovery is unavailable in Native AOT. Use an AddValiantEndpoints overload with explicit assemblies.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services, Action<ValiantEndpointOptions> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var callingAssembly = Assembly.GetCallingAssembly();
        var assemblies = GetDefaultRegistrationAssemblies(callingAssembly);

        return AddValiantEndpoints(services, setup, assemblies);
    }

    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="assemblies">The assemblies containing generated endpoint route registrar markers.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services, Assembly[] assemblies)
    {
        return AddValiantEndpointsCore(services, assemblies);
    }

    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the specified assemblies.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="assembly">The first assembly containing generated endpoint route registrar markers.</param>
    /// <param name="additionalAssemblies">Additional assemblies containing generated endpoint route registrar markers.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services, Assembly assembly, params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        return AddValiantEndpointsCore(services, EnumerateAssemblies(assembly, additionalAssemblies));
    }

    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the specified assemblies and configures endpoint options.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="setup">The endpoint setup action.</param>
    /// <param name="assemblies">The assemblies containing generated endpoint route registrar markers.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services, Action<ValiantEndpointOptions> setup, Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setup);

        services.Configure(setup);
        return AddValiantEndpoints(services, assemblies);
    }

    /// <summary>
    /// Registers source-generated Valiant endpoint route registrars from the specified assemblies and configures endpoint options.
    /// </summary>
    /// <param name="services">The service collection to add endpoint registrations to.</param>
    /// <param name="setup">The endpoint setup action.</param>
    /// <param name="assembly">The first assembly containing generated endpoint route registrar markers.</param>
    /// <param name="additionalAssemblies">Additional assemblies containing generated endpoint route registrar markers.</param>
    /// <returns>The same service collection instance so calls can be chained.</returns>
    public static IServiceCollection AddValiantEndpoints(this IServiceCollection services, Action<ValiantEndpointOptions> setup, Assembly assembly, params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        services.Configure(setup);
        return AddValiantEndpointsCore(services, EnumerateAssemblies(assembly, additionalAssemblies));
    }

    private static IServiceCollection AddValiantEndpointsCore(IServiceCollection services, IEnumerable<Assembly?> assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        services.AddOptions<ValiantEndpointOptions>();
        var registrarTypes = new HashSet<Type>();
        foreach (var assembly in assemblies)
        {
            if (assembly is null)
            {
                continue;
            }

            foreach (var attribute in assembly.GetCustomAttributes<ValiantEndpointRouteRegistrarAttribute>())
            {
                if (registrarTypes.Add(attribute.RegistrarType))
                {
                    services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IValiantEndpointRouteRegistrar), attribute.RegistrarType));
                }
            }
        }

        return services;
    }

    private static Assembly[] GetDefaultRegistrationAssemblies(Assembly callingAssembly)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        return entryAssembly is null || ReferenceEquals(callingAssembly, entryAssembly)
            ? new[] { callingAssembly }
            : new[] { callingAssembly, entryAssembly };
    }

    private static IEnumerable<Assembly?> EnumerateAssemblies(Assembly assembly, IEnumerable<Assembly?> additionalAssemblies)
    {
        yield return assembly;

        foreach (var additionalAssembly in additionalAssemblies)
        {
            yield return additionalAssembly;
        }
    }
}
