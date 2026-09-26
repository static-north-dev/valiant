using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StaticNorth.Valiant.SourceGeneration.Testing;

internal static class GeneratorIdeTestHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

    public static void VerifyUnrelatedEdits(GeneratorIdeScenario scenario, int declarationCount)
    {
        var compilation = CreateCompilation(scenario, declarationCount, unrelatedCount: 512);
        GeneratorDriver driver = CreateDriver(scenario);
        driver = Measure(driver, compilation, scenario.Module, declarationCount, "cold");
        var original = Snapshot(driver);
        Require(driver.GetRunResult().Results.Single().GeneratedSources.Length > 0, "No generated sources.");
        VerifyCompilation(driver, compilation);
        VerifyValueModels(driver, scenario.Module);

        driver = Measure(driver, compilation, scenario.Module, declarationCount, "no-op");
        VerifyCachedOutputs(driver);
        for (var edit = 0; edit < 10; edit++)
        {
            // Preserve the other syntax trees, as an IDE document edit does.
            compilation = Replace(compilation, "/tests/Unrelated0.cs",
                $"public sealed class Unrelated0 {{ public int Value => {edit + 1}; }}");
            driver = Measure(driver, compilation, scenario.Module, declarationCount, $"unrelated-{edit + 1}");
            Require(Snapshot(driver) == original, "Unrelated edit changed generated sources or diagnostics.");
            var outputs = driver.GetRunResult().Results.Single().TrackedSteps[scenario.ItemStep]
                .SelectMany(static step => step.Outputs).ToArray();
            Require(outputs.Length == declarationCount, "Discovery lost or duplicated an attributed declaration.");
            Require(outputs.All(static output => output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
                "Unrelated edit invalidated an item model.");
            VerifyCachedOutputs(driver);
        }

        compilation = Replace(compilation, "/tests/Item0.cs", Expand(scenario.Declaration, 0) + "\n// unrelated trailing trivia\n");
        driver = driver.RunGenerators(compilation);
        Require(Snapshot(driver) == original, "Trailing trivia changed generated output.");
        VerifyCachedOutputs(driver);
    }

    public static void VerifyChangeDetection(GeneratorIdeScenario scenario)
    {
        var compilation = CreateCompilation(scenario, 3, unrelatedCount: 0);
        var driver = CreateDriver(scenario).RunGenerators(compilation);
        var original = Snapshot(driver);
        VerifyCompilation(driver, compilation);

        // Only a referenced model/base declaration changes; the marked file is untouched.
        compilation = Replace(compilation, "/tests/Dependency0.cs", Expand(scenario.ChangedDependency, 0));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        Require(Snapshot(driver) != original, "A dependency edit was missed.");
        var reasons = driver.GetRunResult().Results.Single().TrackedSteps[scenario.ItemStep]
            .SelectMany(static step => step.Outputs).Select(static output => output.Reason).ToArray();
        Require(reasons.Count(static reason => reason == IncrementalStepRunReason.Modified) == 1,
            "A one-model dependency edit must invalidate exactly one item.");
        VerifyCompilation(driver, compilation);

        compilation = Replace(compilation, "/tests/Item0.cs", Expand(scenario.InvalidDeclaration, 0));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        Require(driver.GetRunResult().Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "Invalid authoring did not produce an actionable diagnostic.");

        // Diagnostic locations must follow line and file moves, including cached invalid models.
        compilation = Replace(compilation, "/tests/Item0.cs", "\n\n" + Expand(scenario.InvalidDeclaration, 0));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        var invalidTree = compilation.SyntaxTrees.Single(static tree => tree.FilePath == "/tests/Item0.cs");
        compilation = compilation.ReplaceSyntaxTree(invalidTree,
            CSharpSyntaxTree.ParseText(invalidTree.GetText(), ParseOptions, "/tests/Moved.cs"));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        compilation = compilation.RemoveSyntaxTrees(compilation.SyntaxTrees.Single(static tree => tree.FilePath == "/tests/Moved.cs"))
            .AddSyntaxTrees(Parse(Expand(scenario.Declaration, 0), "/tests/Item0.cs"));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        VerifyCompilation(driver, compilation);

        // Remove/re-add a marked file and undo a dependency edit using the same driver.
        var declaration = compilation.SyntaxTrees.Single(static tree => tree.FilePath == "/tests/Item0.cs");
        compilation = compilation.RemoveSyntaxTrees(declaration);
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        compilation = compilation.AddSyntaxTrees(declaration);
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        compilation = Replace(compilation, "/tests/Dependency0.cs", Expand(scenario.Dependency, 0));
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        Require(Snapshot(driver) == original, "Undo did not restore the original output.");
        VerifyCompilation(driver, compilation);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;
        try
        {
            _ = driver.RunGenerators(Replace(compilation, "/tests/Dependency0.cs", Expand(scenario.ChangedDependency, 0)), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Require(cancelled, "Generator driver ignored cancellation.");
        driver = VerifyAgainstFreshDriver(scenario, driver, compilation);
        VerifyValueModels(driver, scenario.Module);
    }

    private static CSharpCompilation CreateCompilation(GeneratorIdeScenario scenario, int count, int unrelatedCount)
    {
        var trees = Enumerable.Range(0, count).SelectMany(index => new[]
        {
            Parse(Expand(scenario.Declaration, index), $"/tests/Item{index}.cs"),
            Parse(Expand(scenario.Dependency, index), $"/tests/Dependency{index}.cs")
        }).Concat(Enumerable.Range(0, unrelatedCount).Select(index =>
            Parse($"public sealed class Unrelated{index} {{ public int Value => 0; }}", $"/tests/Unrelated{index}.cs")));
        return CSharpCompilation.Create("IdeTests", trees,
            GeneratorTestHarness.GetTrustedPlatformReferences(Path.GetFileName(typeof(GeneratorIdeTestHarness).Assembly.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static CSharpGeneratorDriver CreateDriver(GeneratorIdeScenario scenario) => CSharpGeneratorDriver.Create(
        [scenario.CreateGenerator().AsSourceGenerator()], [], ParseOptions, null,
        new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static SyntaxTree Parse(string source, string path) => CSharpSyntaxTree.ParseText(source, ParseOptions, path);

    private static string Expand(string template, int index) => template.Replace("$ID$", index.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static CSharpCompilation Replace(CSharpCompilation compilation, string path, string source) =>
        compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(tree => tree.FilePath == path), Parse(source, path));

    private static GeneratorDriver Measure(GeneratorDriver driver, Compilation compilation, string module, int count, string phase)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        driver = driver.RunGenerators(compilation);
        var elapsed = Stopwatch.GetElapsedTime(start);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine(FormattableString.Invariant($"IDE,{module},{count},{phase},{elapsed.TotalMilliseconds:F3},{allocated}"));
        Require(driver.GetRunResult().Results.All(static result => result.Exception is null), "Generator threw an exception.");
        return driver;
    }

    private static GeneratorDriver VerifyAgainstFreshDriver(GeneratorIdeScenario scenario, GeneratorDriver driver, Compilation compilation)
    {
        driver = driver.RunGenerators(compilation);
        var fresh = CreateDriver(scenario).RunGenerators(compilation);
        Require(driver.GetRunResult().Results.Single().Exception is null, "Generator threw during an edit.");
        Require(Snapshot(driver) == Snapshot(fresh), "Incremental output differs from a fresh run (stale source or diagnostic).");
        return driver;
    }

    private static string Snapshot(GeneratorDriver driver)
    {
        var run = driver.GetRunResult();
        return string.Join("\n", run.Results.SelectMany(static result => result.GeneratedSources)
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .Select(static source => source.HintName + "\n" + source.SourceText)) + "\nDIAGNOSTICS\n" +
            string.Join("\n", run.Diagnostics.Select(static diagnostic => diagnostic.ToString()).Order(StringComparer.Ordinal));
    }

    private static void VerifyCachedOutputs(GeneratorDriver driver)
    {
        var rerun = driver.GetRunResult().Results.Single().TrackedOutputSteps
            .SelectMany(static entry => entry.Value.SelectMany(step => step.Outputs)
                .Where(static output => output.Reason != IncrementalStepRunReason.Cached)
                .Select(output => entry.Key + ":" + output.Reason)).ToArray();
        Require(rerun.Length == 0, "Unnecessary output callbacks: " + string.Join(", ", rerun));
    }

    private static void VerifyCompilation(GeneratorDriver driver, Compilation compilation)
    {
        _ = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Require(!errors.Any(), string.Join("\n", errors));
    }

    private static void VerifyValueModels(GeneratorDriver driver, string module)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var step in driver.GetRunResult().Results.Single().TrackedSteps.Where(entry => entry.Key.StartsWith(module + ".", StringComparison.Ordinal)))
        {
            foreach (var output in step.Value.SelectMany(static run => run.Outputs))
            {
                Visit(output.Value, step.Key, seen);
            }
        }

        foreach (var step in driver.GetRunResult().Results.Single().TrackedOutputSteps.Values.SelectMany(static steps => steps))
        {
            foreach (var input in step.Inputs)
            {
                Visit(input.Source.Outputs[input.OutputIndex].Value, "RegisteredOutput", seen);
            }
        }
    }

    private static void Visit(object? value, string path, HashSet<object> seen)
    {
        if (value is null || value is string || value is DiagnosticDescriptor || !seen.Add(value))
        {
            return;
        }

        Require(value is not (Compilation or SemanticModel or ISymbol or SyntaxNode or SyntaxTree or Location),
            "IDE model retains Roslyn state at " + path);
        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
        {
            return;
        }

        if (value is IEnumerable sequence && !type.IsValueType)
        {
            foreach (var item in sequence)
            {
                Visit(item, path + "[]", seen);
            }

            return;
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Visit(field.GetValue(value), path + "." + field.Name, seen);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
