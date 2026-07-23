using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.VisualBasic;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowLinkedElementByIdCommand : IExternalCommand
    {
        private const double OffsetFeet = 500.0 / 304.8;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ExecuteFromCommandLine(commandData.Application);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Элемент связи по ID", exception.Message);
                return Result.Failed;
            }
        }

        public static void ExecuteFromCommandLine(UIApplication uiApplication)
        {
            UIDocument uiDocument = uiApplication.ActiveUIDocument;
            if (uiDocument == null)
            {
                return;
            }

            Document document = uiDocument.Document;
            View3D view = document.ActiveView as View3D
                ?? throw new InvalidOperationException("Откройте 3D вид.");

            RevitLinkInstance linkInstance = PickRevitLink(uiDocument, document);
            Document linkDocument = linkInstance.GetLinkDocument()
                ?? throw new InvalidOperationException("Связь не загружена.");

            ElementId linkedElementId = ReadLinkedElementId();
            Element linkedElement = linkDocument.GetElement(linkedElementId)
                ?? throw new InvalidOperationException($"Элемент с ID {linkedElementId.IntegerValue} не найден в выбранной связи.");

            BoundingBoxXYZ linkBox = linkedElement.get_BoundingBox(null)
                ?? throw new InvalidOperationException("У элемента нет BoundingBox.");

            BoundingBoxXYZ hostBox = TransformBoundingBox(linkBox, linkInstance.GetTransform(), OffsetFeet);

            using (var transaction = new Transaction(document, "Показать элемент связи по ID"))
            {
                transaction.Start();
                view.IsSectionBoxActive = true;
                view.SetSectionBox(hostBox);
                transaction.Commit();
            }

            uiDocument.RefreshActiveView();
        }

        private static RevitLinkInstance PickRevitLink(UIDocument uiDocument, Document document)
        {
            Reference reference = uiDocument.Selection.PickObject(
                ObjectType.Element,
                "Выберите связь Revit");

            RevitLinkInstance linkInstance = document.GetElement(reference.ElementId) as RevitLinkInstance
                ?? throw new InvalidOperationException("Выбранный элемент не является связью Revit.");

            return linkInstance;
        }

        private static ElementId ReadLinkedElementId()
        {
            string idText = Interaction.InputBox(
                "Введите ElementId элемента в связи",
                "ID элемента",
                string.Empty);

            if (string.IsNullOrWhiteSpace(idText))
            {
                throw new OperationCanceledException();
            }

            if (!int.TryParse(idText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idValue))
            {
                throw new InvalidOperationException("ElementId должен быть целым числом.");
            }

            return new ElementId(idValue);
        }

        private static BoundingBoxXYZ TransformBoundingBox(BoundingBoxXYZ source, Transform transform, double offset)
        {
            List<XYZ> points = new List<XYZ>();
            foreach (double x in new[] { source.Min.X, source.Max.X })
            {
                foreach (double y in new[] { source.Min.Y, source.Max.Y })
                {
                    foreach (double z in new[] { source.Min.Z, source.Max.Z })
                    {
                        points.Add(transform.OfPoint(new XYZ(x, y, z)));
                    }
                }
            }

            return new BoundingBoxXYZ
            {
                Min = new XYZ(
                    points.Min(point => point.X) - offset,
                    points.Min(point => point.Y) - offset,
                    points.Min(point => point.Z) - offset),
                Max = new XYZ(
                    points.Max(point => point.X) + offset,
                    points.Max(point => point.Y) + offset,
                    points.Max(point => point.Z) + offset)
            };
        }
    }
}
