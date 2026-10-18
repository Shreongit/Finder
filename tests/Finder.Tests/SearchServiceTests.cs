using Finder.Core.Interfaces;
using Finder.Core.Models;
using Finder.Core.Services;

namespace Finder.Tests;

public sealed class SearchServiceTests
{
    [Fact]
    public async Task EmptyQueryDoesNotCallProviders()
    {
        var provider = new StubProvider("apps", [Result("One")]);
        var service = new SearchService([provider]);
        Assert.Empty(await service.SearchAsync("  "));
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task ProviderFailureDoesNotHideSuccessfulResultsAndIsReported()
    {
        var successful = new StubProvider("apps", [Result("Calculator")]);
        var failed = new StubProvider("broken", exception: new InvalidOperationException("oops"));
        string? failedProvider = null;
        var service = new SearchService([successful, failed], (name, _) => failedProvider = name);
        var results = await service.SearchAsync("calc");
        Assert.Equal("broken", failedProvider);
        Assert.Equal("Calculator", Assert.Single(results).Title);
    }

    [Fact]
    public async Task CancellationPropagates()
    {
        var service = new SearchService([new StubProvider("cancel", cancellation: true)]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync("x"));
    }

    [Fact]
    public async Task ResultsAreDeduplicatedAndLimited()
    {
        var results = Enumerable.Range(0, 55)
            .Select(i => Result($"Item {i}")).Append(Result("item 0")).ToArray();
        var service = new SearchService([new StubProvider("many", results)]);
        var found = await service.SearchAsync("item");
        Assert.Equal(50, found.Count);
        Assert.Equal(50, found.Select(x => x.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task WebResultsFollowLocalResults()
    {
        var service = new SearchService([
            new StubProvider("web", [new SearchResult("Web", "", "Web", "https://example.com")]),
            new StubProvider("apps", [Result("Local")])]);
        var found = await service.SearchAsync("query");
        Assert.Equal(["Local", "Web"], found.Select(x => x.Title));
    }

    [Fact]
    public async Task ExactApplicationOutranksPartialFileMatch()
    {
        var service = new SearchService([new StubProvider("mixed", [
            new SearchResult("edge.svg", "C:\\icons\\edge.svg", "File", "C:\\icons\\edge.svg"),
            new SearchResult("Microsoft Edge", "Application", "App", "edge.lnk")])]);

        Assert.Equal("Microsoft Edge", (await service.SearchAsync("edge")).First().Title);
    }

    [Fact]
    public void ExactApplicationOutranksApplicationPrefix()
    {
        var found = SearchService.RankResults([
            new SearchResult("Edge Canary", "Application", "App", "canary.lnk"),
            new SearchResult(" Edge ", "Application", "App", "edge.lnk")], " edge ");

        Assert.Equal(" Edge ", found.First().Title);
    }

    [Fact]
    public void ExactFolderOutranksPartialFolderName()
    {
        var found = SearchService.RankResults([
            new SearchResult("Edge Backups", "C:\\Edge Backups", "Folder", "C:\\Edge Backups"),
            new SearchResult("Edge", "C:\\Edge", "Folder", "C:\\Edge")], "edge");

        Assert.Equal("Edge", found.First().Title);
    }

    [Fact]
    public void GenericWebSuggestionFollowsUsefulLocalResults()
    {
        var found = SearchService.RankResults([
            new SearchResult("Search the web for \"edge\"", "Google Search", "Web", "https://google.com/?q=edge"),
            new SearchResult("Edge", "C:\\Edge", "Folder", "C:\\Edge")], "edge");

        Assert.Equal("Folder", found.First().Kind);
    }

    [Fact]
    public void DuplicateTargetsAreRemovedCaseInsensitively()
    {
        var found = SearchService.RankResults([
            new SearchResult("Edge", "Application", "App", "C:\\Apps\\Edge.lnk"),
            new SearchResult("Microsoft Edge", "Application", "App", "c:\\apps\\edge.lnk")], "edge");

        Assert.Single(found);
    }

    [Fact]
    public void EqualRelevanceHasDeterministicTitleOrdering()
    {
        var found = SearchService.RankResults([
            new SearchResult("Edge Beta", "Application", "App", "b.lnk"),
            new SearchResult("Edge Alpha", "Application", "App", "a.lnk")], "edge");

        Assert.Equal(["Edge Alpha", "Edge Beta"], found.Select(x => x.Title));
    }

    [Fact]
    public void SettingsIntentAndExplicitUrlPrioritizeMatchingResultTypes()
    {
        var settings = SearchService.RankResults([
            new SearchResult("Microsoft Display Helper", "Application", "App", "display.lnk"),
            new SearchResult("Display settings", "Windows Settings", "Settings", "ms-settings:display")], "settings display");
        var url = SearchService.RankResults([
            new SearchResult("Search the web for \"example.com\"", "Google Search", "Web", "https://google.com"),
            new SearchResult("https://example.com/", "Open website in your browser", "Web", "https://example.com/")], "https://example.com");

        Assert.Equal("Settings", settings.First().Kind);
        Assert.Equal("https://example.com/", url.First().Title);
    }

    private static SearchResult Result(string title) => new(title, "", "App", title);

    private sealed class StubProvider(string name, IEnumerable<SearchResult>? results = null,
        Exception? exception = null, bool cancellation = false) : ISearchProvider
    {
        public string Name => name;
        public int CallCount { get; private set; }
        public Task<IEnumerable<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (cancellation) throw new OperationCanceledException(cancellationToken);
            if (exception is not null) throw exception;
            return Task.FromResult(results ?? Enumerable.Empty<SearchResult>());
        }
    }
}
