namespace ContextFilter.Core.Models;

/// <summary>
/// Defines which Revit elements are used to build the context filter tree.
/// </summary>
public enum FilterElementSource
{
    /// <summary>Use the active Revit selection.</summary>
    SelectedElements,

    /// <summary>Use selectable elements visible in the active view.</summary>
    CurrentView
}

