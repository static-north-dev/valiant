using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Valiant.SourceGeneration;

internal static class ValiantFingerprint
{
    public static void AppendFingerprintValues(StringBuilder builder, ImmutableArray<string> values)
    {
        foreach (var value in values)
        {
            AppendFingerprintValue(builder, value);
        }
    }

    public static void AppendFingerprintValue(StringBuilder builder, string? value)
    {
        builder.Append(value?.Length.ToString(CultureInfo.InvariantCulture) ?? "-1");
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
