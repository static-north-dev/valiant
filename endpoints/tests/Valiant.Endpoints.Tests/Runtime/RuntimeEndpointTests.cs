using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Valiant.Endpoints.AspNetCore;
using Valiant.Endpoints.AspNetCore.DependencyInjection;
using Valiant.Endpoints.AspNetCore.Infrastructure;

namespace Valiant.Endpoints.Tests.Runtime;

public sealed partial class RuntimeEndpointTests
{
    [Test]
    public async Task AddValiantEndpoints_With_No_Registrar_Assembly_Is_Available_And_NoOps()
    {
        var services = new ServiceCollection();

        services.AddValiantEndpoints(Array.Empty<Assembly>());

        var provider = services.BuildServiceProvider();
        var registrars = provider.GetServices<IValiantEndpointRouteRegistrar>();

        await Assert.That(registrars).IsEmpty();
    }

    [Test]
    public async Task AddValiantEndpoints_With_Setup_Configures_Options()
    {
        var services = new ServiceCollection();
        RouteGroupBuilder? configuredGroup = null;
        EndpointGroupRegistration? configuredGroupRegistration = null;
        IEndpointConventionBuilder? configuredEndpoint = null;
        EndpointRegistration? configuredEndpointRegistration = null;

        services.AddValiantEndpoints(options =>
        {
            options.ConfigureEndpointGroup = (group, registration) =>
            {
                configuredGroup = group;
                configuredGroupRegistration = registration;
            };
            options.ConfigureEndpoint = (endpoint, registration) =>
            {
                configuredEndpoint = endpoint;
                configuredEndpointRegistration = registration;
            };
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ValiantEndpointOptions>>().Value;
        var routeBuilder = new TestEndpointRouteBuilder(provider);
        var group = ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(
            routeBuilder);
        var endpoint = ValiantEndpointGeneratedCode.MapEndpoint<DefaultRuntimeEndpoint>(
            routeBuilder);

        await Assert.That(configuredGroup).IsSameReferenceAs(group);
        await Assert.That(configuredGroupRegistration!.GroupType)
            .IsEqualTo(typeof(DefaultRuntimeEndpointGroup));
        await Assert.That(configuredEndpoint).IsSameReferenceAs(endpoint);
        await Assert.That(configuredEndpointRegistration!.EndpointType)
            .IsEqualTo(typeof(DefaultRuntimeEndpoint));
        await Assert.That(options.QueryParameters.EnableBinding).IsFalse();
    }

    [Test]
    public async Task AddValiantEndpoints_With_Setup_And_Assembly_Configures_Options()
    {
        var services = new ServiceCollection();
        EndpointGroupRegistration? configuredGroup = null;

        services.AddValiantEndpoints(
            options =>
            {
                options.ConfigureEndpointGroup = (_, registration) =>
                    configuredGroup = registration;
            },
            typeof(RuntimeEndpointTests).Assembly);

        var routeBuilder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(routeBuilder);

        await Assert.That(configuredGroup!.GroupType)
            .IsEqualTo(typeof(DefaultRuntimeEndpointGroup));
    }

    [Test]
    public async Task MapValiantEndpoints_With_No_Registered_Registrars_NoOps()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(services);

        var result = builder.MapValiantEndpoints();

        await Assert.That(result).IsSameReferenceAs(builder);
        await Assert.That(builder.DataSources).IsEmpty();
    }

    [Test]
    public async Task Mapping_Policies_Default_To_Allow()
    {
        var services = new ServiceCollection();
        services.AddValiantEndpoints();

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());

        await Assert.That(ValiantEndpointGeneratedCode.ShouldMapEndpoint<DefaultRuntimeEndpoint>(builder)).IsTrue();
        await Assert.That(ValiantEndpointGeneratedCode.ShouldMapEndpointGroup<DefaultRuntimeEndpointGroup>(builder)).IsTrue();
    }

