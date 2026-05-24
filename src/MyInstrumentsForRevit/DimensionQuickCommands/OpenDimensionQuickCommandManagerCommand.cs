using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    [Transaction(TransactionMode.Manual)]
    public class OpenDimensionQuickCommandManagerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            if (uiapp.ActiveUIDocument?.Document == null)
            {
                TaskDialog.Show("Менеджер размерных команд", "Нет открытого документа Revit.");
                return Result.Cancelled;
            }

            var window = new DimensionQuickCommandManagerWindow(uiapp);
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
