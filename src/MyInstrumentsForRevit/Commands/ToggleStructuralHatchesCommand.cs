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

            using (var transaction = new Transaction(document, "Toggle structural hatches"))
            {
                transaction.Start();

                if (CategoryGraphicsStateStore.HasSavedState(document, view))
                {
                    CategoryGraphicsStateStore.Restore(document, view);
                }
                else
                {
                    CategoryGraphicsStateStore.Save(document, view, StructuralGraphicsCategories.MainCategories);
                    ViewGraphicsService.HideStructuralCategoryPatterns(document, view);
                }

                transaction.Commit();
            }

            return Result.Succeeded;
        }
    }
}
