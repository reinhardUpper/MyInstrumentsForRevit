namespace ContextFilter.Core.Models;

/// <summary>
/// Aggregated parameter values available for a grouped filter node.
/// </summary>
public sealed record FilterParameter(
    string Name,
    IReadOnlyList<FilterParameterValue> Values,
    int ElementCount)
{
    /// <summary>Returns a compact value preview for UI display.</summary>
    public string Preview => Values.Count == 0
        ? string.Empty
        : string.Join(", ", Values.Take(3).Select(value => value.Value)) + (Values.Count > 3 ? $" +{Values.Count - 3}" : string.Empty);
}
