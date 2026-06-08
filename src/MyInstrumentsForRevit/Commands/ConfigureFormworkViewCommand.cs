using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Graphics;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConfigureFormworkViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            View view = document.ActiveView;

            using (var transaction = new Transaction(document, "Configure formwork view"))
            {
                transaction.Start();
                ViewGraphicsService.ApplyStructuralCategorySettings(document, view, 4, true);
                ViewGraphicsService.SetCategoriesHidden(document, view, StructuralGraphicsCategories.RebarCategories, true);
                ViewGraphicsService.HideLinksAndImportedCategories(document, view);
                transaction.Commit();
            }

            return Result.Succeeded;
        }
    }
}
