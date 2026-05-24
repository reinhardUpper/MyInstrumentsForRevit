namespace ContextFilter.Core.Models;

/// <summary>
/// Represents one grouped selection node: category, family, or type.
/// </summary>
public sealed record FilterNode(
    string Name,
    FilterNodeKind Kind,
    int Count,
    IReadOnlyList<int> ElementIds,
    string SearchText,
    IReadOnlyList<FilterParameter> Parameters,
    IReadOnlyList<FilterNode> Children)
{
    /// <summary>Returns an empty node collection.</summary>
    public static readonly IReadOnlyList<FilterNode> EmptyChildren = Array.Empty<FilterNode>();
}
