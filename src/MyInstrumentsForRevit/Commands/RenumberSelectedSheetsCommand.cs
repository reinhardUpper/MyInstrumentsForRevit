using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RenumberSelectedSheetsCommand : IExternalCommand
    {
        private const string DialogTitle = "Перенумерация листов";
        private const string Invisible = "\u200B";

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = data.Application.ActiveUIDocument;
            Document document = uiDocument.Document;
            List<ViewSheet> sheets = uiDocument.Selection.GetElementIds()
                .Select(document.GetElement).OfType<ViewSheet>()
                .OrderBy(sheet => NaturalKey(sheet.SheetNumber), StringComparer.Ordinal)
                .ThenBy(sheet => sheet.Id.IntegerValue).ToList();

            if (sheets.Count == 0)
            {
                TaskDialog.Show(DialogTitle, "Выделите листы в диспетчере проекта.");
                return Result.Cancelled;
            }

            var selected = new HashSet<int>(sheets.Select(sheet => sheet.Id.IntegerValue));
            List<ViewSheet> allSheets = new FilteredElementCollector(document)
                .OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
            var occupied = new HashSet<string>(allSheets
                .Where(sheet => !selected.Contains(sheet.Id.IntegerValue))
                .Select(sheet => sheet.SheetNumber), StringComparer.OrdinalIgnoreCase);
            var targetNumbers = new List<string>();
            for (int index = 0; index < sheets.Count; index++)
            {
                string number = (index + 1).ToString(CultureInfo.InvariantCulture);
                while (!occupied.Add(number)) number += Invisible;
                targetNumbers.Add(number);
            }

            int changed = sheets.Where((sheet, index) => sheet.SheetNumber != targetNumbers[index]).Count();
            if (changed == 0)
            {
                TaskDialog.Show(DialogTitle, "Номера выбранных листов уже соответствуют диапазону 1–"
                    + sheets.Count + ".\nИзменения не требуются.");
                return Result.Cancelled;
            }

            var reserved = new HashSet<string>(allSheets.Select(sheet => sheet.SheetNumber), StringComparer.OrdinalIgnoreCase);
            reserved.UnionWith(targetNumbers);
            var temporaryNumbers = new List<string>();
            for (int index = 0; index < sheets.Count; index++)
            {
                string temporary;
                do { temporary = "__TEMP_" + Guid.NewGuid().ToString("N"); }
                while (!reserved.Add(temporary));
                temporaryNumbers.Add(temporary);
            }

            var failures = new RenumberFailures();
            try
            {
                using (var transaction = new Transaction(document, DialogTitle))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                    {
                        message = "Не удалось начать транзакцию перенумерации листов.";
                        return Result.Failed;
                    }

                    FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failures);
                    options.SetClearAfterRollback(true);
                    options.SetForcedModalHandling(true);
                    transaction.SetFailureHandlingOptions(options);

                    for (int index = 0; index < sheets.Count; index++)
                        sheets[index].SheetNumber = temporaryNumbers[index];
                    document.Regenerate();
                    for (int index = 0; index < sheets.Count; index++)
                        sheets[index].SheetNumber = targetNumbers[index];

                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        message = "Перенумерация листов не сохранена. Статус транзакции: " + status + ".";
                        if (failures.Errors.Count > 0)
                            message += "\n\n" + string.Join("\n", failures.Errors.Distinct());
                        return Result.Failed;
                    }
                }

                if (sheets.Where((sheet, index) => sheet.SheetNumber != targetNumbers[index]).Any())
                {
                    message = "После завершения транзакции номера листов не совпали с рассчитанными. Перенумерация отменена.";
                    return Result.Failed;
                }
            }
            catch (Exception exception)
            {
                message = "Не удалось перенумеровать листы. Изменения отменены.\n\n" + exception.Message;
                return Result.Failed;
            }

            TaskDialog.Show(DialogTitle, "Изменено номеров листов: " + changed
                + "\nВыбрано листов: " + sheets.Count + "\nДиапазон: 1–" + sheets.Count);
            return Result.Succeeded;
        }

        private static string NaturalKey(string value) =>
            Regex.Replace(value ?? string.Empty, "\\d+", match => match.Value.PadLeft(12, '0'));

        private sealed class RenumberFailures : IFailuresPreprocessor
        {
            public List<string> Errors { get; } = new List<string>();

            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                foreach (FailureMessageAccessor failure in accessor.GetFailureMessages())
                {
                    FailureSeverity severity = failure.GetSeverity();
                    if (severity != FailureSeverity.None && severity != FailureSeverity.Warning)
                        Errors.Add(failure.GetDescriptionText());
                }

                return Errors.Count > 0
                    ? FailureProcessingResult.ProceedWithRollBack
                    : FailureProcessingResult.Continue;
            }
        }
    }
}
