using ContextFilter.Core.Models;

namespace ContextFilter.UI.ViewModels;

/// <summary>
/// Visual collector row with a named element set.
/// </summary>
public sealed class CollectorItemViewModel
{
    /// <summary>Creates a collector item from a tree node.</summary>
    public CollectorItemViewModel(FilterNodeViewModel node)
    {
        Name = node.Name;
        Kind = node.Kind;
        Count = node.Count;
        ElementIds = node.ElementIds.ToList();
    }

    /// <summary>Display name copied from the source tree node.</summary>
    public string Name { get; }

    /// <summary>Source node kind.</summary>
    public FilterNodeKind Kind { get; }

    /// <summary>Number of elements in this collected set.</summary>
    public int Count { get; }

    /// <summary>Collected Revit element ids.</summary>
    public IReadOnlyCollection<int> ElementIds { get; }

    /// <summary>Compact label for the UI.</summary>
    public string Label => $"{Name} ({Count})";
}
