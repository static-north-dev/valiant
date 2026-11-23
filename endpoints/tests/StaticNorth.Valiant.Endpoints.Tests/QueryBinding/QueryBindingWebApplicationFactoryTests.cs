using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core.Interfaces;
using StaticNorth.Valiant.Endpoints.AspNetCore;
using SampleStartup = StaticNorth.Valiant.Endpoints.AspNetCore.Samples.QueryBinding.Startup;
using TestApplicationMarker = StaticNorth.Valiant.Endpoints.AspNetCore.TestApp.TestApplicationMarker;

namespace StaticNorth.Valiant.Endpoints.Tests.QueryBinding;

public sealed class QueryBindingSampleApplicationFactory :
    WebApplicationFactory<SampleStartup>,
    IAsyncInitializer
{
    public QueryBindingSampleApplicationFactory()
    {
        UseKestrel(static options => options.Listen(IPAddress.Loopback, 0));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    public Task InitializeAsync()
    {
        using var client = CreateClient();
        return Task.CompletedTask;
    }
}

public sealed class QueryBindingTestApplicationFactory :
    WebApplicationFactory<TestApplicationMarker>,
    IAsyncInitializer
{
    public QueryBindingTestApplicationFactory()
    {
        UseKestrel(static options => options.Listen(IPAddress.Loopback, 0));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
    }

    public Task InitializeAsync()
    {
        using var client = CreateClient();
        return Task.CompletedTask;
    }
}

public sealed class QueryBindingDisabledTestApplicationFactory :
    WebApplicationFactory<TestApplicationMarker>,
    IAsyncInitializer
{
    public QueryBindingDisabledTestApplicationFactory()
    {
        UseKestrel(static options => options.Listen(IPAddress.Loopback, 0));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureServices(services =>
            services.PostConfigure<ValiantEndpointOptions>(options =>
                options.QueryParameters.EnableBinding = false));
    }

    public Task InitializeAsync()
    {
        using var client = CreateClient();
        return Task.CompletedTask;
    }
}

[ClassDataSource<QueryBindingSampleApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class QueryBindingSampleWebApplicationFactoryTests(
    QueryBindingSampleApplicationFactory factory)
{
    [Test]
    public async Task Sample_Application_Starts_And_Maps_Generated_Endpoints()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/array-formats?ids=1");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("?ids=1&ids=2&ids=3")]
    [Arguments("?ids=1,2,3")]
    [Arguments("?ids%5B%5D=1&ids%5B%5D=2&ids%5B%5D=3")]
    [Arguments("?ids%5B2%5D=3&ids%5B0%5D=1&ids%5B1%5D=2")]
    public async Task Sample_Binds_Every_Supported_Array_Format(string query)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/array-formats" + query);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await Assert.That(ReadIntArray(document.RootElement, "ids"))
            .IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    [Arguments("?id=1&name=tesla", false)]
    [Arguments("?filter.id=1&filter.name=tesla", true)]
    [Arguments("?filter%5Bid%5D=1&filter%5Bname%5D=tesla", true)]
    public async Task Sample_Binds_Every_Supported_Object_Format(
        string query,
        bool bindsNestedFilter)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/object-formats" + query);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await AssertObjectBinding(document.RootElement, bindsNestedFilter);
    }

    [Test]
    public async Task Sample_Allows_Json_When_Endpoint_Enables_It()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/json?request=%7B%22name%22%3A%22John%22%2C%22age%22%3A30%2C" +
            "%22address%22%3A%7B%22street%22%3A%22123%20Main%20St%22%2C" +
            "%22city%22%3A%22Anytown%22%7D%7D");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("name").GetString())
            .IsEqualTo("John");
        await Assert.That(document.RootElement.GetProperty("age").GetInt32())
            .IsEqualTo(30);
        await Assert.That(document.RootElement
                .GetProperty("address")
                .GetProperty("city")
                .GetString())
            .IsEqualTo("Anytown");
    }

    [Test]
    public async Task Sample_Rejects_Json_When_Endpoint_Disables_It()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/no-json?request=%7B%22name%22%3A%22John%22%2C%22age%22%3A30%7D");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static int[] ReadIntArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(static value => value.GetInt32())
            .ToArray();
    }

    private static async Task AssertObjectBinding(
        JsonElement root,
        bool bindsNestedFilter)
    {
        if (bindsNestedFilter)
        {
            await Assert.That(root.GetProperty("id").ValueKind)
                .IsEqualTo(JsonValueKind.Null);
            await Assert.That(root.GetProperty("name").ValueKind)
                .IsEqualTo(JsonValueKind.Null);
            var filter = root.GetProperty("filter");
            await Assert.That(filter.GetProperty("id").GetInt32()).IsEqualTo(1);
            await Assert.That(filter.GetProperty("name").GetString())
                .IsEqualTo("tesla");
            return;
        }

        await Assert.That(root.GetProperty("id").GetInt32()).IsEqualTo(1);
        await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("tesla");
        await Assert.That(root.GetProperty("filter").ValueKind)
            .IsEqualTo(JsonValueKind.Null);
    }
}

