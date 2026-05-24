using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MyRevitTools.DimensionQuickCommands
{
    public static class DimensionTypeCollector
    {
        public static List<NamedElementInfo> GetDimensionTypes(Document doc)
        {
            if (doc == null)
            {
                return new List<NamedElementInfo>();
            }

            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(IsLinearDimensionType)
                .Select(type => new NamedElementInfo
                {
                    Id = type.Id,
                    IntegerId = type.Id.IntegerValue,
                    UniqueId = type.UniqueId ?? string.Empty,
                    Name = type.Name ?? string.Empty,
                    DisplayName = type.Name ?? string.Empty
                })
                .OrderBy(info => info.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(info => info.IntegerId)
                .ToList();

            foreach (var group in types.GroupBy(info => info.Name, StringComparer.CurrentCultureIgnoreCase).Where(group => group.Count() > 1))
            {
                foreach (NamedElementInfo item in group)
                {
                    item.DisplayName = $"{item.Name} [{item.IntegerId}]";
                }
            }

            return types;
        }

        private static bool IsLinearDimensionType(DimensionType type)
        {
            // Revit 2021 exposes DimensionType.StyleType. For other Revit versions, verify
            // whether the enum values still map to the same linear/aligned dimension families.
            return type.StyleType == DimensionStyleType.Linear
                || type.StyleType == DimensionStyleType.LinearFixed;
        }
    }
}
