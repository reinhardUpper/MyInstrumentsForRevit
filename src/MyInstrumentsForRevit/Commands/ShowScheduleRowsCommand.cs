using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MyInstrumentsForRevit.ScheduleRows;
using MyInstrumentsForRevit.Windows;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowScheduleRowsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null || !(uiDocument.Document.ActiveView is ViewSheet sheet))
            {
                TaskDialog.Show("Строки спецификации", "Откройте лист и выберите размещённую спецификацию.");
                return Result.Cancelled;
            }
            Document document = uiDocument.Document;
            ScheduleSheetInstance? instance = GetSelectedSchedule(uiDocument, document, sheet) ?? PickSchedule(uiDocument, document, sheet);
            if (instance == null) return Result.Cancelled;

            ViewSchedule? schedule = document.GetElement(instance.ScheduleId) as ViewSchedule;
            if (schedule == null || schedule.IsTitleblockRevisionSchedule || schedule.Definition.IsKeySchedule)
            {
                TaskDialog.Show("Строки спецификации", "Выбранный элемент не является обычной спецификацией элементов.");
                return Result.Cancelled;
            }
            if (schedule.Definition.IncludeLinkedFiles)
            {
                TaskDialog.Show("Строки спецификации", "Спецификации с элементами из Revit-связей пока не поддерживаются.");
                return Result.Cancelled;
            }
            try
            {
                ScheduleRowSnapshot snapshot = ScheduleRowSnapshot.Create(document, schedule);
                if (snapshot.Rows.Count == 0)
                {
                    TaskDialog.Show("Строки спецификации", "В спецификации нет строк с элементами модели.");
                    return Result.Cancelled;
                }
                var window = new ScheduleRowsWindow(schedule.Name, snapshot.Headers, snapshot.Rows,
                    row => uiDocument.Selection.SetElementIds(row.ElementIds.ToList()));
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                TaskDialog.Show("Строки спецификации", "Не удалось прочитать спецификацию:\n" + exception.Message);
                return Result.Failed;
            }
        }

        private static ScheduleSheetInstance? GetSelectedSchedule(UIDocument uiDocument, Document document, ViewSheet sheet) =>
            uiDocument.Selection.GetElementIds().Select(document.GetElement).OfType<ScheduleSheetInstance>()
                .FirstOrDefault(item => item.OwnerViewId == sheet.Id && !item.IsTitleblockRevisionSchedule);

        private static ScheduleSheetInstance? PickSchedule(UIDocument uiDocument, Document document, ViewSheet sheet)
        {
            try
            {
                Reference reference = uiDocument.Selection.PickObject(ObjectType.Element, new ScheduleOnSheetFilter(sheet.Id), "Выберите размещённую спецификацию");
                return document.GetElement(reference) as ScheduleSheetInstance;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return null; }
        }

        private sealed class ScheduleOnSheetFilter : ISelectionFilter
        {
            private readonly ElementId _sheetId;
            public ScheduleOnSheetFilter(ElementId sheetId) => _sheetId = sheetId;
            public bool AllowElement(Element element) => element is ScheduleSheetInstance instance && instance.OwnerViewId == _sheetId && !instance.IsTitleblockRevisionSchedule;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
