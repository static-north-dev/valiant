using StaticNorth.Valiant.SourceGeneration.Testing;

namespace StaticNorth.Valiant.Settings.Tests;

public sealed class GeneratorIdeTests
{
    private static readonly GeneratorIdeScenario Scenario = new(
        "Settings",
        static () => new StaticNorth.Valiant.Settings.SourceGeneration.ValiantSettingsGenerator(),
        "Settings.Model",
        """
        using StaticNorth.Valiant.Settings;
        [ValiantSettings("Section$ID$", configure: true)]
        public sealed class Item$ID$ : Model$ID$;
        """,
        """
        public class Model$ID$ { public string? Name { get; set; } }
        """,
        """
        public class Model$ID$ { public string? Name { get; set; } public int Age { get; set; } }
        """,
        """
        using StaticNorth.Valiant.Settings;
        [ValiantSettings("")]
        public sealed class Item$ID$ : Model$ID$;
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
