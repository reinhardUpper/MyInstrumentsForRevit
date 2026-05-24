using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextFilter.Core.Models;
using ContextFilter.Core.Services;

namespace ContextFilter.UI.ViewModels;

/// <summary>
/// View model for the dockable context filter pane.
/// </summary>
public sealed partial class ContextFilterPaneViewModel : ObservableObject, IDisposable
{
    private readonly IContextFilterHost _host;
    private readonly INotificationService _notifications;
    private bool _disposed;

    /// <summary>Creates the pane view model.</summary>
    public ContextFilterPaneViewModel(IContextFilterHost host, INotificationService notifications)
    {
        _host = host;
        _notifications = notifications;
        Roots = new ObservableCollection<FilterNodeViewModel>();
        FilteredRoots = new ObservableCollection<FilterNodeViewModel>();
        CategoryFilters = new ObservableCollection<string>();
        ParameterRows = new ObservableCollection<ParameterRowViewModel>();
        FilteredParameterRows = new ObservableCollection<ParameterRowViewModel>();
        SelectedParameterValues = new ObservableCollection<ParameterValueViewModel>();
        CollectorItems = new ObservableCollection<CollectorItemViewModel>();
        _host.SelectionSnapshotChanged += OnSelectionSnapshotChanged;
        ApplySnapshot(_host.CurrentSnapshot);
    }

    /// <summary>Unfiltered root nodes.</summary>
    public ObservableCollection<FilterNodeViewModel> Roots { get; }

    /// <summary>Root nodes after live text filtering.</summary>
    public ObservableCollection<FilterNodeViewModel> FilteredRoots { get; }

    /// <summary>Available category filters built from the current cached snapshot.</summary>
    public ObservableCollection<string> CategoryFilters { get; }

    /// <summary>Cached parameter rows for the active tree node.</summary>
    public ObservableCollection<ParameterRowViewModel> ParameterRows { get; }

    /// <summary>Filtered parameter rows for the active tree node.</summary>
    public ObservableCollection<ParameterRowViewModel> FilteredParameterRows { get; }

    /// <summary>Values of the selected parameter row.</summary>
    public ObservableCollection<ParameterValueViewModel> SelectedParameterValues { get; }

    /// <summary>Visual collector with element groups added from the tree.</summary>
    public ObservableCollection<CollectorItemViewModel> CollectorItems { get; }

    /// <summary>Current selection total.</summary>
    [ObservableProperty]
    private int totalCount;

    /// <summary>Search text used for live filtering.</summary>
    [ObservableProperty]
    private string searchText = string.Empty;

    /// <summary>Selected category filter. The first option means all categories.</summary>
    [ObservableProperty]
    private string selectedCategoryFilter = "All categories";

    /// <summary>Search text used to filter parameter names and values.</summary>
    [ObservableProperty]
    private string parameterSearchText = string.Empty;

    /// <summary>Selected parameter row whose values are displayed below the parameter list.</summary>
    [ObservableProperty]
    private ParameterRowViewModel? selectedParameter;

    /// <summary>Active node used by toolbar commands.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
    [NotifyCanExecuteChangedFor(nameof(IsolateCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddToCollectorCommand))]
    private FilterNodeViewModel? selectedNode;

    /// <summary>Status text displayed at the bottom of the pane.</summary>
    [ObservableProperty]
    private string statusText = "No selection";

    /// <summary>Use the current Revit selection as the element source.</summary>
    [ObservableProperty]
    private bool useSelectedElements = true;

    /// <summary>Use all selectable elements visible in the active view as the element source.</summary>
    [ObservableProperty]
    private bool useCurrentViewElements;

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnParameterSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedParameterChanged(ParameterRowViewModel? value)
    {
        SelectedParameterValues.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var parameterValue in value.Values)
        {
            SelectedParameterValues.Add(parameterValue);
        }

