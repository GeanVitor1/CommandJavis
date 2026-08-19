namespace Vox;

public static class SearchEngines
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube"] = "https://www.youtube.com/results?search_query={q}",
        ["google"] = "https://www.google.com/search?q={q}",
        ["bing"] = "https://www.bing.com/search?q={q}",
        ["duckduckgo"] = "https://duckduckgo.com/?q={q}",
        ["wikipedia"] = "https://pt.wikipedia.org/w/index.php?search={q}",
        ["spotify"] = "https://open.spotify.com/search/{q}",
        ["deezer"] = "https://www.deezer.com/search/{q}",
        ["netflix"] = "https://www.netflix.com/search?q={q}",
        ["amazon"] = "https://www.amazon.com.br/s?k={q}",
        ["mercadolivre"] = "https://lista.mercadolivre.com.br/{q}",
        ["steam"] = "https://store.steampowered.com/search/?term={q}",
        ["maps"] = "https://www.google.com/maps/search/{q}",
        ["twitter"] = "https://twitter.com/search?q={q}",
        ["x"] = "https://twitter.com/search?q={q}",
        ["instagram"] = "https://www.instagram.com/explore/tags/{q}",
        ["github"] = "https://github.com/search?q={q}",
        ["stackoverflow"] = "https://stackoverflow.com/search?q={q}",
        ["imdb"] = "https://www.imdb.com/find?q={q}",
        ["g1"] = "https://g1.globo.com/busca/?q={q}"
    };

    public static string? Build(string? searchTemplate, string target, string query)
    {
        var q = Uri.EscapeDataString(query);

        if (!string.IsNullOrWhiteSpace(searchTemplate) && searchTemplate.Contains("{q}"))
            return searchTemplate.Replace("{q}", q, StringComparison.Ordinal);

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return null;

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        var key = BaseDomain(host);

        if (Templates.TryGetValue(key, out var template))
            return template.Replace("{q}", q, StringComparison.Ordinal);

        return null;
    }

    public static bool SupportsSearch(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return false;
        return Templates.ContainsKey(BaseDomain(uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase)));
    }

    private static string BaseDomain(string host)
    {
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
            return parts.Length == 0 ? host : parts[0];
        if (parts[^2] is "com" or "org" or "net" or "gov" or "edu" or "co")
            return parts[^3];
        return parts[^2];
    }
}