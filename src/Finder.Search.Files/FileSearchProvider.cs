using Finder.Core.Interfaces;
using Finder.Core.Models;

namespace Finder.Search.Files;

/// <summary>Searches the current user's standard folders and profile, with a bounded breadth-first scan.</summary>
public sealed class FileSearchProvider : ISearchProvider
{
    private readonly string[] _searchRoots;
    private const int MaxResults = 50;
    private const int MaxDirectoriesVisited = 2_000;
    private const int MaxEntriesVisited = 150_000;

    public string Name => "Files";

    public FileSearchProvider(IEnumerable<string>? searchRoots = null)
    {
        _searchRoots = (searchRoots ?? GetDefaultRoots())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(SafeFullPath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IEnumerable<SearchResult>>(Array.Empty<SearchResult>());

        var normalized = query.Trim();
        return Task.Run<IEnumerable<SearchResult>>(() => SearchFiles(normalized, cancellationToken), cancellationToken);
    }

    private IEnumerable<SearchResult> SearchFiles(string query, CancellationToken token)
    {
        var directories = new Queue<string>();
        var scheduledDirectories = new HashSet<string>(_searchRoots, StringComparer.OrdinalIgnoreCase);
        foreach (var root in _searchRoots) directories.Enqueue(root);

        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Only keep paths for candidates in the bounded result queue. Retaining every
        // visited path here made broad scans consume memory proportional to all entries.
        var candidateTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new PriorityQueue<(SearchResult Result, int Rank, int Order), (int Rank, int Order)>();
        var entriesVisited = 0;
        var order = 0;

        while (directories.Count > 0 && visitedDirectories.Count < MaxDirectoriesVisited && entriesVisited < MaxEntriesVisited)
        {
            token.ThrowIfCancellationRequested();
            var directory = directories.Dequeue();
            if (!visitedDirectories.Add(directory)) continue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();
                    if (++entriesVisited > MaxEntriesVisited) break;
                    AddCandidate(file, "File", query, candidateTargets, candidates, ref order);
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();
                    if (++entriesVisited > MaxEntriesVisited) break;

                    var reparsePoint = false;
                    try { reparsePoint = (File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0; }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (System.Security.SecurityException) { continue; }

                    AddCandidate(subdirectory, "Folder", query, candidateTargets, candidates, ref order);
                    if (!reparsePoint)
                    {
                        var fullPath = SafeFullPath(subdirectory);
                        if (fullPath is not null && directories.Count + visitedDirectories.Count < MaxDirectoriesVisited
                            && scheduledDirectories.Add(fullPath))
                            directories.Enqueue(fullPath);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            catch (System.Security.SecurityException) { }
        }

        token.ThrowIfCancellationRequested();
        return candidates.UnorderedItems.Select(item => item.Element).OrderBy(item => item.Rank)
            .ThenBy(item => item.Order)
            .Take(MaxResults)
            .Select(item => item.Result)
            .ToArray();
    }

    private static void AddCandidate(string path, string kind, string query,
        HashSet<string> candidateTargets,
        PriorityQueue<(SearchResult Result, int Rank, int Order), (int Rank, int Order)> candidates,
        ref int order)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path.AsSpan()));
        var rank = GetMatchRank(name, query.AsSpan());
        if (rank == int.MaxValue) return;

        // Canonicalize only matching paths. Most entries never become results, so
        // avoiding a full-path allocation for them keeps broad profile scans lean.
        var fullPath = SafeFullPath(path);
        if (fullPath is null || candidateTargets.Contains(fullPath)) return;

        var candidateOrder = order++;
        var candidate = (new SearchResult(name.ToString(), fullPath, kind, fullPath), rank, candidateOrder);
        var priority = (-rank, -candidateOrder);
        if (candidates.Count < MaxResults)
        {
            candidates.Enqueue(candidate, priority);
            candidateTargets.Add(fullPath);
        }
        else if (candidates.TryPeek(out _, out var worstPriority) && priority.CompareTo(worstPriority) > 0)
        {
            var removed = candidates.Dequeue();
            candidateTargets.Remove(removed.Result.Target!);
            candidates.Enqueue(candidate, priority);
            candidateTargets.Add(fullPath);
        }
    }

    internal static int GetMatchRank(string name, string query)
        => GetMatchRank(name.AsSpan(), query.AsSpan());

    private static int GetMatchRank(ReadOnlySpan<char> name, ReadOnlySpan<char> query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
        return int.MaxValue;
    }

    private static string? SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException) { return null; }
    }

    private static IEnumerable<string> GetDefaultRoots()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(profile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            profile
        };
    }
}
