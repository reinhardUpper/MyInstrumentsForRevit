using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Graphics;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleRebarCategoryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            View view = document.ActiveView;

            Category category = Category.GetCategory(document, BuiltInCategory.OST_Rebar);
            if (category == null)
            {
                TaskDialog.Show("Арматура", "Категория несущей арматуры не найдена в документе.");
                return Result.Cancelled;
            }

            if (!view.CanCategoryBeHidden(category.Id))
            {
                TaskDialog.Show("Арматура", "Активный вид не позволяет управлять видимостью категории несущей арматуры.");
                return Result.Cancelled;
            }

            bool shouldHide = !view.GetCategoryHidden(category.Id);

            using (var transaction = new Transaction(document, "Toggle rebar category"))
            {
                transaction.Start();
                ViewGraphicsService.SetCategoriesHidden(document, view, StructuralGraphicsCategories.RebarCategories, shouldHide);
                transaction.Commit();
            }

            return Result.Succeeded;
        }
    }
}

