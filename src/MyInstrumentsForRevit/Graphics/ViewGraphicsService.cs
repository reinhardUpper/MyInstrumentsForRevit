using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Graphics
{
    internal static class ViewGraphicsService
    {
        public static void ApplyStructuralCategorySettings(
            Document document,
            View view,
            int lineWeight,
            bool hideSurfacePatterns)
        {
            foreach (BuiltInCategory category in StructuralGraphicsCategories.MainCategories)
            {
                ApplyCategorySettings(document, view, category, lineWeight, hideSurfacePatterns);
            }
        }

        public static void HideStructuralCategoryPatterns(Document document, View view)
        {
            foreach (BuiltInCategory category in StructuralGraphicsCategories.MainCategories)
            {
                ApplyCategoryPatternSettings(document, view, category);
            }
        }

        public static void SetCategoriesHidden(
            Document document,
            View view,
            IEnumerable<BuiltInCategory> categories,
            bool hidden)
        {
            foreach (BuiltInCategory builtInCategory in categories)
            {
                Category category = Category.GetCategory(document, builtInCategory);
                if (category == null)
                {
                    continue;
                }

                ElementId categoryId = category.Id;
                if (view.CanCategoryBeHidden(categoryId))
                {
                    view.SetCategoryHidden(categoryId, hidden);
                }
            }
        }

        private static void ApplyCategorySettings(
            Document document,
            View view,
            BuiltInCategory builtInCategory,
            int lineWeight,
            bool hideSurfacePatterns)
        {
            Category category = Category.GetCategory(document, builtInCategory);
            if (!CanOverrideCategory(category, view))
            {
                return;
            }

            try
            {
                OverrideGraphicSettings settings = view.GetCategoryOverrides(category.Id);
                settings.SetProjectionLineWeight(lineWeight);
                settings.SetCutLineWeight(lineWeight);

                if (hideSurfacePatterns)
                {
                    settings.SetSurfaceForegroundPatternVisible(false);
                    settings.SetCutForegroundPatternVisible(false);
                }

                view.SetCategoryOverrides(category.Id, settings);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Some categories are visible but cannot be overridden in certain view contexts.
            }
        }

        private static void ApplyCategoryPatternSettings(
            Document document,
            View view,
            BuiltInCategory builtInCategory)
        {
            Category category = Category.GetCategory(document, builtInCategory);
            if (!CanOverrideCategory(category, view))
            {
                return;
            }

            try
            {
                OverrideGraphicSettings settings = view.GetCategoryOverrides(category.Id);
                settings.SetSurfaceForegroundPatternVisible(false);
                settings.SetCutForegroundPatternVisible(false);
                view.SetCategoryOverrides(category.Id, settings);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Some categories are visible but cannot be overridden in certain view contexts.
            }
        }

        private static bool CanOverrideCategory(Category category, View view)
        {
            if (category == null)
            {
                return false;
            }

            try
            {
                return category.get_AllowsVisibilityControl(view);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }
        }
    }
}
