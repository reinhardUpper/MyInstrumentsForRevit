using ContextFilter.Core.Models;

namespace ContextFilter.UI.ViewModels;

/// <summary>
/// Presentation row for one distinct parameter value.
/// </summary>
public sealed class ParameterValueViewModel
{
    private readonly FilterParameterValue _value;

    /// <summary>Creates a parameter value row.</summary>
    public ParameterValueViewModel(FilterParameterValue value)
    {
        _value = value;
    }

    /// <summary>Parameter value text.</summary>
    public string Value => _value.Value;

    /// <summary>Number of elements that have this value.</summary>
    public int Count => _value.Count;

    /// <summary>Element ids that have this exact parameter value.</summary>
    public IReadOnlyCollection<int> ElementIds => _value.ElementIds;
}
