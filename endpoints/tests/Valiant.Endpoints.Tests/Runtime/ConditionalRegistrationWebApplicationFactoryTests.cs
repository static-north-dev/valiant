using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core.Interfaces;
using Valiant.Endpoints.AspNetCore;
using Valiant.Endpoints.AspNetCore.TestApp;

namespace Valiant.Endpoints.Tests.Runtime;

public abstract class ConditionalRegistrationApplicationFactory :
    WebApplicationFactory<TestApplicationMarker>,
    IAsyncInitializer
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
            services.PostConfigure<ValiantEndpointOptions>(ConfigureEndpoints));
    }

    protected abstract void ConfigureEndpoints(ValiantEndpointOptions options);

    public Task InitializeAsync()
    {
        using var client = CreateClient();
        return Task.CompletedTask;
    }
}

public sealed class ExcludedGroupApplicationFactory :
    ConditionalRegistrationApplicationFactory
{
    protected override void ConfigureEndpoints(ValiantEndpointOptions options)
    {
        options.MapEndpointGroupsWhen = registration =>
            registration.GroupType != typeof(BindingSourceEndpointGroup);
    }
}

public sealed class ExcludedStandaloneEndpointApplicationFactory :
    ConditionalRegistrationApplicationFactory
{
    protected override void ConfigureEndpoints(ValiantEndpointOptions options)
    {
        options.MapEndpointWhen = registration =>
            registration.EndpointType != typeof(FromRouteBindingSourceEndpoint);
    }
}

public sealed class ExcludedGroupedEndpointApplicationFactory :
    ConditionalRegistrationApplicationFactory
{
    protected override void ConfigureEndpoints(ValiantEndpointOptions options)
    {
        options.MapEndpointWhen = registration =>
            registration.EndpointType != typeof(BindingSourceGroupEndpoint);
    }
}

public sealed class ConditionalParsingApplicationFactory :
    ConditionalRegistrationApplicationFactory
{
    protected override void ConfigureEndpoints(ValiantEndpointOptions options)
    {
        options.ConfigureEndpointWhen(
            registration =>
                registration.EndpointType == typeof(GeneratedGlobalArrayEndpoint),
            endpointOptions => endpointOptions.QueryParameters.Parsing =
                new QueryParsingOptions
                {
                    SupportedArrayFormats = [QueryArrayFormat.CommaSeparated],
                    SupportedObjectFormats = [QueryObjectFormat.Flat]
                });
    }
}

public sealed class MappingConfigurationApplicationFactory :
    ConditionalRegistrationApplicationFactory
{
    protected override void ConfigureEndpoints(ValiantEndpointOptions options)
    {
        options.ConfigureEndpointGroup = (group, registration) =>
        {
            if (registration.GroupType != typeof(BindingSourceEndpointGroup))
            {
                return;
            }

            group.AddEndpointFilter(async (context, next) =>
            {
                var result = await next(context);
                context.HttpContext.Response.Headers["X-Valiant-Group"] = "configured";
                return result;
            });
        };
        options.ConfigureEndpoint = (endpoint, registration) =>
        {
            if (registration.EndpointType != typeof(BindingSourceGroupEndpoint))
            {
                return;
            }

            endpoint.AddEndpointFilter(async (context, next) =>
            {
                var result = await next(context);
                context.HttpContext.Response.Headers["X-Valiant-Endpoint"] = "configured";
                return result;
            });
        };
    }
}

[ClassDataSource<ExcludedGroupApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class ExcludedGroupWebApplicationFactoryTests(
    ExcludedGroupApplicationFactory factory)
{
    [Test]
    public async Task MapEndpointGroupsWhen_Maps_Allowed_Group_And_Excludes_Rejected_Group()
    {
        using var client = factory.CreateClient();

        await Assert.That(
                (await client.GetAsync(
                    "/integration/binding-sources/allowed-group/ping")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/group/ping")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/route/42")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/query?id=42")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
    }
}

[ClassDataSource<ExcludedStandaloneEndpointApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class ExcludedStandaloneEndpointWebApplicationFactoryTests(
    ExcludedStandaloneEndpointApplicationFactory factory)
{
    [Test]
    public async Task Excluded_Standalone_Endpoint_Returns_404_While_Other_Endpoint_Remains_Reachable()
    {
        using var client = factory.CreateClient();

        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/route/42")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/query?id=42")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
    }
}

[ClassDataSource<ExcludedGroupedEndpointApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class ExcludedGroupedEndpointWebApplicationFactoryTests(
    ExcludedGroupedEndpointApplicationFactory factory)
{
    [Test]
    public async Task Excluded_Grouped_Endpoint_Returns_404_While_Sibling_Remains_Reachable()
    {
        using var client = factory.CreateClient();

        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/group/ping")).StatusCode)
            .IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(
                (await client.GetAsync("/integration/binding-sources/group/sibling")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
    }
}

[ClassDataSource<ConditionalParsingApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class ConditionalParsingWebApplicationFactoryTests(
    ConditionalParsingApplicationFactory factory)
{
    [Test]
    public async Task Conditional_Parsing_Configuration_Is_Isolated_To_Selected_Endpoint()
    {
        using var client = factory.CreateClient();

        await Assert.That(
                (await client.GetAsync(
                    "/integration/generated/array/global?ids=1,2,3")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
        await Assert.That(
                (await client.GetAsync(
                    "/integration/as-parameters/array/global?ids=1,2,3")).StatusCode)
            .IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(
                (await client.GetAsync(
                    "/integration/as-parameters/array/global?ids=1&ids=2&ids=3")).StatusCode)
            .IsEqualTo(HttpStatusCode.OK);
    }
}

[ClassDataSource<MappingConfigurationApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class MappingConfigurationWebApplicationFactoryTests(
    MappingConfigurationApplicationFactory factory)
{
    [Test]
    public async Task Group_And_Endpoint_Configuration_Apply_At_Their_Expected_Scopes()
    {
        using var client = factory.CreateClient();

        using var selected =
            await client.GetAsync("/integration/binding-sources/group/ping");
        using var sibling =
            await client.GetAsync("/integration/binding-sources/group/sibling");

        await Assert.That(selected.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(selected.Headers.Contains("X-Valiant-Group")).IsTrue();
        await Assert.That(selected.Headers.Contains("X-Valiant-Endpoint")).IsTrue();
        await Assert.That(sibling.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(sibling.Headers.Contains("X-Valiant-Group")).IsTrue();
        await Assert.That(sibling.Headers.Contains("X-Valiant-Endpoint")).IsFalse();
    }
}
