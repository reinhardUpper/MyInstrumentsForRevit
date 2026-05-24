using ContextFilter.Core.Models;

namespace ContextFilter.Core.Services;

/// <summary>
/// UI-facing gateway for all Revit operations needed by the context filter.
/// Implementations must marshal work to Revit through ExternalEvent.
/// </summary>
public interface IContextFilterHost
{
    /// <summary>Raised when the cached selection tree is rebuilt.</summary>
    event EventHandler<SelectionSnapshot>? SelectionSnapshotChanged;

    /// <summary>Returns the latest cached snapshot without touching the Revit API.</summary>
    SelectionSnapshot CurrentSnapshot { get; }

    /// <summary>Gets the current source used to build the cached snapshot.</summary>
    FilterElementSource ElementSource { get; }

    /// <summary>Updates the source used to build the cached snapshot.</summary>
    void SetElementSource(FilterElementSource source);

    /// <summary>Updates lightweight element filters applied before parameter analysis.</summary>
    void SetPreFilterOptions(ElementPreFilterOptions options);

    /// <summary>Requests a full selection refresh in a valid Revit API context.</summary>
    Task<SelectionSnapshot> RefreshSelectionAsync();

    /// <summary>Requests a full selection refresh in a valid Revit API context.</summary>
    Task<SelectionSnapshot> RefreshSelectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds a grouped snapshot from all selectable elements visible in the active view.</summary>
    Task<SelectionSnapshot> RefreshActiveViewElementsAsync(CancellationToken cancellationToken = default);

    /// <summary>Selects the supplied Revit element ids.</summary>
    Task SelectAsync(IReadOnlyCollection<int> elementIds, CancellationToken cancellationToken = default);

    /// <summary>Temporarily isolates the supplied Revit element ids in the active view.</summary>
    Task IsolateAsync(IReadOnlyCollection<int> elementIds, CancellationToken cancellationToken = default);

    /// <summary>Clears temporary hide/isolate mode in the active view when it is active.</summary>
    Task ClearIsolationAsync(CancellationToken cancellationToken = default);
}
