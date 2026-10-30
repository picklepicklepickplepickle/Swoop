using System.Text.RegularExpressions;

namespace Swoop.Core;

/// <summary>Pure link extraction from drag payloads (file paths, text lines).</summary>
public static partial class DragHelper
{
    [GeneratedRegex(@"(?:https?://\S+)|(?:[\w-]+(?:\.[\w-]+)*\.(?:com|net|org|edu|gov|be|io|tv|de|gg|xyz|me)/(?:\S*))")]
    private static partial Regex UrlRegex();

    /// <summary>
    /// Scans dropped strings for URLs (absolute or scheme-less domain/path),
    /// trims trailing punctuation, preserves first-seen order, dedupes.
    /// </summary>
    public static IReadOnlyList<string> ExtractLinks(IEnumerable<string> lines)
    {
        var seen = new List<string>();
        const string trailing = ".,;:!?)]}\"' \t";
        foreach (var line in lines ?? [])
        {
            foreach (System.Text.RegularExpressions.Match m in UrlRegex().Matches(line ?? ""))
            {
                var url = m.Value.TrimEnd(trailing.ToCharArray());
                if (url.Length > 0 && !seen.Contains(url)) seen.Add(url);
            }
        }
        return seen;
    }
}


