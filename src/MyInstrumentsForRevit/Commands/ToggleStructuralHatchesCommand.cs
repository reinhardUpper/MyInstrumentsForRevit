using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Graphics;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleStructuralHatchesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            View view = document.ActiveView;
            View targetView = ViewGraphicsService.GetGraphicsTargetView(document, view);

            using (var transaction = new Transaction(document, "Toggle structural hatches"))
            {
                transaction.Start();

                if (CategoryGraphicsStateStore.HasSavedState(document, targetView))
                {
                    CategoryGraphicsStateStore.Restore(document, targetView);
                }
                else
                {
                    CategoryGraphicsStateStore.Save(document, targetView, StructuralGraphicsCategories.MainCategories);
                    ViewGraphicsService.HideStructuralCategoryPatterns(document, targetView);
                }

                transaction.Commit();
            }

            return Result.Succeeded;
        }
    }
}
