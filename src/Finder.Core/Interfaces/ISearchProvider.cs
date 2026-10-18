using Finder.Core.Models;

namespace Finder.Core.Interfaces;

public interface ISearchProvider
{
    string Name { get; }

    Task<IEnumerable<SearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}