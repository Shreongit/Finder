namespace Finder.Core.Models;

public sealed record SearchResult(
    string Title,
    string Description,
    string Kind,
    string? Target = null);