using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using QueryBindingDisabledTestApplicationFactory = Valiant.Endpoints.Tests.QueryBinding.QueryBindingDisabledTestApplicationFactory;
using QueryBindingTestApplicationFactory = Valiant.Endpoints.Tests.QueryBinding.QueryBindingTestApplicationFactory;

namespace Valiant.Endpoints.Tests.BindingSources;

[ClassDataSource<QueryBindingTestApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class BindingSourceEnabledWebApplicationFactoryTests(
    QueryBindingTestApplicationFactory factory)
{
    [Test]
    public async Task Enabled_Host_Maps_Standalone_Endpoint_And_Group()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/route/42"),
            "42");
        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/group/ping"),
            "group-value");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromQuery()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/query?id=42"),
            "42");
    }

    [Test]
    public async Task Enabled_Host_Binds_AsParameters()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync(
                "/integration/binding-sources/as-parameters?id=42&name=tesla"),
            "42:tesla");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromRoute()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/route/42"),
            "42");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromHeader()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/integration/binding-sources/header");
        request.Headers.Add("X-Binding-Value", "header-value");

        await BindingSourceAssertions.AssertValueAsync(
            await client.SendAsync(request),
            "header-value");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromBody()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.PostAsJsonAsync(
                "/integration/binding-sources/body",
                new { Value = "body-value" }),
            "body-value");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromForm()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.PostAsync(
                "/integration/binding-sources/form",
                new FormUrlEncodedContent(
                [new KeyValuePair<string, string>("value", "form-value")])),
            "form-value");
    }

    [Test]
    public async Task Enabled_Host_Binds_FromServices()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/services"),
            "service-value");
    }

    [Test]
    public async Task Enabled_Host_Binds_Custom_BindAsync_Source()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/integration/binding-sources/custom");
        request.Headers.Add("X-Custom-Binding-Value", "custom-value");

        await BindingSourceAssertions.AssertValueAsync(
            await client.SendAsync(request),
            "custom-value");
    }
}

[ClassDataSource<QueryBindingDisabledTestApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class BindingSourceDisabledWebApplicationFactoryTests(
    QueryBindingDisabledTestApplicationFactory factory)
{
    [Test]
    public async Task Disabled_Host_Maps_Standalone_Endpoint_And_Group()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/route/42"),
            "42");
        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/group/ping"),
            "group-value");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromQuery()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/query?id=42"),
            "42");
    }

    [Test]
    public async Task Disabled_Host_Binds_AsParameters()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync(
                "/integration/binding-sources/as-parameters?id=42&name=tesla"),
            "42:tesla");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromRoute()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/route/42"),
            "42");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromHeader()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/integration/binding-sources/header");
        request.Headers.Add("X-Binding-Value", "header-value");

        await BindingSourceAssertions.AssertValueAsync(
            await client.SendAsync(request),
            "header-value");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromBody()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.PostAsJsonAsync(
                "/integration/binding-sources/body",
                new { Value = "body-value" }),
            "body-value");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromForm()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.PostAsync(
                "/integration/binding-sources/form",
                new FormUrlEncodedContent(
                [new KeyValuePair<string, string>("value", "form-value")])),
            "form-value");
    }

    [Test]
    public async Task Disabled_Host_Binds_FromServices()
    {
        using var client = factory.CreateClient();

        await BindingSourceAssertions.AssertValueAsync(
            await client.GetAsync("/integration/binding-sources/services"),
            "service-value");
    }

    [Test]
    public async Task Disabled_Host_Binds_Custom_BindAsync_Source()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/integration/binding-sources/custom");
        request.Headers.Add("X-Custom-Binding-Value", "custom-value");

        await BindingSourceAssertions.AssertValueAsync(
            await client.SendAsync(request),
            "custom-value");
    }
}

internal static class BindingSourceAssertions
{
    public static async Task AssertValueAsync(
        HttpResponseMessage response,
        string expectedValue)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(document.RootElement.GetProperty("value").GetString())
            .IsEqualTo(expectedValue);
    }
}
