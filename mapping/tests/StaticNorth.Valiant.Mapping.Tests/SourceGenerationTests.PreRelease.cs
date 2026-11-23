using StaticNorth.Valiant.Mapping;

namespace StaticNorth.Valiant.Mapping.Tests;

public sealed partial class SourceGenerationTests
{
    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Configure_Resolves_Overloads_And_Binds_Named_Arguments_By_Parameter(ValiantMappingMode mode)
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int input);
            public sealed class Output
            {
                public int Value { get; set; }
                public int value { get; set; } = 9;
            }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public void Configure(int irrelevant) { }

                public override void Configure(MappingContext<Input, Output> context) =>
                    context.Map(target: output => output.Value, source: input => input.input)
                        .Ignore(output => output.value);
            }

            public static class TestRunner
            {
                public static string Run(ValiantMappingMode mode)
                {
                    var result = new TestMapper(new ValiantMappingOptions { Mode = mode }).Map(new Input(42));
                    return $"{result.Value}/{result.value}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [mode])!;
        await Assert.That(actual).IsEqualTo("42/9");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Explicit_Source_Generics_Preserve_Conversions_And_Null_Literals(ValiantMappingMode mode)
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(short Number);
            public sealed record Label(int Value);
            public sealed class Output
            {
                public Label? Label { get; set; }
                public string? Note { get; set; } = "preset";
            }

            public sealed class LabelMapper : IMapper<int, Label>
            {
                public Label Map(int source) => new(source);
            }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public override void Configure(MappingContext<Input, Output> context)
                {
                    context.UseMapper(new LabelMapper());
                    context.Map<int, Label?>(input => input.Number, output => output.Label);
                    context.Map<string?, string?>(_ => null, output => output.Note);
                }
            }

            public static class TestRunner
            {
                public static string Run(ValiantMappingMode mode)
                {
                    var result = new TestMapper(new ValiantMappingOptions { Mode = mode }).Map(new Input(42));
                    return $"{result.Label?.Value}/{result.Note is null}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [mode])!;
        await Assert.That(actual).IsEqualTo("42/True");
    }

    [Test]
    public async Task Nested_Target_Selectors_Report_A_Diagnostic()
    {
        var result = RunGenerator(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int Value);
            public sealed class Output
            {
                public int Value { get; set; }
                public Child Nested { get; set; } = new();
            }
            public sealed class Child { public int Value { get; set; } }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public override void Configure(MappingContext<Input, Output> context)
                {
                    context.Map(input => input.Value, output => output.Nested.Value);
                    context.Ignore(output => output.Nested.Value);
                }
            }
            """);

        await Assert.That(result.Diagnostics.Count(diagnostic => diagnostic.Id == "VLM004")).IsEqualTo(2);
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Conditional_Required_Init_Mapping_Uses_The_Explicit_Fallback(ValiantMappingMode mode)
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(string? Value, bool Omit);
            public sealed class Output { public required string? Value { get; init; } }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public override void Configure(MappingContext<Input, Output> context)
                {
                    context.Map(input => input.Value, output => output.Value)
                        .When(input => !input.Omit)
                        .IgnoreNull()
                        .Fallback("fallback");
                }
            }

            public static class TestRunner
            {
                public static string Run(ValiantMappingMode mode)
                {
                    var mapper = new TestMapper(new ValiantMappingOptions { Mode = mode });
                    return $"{mapper.Map(new Input(null, false)).Value}/{mapper.Map(new Input("value", false)).Value}/{mapper.Map(new Input("value", true)).Value}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [mode])!;
        await Assert.That(actual).IsEqualTo("fallback/value/fallback");
    }

    [Test]
    public async Task Convention_Mapping_Uses_Constructor_Defaults_And_Skips_ByRef_Constructors()
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int Number, int Label);

            public sealed class OptionalOutput
            {
                public OptionalOutput(int number, string label = "preset") => Result = $"{number}/{label}";
                public string Result { get; }
            }

            public sealed class ByRefOutput
            {
                public ByRefOutput(ref int number) => Number = -number;
                public ByRefOutput() { }
                public int Number { get; init; }
            }

            [ValiantMapper]
            public sealed partial class OptionalMapper : MapperDefinition<Input, OptionalOutput>;

            [ValiantMapper]
            public sealed partial class ByRefMapper : MapperDefinition<Input, ByRefOutput>;

            public static class TestRunner
            {
                public static string Run()
                {
                    var source = new Input(42, 7);
                    return $"{new OptionalMapper().Map(source).Result}/{new ByRefMapper().Map(source).Number}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;
        await Assert.That(actual).IsEqualTo("42/preset/42");
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Readonly_ByRef_Constructor_Arguments_Map_In_Both_Modes(ValiantMappingMode mode)
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int Value);
            public sealed class Output
            {
                public Output(in long value) => Value = value;
                public long Value { get; }
            }
            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public override void Configure(MappingContext<Input, Output> context)
                {
                    context.Map(input => input.Value, target => target.Value);
                }
            }
            public static class TestRunner
            {
                public static long Run(ValiantMappingMode mode) =>
                    new TestMapper(new ValiantMappingOptions { Mode = mode }).Map(new Input(42)).Value;
            }
            """);

        var actual = (long)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [mode])!;
        await Assert.That(actual).IsEqualTo(42);
    }

    [Test]
    public async Task Flattening_Reads_Nested_Interface_And_Nullable_Struct_Members_Once()
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public interface IItem { int Count { get; } }
            public sealed record Item(int Count) : IItem;
            public readonly record struct Part(int Number);

            public sealed class Input
            {
                private int _reads;
                public int Reads => _reads;
                public IItem? Item => ++_reads == 1 ? new Item(42) : null;
                public Part? Part { get; init; }
                public Box Box { get; } = new();
            }
            public sealed class Box { public int Value => 8; }
            public sealed record Output(int ItemCount, int PartNumber, int BoxValue);

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>;

            public static class TestRunner
            {
                public static string Run()
                {
                    var source = new Input { Part = new Part(7) };
                    var mapper = new TestMapper();
                    var result = mapper.Map(source);
                    return $"{source.Reads}/{result.ItemCount}/{result.PartNumber}/{result.BoxValue}/{mapper.Map(new Input()).PartNumber}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;
        await Assert.That(actual).IsEqualTo("1/42/7/8/0");
    }

    [Test]
    public async Task Unflattening_Supports_Nullable_Structs_And_Skips_Incomplete_Nested_Required_Members()
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int CoordinateX, int AddressZip);
            public struct Coordinate { public int X { get; init; } }
            public sealed class Address
            {
                public required string Street { get; init; }
                public int Zip { get; init; }
            }
            public sealed class Output
            {
                public required Coordinate? Coordinate { get; init; }
                public Address? Address { get; init; }
            }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>;

            public static class TestRunner
            {
                public static string Run()
                {
                    var result = new TestMapper().Map(new Input(42, 1234));
                    return $"{result.Coordinate?.X}/{result.Address is null}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;
        await Assert.That(actual).IsEqualTo("42/True");
    }

    [Test]
    public async Task Convention_Mapping_Skips_Incompatible_Multidimensional_Collection_Shapes()
    {
        var assembly = CompileAndLoad(
            """
            using StaticNorth.Valiant.Mapping;

            public sealed record Input(int[,] Matrix, int[] Array);
            public sealed class Output
            {
                public long[]? Matrix { get; init; }
                public long[,]? Array { get; init; }
            }

            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>;

            public static class TestRunner
            {
                public static bool Run()
                {
                    var result = new TestMapper().Map(new Input(new int[1, 1], new int[1]));
                    return result.Matrix is null && result.Array is null;
                }
            }
            """);

        var actual = (bool)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [])!;
        await Assert.That(actual).IsTrue();
    }

    [Test]
    [Arguments(ValiantMappingMode.Fast)]
    [Arguments(ValiantMappingMode.Debuggable)]
    public async Task Mapping_Custom_Enumerable_Uses_Its_Generic_Contract(ValiantMappingMode mode)
    {
        var assembly = CompileAndLoad(
            """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using StaticNorth.Valiant.Mapping;

            public sealed class Rows : IReadOnlyCollection<int>
            {
                int IReadOnlyCollection<int>.Count => 2;
                public CharEnumerator GetEnumerator() => "unrelated".GetEnumerator();
                IEnumerator<int> IEnumerable<int>.GetEnumerator() => ((IEnumerable<int>)new[] { 42, 7 }).GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
            }
            public sealed class Stream : IEnumerable<int>
            {
                public int Count => -1;
                public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)new[] { 8 }).GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            public readonly struct Bag : IEnumerable<int>
            {
                public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)new[] { 13 }).GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }
            public sealed record Input(Rows Rows, Stream Stream, Bag? Bag);
            public sealed class Output
            {
                public long[]? Rows { get; init; }
                public List<long>? Stream { get; init; }
                public List<long>? Bag { get; init; }
            }
            [ValiantMapper]
            public sealed partial class TestMapper : MapperDefinition<Input, Output>
            {
                public override void Configure(MappingContext<Input, Output> context)
                {
                    context.Map(input => input.Rows, output => output.Rows);
                    context.Map(input => input.Stream, output => output.Stream);
                    context.Map(input => input.Bag, output => output.Bag);
                }
            }
            public static class TestRunner
            {
                public static string Run(ValiantMappingMode mode)
                {
                    var mapper = new TestMapper(new ValiantMappingOptions { Mode = mode });
                    var result = mapper.Map(new Input(new Rows(), new Stream(), new Bag()));
                    return $"{string.Join(",", result.Rows!)}/{string.Join(",", result.Stream!)}/{string.Join(",", result.Bag!)}/{mapper.Map(new Input(new Rows(), new Stream(), null)).Bag is null}";
                }
            }
            """);

        var actual = (string)assembly.GetType("TestRunner", throwOnError: true)!.GetMethod("Run")!.Invoke(null, [mode])!;
        await Assert.That(actual).IsEqualTo("42,7/8/13/True");
    }
}
