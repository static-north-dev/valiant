using Microsoft.CodeAnalysis;

namespace StaticNorth.Valiant.SourceGeneration.Testing;

internal sealed record GeneratorIdeScenario(
    string Module,
    Func<IIncrementalGenerator> CreateGenerator,
    string ItemStep,
    string Declaration,
    string Dependency,
    string ChangedDependency,
    string InvalidDeclaration);
