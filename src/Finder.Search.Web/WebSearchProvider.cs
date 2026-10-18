
using Finder.Core.Interfaces;
using Finder.Core.Models;

namespace Finder.Search.Web;

public sealed class WebSearchProvider : ISearchProvider
{
    public string Name => "Web";

    public Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IEnumerable<SearchResult>>(
                Array.Empty<SearchResult>());
        }

        var trimmedQuery = query.Trim();

        if (TryGetWebUrl(trimmedQuery, out var url))
        {
            IEnumerable<SearchResult> urlResult =
            [
                new SearchResult(
                    Title: url,
                    Description: "Open website in your browser",
                    Kind: "Web",
                    Target: url)
            ];

            return Task.FromResult(urlResult);
        }

        var searchUrl =
            "https://www.google.com/search?q=" +
            Uri.EscapeDataString(trimmedQuery);

        IEnumerable<SearchResult> results =
        [
            new SearchResult(
                Title: $"Search the web for \"{trimmedQuery}\"",
                Description: "Google Search",
                Kind: "Web",
                Target: searchUrl)
        ];

        return Task.FromResult(results);
    }

    private static bool TryGetWebUrl(
        string input,
        out string url)
    {
        url = string.Empty;

        var candidate = input.Contains("://", StringComparison.Ordinal)
            ? input
            : input.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                ? $"http://{input}"
                : input.Contains('.') && !input.Contains(' ')
                    ? $"https://{input}"
                    : string.Empty;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        url = uri.AbsoluteUri;
        return true;
    }
}
