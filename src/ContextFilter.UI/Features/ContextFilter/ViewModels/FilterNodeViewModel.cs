using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ContextFilter.Core.Models;

namespace ContextFilter.UI.ViewModels;

/// <summary>
/// Presentation model for a grouped Revit selection node.
/// </summary>
public sealed partial class FilterNodeViewModel : ObservableObject
{
    private readonly FilterNode _model;

    /// <summary>Creates a view model from a domain node.</summary>
    public FilterNodeViewModel(FilterNode model)
    {
        _model = model;
        Children = new ObservableCollection<FilterNodeViewModel>(
            model.Children.Select(child => new FilterNodeViewModel(child)));
        Parameters = new ObservableCollection<ParameterRowViewModel>(
            model.Parameters.Select(parameter => new ParameterRowViewModel(parameter)));
    }

    /// <summary>Display name of the group.</summary>
    public string Name => _model.Name;

    /// <summary>Grouping level.</summary>
    public FilterNodeKind Kind => _model.Kind;

    /// <summary>Number of elements represented by this group.</summary>
    public int Count => _model.Count;

    /// <summary>Element ids represented by this group.</summary>
    public IReadOnlyCollection<int> ElementIds => _model.ElementIds;

    /// <summary>Cached text used for live name and parameter search.</summary>
    public string SearchText => _model.SearchText;

    /// <summary>Child groups.</summary>
    public ObservableCollection<FilterNodeViewModel> Children { get; }

    /// <summary>Cached parameters available for the node.</summary>
    public ObservableCollection<ParameterRowViewModel> Parameters { get; }

    /// <summary>Indicates whether the row is the active target for toolbar actions.</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>Returns true when this node or any descendant matches the query.</summary>
    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
            || SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
            || Children.Any(child => child.Matches(query));
    }

    /// <summary>Creates a filtered copy preserving matching descendants.</summary>
    public FilterNodeViewModel? Filter(string query, string parameterQuery)
    {
        if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(parameterQuery))
        {
            return new FilterNodeViewModel(_model);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (Kind != FilterNodeKind.Category)
            {
                return null;
            }

            return Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                || SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
                    ? new FilterNodeViewModel(_model)
                    : null;
        }

        var matchingChildren = Children
            .Select(child => child.Filter(query, parameterQuery))
            .Where(child => child is not null)
            .Cast<FilterNodeViewModel>()
            .ToList();

        var isParameterNode = Kind == FilterNodeKind.Parameter || Kind == FilterNodeKind.ParameterValue;
        var hasElementQuery = !string.IsNullOrWhiteSpace(query);
        var directNameMatches = !hasElementQuery
            || Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        var aggregateTextMatches = hasElementQuery
            && SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        var parameterMatches = string.IsNullOrWhiteSpace(parameterQuery)
            || (isParameterNode
                && (Name.IndexOf(parameterQuery, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || SearchText.IndexOf(parameterQuery, StringComparison.CurrentCultureIgnoreCase) >= 0));
        var ownMatch = isParameterNode ? parameterMatches : directNameMatches || aggregateTextMatches;

        if (!ownMatch && matchingChildren.Count == 0)
        {
            return null;
        }

        var copy = new FilterNodeViewModel(_model);
        bool shouldPruneChildren = matchingChildren.Count > 0
            && (!directNameMatches || !string.IsNullOrWhiteSpace(parameterQuery));
        if (!ownMatch || shouldPruneChildren)
        {
            copy.Children.Clear();
            foreach (var child in matchingChildren)
            {
                copy.Children.Add(child);
            }
        }

        return copy;
    }
}
