using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ContextFilter.Plugin.RevitContext;

/// <summary>
/// Mutable Revit context scoped to the current API callback.
/// </summary>
public sealed class RevitContext : IRevitContext
{
    private UIApplication? _uiApplication;

    /// <inheritdoc />
    public UIApplication UiApplication =>
        _uiApplication ?? throw new InvalidOperationException("Revit context is not available.");

    /// <inheritdoc />
    public UIDocument UiDocument =>
        UiApplication.ActiveUIDocument ?? throw new InvalidOperationException("No active Revit document.");

    /// <inheritdoc />
    public Document Document => UiDocument.Document;

    /// <inheritdoc />
    public void Update(UIApplication uiApplication)
    {
        _uiApplication = uiApplication;
    }
}
