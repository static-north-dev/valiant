using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Valiant.SourceGeneration;

/// <summary>
/// Stores diagnostic location data without retaining a syntax tree or compilation.
/// </summary>
internal readonly struct ValiantDiagnosticLocation : IEquatable<ValiantDiagnosticLocation>
{
    private ValiantDiagnosticLocation(
        bool hasLocation,
        string sourcePath,
        TextSpan sourceSpan,
        LinePositionSpan sourceLineSpan,
        string mappedPath,
        LinePositionSpan mappedLineSpan,
        bool hasMappedPath)
    {
        HasLocation = hasLocation;
        SourcePath = sourcePath;
        SourceSpan = sourceSpan;
        SourceLineSpan = sourceLineSpan;
        MappedPath = mappedPath;
        MappedLineSpan = mappedLineSpan;
        HasMappedPath = hasMappedPath;
    }

    public bool HasLocation { get; }

    public string SourcePath { get; }

    public TextSpan SourceSpan { get; }

    public LinePositionSpan SourceLineSpan { get; }

    public string MappedPath { get; }

    public LinePositionSpan MappedLineSpan { get; }

    public bool HasMappedPath { get; }

    public static ValiantDiagnosticLocation Create(Location? location)
    {
        if (location is null || location.Kind == LocationKind.None)
        {
            return default;
        }

        var source = location.GetLineSpan();
        var mapped = location.GetMappedLineSpan();
        return new ValiantDiagnosticLocation(
            true,
            source.Path ?? string.Empty,
            location.SourceSpan,
            source.Span,
            mapped.Path ?? string.Empty,
            mapped.Span,
            mapped.HasMappedPath);
    }

    public Location? ToLocation()
    {
        if (!HasLocation)
        {
            return null;
        }

        return HasMappedPath
            ? Location.Create(MappedPath, SourceSpan, MappedLineSpan)
            : Location.Create(SourcePath, SourceSpan, SourceLineSpan);
    }

    public bool Equals(ValiantDiagnosticLocation other)
    {
        return HasLocation == other.HasLocation &&
            string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal) &&
            SourceSpan.Equals(other.SourceSpan) &&
            SourceLineSpan.Equals(other.SourceLineSpan) &&
            string.Equals(MappedPath, other.MappedPath, StringComparison.Ordinal) &&
            MappedLineSpan.Equals(other.MappedLineSpan) &&
            HasMappedPath == other.HasMappedPath;
    }

    public override bool Equals(object? obj)
    {
        return obj is ValiantDiagnosticLocation other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = HasLocation.GetHashCode();
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(SourcePath ?? string.Empty);
            hashCode = (hashCode * 397) ^ SourceSpan.GetHashCode();
            hashCode = (hashCode * 397) ^ SourceLineSpan.GetHashCode();
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(MappedPath ?? string.Empty);
            hashCode = (hashCode * 397) ^ MappedLineSpan.GetHashCode();
            hashCode = (hashCode * 397) ^ HasMappedPath.GetHashCode();
            return hashCode;
        }
    }

    public override string ToString()
    {
        if (!HasLocation)
        {
            return string.Empty;
        }

        return (HasMappedPath ? MappedPath : SourcePath) + "|" +
            SourceSpan.Start.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
            SourceSpan.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
            (HasMappedPath ? MappedLineSpan : SourceLineSpan).ToString();
    }
}
