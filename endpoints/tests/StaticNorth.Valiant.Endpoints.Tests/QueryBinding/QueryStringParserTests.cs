using System.Collections.Immutable;
using System.Collections.ObjectModel;
using StaticNorth.Valiant.Endpoints.AspNetCore;

namespace StaticNorth.Valiant.Endpoints.Tests.QueryBinding;

public sealed class QueryStringParserTests
{
    [Test]
    public async Task Parser_Rejects_Root_Scalars_And_Collections()
    {
        var scalar = QueryStringParser.Parse<int>("?value=42");
        var collection = QueryStringParser.Parse<int[]>("?value=42");
        var runtimeType = QueryStringParser.Parse("?value=42", typeof(List<>));

        await Assert.That(scalar.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
        await Assert.That(collection.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
        await Assert.That(runtimeType.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
    }

    [Test]
    public async Task Parser_Reports_Required_Nested_Property_With_Full_Path()
    {
        var result = QueryStringParser.Parse<RequiredNestedQuery>("?filter.year=2025", AllParsingFormats());

        await Assert.That(result.Errors.Single().Code).IsEqualTo("MissingQueryValue");
        await Assert.That(result.Errors.Single().Path).IsEqualTo("Filter.Brand");
    }

    [Test]
    [Arguments("?brand.name=volvo", "Brand")]
    [Arguments("?ids.value=42", "Ids")]
    [Arguments("?filter=unused&filter.year=2025", "Filter")]
    public async Task Parser_Reports_Object_And_Leaf_Shape_Mismatches(string query, string path)
    {
        var result = QueryStringParser.Parse<MixedQuery>(query, AllParsingFormats());

        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQueryValue");
        await Assert.That(result.Errors.Single().Path).IsEqualTo(path);
    }

    [Test]
    public async Task Legacy_Parser_Preserves_Shapes_And_Omitted_Required_Members()
    {
        var options = new QueryParsingOptions
        {
            UseStrictBinding = false,
            SupportedObjectFormats = [QueryObjectFormat.Flat, QueryObjectFormat.DotNotation]
        };
        var mixed = QueryStringParser.Parse<MixedQuery>("?brand=volvo&brand.name=ignored&ids.value=42&filter=unused&filter.year=2025", options);
        var missingRequired = QueryStringParser.Parse<RequiredNestedQuery>("?filter.year=2025", options);
        var rootArray = QueryStringParser.Parse<int[]>("?value=42", options);
        var bracketLiteral = QueryStringParser.Parse<StringArrayQuery>("?tags=%5B%5D", options);
        var encodedComma = QueryStringParser.Parse<StringArrayQuery>("?tags=a%2Cb", options);

        await Assert.That(mixed.IsSuccess).IsTrue();
        await Assert.That(mixed.Value!.Brand).IsEqualTo("volvo");
        await Assert.That(mixed.Value.Ids!).IsEmpty();
        await Assert.That(mixed.Value.Filter!.Year).IsEqualTo(2025);
        await Assert.That(missingRequired.IsSuccess).IsTrue();
        await Assert.That(rootArray.Value!).IsEmpty();
        await Assert.That(bracketLiteral.Errors.Single().Code).IsEqualTo("DisabledJsonQueryValues");
        await Assert.That(encodedComma.Errors.Single().Code).IsEqualTo("DisabledQueryArrayFormat");
    }

    [Test]
    public async Task Null_Parsing_Format_Arrays_Do_Not_Throw()
    {
        var result = QueryStringParser.Parse<MixedQuery>(
            "?brand=volvo",
            new QueryParsingOptions { SupportedArrayFormats = null!, SupportedObjectFormats = null! });

        await Assert.That(result.Errors.Single().Code).IsEqualTo("DisabledQueryObjectFormat");
    }

    [Test]
    public async Task Query_Property_Discovery_Uses_Only_The_Derived_Declaration()
    {
        var source = new DerivedPropertyQuery { Value = "ordered" };
        ((BasePropertyQuery)source).Value = 42;

        var serialized = QueryStringSerializer.Serialize(source);
        var parsed = QueryStringParser.Parse<DerivedPropertyQuery>(serialized.QueryString);

        await Assert.That(serialized.QueryString).IsEqualTo("?Value=ordered");
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Value).IsEqualTo("ordered");
        await Assert.That(((BasePropertyQuery)parsed.Value).Value).IsEqualTo(0);
    }

    [Test]
    public async Task Serializer_Uses_Public_Getters()
    {
        var serialized = QueryStringSerializer.Serialize(new SerializerPropertyQuery { Value = "internal", Page = 2 });

        await Assert.That(serialized.QueryString).IsEqualTo("?Page=2");
    }

    [Test]
    public async Task Nullable_Struct_Collections_Round_Trip_Through_Json()
    {
        var serialized = QueryStringSerializer.Serialize(
            new NullableCollectionQuery { Ids = ImmutableArray.Create(1, 2) },
            new QuerySerializationOptions { EnableJsonQueryValues = true });
        var parsed = QueryStringParser.Parse<NullableCollectionQuery>(serialized.QueryString, AllParsingFormats(enableJson: true));
        var delimited = QueryStringParser.Parse<NullableCollectionQuery>("?ids=1&ids=2");

        await Assert.That(serialized.IsSuccess).IsTrue();
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Ids!.Value).IsEquivalentTo([1, 2]);
        await Assert.That(delimited.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
    }

    [Test]
    [Arguments(QueryArrayFormat.Brackets, QueryObjectFormat.DotNotation)]
    [Arguments(QueryArrayFormat.IndexedBrackets, QueryObjectFormat.DotNotation)]
    [Arguments(QueryArrayFormat.Brackets, QueryObjectFormat.DeepObject)]
    [Arguments(QueryArrayFormat.IndexedBrackets, QueryObjectFormat.DeepObject)]
    public async Task Nested_Bracket_Collections_Round_Trip_And_Require_Both_Formats(
        QueryArrayFormat arrayFormat,
        QueryObjectFormat objectFormat)
    {
        var query = new { Filter = new ArrayQuery { Ids = [1, 2] } };
        var serialized = QueryStringSerializer.Serialize(query, SerializationOptions(arrayFormat, objectFormat));
        var parsed = QueryStringParser.Parse<NestedArrayQuery>(serialized.QueryString, new QueryParsingOptions
        {
            UseStrictBinding = true,
            SupportedArrayFormats = [arrayFormat],
            SupportedObjectFormats = [objectFormat]
        });
        var disabledObjects = QueryStringParser.Parse<NestedArrayQuery>(serialized.QueryString, new QueryParsingOptions
        {
            UseStrictBinding = true,
            SupportedArrayFormats = [arrayFormat]
        });
        var disabledArrays = QueryStringParser.Parse<NestedArrayQuery>(serialized.QueryString, new QueryParsingOptions
        {
            UseStrictBinding = true,
            SupportedObjectFormats = [objectFormat]
        });

        await Assert.That(serialized.IsSuccess).IsTrue();
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Filter!.Ids!.SequenceEqual([1, 2])).IsTrue();
        await Assert.That(disabledObjects.Errors.Select(static error => error.Code)).Contains("DisabledQueryObjectFormat");
        await Assert.That(disabledArrays.Errors.Select(static error => error.Code)).Contains("DisabledQueryArrayFormat");
    }

    [Test]
    public async Task Unsupported_Json_Collection_Returns_A_Structured_Error()
    {
        var parsed = QueryStringParser.Parse<ReadOnlyArrayQuery>("?ids=%5B1%2C2%5D", AllParsingFormats(enableJson: true));

        await Assert.That(parsed.IsSuccess).IsFalse();
        await Assert.That(parsed.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Single_Json_Like_String_Round_Trips(bool enableJson)
    {
        var serialized = QueryStringSerializer.Serialize(new StringArrayQuery { Tags = ["[]"] },
            new QuerySerializationOptions { EnableJsonQueryValues = enableJson });
        var parsed = QueryStringParser.Parse<StringArrayQuery>(serialized.QueryString, AllParsingFormats(enableJson));

        await Assert.That(serialized.IsSuccess).IsTrue();
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Tags.Single()).IsEqualTo("[]");
    }

    [Test]
    public async Task Complex_Collection_Requires_Json_For_Round_Trip()
    {
        var query = new ComplexArrayQuery { Filters = [new FilterQuery { Year = 2026 }] };
        var delimited = QueryStringSerializer.Serialize(query);
        var json = QueryStringSerializer.Serialize(query, new QuerySerializationOptions { EnableJsonQueryValues = true });
        var parsed = QueryStringParser.Parse<ComplexArrayQuery>(json.QueryString, AllParsingFormats(enableJson: true));

        await Assert.That(delimited.Errors.Single().Code).IsEqualTo("UnsupportedQueryType");
        await Assert.That(json.IsSuccess).IsTrue();
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Filters.Single().Year).IsEqualTo(2026);
    }

    [Test]
    public async Task Serializer_Uses_Runtime_Types_For_Object_Values_And_Declared_Types_For_Collections()
    {
        var query = new { Value = (object)42, Ids = (IEnumerable<int>)new HashSet<int> { 1 } };

        var serialized = QueryStringSerializer.Serialize(query);

        await Assert.That(serialized.IsSuccess).IsTrue();
        await Assert.That(serialized.QueryString).IsEqualTo("?Value=42&Ids=1");
    }

    [Test]
    public async Task Parser_Constructs_Value_Type_Query_Models()
    {
        var parsed = QueryStringParser.Parse<ValueQuery>("?page=2");

        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value.Page).IsEqualTo(2);
    }

