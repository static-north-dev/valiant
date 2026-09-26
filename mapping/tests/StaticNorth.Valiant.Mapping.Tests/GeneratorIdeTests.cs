using StaticNorth.Valiant.SourceGeneration.Testing;

namespace StaticNorth.Valiant.Mapping.Tests;

public sealed class GeneratorIdeTests
{
    private static readonly GeneratorIdeScenario Scenario = new(
        "Mapping",
        static () => new StaticNorth.Valiant.Mapping.SourceGeneration.ValiantMappingGenerator(),
        "Mapping.Mapper",
        """
        using StaticNorth.Valiant.Mapping;
        [ValiantMapper]
        public sealed partial class Item$ID$ : MapperDefinition<Model$ID$, Target$ID$>;
        """,
        """
        public sealed class Model$ID$ { public string? Name { get; init; } }
        public sealed class Target$ID$ { public string? Name { get; init; } }
        """,
        """
        public sealed class Model$ID$ { public string? Name { get; init; } public int Age { get; init; } }
        public sealed class Target$ID$ { public string? Name { get; init; } public int Age { get; init; } }
        """,
        """
        using StaticNorth.Valiant.Mapping;
        [ValiantMapper]
        public sealed class Item$ID$ : MapperDefinition<Model$ID$, Target$ID$>;
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
    public void Dependency_Edits_Diagnostics_Removal_Undo_And_Cancellation_Match_Fresh_Generation()
    {
        GeneratorIdeTestHarness.VerifyChangeDetection(Scenario);
    }
}
