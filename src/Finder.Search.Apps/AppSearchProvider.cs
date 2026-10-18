using Finder.Core.Interfaces;
using Finder.Core.Models;

namespace Finder.Search.Apps;

public sealed class AppSearchProvider : ISearchProvider
{
    private readonly Lazy<IReadOnlyList<string>> _shortcuts = new(IndexShortcuts, LazyThreadSafetyMode.ExecutionAndPublication);
    public string Name => "Apps";

    public Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IEnumerable<SearchResult>>(
                Array.Empty<SearchResult>());
        }

        return Task.Run<IEnumerable<SearchResult>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _shortcuts.Value
                .Where(path => Path.GetFileNameWithoutExtension(path)
                    .Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .Select(path => new SearchResult(
                    Title: Path.GetFileNameWithoutExtension(path),
                    Description: "Application",
                    Kind: "App",
                    Target: path))
                .ToList();
        }, cancellationToken);
    }

    private static IReadOnlyList<string> IndexShortcuts()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(
                Environment.SpecialFolder.StartMenu),

            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonStartMenu)
        };

        var shortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)))
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            var visited = 0;
            while (pending.Count > 0 && visited++ < 5000)
            {
                var directory = pending.Pop();
                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly)) shortcuts.Add(file);
                    foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                    {
                        try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return shortcuts.ToArray();
    }
}
