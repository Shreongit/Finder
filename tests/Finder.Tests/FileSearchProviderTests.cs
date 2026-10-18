using Finder.Search.Files;

namespace Finder.Tests;

public sealed class FileSearchProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Finder.Tests", Guid.NewGuid().ToString("N"));

    public FileSearchProviderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task FindsExactAndPartialFileNamesAndRanksExactFirst()
    {
        File.WriteAllText(Path.Combine(_root, "needle"), "exact");
        File.WriteAllText(Path.Combine(_root, "needle-notes.txt"), "prefix");
        File.WriteAllText(Path.Combine(_root, "my-needle-notes.txt"), "partial");

        var results = (await new FileSearchProvider([_root]).SearchAsync("needle")).ToArray();

        Assert.Equal("needle", results[0].Title);
        Assert.Equal("File", results[0].Kind);
        Assert.Equal(Path.Combine(_root, "needle"), results[0].Target);
        Assert.Contains(results, result => result.Title == "needle-notes.txt");
        Assert.Contains(results, result => result.Title == "my-needle-notes.txt");
    }

    [Fact]
    public async Task FindsMatchingFoldersAndNestedEntries()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "target-folder")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(folder, "nested")).FullName;
        File.WriteAllText(Path.Combine(nested, "deep-target.txt"), "nested");

        var results = (await new FileSearchProvider([_root]).SearchAsync("target")).ToArray();

        Assert.Contains(results, result => result.Kind == "Folder" && result.Target == folder);
        Assert.Contains(results, result => result.Kind == "File" && result.Target == Path.Combine(nested, "deep-target.txt"));
    }

    [Fact]
    public async Task DeduplicatesResultsWhenSearchRootsOverlap()
    {
        var nestedRoot = Directory.CreateDirectory(Path.Combine(_root, "nested-root")).FullName;
        var path = Path.Combine(nestedRoot, "duplicate-match.txt");
        File.WriteAllText(path, "match");

        var results = await new FileSearchProvider([_root, nestedRoot]).SearchAsync("duplicate");

        Assert.Single(results, result => result.Target == path);
    }

    [Fact]
    public async Task HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileSearchProvider([_root]).SearchAsync("anything", cancellation.Token));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
