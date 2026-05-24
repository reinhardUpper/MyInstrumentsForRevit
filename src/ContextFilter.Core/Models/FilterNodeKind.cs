namespace ContextFilter.Core.Models;

/// <summary>
/// Grouping level displayed in the context filter tree.
/// </summary>
public enum FilterNodeKind
{
    /// <summary>Top-level Revit category.</summary>
    Category,

    /// <summary>Family or system-family group.</summary>
    Family,

    /// <summary>Concrete element type.</summary>
    Type,

    /// <summary>Element or type parameter under a type node.</summary>
    Parameter,

    /// <summary>Distinct parameter value under a parameter node.</summary>
    ParameterValue
}
