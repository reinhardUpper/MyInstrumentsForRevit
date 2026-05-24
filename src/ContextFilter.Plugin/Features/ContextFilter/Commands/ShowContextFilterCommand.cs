using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ContextFilter.Plugin;

/// <summary>
/// Ribbon command that opens the context filter dockable pane.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class ShowContextFilterCommand : IExternalCommand
{
    /// <inheritdoc />
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        App.ShowPane();
        return Result.Succeeded;
    }
}
