using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Graphics
{
    internal static class CategoryGraphicsStateStore
    {
        private static readonly Dictionary<string, Dictionary<int, OverrideGraphicSettings>> SavedSettings =
            new Dictionary<string, Dictionary<int, OverrideGraphicSettings>>();

        public static bool HasSavedState(Document document, View view)
        {
            return SavedSettings.ContainsKey(BuildKey(document, view));
        }

        public static void Save(Document document, View view, IEnumerable<BuiltInCategory> categories)
        {
            var settings = new Dictionary<int, OverrideGraphicSettings>();
            foreach (BuiltInCategory builtInCategory in categories)
            {
                Category category = Category.GetCategory(document, builtInCategory);
                if (category == null)
                {
                    continue;
                }

                settings[category.Id.IntegerValue] = new OverrideGraphicSettings(view.GetCategoryOverrides(category.Id));
            }

            SavedSettings[BuildKey(document, view)] = settings;
        }

        public static bool Restore(Document document, View view)
        {
            string key = BuildKey(document, view);
            if (!SavedSettings.TryGetValue(key, out Dictionary<int, OverrideGraphicSettings> settings))
            {
                return false;
            }

            foreach (KeyValuePair<int, OverrideGraphicSettings> pair in settings)
            {
                view.SetCategoryOverrides(new ElementId(pair.Key), pair.Value);
            }

            SavedSettings.Remove(key);
            return true;
        }

        private static string BuildKey(Document document, View view)
        {
            string documentKey = string.IsNullOrWhiteSpace(document.PathName)
                ? document.GetHashCode().ToString()
                : document.PathName;

            return documentKey + ":" + view.Id.IntegerValue + ":" +
                string.Join(",", StructuralGraphicsCategories.MainCategories.Select(category => ((int)category).ToString()));
        }
    }
}

