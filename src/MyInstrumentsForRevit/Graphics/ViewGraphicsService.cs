using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            View targetView = GetGraphicsTargetView(document, view);
            foreach (BuiltInCategory category in StructuralGraphicsCategories.MainCategories)
            {
                ApplyCategoryPatternSettings(document, targetView, category);
            }
        }

        public static View GetGraphicsTargetView(Document document, View view)
        {
            if (view.ViewTemplateId == ElementId.InvalidElementId)
            {
                return view;
            }

            return document.GetElement(view.ViewTemplateId) as View ?? view;
        }

        public static void HideLinksAndImportedCategories(Document document, View view)
        {
            SetCategoriesHidden(document, view, StructuralGraphicsCategories.LinkAndImportCategories, true);
            HideImportSubcategories(document, view);
            HideElementsOfClass<RevitLinkInstance>(document, view);
            HideElementsOfClass<ImportInstance>(document, view);
        }

        public static bool SetDisplayStyleIfPossible(View view, DisplayStyle displayStyle)
        {
            try
            {
                if (!view.CanModifyDisplayStyle())
                {
                    return false;
                }

                view.DisplayStyle = displayStyle;
                return true;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
        }

        public static void SetViewModelTransparency(View view, int transparency)
        {
            // Reflection keeps the command loadable in older Revit versions if this graphics API changes.
            try
            {
                MethodInfo getViewDisplayModel = view.GetType().GetMethod("GetViewDisplayModel", Type.EmptyTypes);
                if (getViewDisplayModel == null)
                {
                    return;
                }

                object viewDisplayModel = getViewDisplayModel.Invoke(view, null);
                if (viewDisplayModel == null)
                {
                    return;
                }

                PropertyInfo transparencyProperty = viewDisplayModel.GetType().GetProperty("Transparency");
                if (transparencyProperty == null || !transparencyProperty.CanWrite)
                {
                    return;
                }

                transparencyProperty.SetValue(viewDisplayModel, transparency, null);

                MethodInfo setViewDisplayModel = view.GetType().GetMethod("SetViewDisplayModel", new[] { viewDisplayModel.GetType() });
                setViewDisplayModel?.Invoke(view, new[] { viewDisplayModel });
            }
            catch (TargetInvocationException)
            {
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
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
                    settings.SetSurfaceBackgroundPatternVisible(false);
                    settings.SetCutForegroundPatternVisible(false);
                    settings.SetCutBackgroundPatternVisible(false);
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
                settings.SetSurfaceBackgroundPatternVisible(false);
                settings.SetCutForegroundPatternVisible(false);
                settings.SetCutBackgroundPatternVisible(false);
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

        private static void HideImportSubcategories(Document document, View view)
        {
            Category importsCategory = Category.GetCategory(document, BuiltInCategory.OST_ImportObjectStyles);
            if (importsCategory == null)
            {
                return;
            }

            foreach (Category subcategory in importsCategory.SubCategories)
            {
                if (subcategory == null || !view.CanCategoryBeHidden(subcategory.Id))
                {
                    continue;
                }

                view.SetCategoryHidden(subcategory.Id, true);
            }
        }

        private static void HideElementsOfClass<TElement>(Document document, View view)
            where TElement : Element
        {
            List<ElementId> ids = new FilteredElementCollector(document, view.Id)
                .OfClass(typeof(TElement))
                .WhereElementIsNotElementType()
                .Where(element => element.CanBeHidden(view))
                .Select(element => element.Id)
                .ToList();

            if (ids.Count > 0)
            {
                view.HideElements(ids);
            }
        }
    }
}
