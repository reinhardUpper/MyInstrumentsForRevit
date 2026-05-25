using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

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

                view3D.DisplayStyle = DisplayStyle.Shading;

                HideCategoryIfPossible(document, view3D, BuiltInCategory.OST_VolumeOfInterest);
                HideCategoryIfPossible(document, view3D, BuiltInCategory.OST_Levels);
                HideCategoryIfPossible(document, view3D, BuiltInCategory.OST_Grids);
                SetModelCategoriesTransparency(document, view3D, 20);

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

        private static void SetModelCategoriesTransparency(Document document, View view, int transparency)
        {
            foreach (Category category in document.Settings.Categories)
            {
                if (category == null || category.CategoryType != CategoryType.Model)
                {
                    continue;
                }

                if (!category.get_AllowsVisibilityControl(view))
                {
                    continue;
                }

                OverrideGraphicSettings settings = view.GetCategoryOverrides(category.Id);
                settings.SetSurfaceTransparency(transparency);
                view.SetCategoryOverrides(category.Id, settings);
            }
        }
    }
}
