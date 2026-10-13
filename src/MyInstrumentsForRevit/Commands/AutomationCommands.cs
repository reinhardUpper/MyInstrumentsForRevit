using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using MyInstrumentsForRevit.RevitCompatibility;
using RevitGroup = Autodesk.Revit.DB.Group;

namespace MyInstrumentsForRevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FillConsumptionGroupsCommand : IExternalCommand
    {
        private const string ArmSource = "Мрк.МаркаКонструкции";
        private const string ArmTarget = "Арм.Группа ВРС";
        private const string ConcreteTarget = "Орг.ГруппаВедомостьРасходаБетона";

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document document = data.Application.ActiveUIDocument.Document;
            var question = new TaskDialog("Заполнение групп бетона и арматуры")
            {
                MainInstruction = "Использовать самое частое непустое значение группы для каждой марки?",
                MainContent = "Команда обработает всю модель. Для каждой марки будет выбрано единственное наиболее частое значение и записано всем элементам этой марки.",
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel
            };
            if (question.Show() != TaskDialogResult.Yes) return Result.Cancelled;

            var buckets = new Dictionary<string, GroupBucket>(StringComparer.OrdinalIgnoreCase);
            int scanned = 0, noMark = 0, noTarget = 0, skippedWalls = 0;
            foreach (BuiltInCategory category in new[] { BuiltInCategory.OST_Rebar, BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls })
            foreach (Element element in new FilteredElementCollector(document).OfCategory(category).WhereElementIsNotElementType())
            {
                if (category == BuiltInCategory.OST_Walls && element.get_Parameter(BuiltInParameter.WALL_STRUCTURAL_SIGNIFICANT)?.AsInteger() != 1) { skippedWalls++; continue; }
                scanned++;
                bool arm = category == BuiltInCategory.OST_Rebar || category == BuiltInCategory.OST_GenericModel;
                string mark = GetText(document, element, arm ? ArmSource : "Марка", !arm);
                if (string.IsNullOrWhiteSpace(mark)) { noMark++; continue; }
                string targetName = arm ? ArmTarget : ConcreteTarget;
                Parameter? target = FindSingleTextParameter(element, targetName);
                if (target == null) { noTarget++; continue; }
                string key = (arm ? "Арматура / ОМ|" : "Бетон|") + mark;
                if (!buckets.TryGetValue(key, out GroupBucket? bucket)) { bucket = new GroupBucket(targetName); buckets.Add(key, bucket); }
                bucket.Elements.Add(element);
                string value = target.AsString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(value)) bucket.Votes[value] = bucket.Votes.TryGetValue(value, out int count) ? count + 1 : 1;
            }
            int changed = 0, same = 0, ambiguous = 0, readOnly = 0;
            using (var transaction = new Transaction(document, "Заполнение групп бетона и арматуры"))
            {
                transaction.Start();
                foreach (GroupBucket bucket in buckets.Values)
                {
                    if (bucket.Votes.Count == 0) { ambiguous++; continue; }
                    int maximum = bucket.Votes.Values.Max(); string[] winners = bucket.Votes.Where(item => item.Value == maximum).Select(item => item.Key).ToArray();
                    if (winners.Length != 1) { ambiguous++; continue; }
                    foreach (Element element in bucket.Elements)
                    {
                        Parameter? target = FindSingleTextParameter(element, bucket.TargetName);
                        if (target == null || target.IsReadOnly) { readOnly++; continue; }
                        if (target.AsString() == winners[0]) { same++; continue; }
                        target.Set(winners[0]); changed++;
                    }
                }
                transaction.Commit();
            }
            TaskDialog.Show("Группы — результат", "Рассмотрено: " + scanned + "\nИзменено: " + changed + "\nУже совпадало: " + same + "\nБез марки: " + noMark + "\nБез целевого параметра: " + noTarget + "\nНеоднозначных/пустых марок: " + ambiguous + "\nТолько чтение: " + readOnly + "\nНенесущих стен исключено: " + skippedWalls);
            return Result.Succeeded;
        }

        private static string GetText(Document document, Element element, string name, bool preferBuiltInMark)
        {
            Parameter? parameter = preferBuiltInMark ? element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) : null;
            parameter = parameter ?? FindSingleTextParameter(element, name);
            string value = parameter?.AsString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value)) return value;
            Element? type = document.GetElement(element.GetTypeId());
            return type == null ? string.Empty : FindSingleTextParameter(type, name)?.AsString() ?? string.Empty;
        }

        private static Parameter? FindSingleTextParameter(Element element, string name)
        {
            List<Parameter> parameters = element.GetParameters(name).Cast<Parameter>().ToList();
            return parameters.Count == 1 && parameters[0].StorageType == StorageType.String ? parameters[0] : null;
        }

        private sealed class GroupBucket
        {
            public GroupBucket(string targetName) { TargetName = targetName; }
            public string TargetName { get; }
            public List<Element> Elements { get; } = new List<Element>();
            public Dictionary<string, int> Votes { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class LinkTitleBlockDatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document document = data.Application.ActiveUIDocument.Document;
            ViewSheet? sheet = document.ActiveView as ViewSheet;
            if (sheet == null) { TaskDialog.Show("Даты основной надписи", "Откройте лист и повторите команду."); return Result.Cancelled; }
            GlobalParameter? global = GlobalParametersManager.GetAllGlobalParameters(document).Select(document.GetElement).OfType<GlobalParameter>()
                .FirstOrDefault(item => item.Name.IndexOf("ДАТА", StringComparison.CurrentCultureIgnoreCase) >= 0);
            if (global == null) { TaskDialog.Show("Даты основной надписи", "Не найден глобальный параметр с «ДАТА» в имени."); return Result.Cancelled; }
            int linked = 0, same = 0, unavailable = 0;
            using (var transaction = new Transaction(document, "Связать даты основной надписи"))
            {
                transaction.Start();
                foreach (FamilyInstance titleBlock in new FilteredElementCollector(document, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks).WhereElementIsNotElementType().OfType<FamilyInstance>())
                foreach (Parameter parameter in titleBlock.Parameters)
                {
                    string name = parameter.Definition?.Name ?? string.Empty;
                    if (!name.StartsWith("Соглас.", StringComparison.CurrentCultureIgnoreCase) || !name.EndsWith("Дата", StringComparison.CurrentCultureIgnoreCase)) continue;
                    ElementId associated = parameter.GetAssociatedGlobalParameter();
                    if (associated == global.Id) { same++; continue; }
                    if (associated != ElementId.InvalidElementId) parameter.DissociateFromGlobalParameter();
                    if (!parameter.CanBeAssociatedWithGlobalParameter(global.Id)) { unavailable++; continue; }
                    parameter.AssociateWithGlobalParameter(global.Id); linked++;
                }
                transaction.Commit();
            }
            TaskDialog.Show("Даты основной надписи", "Глобальный параметр: " + global.Name + "\nСоздано связей: " + linked + "\nУже связаны: " + same + "\nНельзя связать: " + unavailable);
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class RenumberSelectedSheetsCommand : IExternalCommand
    {
        private const string Invisible = "\u200B";
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = data.Application.ActiveUIDocument; Document document = uiDocument.Document;
            List<ViewSheet> sheets = uiDocument.Selection.GetElementIds().Select(document.GetElement).OfType<ViewSheet>()
                .OrderBy(sheet => NaturalKey(sheet.SheetNumber)).ToList();
            if (sheets.Count == 0) { TaskDialog.Show("Перенумерация листов", "Выделите листы в диспетчере проекта."); return Result.Cancelled; }
            var selected = new HashSet<int>(sheets.Select(item => item.Id.IntegerValue));
            var occupied = new HashSet<string>(new FilteredElementCollector(document).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Where(item => !selected.Contains(item.Id.IntegerValue)).Select(item => item.SheetNumber));
            using (var transaction = new Transaction(document, "Перенумерация листов"))
            {
                transaction.Start();
                for (int index = 0; index < sheets.Count; index++) sheets[index].SheetNumber = "__TEMP_" + sheets[index].Id.IntegerValue + "_" + index + "__";
                document.Regenerate();
                for (int index = 0; index < sheets.Count; index++)
                {
                    string number = (index + 1).ToString(CultureInfo.InvariantCulture); while (occupied.Contains(number)) number += Invisible;
                    sheets[index].SheetNumber = number; occupied.Add(number);
                }
                transaction.Commit();
            }
            TaskDialog.Show("Перенумерация листов", "Перенумеровано листов: " + sheets.Count + "\nДиапазон: 1–" + sheets.Count);
            return Result.Succeeded;
        }
        private static string NaturalKey(string value) => Regex.Replace(value ?? string.Empty, "\\d+", match => match.Value.PadLeft(12, '0'));
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class CalculateFilledRegionAreasCommand : IExternalCommand
    {
        private const double SquareFeetToSquareMeters = 0.09290304;
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = data.Application.ActiveUIDocument; Document document = uiDocument.Document;
            List<View> views = uiDocument.Selection.GetElementIds().Select(document.GetElement).OfType<View>().Where(view => !(view is ViewSheet) && !view.IsTemplate).ToList();
            if (views.Count == 0 && !(document.ActiveView is ViewSheet) && !document.ActiveView.IsTemplate) views.Add(document.ActiveView);
            if (views.Count == 0) { TaskDialog.Show("Площади зон", "Выберите виды или откройте нужный вид."); return Result.Cancelled; }
            double area12 = 0, area14 = 0, other = 0; int count12 = 0, count14 = 0, countOther = 0;
            foreach (View view in views)
            foreach (FilledRegion region in new FilteredElementCollector(document, view.Id).OfClass(typeof(FilledRegion)).WhereElementIsNotElementType().OfType<FilledRegion>())
            {
                Element? type = document.GetElement(region.GetTypeId()); string typeName = type?.Name ?? string.Empty;
                double area = region.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0;
                if (typeName.Replace(" ", string.Empty).IndexOf("12", StringComparison.OrdinalIgnoreCase) >= 0) { area12 += area; count12++; }
                else if (typeName.Replace(" ", string.Empty).IndexOf("14", StringComparison.OrdinalIgnoreCase) >= 0) { area14 += area; count14++; }
                else { other += area; countOther++; }
            }
            TaskDialog.Show("Площади цветовых областей", "Видов: " + views.Count + "\n\nТип содержит «12»: " + (area12 * SquareFeetToSquareMeters).ToString("F2") + " м² | " + count12 + " шт.\nТип содержит «14»: " + (area14 * SquareFeetToSquareMeters).ToString("F2") + " м² | " + count14 + " шт.\nОстальные: " + (other * SquareFeetToSquareMeters).ToString("F2") + " м² | " + countOther + " шт.");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class MarkConstructionFromHostCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            Document doc = data.Application.ActiveUIDocument.Document; View view = doc.ActiveView;
            List<Element> hosts = new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType()
                .Where(e => e.Category != null && (e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Floors || e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Walls))
                .Where(e => !string.IsNullOrWhiteSpace(GetMark(doc, e))).ToList();
            if (hosts.Count == 0) { TaskDialog.Show("Мрк.МаркаКонструкции", "На активном виде не найдены плиты или стены с заполненной маркой."); return Result.Cancelled; }
            int changed = 0, same = 0, noHost = 0, noParameter = 0;
            using (var transaction = new Transaction(doc, "Мрк.МаркаКонструкции — активный вид"))
            {
                transaction.Start();
                foreach (Element item in new FilteredElementCollector(doc, view.Id).WhereElementIsNotElementType())
                {
                    if (!IsTarget(item)) continue;
                    Parameter? target = item.LookupParameter("Мрк.МаркаКонструкции");
                    if (target == null || target.StorageType != StorageType.String || target.IsReadOnly) { noParameter++; continue; }
                    Element? host = ChooseHost(item, hosts, view);
                    if (host == null) { noHost++; continue; }
                    string mark = GetMark(doc, host);
                    if (target.AsString() == mark) { same++; continue; }
                    target.Set(mark); changed++;
                }
                transaction.Commit();
            }
            TaskDialog.Show("Мрк.МаркаКонструкции", "Изменено: " + changed + "\nУже совпадало: " + same + "\nНе найдена основа: " + noHost + "\nНет целевого параметра: " + noParameter);
            return Result.Succeeded;
        }
        private static bool IsTarget(Element item) { int id = item.Category?.Id.IntegerValue ?? 0; return id == (int)BuiltInCategory.OST_Rebar || id == (int)BuiltInCategory.OST_GenericModel || id == (int)BuiltInCategory.OST_Windows || item.LookupParameter("Мрк.МаркаКонструкции") != null; }
        private static string GetMark(Document doc, Element item) { Parameter? p = item.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? item.LookupParameter("Марка"); string text = p?.AsString() ?? string.Empty; if (!string.IsNullOrWhiteSpace(text)) return text; return doc.GetElement(item.GetTypeId())?.LookupParameter("Марка")?.AsString() ?? string.Empty; }
        private static Element? ChooseHost(Element item, IEnumerable<Element> hosts, View view) { BoundingBoxXYZ? bb = item.get_BoundingBox(view); if (bb == null) return null; return hosts.Select(host => new { Host = host, Box = host.get_BoundingBox(view) }).Where(x => x.Box != null && Overlaps(bb, x.Box)).OrderByDescending(x => Score(bb, x.Box!)).Select(x => x.Host).FirstOrDefault(); }
        private static bool Overlaps(BoundingBoxXYZ a, BoundingBoxXYZ b) => a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        private static double Score(BoundingBoxXYZ a, BoundingBoxXYZ b) => Math.Max(0, Math.Min(a.Max.X,b.Max.X)-Math.Max(a.Min.X,b.Min.X))*Math.Max(0, Math.Min(a.Max.Y,b.Max.Y)-Math.Max(a.Min.Y,b.Min.Y))*Math.Max(0, Math.Min(a.Max.Z,b.Max.Z)-Math.Max(a.Min.Z,b.Min.Z));
    }

    [Transaction(TransactionMode.Manual)]
    public class DimensionFilledRegionsCommand : IExternalCommand
    {
        private const double OffsetMillimeters = 500;
        private const double DirectionTolerance = 0.999;
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument ui = data.Application.ActiveUIDocument; Document doc = ui.Document; View view = doc.ActiveView;
            if (view is View3D) { TaskDialog.Show("Размеры штриховок", "Откройте плоский вид."); return Result.Cancelled; }
            RevitGroup? group = ui.Selection.GetElementIds().Select(doc.GetElement).OfType<RevitGroup>().FirstOrDefault();
            if (group == null) try { group = doc.GetElement(ui.Selection.PickObject(ObjectType.Element, new GroupFilter(), "Выберите группу со штриховками")) as RevitGroup; } catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            if (group == null) return Result.Cancelled;
            List<FilledRegion> regions = CollectRegions(doc, group, new HashSet<int>());
            XYZ right = view.RightDirection.Normalize(), up = view.UpDirection.Normalize(), normal = view.ViewDirection.Normalize();
            List<AxisRef> vertical = CollectGrids(doc, view, up, right), horizontal = CollectGrids(doc, view, right, up);
            int created = 0, skipped = 0;
            using (var transaction = new Transaction(doc, "Размеры контуров Filled Region"))
            {
                transaction.Start();
                foreach (FilledRegion region in regions)
                foreach (CurveLoop loop in region.GetBoundaries())
                {
                    List<Curve> curves = loop.ToList();
                    if (curves.Count != 4 || curves.Any(curve => !(curve is Line))) { skipped++; continue; }
                    List<AxisRef> sidesV = CreateSides(curves, up, right), sidesH = CreateSides(curves, right, up);
                    if (sidesV.Count != 2 || sidesH.Count != 2 || sidesV.Any(x => x.Reference == null) || sidesH.Any(x => x.Reference == null)) { skipped++; continue; }
                    double minX = sidesV.Min(x => x.Position), maxX = sidesV.Max(x => x.Position), minY = sidesH.Min(x => x.Position), maxY = sidesH.Max(x => x.Position);
                    double plane = curves[0].GetEndPoint(0).DotProduct(normal), offset = UnitConversion.MillimetersToInternalUnits(OffsetMillimeters);
                    created += CreateDimension(doc, view, Combine(sidesV, Nearest(vertical, (minX + maxX) / 2)), right, up, normal, minY - offset, plane);
                    created += CreateDimension(doc, view, Combine(sidesH, Nearest(horizontal, (minY + maxY) / 2)), up, right, normal, minX + offset, plane);
                }
                transaction.Commit();
            }
            TaskDialog.Show("Размеры штриховок", "Найдено Filled Region: " + regions.Count + "\nСоздано размерных цепочек: " + created + "\nПропущено контуров: " + skipped);
            return Result.Succeeded;
        }
        private static List<FilledRegion> CollectRegions(Document doc, RevitGroup group, ISet<int> visited)
        {
            var result = new List<FilledRegion>();
            if (!visited.Add(group.Id.IntegerValue)) return result;
            foreach (ElementId id in group.GetMemberIds())
            {
                Element? element = doc.GetElement(id);
                if (element is RevitGroup nested) result.AddRange(CollectRegions(doc, nested, visited));
                else if (element is FilledRegion region) result.Add(region);
            }
            return result;
        }
        private static List<AxisRef> CollectGrids(Document doc, View view, XYZ expected, XYZ coordinateAxis) => new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid)).OfType<Grid>().Select(g => new { Grid = g, Curve = g.Curve as Line }).Where(x => x.Curve != null && Math.Abs(x.Curve.Direction.Normalize().DotProduct(expected)) >= DirectionTolerance).Select(x => new AxisRef(new Reference(x.Grid), x.Curve!.GetEndPoint(0).DotProduct(coordinateAxis))).ToList();
        private static List<AxisRef> CreateSides(IEnumerable<Curve> curves, XYZ expected, XYZ coordinateAxis) => curves.OfType<Line>().Where(line => Math.Abs(line.Direction.Normalize().DotProduct(expected)) >= DirectionTolerance).Select(line => new AxisRef(line.Reference, (line.GetEndPoint(0).DotProduct(coordinateAxis) + line.GetEndPoint(1).DotProduct(coordinateAxis)) / 2)).ToList();
        private static AxisRef? Nearest(IEnumerable<AxisRef> values, double position) => values.OrderBy(value => Math.Abs(value.Position - position)).FirstOrDefault();
        private static IEnumerable<AxisRef> Combine(IEnumerable<AxisRef> sides, AxisRef? grid) => grid == null ? sides : sides.Concat(new[] { grid });
        private static int CreateDimension(Document doc, View view, IEnumerable<AxisRef> items, XYZ run, XYZ offsetAxis, XYZ normal, double offset, double plane) { List<AxisRef> list = items.Where(i => i.Reference != null).OrderBy(i => i.Position).ToList(); if (list.Count < 2) return 0; var refs = new ReferenceArray(); foreach (AxisRef item in list) refs.Append(item.Reference!); XYZ start = run.Multiply(list.First().Position).Add(offsetAxis.Multiply(offset)).Add(normal.Multiply(plane)); XYZ end = run.Multiply(list.Last().Position).Add(offsetAxis.Multiply(offset)).Add(normal.Multiply(plane)); doc.Create.NewDimension(view, Line.CreateBound(start, end), refs); return 1; }
        private sealed class AxisRef { public AxisRef(Reference? reference, double position) { Reference = reference; Position = position; } public Reference? Reference { get; } public double Position { get; } }
        private sealed class GroupFilter : ISelectionFilter { public bool AllowElement(Element element) => element is RevitGroup; public bool AllowReference(Reference reference, XYZ position) => false; }
    }
}
