using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ContextFilter.UI.Views;

namespace ContextFilter.Plugin;

/// <summary>
/// Ribbon command that opens the built-in snake game.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class ShowSnakeGameCommand : IExternalCommand
{
    /// <inheritdoc />
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var window = new SnakeGameWindow();
        window.Show();
        return Result.Succeeded;
    }
}
