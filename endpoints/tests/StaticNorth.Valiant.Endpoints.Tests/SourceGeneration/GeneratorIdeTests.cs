using StaticNorth.Valiant.SourceGeneration.Testing;

namespace StaticNorth.Valiant.Endpoints.Tests.SourceGeneration;

public sealed class GeneratorIdeTests
{
    private static readonly GeneratorIdeScenario Scenario = new(
        "Endpoints",
        static () => new StaticNorth.Valiant.Endpoints.AspNetCore.SourceGeneration.ValiantEndpointGenerator(),
        "Endpoints.Endpoint",
        """
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Routing;
        using StaticNorth.Valiant.Endpoints;
        using StaticNorth.Valiant.Endpoints.AspNetCore;
        [ValiantEndpoint(Order = Model$ID$.Order)]
        public sealed class Item$ID$ : IEndpoint
        {
            public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) => builder.MapGet("/item$ID$", () => "ok");
        }
        """,
        """
        public static class Model$ID$ { public const int Order = 0; }
        """,
        """
        public static class Model$ID$ { public const int Order = -1; }
        """,
        """
        using StaticNorth.Valiant.Endpoints;
        [ValiantEndpoint]
        public sealed class Item$ID$;
        """);

    [Test]
    [Arguments(1)]
    [Arguments(32)]
    [Arguments(128)]
    public void Unrelated_Edits_Cache_All_Outputs_And_Keep_Value_Only_Models(int count)
    {
        GeneratorIdeTestHarness.VerifyUnrelatedEdits(Scenario, count);
    }

    [Test]
    public void Partial_Endpoint_Map_In_Another_File_Has_Stable_Discovery()
    {
        var scenario = Scenario with
        {
            Declaration = """
                using StaticNorth.Valiant.Endpoints;
                using StaticNorth.Valiant.Endpoints.AspNetCore;
                [ValiantEndpoint]
                public sealed partial class Item$ID$ : IEndpoint;
                """,
            Dependency = """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.AspNetCore.Routing;
                using Microsoft.AspNetCore.Mvc;
                using Microsoft.Extensions.DependencyInjection;
                using StaticNorth.Valiant.Endpoints.AspNetCore.DependencyInjection;
                internal static class QueryConfiguration
                {
                    internal static void Configure(IServiceCollection services) =>
                        services.AddValiantEndpoints(options => options.QueryParameters.EnableBinding = true);
                }
                public sealed partial class Item$ID$
                {
                    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder builder) =>
                        builder.MapGet("/item$ID$", ([FromQuery] Filter$ID$ filter) => filter.Id);
                }
                public sealed partial class Filter$ID$ { public int Id { get; set; } }
                """
        };
        GeneratorIdeTestHarness.VerifyUnrelatedEdits(scenario, 1);
    }

    [Test]
    public void Dependency_Edits_Diagnostics_Removal_Undo_And_Cancellation_Match_Fresh_Generation()
    {
        GeneratorIdeTestHarness.VerifyChangeDetection(Scenario);
    }
}
