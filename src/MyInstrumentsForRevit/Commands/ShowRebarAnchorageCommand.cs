using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Windows;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowRebarAnchorageCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ShowWindow();
            return Result.Succeeded;
        }

        public static void ShowWindow()
        {
            var window = new RebarAnchorageWindow();
            window.ShowDialog();
        }
    }
}
