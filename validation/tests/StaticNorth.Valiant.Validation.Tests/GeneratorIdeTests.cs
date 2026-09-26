using StaticNorth.Valiant.SourceGeneration.Testing;

namespace StaticNorth.Valiant.Validation.Tests;

public sealed class GeneratorIdeTests
{
    private static readonly GeneratorIdeScenario Scenario = new(
        "Validation",
        static () => new StaticNorth.Valiant.Validation.SourceGeneration.ValiantGenerator(),
        "Validation.NonGenericValidator",
        """
        using StaticNorth.Valiant.Validation;
        [ValiantValidator]
        public sealed partial class Item$ID$ : ValidatorDefinition<Model$ID$>;
        """,
        """
        using StaticNorth.Valiant.Validation;
        public sealed class Model$ID$ { [Required] public string? Name { get; init; } }
        """,
        """
        using StaticNorth.Valiant.Validation;
        public sealed class Model$ID$ { [Required, MaxLength(12)] public string? Name { get; init; } }
        """,
        """
        using StaticNorth.Valiant.Validation;
        [ValiantValidator]
        public sealed class Item$ID$ : ValidatorDefinition<Model$ID$>;
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
