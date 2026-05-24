namespace ContextFilter.Core.Models;

/// <summary>
/// Distinct parameter value with the elements that contain it.
/// </summary>
public sealed record FilterParameterValue(
    string Value,
    IReadOnlyList<int> ElementIds)
{
    /// <summary>Number of elements that have this exact parameter value.</summary>
    public int Count => ElementIds.Count;
}