    [Test]
    public async Task Mapping_Policies_Are_Independent_And_The_Last_Configuration_Wins()
    {
        var services = new ServiceCollection();
        services.AddValiantEndpoints(options =>
        {
            options.MapEndpointGroupsWhen = static _ => false;
            options.MapEndpointGroupsWhen = static _ => true;
            options.MapEndpointWhen = static _ => true;
            options.MapEndpointWhen = registration =>
                registration.EndpointType != typeof(DefaultRuntimeEndpoint);
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());

        await Assert.That(ValiantEndpointGeneratedCode.ShouldMapEndpoint<DefaultRuntimeEndpoint>(builder)).IsFalse();
        await Assert.That(ValiantEndpointGeneratedCode.ShouldMapEndpointGroup<DefaultRuntimeEndpointGroup>(builder)).IsTrue();
        await Assert.That(ValiantEndpointGeneratedCode.ShouldMap<DefaultRuntimeEndpoint>(builder)).IsFalse();
        await Assert.That(ValiantEndpointGeneratedCode.ShouldMap<DefaultRuntimeEndpointGroup>(builder)).IsTrue();
    }

    [Test]
    public async Task Legacy_Generated_Bridge_Recovers_Group_Membership()
    {
        EndpointRegistration? observed = null;
        var services = new ServiceCollection();
        services.AddValiantEndpoints(options =>
        {
            options.MapEndpointWhen = registration =>
            {
                observed = registration;
                return true;
            };
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var group = ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(
            builder);

        await Assert.That(
                ValiantEndpointGeneratedCode.ShouldMapEndpoint<IncludedRuntimeGroupedEndpoint>(
                    group))
            .IsTrue();
        await Assert.That(observed).IsNotNull();
        await Assert.That(observed!.Group).IsNotNull();
        await Assert.That(observed.Group!.GroupType)
            .IsEqualTo(typeof(DefaultRuntimeEndpointGroup));
        await Assert.That(observed.Group.Order).IsEqualTo(0);
    }

    [Test]
    public async Task Mapping_Policies_And_Configuration_Hooks_Reject_Null_Delegates()
    {
        var options = new ValiantEndpointOptions();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.MapEndpointGroupsWhen = null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.MapEndpointWhen = null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.ConfigureEndpointGroup = null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.ConfigureEndpoint = null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.ConfigureEndpointWhen(null!, static _ => { })));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.Run(() => options.ConfigureEndpointWhen(static _ => true, null!)));
    }

    [Test]
    public async Task MapValiantEndpoints_Skips_An_Excluded_Standalone_Endpoint()
    {
        var services = new ServiceCollection();
        services.AddValiantEndpoints(
            options => options.MapEndpointWhen = registration =>
                registration.EndpointType != typeof(DefaultRuntimeEndpoint),
            typeof(RuntimeEndpointTests).Assembly);

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        builder.MapValiantEndpoints();

        await Assert.That(GetRoutePatterns(builder)).DoesNotContain("/items");
    }

    [Test]
    public async Task MapValiantEndpoints_Skips_An_Excluded_Group_And_Its_Endpoints()
    {
        var services = new ServiceCollection();
        var configuredGroups = 0;
        var evaluatedGroups = new List<EndpointGroupRegistration>();
        var evaluatedEndpoints = new List<EndpointRegistration>();
        services.AddValiantEndpoints(
            options =>
            {
                options.MapEndpointGroupsWhen = registration =>
                {
                    evaluatedGroups.Add(registration);
                    return registration.GroupType != typeof(DefaultRuntimeEndpointGroup);
                };
                options.MapEndpointWhen = registration =>
                {
                    evaluatedEndpoints.Add(registration);
                    return true;
                };
                options.ConfigureEndpointGroup = (_, registration) =>
                {
                    if (registration.GroupType == typeof(DefaultRuntimeEndpointGroup))
                    {
                        configuredGroups++;
                    }
                };
            },
            typeof(RuntimeEndpointTests).Assembly);

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        builder.MapValiantEndpoints();

        await Assert.That(GetRoutePatterns(builder)).DoesNotContain("/DefaultRuntime/included");
        await Assert.That(GetRoutePatterns(builder)).DoesNotContain("/DefaultRuntime/excluded");
        await Assert.That(configuredGroups).IsEqualTo(0);
        await Assert.That(evaluatedGroups.Select(static group => group.GroupType))
            .Contains(typeof(DefaultRuntimeEndpointGroup));
        await Assert.That(evaluatedGroups.Select(static group => group.GroupType))
            .DoesNotContain(typeof(IncludedRuntimeGroupedEndpoint));
        await Assert.That(evaluatedEndpoints.Select(static endpoint => endpoint.EndpointType))
            .DoesNotContain(typeof(IncludedRuntimeGroupedEndpoint));
        await Assert.That(evaluatedEndpoints.Select(static endpoint => endpoint.EndpointType))
            .DoesNotContain(typeof(ExcludedRuntimeGroupedEndpoint));
    }

