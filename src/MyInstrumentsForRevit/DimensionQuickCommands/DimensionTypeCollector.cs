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
            return GetAvailableTypes(doc);
        }

        public static List<NamedElementInfo> GetAvailableTypes(Document doc)
        {
            if (doc == null)
            {
                return new List<NamedElementInfo>();
            }

            var dimensionTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(IsLinearDimensionType)
                .Select(type => new NamedElementInfo
                {
                    Id = type.Id,
                    IntegerId = type.Id.IntegerValue,
                    UniqueId = type.UniqueId ?? string.Empty,
                    Kind = QuickCommandKind.LinearDimension,
                    Name = type.Name ?? string.Empty,
                    DisplayName = $"Размер: {type.Name}"
                })
                .ToList();

            var detailItemTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(symbol => symbol.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DetailComponents)
                .Select(symbol => new NamedElementInfo
                {
                    Id = symbol.Id,
                    IntegerId = symbol.Id.IntegerValue,
                    UniqueId = symbol.UniqueId ?? string.Empty,
                    Kind = QuickCommandKind.DetailItem,
                    Name = symbol.Name ?? string.Empty,
                    DisplayName = $"Элемент узла: {GetDetailItemDisplayName(symbol)}"
                })
                .ToList();

            var types = dimensionTypes
                .Concat(detailItemTypes)
                .OrderBy(info => info.KindDisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(info => info.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(info => info.IntegerId)
                .ToList();

            foreach (var group in types.GroupBy(info => $"{info.Kind}\u001F{info.Name}", StringComparer.CurrentCultureIgnoreCase).Where(group => group.Count() > 1))
            {
                foreach (NamedElementInfo item in group)
                {
                    item.DisplayName = $"{item.DisplayName} [{item.IntegerId}]";
                }
            }

            return types;
        }

        private static string GetDetailItemDisplayName(FamilySymbol symbol)
        {
            string familyName = symbol.FamilyName ?? symbol.Family?.Name ?? string.Empty;
            return string.IsNullOrWhiteSpace(familyName)
                ? symbol.Name
                : $"{familyName}: {symbol.Name}";
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
