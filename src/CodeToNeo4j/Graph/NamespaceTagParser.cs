using System.Text;

namespace CodeToNeo4j.Graph;

public static class NamespaceTagParser
{
    /// <summary>
    /// Splits a dot-separated namespace into underscore-separated tags.
    /// Pascal Case words are separated, but consecutive uppercase letters (acronyms) are kept together.
    /// Examples:
    ///   "Microsoft.DotNet.Cli"      → ["Microsoft", "Dot_Net", "Cli"]
    ///   "SomeApp.SomeFeature.BDC"   → ["Some_App", "Some_Feature", "BDC"]
    ///   "MyApp.HTTPClient.Core"     → ["My_App", "HTTP_Client", "Core"]
    /// </summary>
    public static IReadOnlyList<string> ParseTags(string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            return [];

        var tags = new List<string>();
        foreach (var segment in @namespace.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var tag = SegmentToTag(segment);
            if (!string.IsNullOrEmpty(tag))
                tags.Add(tag);
        }

        return tags;
    }

    private static string SegmentToTag(string segment)
    {
        if (string.IsNullOrEmpty(segment))
            return string.Empty;

        var words = new List<string>();
        var current = new StringBuilder();

        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];

            if (char.IsUpper(c))
            {
                if (current.Length > 0 && char.IsLower(current[^1]))
                {
                    // Transition from lowercase → uppercase: end current word
                    words.Add(current.ToString());
                    current.Clear();
                }
                else if (current.Length > 0 && char.IsUpper(current[^1])
                         && i + 1 < segment.Length && char.IsLower(segment[i + 1]))
                {
                    // End of an acronym run, next char is lowercase (e.g. "HTTP" + "Client")
                    words.Add(current.ToString());
                    current.Clear();
                }

                current.Append(c);
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            words.Add(current.ToString());

        return string.Join("_", words);
    }
}
