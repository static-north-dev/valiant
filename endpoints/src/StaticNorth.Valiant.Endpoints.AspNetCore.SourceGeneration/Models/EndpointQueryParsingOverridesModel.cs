using System.Collections.Immutable;

namespace Valiant.Endpoints.AspNetCore.SourceGeneration;

internal readonly struct EndpointQueryParsingOverridesModel(
    ImmutableArray<int> supportedArrayFormats,
    ImmutableArray<int> supportedObjectFormats,
    bool? enableJsonQueryValues) : IEquatable<EndpointQueryParsingOverridesModel>
{
    public ImmutableArray<int> SupportedArrayFormats { get; } = supportedArrayFormats;

    public ImmutableArray<int> SupportedObjectFormats { get; } = supportedObjectFormats;

    public bool? EnableJsonQueryValues { get; } = enableJsonQueryValues;

    public bool IsEmpty =>
        SupportedArrayFormats.IsDefault &&
        SupportedObjectFormats.IsDefault &&
        EnableJsonQueryValues is null;

    public bool Equals(EndpointQueryParsingOverridesModel other)
    {
        return ArraysEqual(SupportedArrayFormats, other.SupportedArrayFormats) &&
            ArraysEqual(SupportedObjectFormats, other.SupportedObjectFormats) &&
            EnableJsonQueryValues == other.EnableJsonQueryValues;
    }

    public override bool Equals(object? obj)
    {
        return obj is EndpointQueryParsingOverridesModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = AppendArrayHash(17, SupportedArrayFormats);
            hashCode = AppendArrayHash(hashCode, SupportedObjectFormats);
            hashCode = (hashCode * 397) ^ EnableJsonQueryValues.GetHashCode();
            return hashCode;
        }
    }

    private static bool ArraysEqual(ImmutableArray<int> left, ImmutableArray<int> right)
    {
        if (left.IsDefault || right.IsDefault)
        {
            return left.IsDefault == right.IsDefault;
        }

        return left.SequenceEqual(right);
    }

    private static int AppendArrayHash(int hashCode, ImmutableArray<int> values)
    {
        unchecked
        {
            hashCode = (hashCode * 397) ^ values.IsDefault.GetHashCode();
            if (!values.IsDefault)
            {
                foreach (var value in values)
                {
                    hashCode = (hashCode * 397) ^ value;
                }
            }

            return hashCode;
        }
    }
}