    [Test]
    public async Task MapValiantEndpoints_Skips_Only_The_Excluded_Endpoint_In_An_Enabled_Group()
    {
        var services = new ServiceCollection();
        var configuredGroups = 0;
        var configuredEndpoints = new List<Type>();
        services.AddValiantEndpoints(
            options =>
            {
                options.MapEndpointWhen = registration =>
                    registration.EndpointType != typeof(ExcludedRuntimeGroupedEndpoint);
                options.ConfigureEndpointGroup = (_, registration) =>
                {
                    if (registration.GroupType == typeof(DefaultRuntimeEndpointGroup))
                    {
                        configuredGroups++;
                    }
                };
                options.ConfigureEndpoint = (_, registration) =>
                    configuredEndpoints.Add(registration.EndpointType);
            },
            typeof(RuntimeEndpointTests).Assembly);

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        builder.MapValiantEndpoints();

        await Assert.That(GetRoutePatterns(builder)).Contains("/DefaultRuntime/included");
        await Assert.That(GetRoutePatterns(builder)).DoesNotContain("/DefaultRuntime/excluded");
        await Assert.That(configuredGroups).IsEqualTo(1);
        await Assert.That(configuredEndpoints)
            .Contains(typeof(IncludedRuntimeGroupedEndpoint));
        await Assert.That(configuredEndpoints)
            .DoesNotContain(typeof(ExcludedRuntimeGroupedEndpoint));
    }

    [Test]
    public async Task Generated_Endpoint_Registration_Exposes_Group_Order_And_Query_Metadata()
    {
        var services = new ServiceCollection();
        EndpointGroupRegistration? evaluatedGroup = null;
        EndpointGroupRegistration? configuredGroup = null;
        EndpointRegistration? observed = null;
        EndpointRegistration? configuredEndpoint = null;
        services.AddValiantEndpoints(
            options =>
            {
                options.MapEndpointGroupsWhen = registration =>
                {
                    if (registration.GroupType == typeof(DefaultRuntimeEndpointGroup))
                    {
                        evaluatedGroup = registration;
                    }

                    return true;
                };
                options.ConfigureEndpointGroup = (_, registration) =>
                {
                    if (registration.GroupType == typeof(DefaultRuntimeEndpointGroup))
                    {
                        configuredGroup = registration;
                    }
                };
                options.MapEndpointWhen = registration =>
                {
                    if (registration.EndpointType == typeof(IncludedRuntimeGroupedEndpoint))
                    {
                        observed = registration;
                    }

                    return true;
                };
                options.ConfigureEndpoint = (_, registration) =>
                {
                    if (registration.EndpointType == typeof(IncludedRuntimeGroupedEndpoint))
                    {
                        configuredEndpoint = registration;
                    }
                };
            },
            typeof(RuntimeEndpointTests).Assembly);

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        builder.MapValiantEndpoints();

        await Assert.That(observed).IsNotNull();
        await Assert.That(evaluatedGroup).IsSameReferenceAs(configuredGroup);
        await Assert.That(observed).IsSameReferenceAs(configuredEndpoint);
        await Assert.That(observed!.Group).IsSameReferenceAs(configuredGroup);
        await Assert.That(observed.EndpointType).IsEqualTo(typeof(IncludedRuntimeGroupedEndpoint));
        await Assert.That(observed.IsGrouped).IsTrue();
        await Assert.That(observed.Group!.GroupType).IsEqualTo(typeof(DefaultRuntimeEndpointGroup));
        await Assert.That(observed.Order).IsEqualTo(0);
        await Assert.That(observed.Group.Order).IsEqualTo(0);
        await Assert.That(observed.QueryParameters.IsBindingGenerated).IsTrue();
        await Assert.That(observed.QueryParameters.IsRuntimeBindingEnabled).IsFalse();

        var firstSnapshot = observed.QueryParameters.PreMappingParsingOptions;
        firstSnapshot.SupportedArrayFormats[0] = QueryArrayFormat.CommaSeparated;
        await Assert.That(observed.QueryParameters.PreMappingParsingOptions.SupportedArrayFormats)
            .IsEquivalentTo([QueryArrayFormat.RepeatedKeys]);
    }

