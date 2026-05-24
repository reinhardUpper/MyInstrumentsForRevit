using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ToggleRevitLinksCommand : IExternalCommand
    {
        private static readonly Dictionary<string, List<ElementId>> HiddenLinksByView =
            new Dictionary<string, List<ElementId>>();

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;
            View view = document.ActiveView;
            string key = BuildKey(document, view);

            using (var transaction = new Transaction(document, "Toggle Revit links"))
            {
                transaction.Start();

                if (HiddenLinksByView.TryGetValue(key, out List<ElementId> savedIds))
                {
                    List<ElementId> idsToUnhide = savedIds
                        .Where(id => document.GetElement(id) != null)
                        .ToList();

                    if (idsToUnhide.Count > 0)
                    {
                        view.UnhideElements(idsToUnhide);
                    }

                    HiddenLinksByView.Remove(key);
                    transaction.Commit();
                    return Result.Succeeded;
                }

                List<ElementId> visibleLinkIds = new FilteredElementCollector(document, view.Id)
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .Where(element => element.CanBeHidden(view))
                    .Select(element => element.Id)
                    .ToList();

                if (visibleLinkIds.Count == 0)
                {
                    transaction.RollBack();
                    TaskDialog.Show("Revit links", "There are no visible Revit links on the active view.");
                    return Result.Cancelled;
                }

                HiddenLinksByView[key] = visibleLinkIds;
                view.HideElements(visibleLinkIds);
                transaction.Commit();
            }

            return Result.Succeeded;
        }

        private static string BuildKey(Document document, View view)
        {
            string documentKey = string.IsNullOrWhiteSpace(document.PathName)
                ? document.GetHashCode().ToString()
                : document.PathName;

            return documentKey + ":" + view.Id.IntegerValue;
        }
    }
}

