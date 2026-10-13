using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.ScheduleRows
{
    /// <summary>Runs a request from the modeless window in Revit's API context.</summary>
    internal sealed class ScheduleRowsSelectionController : IExternalEventHandler, IDisposable
    {
        private readonly Document _document;
        private readonly ElementId _scheduleId;
        private readonly Action<string> _reportStatus;
        private readonly ExternalEvent _externalEvent;
        private IReadOnlyCollection<ScheduleRow> _requestedRows = Array.Empty<ScheduleRow>();
        private bool _isDisposed;

        public ScheduleRowsSelectionController(Document document, ElementId scheduleId, Action<string> reportStatus)
        {
            _document = document;
            _scheduleId = scheduleId;
            _reportStatus = reportStatus;
            _externalEvent = ExternalEvent.Create(this);
        }

        public void RequestSelection(IReadOnlyCollection<ScheduleRow> rows)
        {
            if (_isDisposed) return;
            _requestedRows = rows.ToList();
            _externalEvent.Raise();
        }

        public void Execute(UIApplication application)
        {
            if (_isDisposed) return;
            try
            {
                UIDocument? uiDocument = application.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document != _document)
                {
                    Report("Откройте документ, в котором была запущена команда.");
                    return;
                }

                ViewSchedule? schedule = _document.GetElement(_scheduleId) as ViewSchedule;
                if (schedule == null)
                {
                    Report("Спецификация была удалена.");
                    return;
                }

                IReadOnlyCollection<ElementId> ids = ScheduleRowSnapshot.CollectElementIds(_document, schedule, _requestedRows);
                uiDocument.Selection.SetElementIds(ids.ToList());
                Report(ids.Count > 0 ? "Выделено элементов: " + ids.Count : "Для выбранных строк элементы не найдены.");
            }
            catch (Exception exception)
            {
                Report("Не удалось выбрать элементы: " + exception.Message);
            }
        }

        public string GetName() => "Выбрать элементы строк спецификации";

        public void Dispose()
        {
            _isDisposed = true;
            _externalEvent.Dispose();
        }

        private void Report(string status)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => _reportStatus(status)));
        }
    }
}
