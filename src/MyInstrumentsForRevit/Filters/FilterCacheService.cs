using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.Filters
{
    public static class FilterCacheService
    {
        private static IReadOnlyList<FilterItem> _filters = Array.Empty<FilterItem>();
        private static string _documentKey = string.Empty;

        public static IReadOnlyList<FilterItem> Filters => _filters;

        public static bool HasFiltersFor(Document document)
        {
            return _filters.Count > 0 && _documentKey == GetDocumentKey(document);
        }

        public static IReadOnlyList<FilterItem> Refresh(Document document)
        {
            _documentKey = GetDocumentKey(document);
            _filters = new FilteredElementCollector(document)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .Select(filter => new FilterItem(filter.Id, filter.Name))
                .OrderBy(filter => filter.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return _filters;
        }

        public static bool Exists(Document document, FilterItem item)
        {
            return document.GetElement(item.Id) is ParameterFilterElement;
        }

        private static string GetDocumentKey(Document document)
        {
            return string.IsNullOrWhiteSpace(document.PathName)
                ? document.GetHashCode().ToString()
                : document.PathName;
        }
    }
}

