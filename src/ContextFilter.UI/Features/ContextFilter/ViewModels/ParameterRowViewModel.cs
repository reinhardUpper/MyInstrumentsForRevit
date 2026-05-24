using ContextFilter.Core.Models;

namespace ContextFilter.UI.ViewModels;

/// <summary>
/// Presentation row for one aggregated Revit parameter.
/// </summary>
public sealed class ParameterRowViewModel
{
    private readonly FilterParameter _parameter;

    /// <summary>Creates a parameter row.</summary>
    public ParameterRowViewModel(FilterParameter parameter)
    {
        _parameter = parameter;
    }

    /// <summary>Parameter display name.</summary>
    public string Name => _parameter.Name;

    /// <summary>Short preview of values found in the selected group.</summary>
    public string Preview => _parameter.Preview;

    /// <summary>Number of distinct values found in the selected group.</summary>
    public int ValueCount => _parameter.Values.Count;

    /// <summary>Number of elements having this parameter in the selected group.</summary>
    public int ElementCount => _parameter.ElementCount;

    /// <summary>All cached values for the parameter.</summary>
    public IReadOnlyList<ParameterValueViewModel> Values =>
        _parameter.Values.Select(value => new ParameterValueViewModel(value)).ToList();

    /// <summary>True when the row or one of its values matches the query.</summary>
    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0
            || Values.Any(value => value.Value.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
    }
}
