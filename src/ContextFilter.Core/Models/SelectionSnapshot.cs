namespace ContextFilter.Core.Models;

/// <summary>
/// Immutable cached representation of the current Revit selection.
/// </summary>
public sealed record SelectionSnapshot(
    IReadOnlyList<FilterNode> Roots,
    IReadOnlyList<int> ElementIds,
    int TotalCount,
    string Signature,
    DateTimeOffset CreatedAt)
{
    /// <summary>Empty snapshot used before Revit supplies the first selection.</summary>
    public static SelectionSnapshot Empty { get; } =
        new(Array.Empty<FilterNode>(), Array.Empty<int>(), 0, string.Empty, DateTimeOffset.MinValue);
}