        StatusText = $"{value.Name}: {value.ValueCount} values";
    }

    partial void OnUseSelectedElementsChanged(bool value)
    {
        if (!value)
        {
            if (!UseCurrentViewElements)
            {
                UseSelectedElements = true;
            }

            return;
        }

        if (UseCurrentViewElements)
        {
            UseCurrentViewElements = false;
        }

        _host.SetElementSource(FilterElementSource.SelectedElements);
        _ = RefreshAsync();
    }

    partial void OnUseCurrentViewElementsChanged(bool value)
    {
        if (!value)
        {
            if (!UseSelectedElements)
            {
                UseCurrentViewElements = true;
            }

            return;
        }

        if (UseSelectedElements)
        {
            UseSelectedElements = false;
        }

        _host.SetElementSource(FilterElementSource.CurrentView);
        _ = RefreshAsync();
    }

    /// <summary>Selects elements that have the clicked parameter value.</summary>
    [RelayCommand]
    private async Task SelectParameterValueAsync(ParameterValueViewModel? value)
    {
        if (value is null || value.ElementIds.Count == 0)
        {
            return;
        }

        await _host.SelectAsync(value.ElementIds).ConfigureAwait(true);
        StatusText = $"{value.Value}: {value.Count}";
    }

    /// <summary>Refreshes the tree from the configured Revit element source.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        var snapshot = UseCurrentViewElements
            ? await _host.RefreshActiveViewElementsAsync().ConfigureAwait(true)
            : await _host.RefreshSelectionAsync().ConfigureAwait(true);
        ApplySnapshot(snapshot);
        _notifications.ShowStatus($"{GetSourceLabel()} refreshed: {snapshot.TotalCount}");
    }

    /// <summary>Loads all selectable elements visible in the active Revit view without changing selection.</summary>
    [RelayCommand]
    private async Task LoadActiveViewAsync()
    {
        if (!UseCurrentViewElements)
        {
            UseCurrentViewElements = true;
            return;
        }

        var snapshot = await _host.RefreshActiveViewElementsAsync().ConfigureAwait(true);
        ApplySnapshot(snapshot);

        StatusText = $"Active view: {snapshot.TotalCount}";
    }

    /// <summary>Makes a clicked tree row active without changing the Revit selection.</summary>
    [RelayCommand]
    private void ActivateNode(FilterNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        SelectNode(node);
        StatusText = $"{node.Name}: {node.Count}";
    }

    /// <summary>Selects elements represented by the active group.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedNode))]
    private async Task SelectAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        await _host.SelectAsync(SelectedNode.ElementIds).ConfigureAwait(true);
        StatusText = $"Selected: {SelectedNode.Count}";
    }

    /// <summary>Isolates elements represented by the active group in the active view.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedNode))]
    private async Task IsolateAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        await _host.IsolateAsync(SelectedNode.ElementIds).ConfigureAwait(true);
        StatusText = $"Isolated: {SelectedNode.Count}";
    }

    /// <summary>Clears temporary isolate mode in the active Revit view.</summary>
    [RelayCommand]
    private async Task ClearIsolationAsync()
    {
        await _host.ClearIsolationAsync().ConfigureAwait(true);
        StatusText = "Isolation cleared";
    }

    /// <summary>Adds the active tree node to the visual collector.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedNode))]
    private void AddToCollector()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var existingIds = new HashSet<int>(CollectorItems.SelectMany(item => item.ElementIds));
        var idsToAdd = SelectedNode.ElementIds.Where(id => !existingIds.Contains(id)).ToList();

        if (idsToAdd.Count == 0)
        {
            StatusText = "Collector already contains this set";
            return;
        }

        CollectorItems.Add(new CollectorItemViewModel(SelectedNode));
        StatusText = $"Added to collector: {SelectedNode.Name} ({idsToAdd.Count})";
    }

    /// <summary>Selects all unique elements currently stored in the visual collector.</summary>
    [RelayCommand]
    private async Task SelectCollectorAsync()
    {
        var ids = GetCollectorElementIds();
        if (ids.Count == 0)
        {
            StatusText = "Collector is empty";
            return;
        }

        await _host.SelectAsync(ids).ConfigureAwait(true);
        StatusText = $"Collector selected: {ids.Count}";
    }

    /// <summary>Isolates all unique elements currently stored in the visual collector.</summary>
    [RelayCommand]
    private async Task IsolateCollectorAsync()
    {
        var ids = GetCollectorElementIds();
        if (ids.Count == 0)
        {
            StatusText = "Collector is empty";
            return;
        }

        await _host.IsolateAsync(ids).ConfigureAwait(true);
        StatusText = $"Collector isolated: {ids.Count}";
    }

    /// <summary>Clears the visual collector.</summary>
    [RelayCommand]
    private void ClearCollector()
    {
        CollectorItems.Clear();
        StatusText = "Collector cleared";
    }

    private bool HasSelectedNode() => SelectedNode is not null && SelectedNode.ElementIds.Count > 0;

    private IReadOnlyCollection<int> GetCollectorElementIds()
    {
        return CollectorItems
            .SelectMany(item => item.ElementIds)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private void OnSelectionSnapshotChanged(object? sender, SelectionSnapshot snapshot)
    {
        AppDispatcher.Invoke(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(SelectionSnapshot snapshot)
    {
        Roots.Clear();
        foreach (var root in snapshot.Roots.Select(node => new FilterNodeViewModel(node)))
        {
            Roots.Add(root);
        }

        RebuildCategoryFilters();
        TotalCount = snapshot.TotalCount;
        StatusText = snapshot.TotalCount == 0 ? $"No elements in {GetSourceLabel().ToLowerInvariant()}" : $"{GetSourceLabel()}: {snapshot.TotalCount}";
        ApplyFilter();
    }

    private string GetSourceLabel()
    {
        return UseCurrentViewElements ? "Current view" : "Selection";
    }

    private void ApplyFilter()
    {
        FilteredRoots.Clear();
        foreach (var root in Roots.Where(PassesCategoryFilter))
        {
            var filtered = root.Filter(SearchText, ParameterSearchText);
            if (filtered is not null)
            {
                FilteredRoots.Add(filtered);
            }
        }
    }

    private bool PassesCategoryFilter(FilterNodeViewModel root)
    {
        return string.IsNullOrWhiteSpace(SelectedCategoryFilter)
            || SelectedCategoryFilter == "All categories"
            || string.Equals(root.Name, SelectedCategoryFilter, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RebuildCategoryFilters()
    {
        var previous = SelectedCategoryFilter;
        CategoryFilters.Clear();
        CategoryFilters.Add("All categories");

        foreach (var category in Roots.Select(root => root.Name).Distinct().OrderBy(name => name))
        {
            CategoryFilters.Add(category);
        }

        SelectedCategoryFilter = CategoryFilters.Contains(previous) ? previous : "All categories";
    }

    private void SelectNode(FilterNodeViewModel node)
    {
        if (SelectedNode is not null)
        {
            SelectedNode.IsSelected = false;
        }

        SelectedNode = node;
        node.IsSelected = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _host.SelectionSnapshotChanged -= OnSelectionSnapshotChanged;
        _disposed = true;
    }
}
