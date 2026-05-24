using Autodesk.Revit.UI;

namespace ContextFilter.Plugin.Docking;

/// <summary>
/// Stable dockable pane identifiers.
/// </summary>
public static class PaneIds
{
    /// <summary>Unique id of the context filter pane.</summary>
    public static readonly DockablePaneId ContextFilterPaneId =
        new(new Guid("B902787B-C8B5-4F67-B9C9-63E4DF5A0E41"));
}
