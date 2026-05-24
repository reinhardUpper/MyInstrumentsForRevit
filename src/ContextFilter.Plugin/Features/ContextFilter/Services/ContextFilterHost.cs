using Autodesk.Revit.DB;
using ContextFilter.Core.Models;
using ContextFilter.Core.Services;
using ContextFilter.Plugin.Infrastructure;
using ContextFilter.Plugin.RevitContext;

namespace ContextFilter.Plugin.Services;

/// <summary>
/// Production Revit implementation of the context filter host gateway.
/// </summary>
public sealed class ContextFilterHost : IContextFilterHost
{
    private readonly ExternalEventDispatcher _dispatcher;
    private readonly IRevitContext _context;
    private readonly SelectionService _selectionService;
    private SelectionSnapshot _currentSnapshot = SelectionSnapshot.Empty;
    private FilterElementSource _elementSource = FilterElementSource.SelectedElements;
    private ElementPreFilterOptions _preFilterOptions = ElementPreFilterOptions.Default;

    /// <summary>Creates the Revit-backed host.</summary>
    public ContextFilterHost(
        ExternalEventDispatcher dispatcher,
        IRevitContext context,
        SelectionService selectionService)
    {
        _dispatcher = dispatcher;
        _context = context;
        _selectionService = selectionService;
    }

    /// <inheritdoc />
    public event EventHandler<SelectionSnapshot>? SelectionSnapshotChanged;

    /// <inheritdoc />
    public SelectionSnapshot CurrentSnapshot => _currentSnapshot;

    /// <inheritdoc />
    public FilterElementSource ElementSource => _elementSource;

    /// <inheritdoc />
    public void SetElementSource(FilterElementSource source)
    {
        _elementSource = source;
    }

    /// <inheritdoc />
    public void SetPreFilterOptions(ElementPreFilterOptions options)
    {
        _preFilterOptions = options;
    }

    /// <inheritdoc />
    public Task<SelectionSnapshot> RefreshSelectionAsync()
    {
        return RefreshSelectionAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public Task<SelectionSnapshot> RefreshSelectionAsync(CancellationToken cancellationToken = default)
    {
        return _dispatcher.InvokeAsync(
            uiApplication =>
            {
                _context.Update(uiApplication);
                var snapshot = _selectionService.BuildSnapshot(_preFilterOptions);
                PublishSnapshot(snapshot);
                return snapshot;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<SelectionSnapshot> RefreshActiveViewElementsAsync(CancellationToken cancellationToken = default)
    {
        return _dispatcher.InvokeAsync(
            uiApplication =>
            {
                _context.Update(uiApplication);
                var snapshot = _selectionService.BuildActiveViewSnapshot(_preFilterOptions);
                PublishSnapshot(snapshot);
                return snapshot;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task SelectAsync(IReadOnlyCollection<int> elementIds, CancellationToken cancellationToken = default)
    {
        if (elementIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(
            uiApplication =>
            {
                _context.Update(uiApplication);
                var ids = elementIds.Select(id => new ElementId(id)).ToList();
                _context.UiDocument.Selection.SetElementIds(ids);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task IsolateAsync(IReadOnlyCollection<int> elementIds, CancellationToken cancellationToken = default)
    {
        if (elementIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(
            uiApplication =>
            {
                _context.Update(uiApplication);
                var document = _context.Document;
                var view = document.ActiveView;
                var ids = elementIds.Select(id => new ElementId(id)).ToList();

                using var transaction = new Transaction(document, "Context Filter: Isolate");
                transaction.Start();
                if (view.IsTemporaryHideIsolateActive())
                {
                    view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                }

                view.IsolateElementsTemporary(ids);
                transaction.Commit();
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearIsolationAsync(CancellationToken cancellationToken = default)
    {
        return _dispatcher.InvokeAsync(
            uiApplication =>
            {
                _context.Update(uiApplication);
                var document = _context.Document;
                var view = document.ActiveView;

                if (!view.IsTemporaryHideIsolateActive())
                {
                    return;
                }

                using var transaction = new Transaction(document, "Context Filter: Clear Isolation");
                transaction.Start();
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                transaction.Commit();
            },
            cancellationToken);
    }

    private void PublishSnapshot(SelectionSnapshot snapshot)
    {
        if (snapshot.Signature == _currentSnapshot.Signature)
        {
            _currentSnapshot = snapshot;
            return;
        }

        _currentSnapshot = snapshot;
        SelectionSnapshotChanged?.Invoke(this, snapshot);
    }
}
