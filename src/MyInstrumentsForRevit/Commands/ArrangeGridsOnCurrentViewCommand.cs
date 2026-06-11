using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.VisualBasic;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ArrangeGridsOnCurrentViewCommand : IExternalCommand
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double BubbleOffset = 2500.0 * MmToFeet;
        private const double CropOffset = 3000.0 * MmToFeet;
        private const double FirstDimensionOffset = 1000.0 * MmToFeet;
        private const double OverallDimensionOffset = 1700.0 * MmToFeet;
        private const double DirectionTolerance = 0.10;
        private const double BoundsTolerance = 1.0e-6;
        private const string FilterPrefix = "КЖ. Оси - оставить";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Run(commandData.Application);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Оформление вида", exception.Message);
                return Result.Failed;
            }
        }

        public static void ExecuteFromCommandLine(UIApplication uiApplication)
        {
            Run(uiApplication);
        }

        private static void Run(UIApplication application)
        {
            UIDocument uiDocument = application.ActiveUIDocument;
            Document document = uiDocument.Document;
            View view = document.ActiveView;

            CheckActiveView(view);

            string? suffix = NormalizeSuffix(Interaction.InputBox(
                "Введите суффикс осей.\n\nПримеры:\n3 или /3 - оставить оси 1/3, 2/3, А/3.\n\nПустое поле - оставить оси без суффикса.",
                "Суффикс осей",
                string.Empty));

            PickedFloor pickedFloor = PickFloor(uiDocument, document);
            ViewBounds floorBounds = GetFloorBoundsOnView(document, view, pickedFloor);

            IList<Grid> allGrids = new FilteredElementCollector(document, view.Id)
                .OfClass(typeof(Grid))
                .WhereElementIsNotElementType()
                .Cast<Grid>()
                .ToList();

            List<Grid> gridsToProcess = allGrids.Where(grid => GridMatchesSuffix(grid.Name, suffix)).ToList();
            List<ElementId> hiddenGridIds = allGrids.Where(grid => !GridMatchesSuffix(grid.Name, suffix)).Select(grid => grid.Id).ToList();

            if (gridsToProcess.Count == 0)
            {
                TaskDialog.Show("Оформление вида", "Подходящие оси не найдены.");
                return;
            }

            var processedVertical = new List<Grid>();
            var processedHorizontal = new List<Grid>();
            var skipped = new List<string>();

            using (var transaction = new Transaction(document, "Оформление осей на текущем виде"))
            {
                transaction.Start();

                SetCrop(view, floorBounds.Expand(CropOffset));
                RemoveOldGridFilters(document, view);
                TryCreateHiddenGridFilter(document, view, hiddenGridIds, suffix);

                foreach (Grid grid in gridsToProcess)
                {
                    if (!TryArrangeGrid(view, floorBounds, grid, out GridDirection direction))
                    {
                        skipped.Add(grid.Name);
                        continue;
                    }

                    if (direction == GridDirection.Vertical)
                    {
                        processedVertical.Add(grid);
                    }
                    else
                    {
                        processedHorizontal.Add(grid);
                    }
                }

                document.Regenerate();
                CreateGridDimensions(document, view, floorBounds, processedVertical, processedHorizontal);

                transaction.Commit();
            }

            TaskDialog.Show(
                "Оформление вида",
                $"Готово.\nОформлено вертикальных осей: {processedVertical.Count}\nОформлено горизонтальных осей: {processedHorizontal.Count}\nСкрыто фильтром: {hiddenGridIds.Count}\nПропущено: {skipped.Count}");
        }

        private static void CheckActiveView(View view)
        {
            if (view == null)
            {
                throw new InvalidOperationException("Активный вид не найден.");
            }

            if (view.IsTemplate)
            {
                throw new InvalidOperationException("Команду нельзя запускать на шаблоне вида.");
            }

            if (view.ViewType != ViewType.FloorPlan
                && view.ViewType != ViewType.CeilingPlan
                && view.ViewType != ViewType.EngineeringPlan
                && view.ViewType != ViewType.AreaPlan)
            {
                throw new InvalidOperationException("Команду нужно запускать на плане.");
            }
        }

        private static PickedFloor PickFloor(UIDocument uiDocument, Document document)
        {
            Reference reference = uiDocument.Selection.PickObject(ObjectType.PointOnElement, "Выберите плиту в модели или плиту внутри Revit-связи");
            Element hostElement = document.GetElement(reference.ElementId);

            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                var linkInstance = hostElement as RevitLinkInstance
                    ?? throw new InvalidOperationException("Родитель выбранного элемента не является RevitLinkInstance.");
                Document linkDocument = linkInstance.GetLinkDocument()
                    ?? throw new InvalidOperationException("Выбранная Revit-связь не загружена.");
                Element linkedElement = linkDocument.GetElement(reference.LinkedElementId);
                EnsureFloor(linkedElement);
                return new PickedFloor(linkedElement, linkInstance.GetTotalTransform());
            }

            EnsureFloor(hostElement);
            return new PickedFloor(hostElement, Transform.Identity);
        }

        private static void EnsureFloor(Element element)
        {
            if (element?.Category == null || element.Category.Id.IntegerValue != (int)BuiltInCategory.OST_Floors)
            {
                throw new InvalidOperationException("Выбранный элемент не является плитой.");
            }
        }

        private static ViewBounds GetFloorBoundsOnView(Document document, View view, PickedFloor pickedFloor)
        {
            BoundingBoxXYZ box = pickedFloor.Element.get_BoundingBox(null)
                ?? throw new InvalidOperationException("У выбранной плиты не найден габарит.");

            var points = new[]
            {
                new XYZ(box.Min.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Min.X, box.Max.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Min.Y, box.Max.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Min.Z),
                new XYZ(box.Max.X, box.Max.Y, box.Max.Z)
            };

            List<UVPoint> uvPoints = points
                .Select(point => ToViewPoint(view, pickedFloor.Transform.OfPoint(point)))
                .ToList();

            return new ViewBounds(
                uvPoints.Min(point => point.U),
                uvPoints.Max(point => point.U),
                uvPoints.Min(point => point.V),
                uvPoints.Max(point => point.V));
        }

        private static bool TryArrangeGrid(View view, ViewBounds bounds, Grid grid, out GridDirection direction)
        {
            direction = GridDirection.Unknown;

            try
            {
                if (!IsGridAvailableOnView(grid, view))
                {
                    return false;
                }

                grid.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                grid.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);

                Curve curve = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view).FirstOrDefault();
                var line = curve as Line;
                if (line == null)
                {
                    return false;
                }

                UVPoint start = ToViewPoint(view, line.GetEndPoint(0));
                UVPoint end = ToViewPoint(view, line.GetEndPoint(1));
                double deltaU = end.U - start.U;
                double deltaV = end.V - start.V;
                double length = Math.Sqrt(deltaU * deltaU + deltaV * deltaV);
                if (length < 1.0e-8)
                {
                    return false;
                }

                double directionU = deltaU / length;
                double directionV = deltaV / length;

                if (Math.Abs(directionU) <= DirectionTolerance)
                {
                    direction = GridDirection.Vertical;
                    if (Math.Abs(directionV) < 1.0e-8)
                    {
                        return false;
                    }

                    XYZ bubble = PointOnLineAtViewV(line, start.V, directionV, bounds.MinV - BubbleOffset);
                    XYZ tail = PointOnLineAtViewV(line, start.V, directionV, bounds.MaxV);
                    SetGridCurveAndBubble(grid, view, bubble, tail);
                    return true;
                }

                if (Math.Abs(directionV) <= DirectionTolerance)
                {
                    direction = GridDirection.Horizontal;
                    if (Math.Abs(directionU) < 1.0e-8)
                    {
                        return false;
                    }

                    XYZ bubble = PointOnLineAtViewU(line, start.U, directionU, bounds.MinU - BubbleOffset);
                    XYZ tail = PointOnLineAtViewU(line, start.U, directionU, bounds.MaxU);
                    SetGridCurveAndBubble(grid, view, bubble, tail);
                    return true;
                }

                return false;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
        }

        private static bool IsGridAvailableOnView(Grid grid, View view)
        {
            try
            {
                return grid.CanBeVisibleInView(view)
                    && grid.GetCurvesInView(DatumExtentType.Model, view).Count > 0;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
        }

        private static void SetGridCurveAndBubble(Grid grid, View view, XYZ bubble, XYZ tail)
        {
            grid.SetCurveInView(DatumExtentType.ViewSpecific, view, Line.CreateBound(bubble, tail));

            HideBubble(grid, view, DatumEnds.End0);
            HideBubble(grid, view, DatumEnds.End1);
            ShowBubble(grid, view, DatumEnds.End0);
        }

        private static void CreateGridDimensions(
            Document document,
            View view,
            ViewBounds bounds,
            IList<Grid> verticalGrids,
            IList<Grid> horizontalGrids)
        {
            DimensionType dimensionType = new FilteredElementCollector(document)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .FirstOrDefault();

            if (dimensionType == null)
            {
                return;
            }

            List<GridCoordinate> vertical = verticalGrids
                .Select(grid => TryGetGridCoordinate(view, grid, GridDirection.Vertical))
                .Where(item => item != null && item.Coordinate >= bounds.MinU - BoundsTolerance && item.Coordinate <= bounds.MaxU + BoundsTolerance)
                .Cast<GridCoordinate>()
                .OrderBy(item => item.Coordinate)
                .ToList();

            List<GridCoordinate> horizontal = horizontalGrids
                .Select(grid => TryGetGridCoordinate(view, grid, GridDirection.Horizontal))
                .Where(item => item != null && item.Coordinate >= bounds.MinV - BoundsTolerance && item.Coordinate <= bounds.MaxV + BoundsTolerance)
                .Cast<GridCoordinate>()
                .OrderBy(item => item.Coordinate)
                .ToList();

            CreateDimensionChain(
                document,
                view,
                dimensionType,
                vertical.Select(item => item.Grid).ToList(),
                ToModelPoint(view, bounds.MinU, bounds.MinV - FirstDimensionOffset),
                ToModelPoint(view, bounds.MaxU, bounds.MinV - FirstDimensionOffset));

            CreateOverallDimension(
                document,
                view,
                dimensionType,
                vertical.Select(item => item.Grid).ToList(),
                ToModelPoint(view, bounds.MinU, bounds.MinV - OverallDimensionOffset),
                ToModelPoint(view, bounds.MaxU, bounds.MinV - OverallDimensionOffset));

            CreateDimensionChain(
                document,
                view,
                dimensionType,
                horizontal.Select(item => item.Grid).ToList(),
                ToModelPoint(view, bounds.MinU - FirstDimensionOffset, bounds.MinV),
                ToModelPoint(view, bounds.MinU - FirstDimensionOffset, bounds.MaxV));

            CreateOverallDimension(
                document,
                view,
                dimensionType,
                horizontal.Select(item => item.Grid).ToList(),
                ToModelPoint(view, bounds.MinU - OverallDimensionOffset, bounds.MinV),
                ToModelPoint(view, bounds.MinU - OverallDimensionOffset, bounds.MaxV));
        }

        private static void CreateDimensionChain(
            Document document,
            View view,
            DimensionType dimensionType,
            IList<Grid> grids,
            XYZ start,
            XYZ end)
        {
            if (grids.Count < 2)
            {
                return;
            }

            TryCreateDimension(document, view, dimensionType, grids, start, end);
        }

        private static void CreateOverallDimension(
            Document document,
            View view,
            DimensionType dimensionType,
            IList<Grid> grids,
            XYZ start,
            XYZ end)
        {
            if (grids.Count < 2)
            {
                return;
            }

            TryCreateDimension(document, view, dimensionType, new[] { grids.First(), grids.Last() }, start, end);
        }

        private static void TryCreateDimension(
            Document document,
            View view,
            DimensionType dimensionType,
            IEnumerable<Grid> grids,
            XYZ start,
            XYZ end)
        {
            try
            {
                var references = new ReferenceArray();
                foreach (Grid grid in grids)
                {
                    references.Append(new Reference(grid));
                }

                document.Create.NewDimension(view, Line.CreateBound(start, end), references, dimensionType);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        private static GridCoordinate? TryGetGridCoordinate(View view, Grid grid, GridDirection direction)
        {
            try
            {
                Curve curve = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view).FirstOrDefault();
                if (curve == null)
                {
                    return null;
                }

                UVPoint start = ToViewPoint(view, curve.GetEndPoint(0));
                UVPoint end = ToViewPoint(view, curve.GetEndPoint(1));
                double coordinate = direction == GridDirection.Vertical
                    ? (start.U + end.U) * 0.5
                    : (start.V + end.V) * 0.5;

                return new GridCoordinate(grid, coordinate);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return null;
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return null;
            }
        }

        private static XYZ PointOnLineAtViewU(Line line, double startU, double directionU, double targetU)
        {
            double parameter = (targetU - startU) / directionU;
            XYZ direction = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
            return line.GetEndPoint(0) + direction.Multiply(parameter);
        }

        private static XYZ PointOnLineAtViewV(Line line, double startV, double directionV, double targetV)
        {
            double parameter = (targetV - startV) / directionV;
            XYZ direction = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize();
            return line.GetEndPoint(0) + direction.Multiply(parameter);
        }

        private static void SetCrop(View view, ViewBounds bounds)
        {
            ViewCropRegionShapeManager cropManager = view.GetCropRegionShapeManager()
                ?? throw new InvalidOperationException("Активный вид не поддерживает управление областью обрезки.");

            var loop = new CurveLoop();
            XYZ bottomLeft = ToModelPoint(view, bounds.MinU, bounds.MinV);
            XYZ bottomRight = ToModelPoint(view, bounds.MaxU, bounds.MinV);
            XYZ topRight = ToModelPoint(view, bounds.MaxU, bounds.MaxV);
            XYZ topLeft = ToModelPoint(view, bounds.MinU, bounds.MaxV);
            loop.Append(Line.CreateBound(bottomLeft, bottomRight));
            loop.Append(Line.CreateBound(bottomRight, topRight));
            loop.Append(Line.CreateBound(topRight, topLeft));
            loop.Append(Line.CreateBound(topLeft, bottomLeft));

            view.CropBoxActive = true;
            view.CropBoxVisible = true;
            try
            {
                cropManager.RemoveSplit();
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }

            cropManager.SetCropShape(loop);
        }

        private static void RemoveOldGridFilters(Document document, View view)
        {
            foreach (ElementId filterId in view.GetFilters().ToList())
            {
                Element filter = document.GetElement(filterId);
                if (filter is SelectionFilterElement && (filter.Name ?? string.Empty).StartsWith(FilterPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    view.RemoveFilter(filterId);
                    try
                    {
                        document.Delete(filterId);
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                    }
                }
            }
        }

        private static void TryCreateHiddenGridFilter(Document document, View view, IList<ElementId> hiddenGridIds, string? suffix)
        {
            if (hiddenGridIds.Count == 0)
            {
                return;
            }

            try
            {
                string mode = suffix == null ? "без суффикса" : "суффикс " + suffix.TrimStart('/');
                string name = $"{FilterPrefix} {mode} - {SafeName(view.Name)}";
                SelectionFilterElement filter = SelectionFilterElement.Create(document, name);
                filter.SetElementIds(hiddenGridIds);
                view.AddFilter(filter.Id);
                view.SetFilterVisibility(filter.Id, false);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        private static string? NormalizeSuffix(string input)
        {
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            string value = text.Contains("/") ? text.Split('/').Last().Trim() : text;
            return value.Length == 0 ? null : "/" + value;
        }

        private static bool GridMatchesSuffix(string gridName, string? suffix)
        {
            string name = (gridName ?? string.Empty).Trim();
            return suffix == null
                ? !name.Contains("/")
                : name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static UVPoint ToViewPoint(View view, XYZ point)
        {
            XYZ vector = point - view.Origin;
            return new UVPoint(vector.DotProduct(view.RightDirection), vector.DotProduct(view.UpDirection));
        }

        private static XYZ ToModelPoint(View view, double u, double v)
        {
            return view.Origin
                + view.RightDirection.Multiply(u)
                + view.UpDirection.Multiply(v);
        }

        private static void HideBubble(Grid grid, View view, DatumEnds end)
        {
            try
            {
                grid.HideBubbleInView(end, view);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        private static void ShowBubble(Grid grid, View view, DatumEnds end)
        {
            try
            {
                grid.ShowBubbleInView(end, view);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
            }
        }

        private static string SafeName(string value)
        {
            char[] invalid = { '\\', '/', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            string result = value ?? string.Empty;
            foreach (char character in invalid)
            {
                result = result.Replace(character, '-');
            }

            return result.Trim();
        }

        private sealed class PickedFloor
        {
            public PickedFloor(Element element, Transform transform)
            {
                Element = element;
                Transform = transform;
            }

            public Element Element { get; }

            public Transform Transform { get; }
        }

        private sealed class ViewBounds
        {
            public ViewBounds(double minU, double maxU, double minV, double maxV)
            {
                MinU = minU;
                MaxU = maxU;
                MinV = minV;
                MaxV = maxV;
            }

            public double MinU { get; }

            public double MaxU { get; }

            public double MinV { get; }

            public double MaxV { get; }

            public ViewBounds Expand(double offset)
            {
                return new ViewBounds(MinU - offset, MaxU + offset, MinV - offset, MaxV + offset);
            }
        }

        private sealed class UVPoint
        {
            public UVPoint(double u, double v)
            {
                U = u;
                V = v;
            }

            public double U { get; }

            public double V { get; }
        }

        private sealed class GridCoordinate
        {
            public GridCoordinate(Grid grid, double coordinate)
            {
                Grid = grid;
                Coordinate = coordinate;
            }

            public Grid Grid { get; }

            public double Coordinate { get; }
        }

        private enum GridDirection
        {
            Unknown,
            Horizontal,
            Vertical
        }

    }
}
