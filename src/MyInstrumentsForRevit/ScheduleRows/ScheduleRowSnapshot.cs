using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace MyInstrumentsForRevit.ScheduleRows
{
    internal sealed class ScheduleRowSnapshot
    {
        private ScheduleRowSnapshot(IReadOnlyList<string> headers, IReadOnlyList<ScheduleRow> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<ScheduleRow> Rows { get; }

        public static ScheduleRowSnapshot Create(Document document, ViewSchedule schedule)
        {
            if (!FilteredElementCollector.IsViewValidForElementIteration(document, schedule.Id))
                throw new InvalidOperationException("Revit не позволяет получить элементы этой спецификации.");

            TableSectionData header = schedule.GetTableData().GetSectionData(SectionType.Header);
            TableSectionData body = schedule.GetTableData().GetSectionData(SectionType.Body);
            int columnCount = body.NumberOfColumns;
            var headers = new List<string>();
            for (int column = 0; column < columnCount; column++)
            {
                int columnNumber = body.FirstColumnNumber + column;
                string title = header.NumberOfRows > 0
                    && columnNumber >= header.FirstColumnNumber
                    && columnNumber <= header.LastColumnNumber
                    ? schedule.GetCellText(SectionType.Header, header.LastRowNumber, columnNumber)
                    : string.Empty;
                headers.Add(string.IsNullOrWhiteSpace(title) ? "Столбец " + (column + 1) : title);
            }

            List<Element> elements = new FilteredElementCollector(document, schedule.Id)
                .WhereElementIsNotElementType().ToElements()
                .Where(element => element != null && element.IsValidObject)
                .GroupBy(element => element.Id.IntegerValue).Select(group => group.First()).ToList();

            var rows = new List<ScheduleRow>();
            for (int rowNumber = body.FirstRowNumber; rowNumber <= body.LastRowNumber; rowNumber++)
            {
                List<string> cells = Enumerable.Range(0, columnCount)
                    .Select(column => schedule.GetCellText(SectionType.Body, rowNumber, body.FirstColumnNumber + column) ?? string.Empty).ToList();
                if (cells.All(string.IsNullOrWhiteSpace)) continue;

                List<ElementId> matches = elements.Where(element => MatchesRow(element, body, rowNumber, cells))
                    .Select(element => element.Id).ToList();
                rows.Add(new ScheduleRow(cells, matches));
            }
            return new ScheduleRowSnapshot(headers, rows);
        }

        private static bool MatchesRow(Element element, TableSectionData body, int rowNumber, IReadOnlyList<string> cells)
        {
            bool hasComparableCell = false;
            for (int column = 0; column < cells.Count; column++)
            {
                ElementId parameterId = body.GetCellParamId(rowNumber, body.FirstColumnNumber + column);
                if (parameterId == ElementId.InvalidElementId) continue;

                Parameter? parameter = FindParameter(element, parameterId)
                    ?? FindParameter(element.Document.GetElement(element.GetTypeId()), parameterId);
                if (parameter == null) continue;

                hasComparableCell = true;
                if (!string.Equals(GetDisplayValue(parameter), cells[column], StringComparison.CurrentCultureIgnoreCase)) return false;
            }
            return hasComparableCell;
        }

        private static Parameter? FindParameter(Element? element, ElementId parameterId)
        {
            if (element == null) return null;
            return element.Parameters.Cast<Parameter>().FirstOrDefault(parameter => parameter.Id == parameterId);
        }

        private static string GetDisplayValue(Parameter parameter)
        {
            string? formatted = parameter.AsValueString();
            if (!string.IsNullOrEmpty(formatted)) return formatted;
            return parameter.StorageType == StorageType.String ? parameter.AsString() ?? string.Empty : string.Empty;
        }
    }

    internal sealed class ScheduleRow
    {
        public ScheduleRow(IReadOnlyList<string> cells, IReadOnlyCollection<ElementId> elementIds)
        {
            Cells = cells;
            ElementIds = elementIds;
        }
        public IReadOnlyList<string> Cells { get; }
        public IReadOnlyCollection<ElementId> ElementIds { get; }
    }
}
