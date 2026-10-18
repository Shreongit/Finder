using Finder.Core.Interfaces;
using Finder.Core.Models;

namespace Finder.Core.Services;

public sealed class SearchService
{
    private readonly Action<string, Exception>? _logError;
    private readonly IReadOnlyList<ISearchProvider> _providers;

    public SearchService(IEnumerable<ISearchProvider> providers, Action<string, Exception>? logError = null)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        _logError = logError;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var searchTasks = _providers.Select(async provider =>
        {
            try
            {
                return await provider.SearchAsync(
                    query,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logError?.Invoke(provider.Name, ex);
                return Enumerable.Empty<SearchResult>();
            }
        });

        var results = await Task.WhenAll(searchTasks);

        cancellationToken.ThrowIfCancellationRequested();

        return RankResults(results.SelectMany(items => items), query);
    }

    /// <summary>Ranks and de-duplicates provider results without depending on the UI.</summary>
    public static IReadOnlyList<SearchResult> RankResults(IEnumerable<SearchResult> results, string query)
    {
        ArgumentNullException.ThrowIfNull(results);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0) return Array.Empty<SearchResult>();

        var hasUrlShape = normalizedQuery.Contains("://", StringComparison.Ordinal)
            || normalizedQuery.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
            || (normalizedQuery.Contains('.') && !normalizedQuery.Contains(' '));
        var urlCandidate = normalizedQuery.Contains("://", StringComparison.Ordinal)
            ? normalizedQuery
            : $"https://{normalizedQuery}";
        var websiteIntent = hasUrlShape
            && Uri.TryCreate(urlCandidate, UriKind.Absolute, out var parsedUrl)
            && (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps);
        var settingsIntent = normalizedQuery.Contains("settings", StringComparison.OrdinalIgnoreCase)
            || normalizedQuery.StartsWith("display", StringComparison.OrdinalIgnoreCase)
            || normalizedQuery.StartsWith("sound", StringComparison.OrdinalIgnoreCase)
            || normalizedQuery.StartsWith("wifi", StringComparison.OrdinalIgnoreCase)
            || normalizedQuery.StartsWith("bluetooth", StringComparison.OrdinalIgnoreCase);
        var filenameIntent = normalizedQuery.Contains('.') || normalizedQuery.Contains('\\') || normalizedQuery.Contains('/');

        return results
            .Where(result => result is not null)
            .DistinctBy(result => string.IsNullOrWhiteSpace(result.Target)
                ? $"{result.Kind}\0{result.Title}"
                : result.Target.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(result => GetCategoryRank(result, websiteIntent, settingsIntent, filenameIntent))
            .ThenBy(result => GetMatchRank(result.Title, normalizedQuery))
            .ThenBy(result => result.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Target ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Kind, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();
    }

    private static int GetCategoryRank(SearchResult result, bool websiteIntent, bool settingsIntent, bool filenameIntent)
    {
        if (websiteIntent) return result.Kind == "Web" ? 0 : 1;
        if (settingsIntent) return result.Kind == "Settings" ? 0 : result.Kind == "App" ? 1 : result.Kind == "Web" ? 4 : 2;
        if (filenameIntent) return result.Kind is "File" or "Folder" ? 0 : result.Kind == "App" ? 1 : result.Kind == "Settings" ? 2 : 3;

        return result.Kind switch
        {
            "App" => 0,
            "Folder" or "File" => 1,
            "Settings" => 2,
            "Web" when result.Description.Equals("Open website in your browser", StringComparison.OrdinalIgnoreCase) => 1,
            "Web" => 3,
            _ => 2
        };
    }

    private static int GetMatchRank(string title, string query)
    {
        var normalizedTitle = title.Trim();
        if (normalizedTitle.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (normalizedTitle.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (normalizedTitle.Contains(query, StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }
}