[ClassDataSource<QueryBindingTestApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class QueryBindingWebApplicationFactoryTests(
    QueryBindingTestApplicationFactory factory)
{
    [Test]
    public async Task Query_Binding_Runs_Before_Group_And_Route_Filters()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/integration/filter-observation/observe?filter.id=7&day=2026-01-02");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Headers.GetValues("X-Group-Observed-Filter").Single()).IsEqualTo("7");
        await Assert.That(response.Headers.GetValues("X-Route-Observed-Filter").Single()).IsEqualTo("7");
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("day").GetString()).IsEqualTo("2026-01-02");
    }

    [Test]
    public async Task Whole_Request_Binds_Inherited_Dotted_Query_Alias()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/integration/query/inherited?page.size=5&term=books");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("limit").GetInt32()).IsEqualTo(5);
        await Assert.That(document.RootElement.GetProperty("search").GetString()).IsEqualTo("books");
    }

    [Test]
    public async Task Whole_Request_Reports_Missing_Required_Query_Alias()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/integration/query/inherited?page.size=5");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement[0].GetProperty("code").GetString()).IsEqualTo("MissingQueryValue");
        await Assert.That(document.RootElement[0].GetProperty("path").GetString()).IsEqualTo("term");
    }

    [Test]
    public async Task Ordinary_Route_Binds_A_Query_Type_Also_Used_By_Valiant()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/integration/query/native?filter=%7B%22id%22%3A7%7D");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("id").GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task Generated_Whole_Request_Binding_Uses_Only_Declared_Query_Members()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/integration/generated/object/local?page-size=5&label=custom");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("limit").GetInt32()).IsEqualTo(5);
        await Assert.That(document.RootElement.GetProperty("label").GetString()).IsEqualTo("default");
    }

    [Test]
    public async Task Query_Filter_Preserves_AspNetCore_Binding_Failure()
    {
        using var client = factory.CreateClient();
        const string path = "/integration/as-parameters/binding-failure/";
        const string invalidQuery = "?filter%5Bid%5D=1";

        using var invalidRoute = await client.GetAsync(path + "invalid" + invalidQuery);
        await Assert.That(invalidRoute.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await invalidRoute.Content.ReadAsStringAsync()).IsEqualTo(string.Empty);

        using var validRoute = await client.GetAsync(path + "1" + invalidQuery);
        await Assert.That(validRoute.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await validRoute.Content.ReadAsStringAsync()).Contains("DisabledQueryObjectFormat");
    }

    [Test]
    public async Task Test_Application_Starts_And_Maps_Generated_Endpoints()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/integration/generated/object/global?filter.id=1");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    [Arguments("/integration/generated/object/global", "?filter.id=1", HttpStatusCode.OK, true)]
    [Arguments("/integration/generated/object/global", "?id=1", HttpStatusCode.BadRequest, false)]
    [Arguments("/integration/generated/object/global", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/generated/object/local", "?id=1", HttpStatusCode.OK, false)]
    [Arguments("/integration/generated/object/local", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.OK, true)]
    [Arguments("/integration/generated/object/local", "?filter.id=1", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/generated/object/attribute", "?filter%5Bid%5D=1", HttpStatusCode.OK, true)]
    [Arguments("/integration/generated/object/attribute", "?id=1", HttpStatusCode.BadRequest, false)]
    [Arguments("/integration/generated/object/attribute", "?filter.id=1", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/generated/object/attribute", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/as-parameters/object/global", "?filter.id=1", HttpStatusCode.OK, true)]
    [Arguments("/integration/as-parameters/object/global", "?id=1", HttpStatusCode.BadRequest, false)]
    [Arguments("/integration/as-parameters/object/global", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/as-parameters/object/local", "?id=1", HttpStatusCode.OK, false)]
    [Arguments("/integration/as-parameters/object/local", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.OK, true)]
    [Arguments("/integration/as-parameters/object/local", "?filter.id=1", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/as-parameters/object/attribute", "?filter%5Bid%5D=1", HttpStatusCode.OK, true)]
    [Arguments("/integration/as-parameters/object/attribute", "?id=1", HttpStatusCode.BadRequest, false)]
    [Arguments("/integration/as-parameters/object/attribute", "?filter.id=1", HttpStatusCode.BadRequest, true)]
    [Arguments("/integration/as-parameters/object/attribute", "?filter=%7B%22id%22%3A1%7D", HttpStatusCode.BadRequest, true)]
    public async Task Hosted_Object_Binding_Uses_Global_Local_And_Attribute_Precedence(
        string route,
        string query,
        HttpStatusCode expectedStatus,
        bool bindsNestedFilter)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route + query);

        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
        if (expectedStatus != HttpStatusCode.OK)
        {
            return;
        }

        using var document = await ReadJson(response);
        await AssertObjectBinding(document.RootElement, bindsNestedFilter);
    }

    [Test]
    [Arguments("/integration/generated/array/global", "?ids=1&ids=2&ids=3", HttpStatusCode.OK)]
    [Arguments("/integration/generated/array/global", "?ids=1,2,3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/generated/array/local", "?ids=1,2,3", HttpStatusCode.OK)]
    [Arguments("/integration/generated/array/local", "?ids=1&ids=2&ids=3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/generated/array/attribute", "?ids%5B%5D=1&ids%5B%5D=2&ids%5B%5D=3", HttpStatusCode.OK)]
    [Arguments("/integration/generated/array/attribute", "?ids=1,2,3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/as-parameters/array/global", "?ids=1&ids=2&ids=3", HttpStatusCode.OK)]
    [Arguments("/integration/as-parameters/array/global", "?ids=1,2,3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/as-parameters/array/local", "?ids%5B2%5D=3&ids%5B0%5D=1&ids%5B1%5D=2", HttpStatusCode.OK)]
    [Arguments("/integration/as-parameters/array/local", "?ids=1&ids=2&ids=3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/as-parameters/array/attribute", "?ids%5B%5D=1&ids%5B%5D=2&ids%5B%5D=3", HttpStatusCode.OK)]
    [Arguments("/integration/as-parameters/array/attribute", "?ids=1&ids=2&ids=3", HttpStatusCode.BadRequest)]
    [Arguments("/integration/as-parameters/array/attribute", "?ids%5B0%5D=1&ids%5B1%5D=2&ids%5B2%5D=3", HttpStatusCode.BadRequest)]
    public async Task Hosted_Array_Binding_Uses_Global_Local_And_Attribute_Precedence(
        string route,
        string query,
        HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(route + query);

        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
        if (expectedStatus != HttpStatusCode.OK)
        {
            return;
        }

        using var document = await ReadJson(response);
        await Assert.That(ReadIntArray(document.RootElement, "ids"))
            .IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    [Arguments("/integration/direct/enabled", HttpStatusCode.OK)]
    [Arguments("/integration/direct/disabled", HttpStatusCode.BadRequest)]
    [Arguments("/integration/direct/default", HttpStatusCode.OK)]
    public async Task Hosted_Direct_FromQuery_Json_Uses_Endpoint_Policy(
        string route,
        HttpStatusCode expectedStatus)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            route + "?payload=%7B%22id%22%3A1%7D");

        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
        if (expectedStatus != HttpStatusCode.OK)
        {
            return;
        }

        using var document = await ReadJson(response);
        await Assert.That(document.RootElement.GetProperty("id").GetInt32())
            .IsEqualTo(1);
    }

    [Test]
    [Arguments("/integration/generated/array/global")]
    [Arguments("/integration/as-parameters/array/global")]
    public async Task Hosted_Valiant_Binding_Reports_All_Query_Errors(string route)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(route + "?ids[]=1&ids[]=2");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        using var document = await ReadJson(response);
        var errors = document.RootElement.EnumerateArray().ToArray();
        await Assert.That(errors.Length).IsEqualTo(2);
        foreach (var error in errors)
        {
            await Assert.That(error.GetProperty("path").GetString()).IsEqualTo("ids[]");
            await Assert.That(error.GetProperty("code").GetString()).IsEqualTo("DisabledQueryArrayFormat");
        }
    }

    [Test]
    public async Task Hosted_AsParameters_Cannot_Repair_Comma_Separated_Integer_Array_After_Native_Binding_Fails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/integration/as-parameters/array/local?ids=1,2,3");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Hosted_Direct_FromQuery_Allows_Missing_Optional_Value()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/integration/direct/optional");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo("-1");
    }

    [Test]
    public async Task Hosted_Direct_FromQuery_Rejects_Malformed_Present_Value()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/integration/direct/optional?payload=not-json");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static int[] ReadIntArray(JsonElement root, string propertyName)
    {
        return root.GetProperty(propertyName)
            .EnumerateArray()
            .Select(static value => value.GetInt32())
            .ToArray();
    }

    private static async Task AssertObjectBinding(
        JsonElement root,
        bool bindsNestedFilter)
    {
        if (bindsNestedFilter)
        {
            await Assert.That(root.GetProperty("id").ValueKind)
                .IsEqualTo(JsonValueKind.Null);
            await Assert.That(root.GetProperty("filter").GetProperty("id").GetInt32())
                .IsEqualTo(1);
            return;
        }

        await Assert.That(root.GetProperty("id").GetInt32()).IsEqualTo(1);
        await Assert.That(root.GetProperty("filter").ValueKind)
            .IsEqualTo(JsonValueKind.Null);
    }
}

[ClassDataSource<QueryBindingDisabledTestApplicationFactory>(Shared = SharedType.PerClass)]
public sealed class QueryBindingDisabledWebApplicationFactoryTests(
    QueryBindingDisabledTestApplicationFactory factory)
{
    [Test]
    [Arguments("/integration/generated/object/global")]
    [Arguments("/integration/as-parameters/object/global")]
    public async Task Compiled_Query_Binding_Remains_Enabled_With_A_Runtime_Override(string route)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(route + "?filter.id=7");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        await Assert.That(document.RootElement.GetProperty("filter").GetProperty("id").GetInt32()).IsEqualTo(7);
    }

    [Test]
    public async Task Compiled_Query_Binding_Preserves_Endpoint_Policy_With_A_Runtime_Override()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/integration/as-parameters/array/attribute?ids=1&ids=2&ids=3");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("DisabledQueryArrayFormat");
    }
}
