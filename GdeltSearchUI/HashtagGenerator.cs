using System.Text.RegularExpressions;

namespace GdeltSearchUI;

internal static class HashtagGenerator
{
    // Words to skip entirely
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "is", "was", "are", "were", "be", "been",
        "being", "have", "has", "had", "do", "does", "did", "will", "would",
        "could", "should", "may", "might", "shall", "can", "not", "no", "nor",
        "so", "as", "if", "it", "its", "this", "that", "these", "those",
        "he", "she", "they", "we", "you", "his", "her", "their", "our",
        "who", "what", "when", "where", "why", "how", "all", "any", "after",
        "before", "during", "also", "about", "into", "up", "out", "more",
        "just", "than", "then", "over", "says", "said", "say", "calls",
        "warns", "hits", "kills", "wins", "loses", "faces", "seeks", "plans",
        "wants", "gets", "makes", "takes", "gives", "comes", "goes", "looks",
        "report", "reports", "amid", "following", "amid", "via", "per",
        "against", "without", "within", "between", "among", "around",
    };

    // Words that need a companion noun — never used as standalone hashtags
    private static readonly HashSet<string> ModifierWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "mass", "major", "senior", "former", "chief", "lead", "grand",
        "late", "early", "high", "low", "top", "key", "head", "deputy",
        "anti", "pro", "ex", "near", "dead", "live", "full", "open",
        "armed", "deadly", "fatal", "alleged", "suspected", "multiple",
        "federal", "local", "state", "national", "global", "international",
    };

    private static readonly Regex NonAlphanumeric = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static string[] Generate(string headline, int max = 3)
    {
        var words = headline
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => NonAlphanumeric.Replace(w, ""))
            .Where(w => w.Length >= 3)
            .ToArray();

        var tags = new List<string>();
        var i = 0;

        while (i < words.Length && tags.Count < max)
        {
            var word = words[i];

            if (StopWords.Contains(word)) { i++; continue; }

            if (ModifierWords.Contains(word))
            {
                // Pair with the next usable word to form a compound hashtag
                var j = i + 1;
                while (j < words.Length && (StopWords.Contains(words[j]) || words[j].Length < 3)) j++;

                if (j < words.Length && !ModifierWords.Contains(words[j]))
                {
                    tags.Add(Normalize(word) + Normalize(words[j]));
                    i = j + 1;
                }
                else
                {
                    i++; // no suitable pair — skip the modifier
                }
                continue;
            }

            tags.Add(Normalize(word));
            i++;
        }

        return tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(max).ToArray();
    }

    // Acronyms (NATO, FBI) stay uppercase; everything else is PascalCase.
    private static string Normalize(string word) =>
        word.All(char.IsUpper) && word.Length <= 5
            ? word
            : char.ToUpper(word[0]) + word[1..].ToLower();
}
