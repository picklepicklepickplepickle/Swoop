using System.Text.RegularExpressions;

namespace Swoop.Core;

public static partial class ProgressParser
{
    [GeneratedRegex(@"\[download\]\s*(?<pct>\d+(?:\.\d+)?)%(?!\w)")]
    private static partial Regex PercentRegex();

    /// <summary>Extracts the percent from a yt-dlp "[download] NN.N%" line, else null.</summary>
    public static double? Parse(string line)
    {
        if (line is null) return null;
        var m = PercentRegex().Match(line);
        return m.Success ? double.Parse(m.Groups["pct"].Value, System.Globalization.CultureInfo.InvariantCulture) : null;
    }
}

