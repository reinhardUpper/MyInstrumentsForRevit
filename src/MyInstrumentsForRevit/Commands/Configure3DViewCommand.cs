using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Graphics;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class Configure3DViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            Document document = uiDocument.Document;

            if (!(document.ActiveView is View3D view3D) || view3D.IsTemplate)
            {
                TaskDialog.Show("Настроить 3D вид", "Откройте обычный 3D вид и запустите команду еще раз.");
                return Result.Cancelled;
            }

            using (var transaction = new Transaction(document, "Настроить 3D вид"))
            {
                transaction.Start();

                View targetView = ViewGraphicsService.GetGraphicsTargetView(document, view3D);
                ViewGraphicsService.SetDisplayStyleIfPossible(targetView, DisplayStyle.Shading);

                HideCategoryIfPossible(document, targetView, BuiltInCategory.OST_VolumeOfInterest);
                HideCategoryIfPossible(document, targetView, BuiltInCategory.OST_Levels);
                HideCategoryIfPossible(document, targetView, BuiltInCategory.OST_Grids);
                ViewGraphicsService.SetViewModelTransparency(targetView, 20);

                transaction.Commit();
            }

            return Result.Succeeded;
        }

        private static void HideCategoryIfPossible(Document document, View view, BuiltInCategory builtInCategory)
        {
            Category category = Category.GetCategory(document, builtInCategory);
            if (category == null)
            {
                return;
            }

            ElementId categoryId = category.Id;
            if (view.CanCategoryBeHidden(categoryId))
            {
                view.SetCategoryHidden(categoryId, true);
            }
        }

    }
}
