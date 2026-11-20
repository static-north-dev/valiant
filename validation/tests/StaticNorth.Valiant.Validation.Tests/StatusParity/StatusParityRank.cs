namespace StaticNorth.Valiant.Validation.Tests.StatusParity;

public sealed record StatusParityRank(int Value) : IComparable<StatusParityRank>
{
    public int CompareTo(StatusParityRank? other) => Value.CompareTo(other?.Value ?? 0);

    public static bool operator <(StatusParityRank left, StatusParityRank right) => left.CompareTo(right) < 0;

    public static bool operator <=(StatusParityRank left, StatusParityRank right) => left.CompareTo(right) <= 0;

    public static bool operator >(StatusParityRank left, StatusParityRank right) => left.CompareTo(right) > 0;

    public static bool operator >=(StatusParityRank left, StatusParityRank right) => left.CompareTo(right) >= 0;

    public override string ToString() => throw new InvalidOperationException("comparison bound cannot be formatted");
}
