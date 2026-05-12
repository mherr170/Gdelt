namespace GdeltSearchUI;

public static class ArticleFilter
{
    private static readonly string[] AllowedTlds = [".com", ".net", ".org"];

    public static bool IsAllowedDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return AllowedTlds.Any(tld => uri.Host.EndsWith(tld));
    }

    public static List<GdeltArticle> FilterByDomain(IEnumerable<GdeltArticle> articles) =>
        articles.Where(a => IsAllowedDomain(a.Url)).ToList();

    public static List<GdeltArticle> FilterByTitle(IEnumerable<GdeltArticle> articles, string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return articles
            .Where(a => words.All(w => a.Title.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
