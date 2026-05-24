using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ContextFilter.Plugin.RevitContext;

/// <summary>
/// Provides access to the active Revit application objects inside a valid API context.
/// </summary>
public interface IRevitContext
{
    /// <summary>Current Revit UI application.</summary>
    UIApplication UiApplication { get; }

    /// <summary>Current active UI document.</summary>
    UIDocument UiDocument { get; }

    /// <summary>Current active database document.</summary>
    Document Document { get; }

    /// <summary>Updates the context from an ExternalEvent or Revit callback.</summary>
    void Update(UIApplication uiApplication);
}