    [Test]
    public async Task Serializer_Bounds_Object_Depth()
    {
        var query = new RecursiveQuery();
        query.Next = query;

        var serialized = QueryStringSerializer.Serialize(query, new QuerySerializationOptions { ObjectFormat = QueryObjectFormat.DotNotation });

        await Assert.That(serialized.Errors.Single().Code).IsEqualTo("QuerySerializationDepthExceeded");
    }

    [Test]
    public async Task Sparse_Array_Indices_Do_Not_Allocate_By_The_Untrusted_Index()
    {
        var result = QueryStringParser.Parse<ArrayQuery>("?ids[2147483647]=2&ids[0]=1", AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2]);
    }

    [Test]
    [Arguments("?year=2147483648")]
    [Arguments("?year=%00")]
    [Arguments("?year=%FF")]
    [Arguments("?year=1e999")]
    public async Task Malformed_Scalar_Values_Return_Errors_Without_Throwing(string query)
    {
        var result = QueryStringParser.Parse<FlatFilterQuery>(query);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    [Arguments("128")]
    [Arguments("-129")]
    public async Task Parser_Returns_Validation_Error_For_Enum_Overflow(string value)
    {
        var result = QueryStringParser.Parse<EnumQuery>("?value=" + value);

        await Assert.That(result.IsSuccess).IsFalse();
        var error = result.Errors.Single();
        await Assert.That(error.Code).IsEqualTo("InvalidQueryValue");
        await Assert.That(error.Kind).IsEqualTo(QueryErrorKind.Validation);
        await Assert.That(error.Path).IsEqualTo(nameof(EnumQuery.Value));
    }

    [Test]
    public async Task Parser_Reports_Incompatible_Collection_Shapes()
    {
        var result = QueryStringParser.Parse<SetQuery>("?tags=one&tags=two");

        await Assert.That(result.IsSuccess).IsFalse();
        var error = result.Errors.Single();
        await Assert.That(error.Code).IsEqualTo("UnsupportedQueryType");
        await Assert.That(error.Kind).IsEqualTo(QueryErrorKind.Validation);
        await Assert.That(error.Path).IsEqualTo(nameof(SetQuery.Tags));

        var json = QueryStringParser.Parse<SetQuery>(
            "?tags=%5B%22one%22%2C%22two%22%5D",
            AllParsingFormats(enableJson: true));
        await Assert.That(json.IsSuccess).IsTrue();
        await Assert.That(json.Value!.Tags).IsEquivalentTo(["one", "two"]);
    }

    [Test]
    public async Task Parser_Only_Binds_Public_Setters()
    {
        var result = QueryStringParser.Parse<SetterQuery>("?page=2&label=selected");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Page).IsEqualTo(2);
        await Assert.That(result.Value!.Label).IsEqualTo("default");
    }

    [Test]
    public void Query_Parser_Performance_Sample()
    {
        const string query = "?ids=1&ids=2&brand=tesla";
        for (var index = 0; index < 100; index++)
        {
            _ = QueryStringParser.Parse<MixedQuery>(query);
        }

        const int iterations = 10_000;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var index = 0; index < iterations; index++)
        {
            _ = QueryStringParser.Parse<MixedQuery>(query);
        }

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine(FormattableString.Invariant($"RUNTIME,Endpoints,query,{elapsed.TotalNanoseconds / iterations:F1} ns/op,{allocated / iterations} B/op"));
    }

    [Test]
    public async Task Parser_Accepts_Repeated_Array_Params_By_Default()
    {
        var result = QueryStringParser.Parse<ArrayQuery>("?ids=1&ids=2&ids=3");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Parser_Accepts_Comma_Separated_Array_Params_When_Enabled()
    {
        var result = QueryStringParser.Parse<ArrayQuery>(
            "?ids=1,2,3",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Parser_Accepts_Bracket_Array_Params_When_Enabled()
    {
        var result = QueryStringParser.Parse<ArrayQuery>(
            "?ids[]=1&ids[]=2&ids[]=3",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Parser_Accepts_Indexed_Bracket_Array_Params_And_Preserves_Index_Order_When_Enabled()
    {
        var result = QueryStringParser.Parse<ArrayQuery>(
            "?ids[2]=3&ids[0]=1&ids[1]=2",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task Mixed_Array_Formats_Keep_Their_Group_Order_And_Stable_Indices()
    {
        var result = QueryStringParser.Parse<ArrayQuery>(
            "?ids[2]=5&ids[]=3&ids=1,2&ids[0]=4&ids[2]=6",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids!.SequenceEqual([1, 2, 3, 4, 5, 6])).IsTrue();
    }

    [Test]
    public async Task Parser_Accepts_Flat_Object_Params_By_Default()
    {
        var result = QueryStringParser.Parse<FlatFilterQuery>("?brand=tesla&year=2025");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Brand).IsEqualTo("tesla");
        await Assert.That(result.Value!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Accepts_Dot_Notation_Object_Params_When_Enabled()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>(
            "?filter.brand=tesla&filter.year=2025",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Filter!.Brand).IsEqualTo("tesla");
        await Assert.That(result.Value!.Filter!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Accepts_Deep_Object_Params_When_Enabled()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>(
            "?filter[brand]=tesla&filter[year]=2025",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Filter!.Brand).IsEqualTo("tesla");
        await Assert.That(result.Value!.Filter!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Accepts_Json_Query_Values_When_Enabled()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>(
            "?filter=%7B%22brand%22%3A%22tesla%22%2C%22year%22%3A2025%7D",
            AllParsingFormats(enableJson: true));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Filter!.Brand).IsEqualTo("tesla");
        await Assert.That(result.Value!.Filter!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Accepts_Mixed_Array_And_Object_Params_When_Formats_Are_Enabled()
    {
        var result = QueryStringParser.Parse<MixedQuery>(
            "?ids=1,2,3&filter.brand=tesla&filter.year=2025",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2, 3]);
        await Assert.That(result.Value!.Filter!.Brand).IsEqualTo("tesla");
        await Assert.That(result.Value!.Filter!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Decodes_Url_Encoded_Values()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>(
            "?filter.brand=red%20tea%26coffee",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Filter!.Brand).IsEqualTo("red tea&coffee");
    }

    [Test]
    public async Task Parser_Returns_Validation_Error_For_Invalid_Integer_Conversion()
    {
        var result = QueryStringParser.Parse<FlatFilterQuery>("?year=not-an-int");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQueryValue");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Validation);
    }

    [Test]
    public async Task Parser_Returns_Validation_Error_For_Invalid_Guid_Conversion()
    {
        var result = QueryStringParser.Parse<GuidQuery>("?correlationId=not-a-guid");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQueryValue");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Validation);
    }

    [Test]
    public async Task Parser_Returns_Validation_Error_For_Empty_Array_Values()
    {
        var result = QueryStringParser.Parse<ArrayQuery>(
            "?ids=1,,3",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQueryValue");
    }

    [Test]
    public async Task Parser_Uses_Last_Value_For_Duplicate_Scalar_Params()
    {
        var result = QueryStringParser.Parse<FlatFilterQuery>("?year=2024&year=2025");

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Year).IsEqualTo(2025);
    }

    [Test]
    public async Task Parser_Supports_Primitive_Arrays_Lists_And_Nullable_Values()
    {
        var guid = Guid.Parse("5ff14552-ae09-4de1-a024-5cc432054b10");
        var result = QueryStringParser.Parse<PrimitiveQuery>(
            "?names=alpha,beta" +
            "&ids=1,2" +
            "&longIds=3,4" +
            "&guids=5ff14552-ae09-4de1-a024-5cc432054b10" +
            "&flags=true,false" +
            "&prices=1.5,2.75" +
            "&created=2026-07-04T10%3A00%3A00.0000000Z" +
            "&pageIds=5,6" +
            "&limit=",
            AllParsingFormats());

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.Names).IsEquivalentTo(["alpha", "beta"]);
        await Assert.That(result.Value!.Ids).IsEquivalentTo([1, 2]);
        await Assert.That(result.Value!.LongIds).IsEquivalentTo([3L, 4L]);
        await Assert.That(result.Value!.Guids).IsEquivalentTo([guid]);
        await Assert.That(result.Value!.Flags).IsEquivalentTo([true, false]);
        await Assert.That(result.Value!.Prices).IsEquivalentTo([1.5m, 2.75m]);
        await Assert.That(result.Value!.Created![0]).IsEqualTo(DateTime.Parse("2026-07-04T10:00:00.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind));
        await Assert.That(result.Value!.PageIds).IsEquivalentTo([5, 6]);
        await Assert.That(result.Value!.Limit).IsNull();
    }

    [Test]
    public async Task Parser_Rejects_Disabled_Array_Formats()
    {
        var result = QueryStringParser.Parse<ArrayQuery>("?ids[]=1&ids[]=2");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Select(static error => error.Code)).Contains("DisabledQueryArrayFormat");
        await Assert.That(result.Errors.All(static error => error.Kind == QueryErrorKind.Configuration)).IsTrue();
    }

    [Test]
    public async Task Parser_Rejects_Disabled_Object_Formats()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>("?filter.brand=tesla");

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("DisabledQueryObjectFormat");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Configuration);
    }

    [Test]
    public async Task Parser_Rejects_Json_Query_Values_When_Disabled()
    {
        var result = QueryStringParser.Parse<NestedFilterQuery>(
            "?filter=%7B%22brand%22%3A%22tesla%22%7D",
            AllParsingFormats(enableJson: false));

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("DisabledJsonQueryValues");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Configuration);
    }

    [Test]
    public async Task Parser_Defaults_Remain_Conservative()
    {
        var repeatedAndFlat = QueryStringParser.Parse<MixedQuery>("?ids=1&ids=2&brand=tesla");
        var comma = QueryStringParser.Parse<ArrayQuery>("?ids=1,2");
        var dot = QueryStringParser.Parse<NestedFilterQuery>("?filter.brand=tesla");
        var json = QueryStringParser.Parse<NestedFilterQuery>("?filter=%7B%22brand%22%3A%22tesla%22%7D");

        await Assert.That(repeatedAndFlat.IsSuccess).IsTrue();
        await Assert.That(repeatedAndFlat.Value!.Ids).IsEquivalentTo([1, 2]);
        await Assert.That(repeatedAndFlat.Value!.Brand).IsEqualTo("tesla");
        await Assert.That(comma.Errors.Single().Code).IsEqualTo("DisabledQueryArrayFormat");
        await Assert.That(dot.Errors.Single().Code).IsEqualTo("DisabledQueryObjectFormat");
        await Assert.That(json.Errors.Single().Code).IsEqualTo("DisabledJsonQueryValues");
    }

    [Test]
    public async Task Serializer_Emits_Repeated_Array_Params()
    {
        var result = QueryStringSerializer.Serialize(new { ids = new[] { 1, 2, 3 } });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?ids=1&ids=2&ids=3");
    }

    [Test]
    public async Task Serializer_Emits_Comma_Separated_Array_Params()
    {
        var result = QueryStringSerializer.Serialize(
            new { ids = new[] { 1, 2, 3 } },
            SerializationOptions(QueryArrayFormat.CommaSeparated, QueryObjectFormat.Flat));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?ids=1,2,3");
    }

    [Test]
    [Arguments(QueryArrayFormat.RepeatedKeys)]
    [Arguments(QueryArrayFormat.CommaSeparated)]
    [Arguments(QueryArrayFormat.Brackets)]
    [Arguments(QueryArrayFormat.IndexedBrackets)]
    public async Task String_Array_Round_Trips_Escaped_Separators(QueryArrayFormat format)
    {
        string[] tags = ["New York, NY", "Boston + Cambridge", "%2C"];
        var serialized = QueryStringSerializer.Serialize(new { tags }, SerializationOptions(format, QueryObjectFormat.Flat));
        var parsed = QueryStringParser.Parse<StringArrayQuery>(
            serialized.QueryString, new QueryParsingOptions { SupportedArrayFormats = [format], UseStrictBinding = true });

        await Assert.That(serialized.IsSuccess).IsTrue();
        await Assert.That(parsed.IsSuccess).IsTrue();
        await Assert.That(parsed.Value!.Tags.SequenceEqual(tags)).IsTrue();
    }

    [Test]
    public async Task Serializer_Emits_Bracket_Array_Params()
    {
        var result = QueryStringSerializer.Serialize(
            new { ids = new[] { 1, 2, 3 } },
            SerializationOptions(QueryArrayFormat.Brackets, QueryObjectFormat.Flat));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?ids[]=1&ids[]=2&ids[]=3");
    }

    [Test]
    public async Task Serializer_Emits_Indexed_Bracket_Array_Params()
    {
        var result = QueryStringSerializer.Serialize(
            new { ids = new[] { 1, 2, 3 } },
            SerializationOptions(QueryArrayFormat.IndexedBrackets, QueryObjectFormat.Flat));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?ids[0]=1&ids[1]=2&ids[2]=3");
    }

    [Test]
    public async Task Serializer_Emits_Flat_Object_Params()
    {
        var result = QueryStringSerializer.Serialize(new { brand = "tesla", year = 2025 });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?brand=tesla&year=2025");
    }

    [Test]
    public async Task Serializer_Emits_Dot_Notation_Object_Params()
    {
        var result = QueryStringSerializer.Serialize(
            new { filter = new { brand = "tesla", year = 2025 } },
            SerializationOptions(QueryArrayFormat.RepeatedKeys, QueryObjectFormat.DotNotation));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?filter.brand=tesla&filter.year=2025");
    }

    [Test]
    public async Task Serializer_Emits_Deep_Object_Params()
    {
        var result = QueryStringSerializer.Serialize(
            new { filter = new { brand = "tesla", year = 2025 } },
            SerializationOptions(QueryArrayFormat.RepeatedKeys, QueryObjectFormat.DeepObject));

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?filter[brand]=tesla&filter[year]=2025");
    }

    [Test]
    public async Task Serializer_Url_Encodes_Values()
    {
        var result = QueryStringSerializer.Serialize(new { term = "red tea&coffee" });

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.QueryString).IsEqualTo("?term=red%20tea%26coffee");
    }

    [Test]
    [Arguments(QueryArrayFormat.RepeatedKeys)]
    [Arguments(QueryArrayFormat.CommaSeparated)]
    [Arguments(QueryArrayFormat.Brackets)]
    [Arguments(QueryArrayFormat.IndexedBrackets)]
    public async Task Serializer_Omits_Empty_Collections(QueryArrayFormat format)
    {
        var encoded = QueryStringSerializer.Serialize(
            new { ids = Array.Empty<int>(), page = 2 },
            SerializationOptions(format, QueryObjectFormat.Flat));
        var decoded = QueryStringParser.Parse<ArrayQuery>(encoded.QueryString, AllParsingFormats());

        await Assert.That(encoded.IsSuccess).IsTrue();
        await Assert.That(encoded.QueryString).IsEqualTo("?page=2");
        await Assert.That(decoded.IsSuccess).IsTrue();
        await Assert.That(decoded.Value!.Ids).IsNull();
    }

    [Test]
    public async Task Serializer_Rejects_Invalid_Array_Format()
    {
        var result = QueryStringSerializer.Serialize(
            new { ids = new[] { 1, 2 } },
            new QuerySerializationOptions
            {
                ArrayFormat = (QueryArrayFormat)999
            });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQuerySerializationOptions");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Configuration);
    }

    [Test]
    public async Task Serializer_Rejects_Invalid_Object_Format()
    {
        var result = QueryStringSerializer.Serialize(
            new { filter = new { brand = "tesla" } },
            new QuerySerializationOptions
            {
                ObjectFormat = (QueryObjectFormat)999
            });

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Errors.Single().Code).IsEqualTo("InvalidQuerySerializationOptions");
        await Assert.That(result.Errors.Single().Kind).IsEqualTo(QueryErrorKind.Configuration);
    }

    private static QueryParsingOptions AllParsingFormats(bool enableJson = false)
    {
        return new QueryParsingOptions
        {
            UseStrictBinding = true,
            SupportedArrayFormats =
            [
                QueryArrayFormat.RepeatedKeys,
                QueryArrayFormat.CommaSeparated,
                QueryArrayFormat.Brackets,
                QueryArrayFormat.IndexedBrackets
            ],
            SupportedObjectFormats =
            [
                QueryObjectFormat.Flat,
                QueryObjectFormat.DotNotation,
                QueryObjectFormat.DeepObject
            ],
            EnableJsonQueryValues = enableJson
        };
    }

    private static QuerySerializationOptions SerializationOptions(
        QueryArrayFormat arrayFormat,
        QueryObjectFormat objectFormat)
    {
        return new QuerySerializationOptions
        {
            ArrayFormat = arrayFormat,
            ObjectFormat = objectFormat
        };
    }

    private sealed class ArrayQuery
    {
        public int[]? Ids { get; set; }
    }

    private sealed class NestedArrayQuery
    {
        public ArrayQuery? Filter { get; init; }
    }

    private sealed class ReadOnlyArrayQuery
    {
        public ReadOnlyCollection<int>? Ids { get; init; }
    }

    private sealed class SetterQuery
    {
        public int Page { get; init; }

        public string Label { get; private set; } = "default";
    }

    private sealed class SetQuery
    {
        public HashSet<string>? Tags { get; init; }
    }

    private sealed class EnumQuery
    {
        public QueryChoice Value { get; init; }
    }

    private enum QueryChoice : sbyte
    {
        Default
    }

    private sealed class StringArrayQuery
    {
        public string[] Tags { get; set; } = [];
    }

    private sealed class ComplexArrayQuery
    {
        public FilterQuery[] Filters { get; init; } = [];
    }

    private struct ValueQuery
    {
        public int Page { get; init; }
    }

    private sealed class RecursiveQuery
    {
        public RecursiveQuery? Next { get; set; }
    }

    private sealed class FlatFilterQuery
    {
        public string? Brand { get; set; }

        public int Year { get; set; }
    }

    private sealed class NestedFilterQuery
    {
        public FilterQuery? Filter { get; set; }
    }

    private sealed class RequiredNestedQuery
    {
        public RequiredFilter? Filter { get; init; }
    }

    private sealed class RequiredFilter
    {
        public required string Brand { get; init; }

        public int Year { get; init; }
    }

    private class BasePropertyQuery
    {
        public int Value { get; set; }
    }

    private sealed class DerivedPropertyQuery : BasePropertyQuery
    {
        public new string? Value { get; set; }
    }

    private sealed class SerializerPropertyQuery
    {
        public string? Value { private get; init; }

        public int Page { get; init; }
    }

    private sealed class NullableCollectionQuery
    {
        public ImmutableArray<int>? Ids { get; init; }
    }

    private sealed class MixedQuery
    {
        public int[]? Ids { get; set; }

        public string? Brand { get; set; }

        public FilterQuery? Filter { get; set; }
    }

    private sealed class FilterQuery
    {
        public string? Brand { get; set; }

        public int Year { get; set; }
    }

    private sealed class GuidQuery
    {
        public Guid CorrelationId { get; set; }
    }

    private sealed class PrimitiveQuery
    {
        public string[]? Names { get; set; }

        public int[]? Ids { get; set; }

        public long[]? LongIds { get; set; }

        public Guid[]? Guids { get; set; }

        public bool[]? Flags { get; set; }

        public decimal[]? Prices { get; set; }

        public DateTime[]? Created { get; set; }

        public List<int>? PageIds { get; set; }

        public int? Limit { get; set; }
    }
}
