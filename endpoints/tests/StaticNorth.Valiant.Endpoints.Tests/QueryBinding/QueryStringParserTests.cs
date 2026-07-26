using StaticNorth.Valiant.Endpoints.AspNetCore;

namespace StaticNorth.Valiant.Endpoints.Tests.QueryBinding;

public sealed class QueryStringParserTests
{
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

    private sealed class FlatFilterQuery
    {
        public string? Brand { get; set; }

        public int Year { get; set; }
    }

    private sealed class NestedFilterQuery
    {
        public FilterQuery? Filter { get; set; }
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