    [Test]
    public async Task Conditional_Endpoint_Configuration_Is_Ordered_And_Attribute_Overrides_Win()
    {
        var predicateFormats = new List<QueryArrayFormat[]>();
        var services = new ServiceCollection();
        services.AddValiantEndpoints(options =>
        {
            options.QueryParameters.EnableBinding = true;
            options.ConfigureEndpointWhen(
                registration =>
                {
                    predicateFormats.Add(
                        registration.QueryParameters.PreMappingParsingOptions.SupportedArrayFormats);
                    return true;
                },
                endpointOptions => endpointOptions.QueryParameters.Parsing = new QueryParsingOptions
                {
                    SupportedArrayFormats = [QueryArrayFormat.CommaSeparated]
                });
            options.ConfigureEndpointWhen(
                registration =>
                {
                    predicateFormats.Add(
                        registration.QueryParameters.PreMappingParsingOptions.SupportedArrayFormats);
                    return true;
                },
                endpointOptions => endpointOptions.QueryParameters.Parsing = new QueryParsingOptions
                {
                    SupportedArrayFormats = [QueryArrayFormat.IndexedBrackets]
                });
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var registration = ValiantEndpointGeneratedCode.CreateEndpointRegistration<DefaultRuntimeEndpoint>(
            builder,
            group: null,
            order: 42,
            isBindingGenerated: true,
            supportedArrayFormats: [QueryArrayFormat.Brackets],
            supportedObjectFormats: null,
            enableJsonQueryValues: null);

        await Assert.That(predicateFormats.Count).IsEqualTo(2);
        await Assert.That(predicateFormats[0]).IsEquivalentTo([QueryArrayFormat.Brackets]);
        await Assert.That(predicateFormats[1]).IsEquivalentTo([QueryArrayFormat.Brackets]);
        await Assert.That(registration.Order).IsEqualTo(42);
        await Assert.That(registration.QueryParameters.IsBindingGenerated).IsTrue();
        await Assert.That(registration.QueryParameters.IsRuntimeBindingEnabled).IsTrue();
        await Assert.That(registration.QueryParameters.PreMappingParsingOptions.SupportedArrayFormats)
            .IsEquivalentTo([QueryArrayFormat.Brackets]);

        var registrationWithoutAttributeOverride =
            ValiantEndpointGeneratedCode.CreateEndpointRegistration<DefaultRuntimeEndpoint>(
                builder,
                group: null,
                order: 43,
                isBindingGenerated: true,
                supportedArrayFormats: null,
                supportedObjectFormats: null,
                enableJsonQueryValues: null);
        await Assert.That(
                registrationWithoutAttributeOverride.QueryParameters
                    .PreMappingParsingOptions.SupportedArrayFormats)
            .IsEquivalentTo([QueryArrayFormat.IndexedBrackets]);
    }

    [Test]
    public async Task MapEndpointGroup_Uses_The_Group_CreateGroup_Route()
    {
        var services = new ServiceCollection();
        services.AddValiantEndpoints();

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var group = ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(builder);

        group.MapGet("/items", () => Results.Ok());

        await Assert.That(group).IsNotNull();
        await Assert.That(GetSingleRoutePattern(builder)).IsEqualTo("/DefaultRuntime/items");
    }

    [Test]
    public async Task MapEndpointGroup_Configures_The_Returned_Group_After_Associating_Registration()
    {
        var services = new ServiceCollection();
        RouteGroupBuilder? configuredGroup = null;
        EndpointGroupRegistration? configuredRegistration = null;
        EndpointRegistration? endpointRegistrationObservedInsideHook = null;
        services.AddValiantEndpoints(options =>
        {
            options.MapEndpointWhen = registration =>
            {
                endpointRegistrationObservedInsideHook = registration;
                return true;
            };
            options.ConfigureEndpointGroup = (group, registration) =>
            {
                configuredGroup = group;
                configuredRegistration = registration;
                ValiantEndpointGeneratedCode
                    .ShouldMapEndpoint<IncludedRuntimeGroupedEndpoint>(group);
            };
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var group = ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(builder);

        await Assert.That(configuredGroup).IsSameReferenceAs(group);
        await Assert.That(configuredRegistration!.GroupType)
            .IsEqualTo(typeof(DefaultRuntimeEndpointGroup));
        await Assert.That(endpointRegistrationObservedInsideHook!.Group)
            .IsSameReferenceAs(configuredRegistration);
    }

    [Test]
    public async Task MapEndpoint_Applies_Endpoint_Behaviors()
    {
        var services = new ServiceCollection();
        IEndpointConventionBuilder? configuredEndpoint = null;
        EndpointRegistration? configuredRegistration = null;
        services.AddValiantEndpoints(options =>
        {
            options.ConfigureEndpoint = (endpoint, registration) =>
            {
                configuredEndpoint = endpoint;
                configuredRegistration = registration;
            };
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var endpoint = ValiantEndpointGeneratedCode.MapEndpoint<DefaultRuntimeEndpoint>(builder);

        await Assert.That(configuredEndpoint).IsSameReferenceAs(endpoint);
        await Assert.That(configuredRegistration!.EndpointType)
            .IsEqualTo(typeof(DefaultRuntimeEndpoint));
        await Assert.That(configuredRegistration.Group).IsNull();
    }

    [Test]
    public async Task MapEndpoint_AsParameters_Query_Filter_Uses_Configured_Full_Query_Parsing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValiantEndpoints(options =>
        {
            options.QueryParameters.EnableBinding = true;
            options.QueryParameters.Parsing = new QueryParsingOptions
            {
                SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
                SupportedObjectFormats = [QueryObjectFormat.DotNotation]
            };
        });

        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<AsParametersQueryEndpoint>(builder);

        var endpoint = builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();
        var responseBody = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response =
            {
                Body = responseBody
            }
        };
        context.Request.QueryString = new QueryString("?filter.id=2");

        await endpoint.RequestDelegate!(context);

        responseBody.Position = 0;
        var body = await new StreamReader(responseBody).ReadToEndAsync();

        await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(body).IsEqualTo("2");
    }

    [Test]
    public async Task MapEndpoint_Leaves_AsParameters_Query_Binding_Native_By_Default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValiantEndpoints();

        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<AsParametersQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query", "?filter.id=2");

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(response.Body).IsEmpty();
    }

    [Test]
    public async Task Endpoint_Query_Parsing_Options_Replace_Global_Options_For_AsParameters_Binding()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<AsParametersLocalJsonQueryEndpoint>(builder);
        ValiantEndpointGeneratedCode.MapEndpoint<AsParametersGlobalQueryEndpoint>(builder);

        var localJson = await InvokeEndpoint(builder, provider, "/query/as-parameters/local", JsonFilterQuery);
        var localDotNotation = await InvokeEndpoint(builder, provider, "/query/as-parameters/local", "?filter.id=2");
        var globalJson = await InvokeEndpoint(builder, provider, "/query/as-parameters/global", JsonFilterQuery);
        var globalDotNotation = await InvokeEndpoint(builder, provider, "/query/as-parameters/global", "?filter.id=2");

        await Assert.That(localJson.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(localJson.Body).IsEqualTo("2");
        await Assert.That(localDotNotation.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(globalJson.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(globalDotNotation.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(globalDotNotation.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Endpoint_Query_Parsing_Options_Replace_Global_Options_For_Generated_BindAsync()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<GeneratedLocalJsonQueryEndpoint>(builder);
        ValiantEndpointGeneratedCode.MapEndpoint<GeneratedGlobalQueryEndpoint>(builder);

        var localJson = await InvokeEndpoint(builder, provider, "/query/generated/local", JsonFilterQuery);
        var localDotNotation = await InvokeEndpoint(builder, provider, "/query/generated/local", "?filter.id=2");
        var globalJson = await InvokeEndpoint(builder, provider, "/query/generated/global", JsonFilterQuery);
        var globalDotNotation = await InvokeEndpoint(builder, provider, "/query/generated/global", "?filter.id=2");

        await Assert.That(localJson.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(localJson.Body).IsEqualTo("2");
        await Assert.That(localDotNotation.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(globalJson.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(globalDotNotation.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(globalDotNotation.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Attribute_Query_Parsing_Overrides_Endpoint_Object_Format_For_AsParameters_Binding()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        var endpoint = ValiantEndpointGeneratedCode.MapEndpoint<AsParametersLocalJsonQueryEndpoint>(builder);
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            endpoint,
            supportedArrayFormats: null,
            supportedObjectFormats: [QueryObjectFormat.DotNotation],
            enableJsonQueryValues: null);

        var overriddenDotNotation = await InvokeEndpoint(
            builder,
            provider,
            "/query/as-parameters/local",
            "?filter.id=2");

        await Assert.That(overriddenDotNotation.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(overriddenDotNotation.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Attribute_Query_Parsing_Overrides_Endpoint_Object_Format_For_Generated_BindAsync()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        var endpoint = ValiantEndpointGeneratedCode.MapEndpoint<GeneratedLocalJsonQueryEndpoint>(builder);
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            endpoint,
            supportedArrayFormats: null,
            supportedObjectFormats: [QueryObjectFormat.DotNotation],
            enableJsonQueryValues: null);

        var overriddenDotNotation = await InvokeEndpoint(
            builder,
            provider,
            "/query/generated/local",
            "?filter.id=2");

        await Assert.That(overriddenDotNotation.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(overriddenDotNotation.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Attribute_Json_Query_Parsing_Override_Applies_To_Full_Query_Binding()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        var endpoint = ValiantEndpointGeneratedCode.MapEndpoint<GeneratedLocalJsonQueryEndpoint>(builder);
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            endpoint,
            supportedArrayFormats: null,
            supportedObjectFormats: null,
            enableJsonQueryValues: false);

        var response = await InvokeEndpoint(builder, provider, "/query/generated/local", JsonFilterQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task Attribute_Query_Parsing_Overrides_Are_Partial_And_Mutation_Isolated()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        QueryArrayFormat[] localArrayFormats = [QueryArrayFormat.RepeatedKeys];
        QueryObjectFormat[] localObjectFormats = [QueryObjectFormat.Flat];
        var localOptions = new QueryParsingOptions
        {
            SupportedArrayFormats = localArrayFormats,
            SupportedObjectFormats = localObjectFormats,
            EnableJsonQueryValues = true
        };
        QueryArrayFormat[] attributeArrayFormats =
        [
            QueryArrayFormat.CommaSeparated,
            QueryArrayFormat.RepeatedKeys
        ];
        var endpoint = builder
            .MapGet("/query/options/partial", static () => Results.Ok())
            .WithQueryParsingOptions(localOptions);
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            endpoint,
            attributeArrayFormats,
            supportedObjectFormats: null,
            enableJsonQueryValues: null);
        attributeArrayFormats[0] = QueryArrayFormat.Brackets;

        var routeEndpoint = builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .Single();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.SetEndpoint(routeEndpoint);
        var resolved = ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(context);

        await Assert.That(resolved.SupportedArrayFormats.SequenceEqual([
            QueryArrayFormat.CommaSeparated,
            QueryArrayFormat.RepeatedKeys
        ])).IsTrue();
        await Assert.That(resolved.SupportedObjectFormats.SequenceEqual([QueryObjectFormat.Flat])).IsTrue();
        await Assert.That(resolved.EnableJsonQueryValues).IsTrue();

        resolved.SupportedArrayFormats[0] = QueryArrayFormat.Brackets;
        resolved.SupportedObjectFormats[0] = QueryObjectFormat.DotNotation;

        await Assert.That(attributeArrayFormats[0]).IsEqualTo(QueryArrayFormat.Brackets);
        await Assert.That(localArrayFormats[0]).IsEqualTo(QueryArrayFormat.RepeatedKeys);
        await Assert.That(localObjectFormats[0]).IsEqualTo(QueryObjectFormat.Flat);
    }

    [Test]
    public async Task Attribute_Query_Parsing_Empty_And_Invalid_Overrides_Reach_Parser_Unchanged()
    {
        var services = CreateEndpointQueryParsingServices();
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        var emptyEndpoint = builder.MapGet("/query/options/empty", static () => Results.Ok());
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            emptyEndpoint,
            supportedArrayFormats: [],
            supportedObjectFormats: [],
            enableJsonQueryValues: null);
        var invalidEndpoint = builder.MapGet("/query/options/invalid", static () => Results.Ok());
        ValiantEndpointGeneratedCode.ApplyQueryParsingOverrides(
            invalidEndpoint,
            supportedArrayFormats: [(QueryArrayFormat)999],
            supportedObjectFormats: null,
            enableJsonQueryValues: null);
        var routeEndpoints = builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(static endpoint => endpoint.RoutePattern.RawText!);

        var emptyContext = new DefaultHttpContext { RequestServices = provider };
        emptyContext.SetEndpoint(routeEndpoints["/query/options/empty"]);
        var emptyOptions = ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(emptyContext);
        var invalidContext = new DefaultHttpContext { RequestServices = provider };
        invalidContext.SetEndpoint(routeEndpoints["/query/options/invalid"]);
        var invalidOptions = ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(invalidContext);
        var parseResult = QueryStringParser.Parse<AsParametersQueryRequest>(
            "?filter.id=2",
            invalidOptions);

        await Assert.That(emptyOptions.SupportedArrayFormats).IsEmpty();
        await Assert.That(emptyOptions.SupportedObjectFormats).IsEmpty();
        await Assert.That(invalidOptions.SupportedArrayFormats.Single())
            .IsEqualTo((QueryArrayFormat)999);
        await Assert.That(parseResult.IsSuccess).IsFalse();
        await Assert.That(parseResult.Errors.Single().Code).IsEqualTo("InvalidQueryParsingOptions");
        await Assert.That(parseResult.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Configuration);
    }

    [Test]
    public async Task Endpoint_Attribute_Allows_Direct_FromQuery_Json_When_Enabled()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: false);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<DirectJsonAllowedQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query/direct/allowed", DirectJsonQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(response.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Endpoint_Attribute_Rejects_Direct_FromQuery_Json_When_Disabled()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: true);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<DirectJsonDisabledQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query/direct/disabled", DirectWhitespaceJsonQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(response.Body).IsEmpty();
    }

    [Test]
    public async Task Direct_FromQuery_Json_Remains_Enabled_When_Endpoint_Attribute_Is_Absent()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: false);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<DirectJsonGlobalQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query/direct/global", DirectJsonQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(response.Body).IsEqualTo("2");
    }

    [Test]
    public async Task Direct_FromQuery_Json_Enforcement_Checks_All_Repeated_Collection_Values()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: false);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<DirectJsonCollectionQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query/direct/collection", DirectRepeatedJsonQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(response.Body).IsEmpty();
    }

    [Test]
    public async Task Direct_FromQuery_Json_Enforcement_Allows_Missing_Optional_Value_And_Preserves_Framework_Invalid_Value_Response()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: false);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<DirectJsonOptionalQueryEndpoint>(builder);

        var missing = await InvokeEndpoint(builder, provider, "/query/direct/optional", string.Empty);
        var invalid = await InvokeEndpoint(builder, provider, "/query/direct/optional", "?payload=not-json");

        await Assert.That(missing.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(missing.Body).IsEqualTo("-1");
        await Assert.That(invalid.StatusCode).IsEqualTo(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task Direct_FromQuery_Json_Enforcement_Ignores_User_TryParse_Generic_Wrappers()
    {
        var services = CreateEndpointQueryParsingServices(enableJson: false);
        var provider = services.BuildServiceProvider();
        var builder = new TestEndpointRouteBuilder(provider);
        ValiantEndpointGeneratedCode.MapEndpoint<UserTryParseWrapperQueryEndpoint>(builder);

        var response = await InvokeEndpoint(builder, provider, "/query/direct/user-parser", DirectJsonQuery);

        await Assert.That(response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(response.Body).IsEqualTo("\"accepted\"");
    }

    [Test]
    public async Task WithQueryParsingOptions_Uses_Last_Options_And_Validates_Arguments()
    {
        var builder = new TestEndpointRouteBuilder(new ServiceCollection().BuildServiceProvider());
        var first = new QueryParsingOptions { EnableJsonQueryValues = true };
        var last = new QueryParsingOptions { EnableJsonQueryValues = false };
        var convention = builder.MapGet("/query-options", () => Results.Ok());

        var result = convention
            .WithQueryParsingOptions(first)
            .WithQueryParsingOptions(last);
        var endpoint = builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .Single();

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.SetEndpoint(endpoint);

        await Assert.That(result).IsSameReferenceAs(convention);
        await Assert.That(ValiantEndpointGeneratedCode.ResolveQueryParsingOptions(context))
            .IsSameReferenceAs(last);
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(ValiantEndpointRouteBuilderExtensions.WithQueryParsingOptions(null!, first)));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(convention.WithQueryParsingOptions(null!)));
    }

    [Test]
    public async Task Legacy_Post_Group_Bridge_Is_A_NoOp_And_Returns_The_Same_Group()
    {
        var services = new ServiceCollection();
        var configurationCount = 0;
        services.AddValiantEndpoints(options =>
        {
            options.ConfigureEndpointGroup = (_, _) => configurationCount++;
        });

        var builder = new TestEndpointRouteBuilder(services.BuildServiceProvider());
        var group = ValiantEndpointGeneratedCode.MapEndpointGroup<DefaultRuntimeEndpointGroup>(builder);

        var result =
            ValiantEndpointGeneratedCode
                .ConfigureEndpointGroupAfterMapping<DefaultRuntimeEndpointGroup>(group);

        await Assert.That(result).IsSameReferenceAs(group);
        await Assert.That(configurationCount).IsEqualTo(1);
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder()
        {
            throw new NotSupportedException();
        }
    }

    private static string? GetSingleRoutePattern(IEndpointRouteBuilder builder)
    {
        return builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single()
            .RoutePattern
            .RawText;
    }

    private static string?[] GetRoutePatterns(IEndpointRouteBuilder builder)
    {
        return builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
    }

    private const string JsonFilterQuery = "?filter=%7B%22id%22%3A2%7D";
    private const string DirectJsonQuery = "?payload=%7B%22id%22%3A2%7D";
    private const string DirectWhitespaceJsonQuery = "?payload=%20%20%7B%22id%22%3A2%7D";
    private const string DirectRepeatedJsonQuery = "?payload=%7B%22id%22%3A1%7D&payload=%7B%22id%22%3A2%7D";

    private static ServiceCollection CreateEndpointQueryParsingServices(bool enableJson = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddValiantEndpoints(options =>
        {
            options.QueryParameters.EnableBinding = true;
            options.QueryParameters.Parsing = new QueryParsingOptions
            {
                SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
                SupportedObjectFormats = [QueryObjectFormat.DotNotation],
                EnableJsonQueryValues = enableJson
            };
        });
        return services;
    }

    private static async Task<(int StatusCode, string Body)> InvokeEndpoint(
        IEndpointRouteBuilder builder,
        IServiceProvider provider,
        string routePattern,
        string queryString)
    {
        var endpoint = builder.DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText == routePattern);
        var responseBody = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            Response =
            {
                Body = responseBody
            }
        };
        context.Request.QueryString = new QueryString(queryString);
        context.SetEndpoint(endpoint);

        try
        {
            await endpoint.RequestDelegate!(context);
        }
        catch (BadHttpRequestException exception)
        {
            context.Response.StatusCode = exception.StatusCode;
        }

        responseBody.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(responseBody).ReadToEndAsync());
    }

    [ValiantEndpointGroup]
    public sealed class DefaultRuntimeEndpointGroup : IEndpointGroup
    {
        public static RouteGroupBuilder CreateGroup(IEndpointRouteBuilder builder) =>
            builder.MapGroup("/DefaultRuntime");
    }

    [ValiantEndpoint]
    public sealed class DefaultRuntimeEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("/items", () => Results.Ok());
        }
    }

    [ValiantEndpoint<DefaultRuntimeEndpointGroup>]
    public sealed class IncludedRuntimeGroupedEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("/included", () => Results.Ok());
        }
    }

    [ValiantEndpoint<DefaultRuntimeEndpointGroup>]
    public sealed class ExcludedRuntimeGroupedEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("/excluded", () => Results.Ok());
        }
    }

    [ValiantEndpoint]
    public sealed class AsParametersQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("/query", ([AsParameters] AsParametersQueryRequest request) =>
                Results.Ok(request.Filter?.Id));
        }
    }

    public sealed record AsParametersQueryRequest
    {
        [FromQuery]
        public AsParametersQueryFilter? Filter { get; init; }
    }

    public sealed partial record AsParametersQueryFilter
    {
        public int Id { get; init; }
    }

    [ValiantEndpoint]
    public sealed class AsParametersLocalJsonQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                    "/query/as-parameters/local",
                    ([AsParameters] AsParametersQueryRequest request) => Results.Ok(request.Filter?.Id))
                .WithQueryParsingOptions(LocalJsonQueryParsingOptions());
        }
    }

    [ValiantEndpoint]
    public sealed class AsParametersGlobalQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/as-parameters/global",
                ([AsParameters] AsParametersQueryRequest request) => Results.Ok(request.Filter?.Id));
        }
    }

    [ValiantEndpoint]
    public sealed class GeneratedLocalJsonQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                    "/query/generated/local",
                    (GeneratedQueryRequest? request) => request is null
                        ? (IResult)Results.BadRequest()
                        : Results.Ok(request.Filter?.Id))
                .WithQueryParsingOptions(LocalJsonQueryParsingOptions());
        }
    }

    [ValiantEndpoint]
    public sealed class GeneratedGlobalQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/generated/global",
                (GeneratedQueryRequest? request) => request is null
                    ? (IResult)Results.BadRequest()
                    : Results.Ok(request.Filter?.Id));
        }
    }

    public sealed partial record GeneratedQueryRequest
    {
        [FromQuery]
        public AsParametersQueryFilter? Filter { get; init; }
    }

    [ValiantEndpoint]
    [ValiantQueryParsing(JsonValues = JsonQueryValues.Enabled)]
    public sealed class DirectJsonAllowedQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/allowed",
                ([FromQuery(Name = "payload")] DirectJsonQueryRequest request) => Results.Ok(request.Id));
        }
    }

    [ValiantEndpoint]
    [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
    public sealed class DirectJsonDisabledQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/disabled",
                ([FromQuery(Name = "payload")] DirectJsonDisabledQueryRequest request) => Results.Ok(request.Id));
        }
    }

    [ValiantEndpoint]
    public sealed class DirectJsonGlobalQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/global",
                ([FromQuery(Name = "payload")] DirectJsonQueryRequest request) => Results.Ok(request.Id));
        }
    }

    [ValiantEndpoint]
    [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
    public sealed class DirectJsonCollectionQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/collection",
                ([FromQuery(Name = "payload")] DirectJsonDisabledQueryRequest[] request) => Results.Ok(request.Length));
        }
    }

    [ValiantEndpoint]
    [ValiantQueryParsing(JsonValues = JsonQueryValues.Disabled)]
    public sealed class DirectJsonOptionalQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/optional",
                ([FromQuery(Name = "payload")] DirectJsonDisabledQueryRequest? request) => Results.Ok(request?.Id ?? -1));
        }
    }

    [ValiantEndpoint]
    public sealed class UserTryParseWrapperQueryEndpoint : IEndpoint
    {
        public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(
                "/query/direct/user-parser",
                ([FromQuery(Name = "payload")] UserTryParseWrapper<DirectJsonQueryRequest> request) => Results.Ok(request.Value));
        }
    }

    public sealed partial record DirectJsonQueryRequest
    {
        public int Id { get; init; }
    }

    public sealed partial record DirectJsonDisabledQueryRequest
    {
        public int Id { get; init; }
    }

    public sealed record UserTryParseWrapper<T>(string Value)
    {
        public static bool TryParse(string value, out UserTryParseWrapper<T>? result)
        {
            result = new UserTryParseWrapper<T>("accepted");
            return true;
        }
    }

    private static QueryParsingOptions LocalJsonQueryParsingOptions()
    {
        return new QueryParsingOptions
        {
            SupportedArrayFormats = [QueryArrayFormat.RepeatedKeys],
            SupportedObjectFormats = [QueryObjectFormat.Flat],
            EnableJsonQueryValues = true
        };
    }
}
