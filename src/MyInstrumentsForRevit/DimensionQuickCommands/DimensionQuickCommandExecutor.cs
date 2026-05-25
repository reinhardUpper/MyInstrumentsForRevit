using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    public static class DimensionQuickCommandExecutor
    {
        public static Result Execute(UIApplication uiapp, DimensionQuickCommandConfig config)
        {
            if (uiapp == null)
            {
                TaskDialog.Show("Быстрая команда", "Не удалось получить контекст Revit.");
                return Result.Cancelled;
            }

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            Document? doc = uidoc?.Document;
            if (doc == null)
            {
                TaskDialog.Show("Быстрая команда", "Нет открытого документа Revit.");
                return Result.Cancelled;
            }

            string kind = QuickCommandKind.Normalize(config.CommandKind);
            return kind == QuickCommandKind.DetailItem
                ? ExecuteDetailItem(uiapp, doc, config)
                : ExecuteLinearDimension(uiapp, doc, config);
        }

        private static Result ExecuteLinearDimension(UIApplication uiapp, Document doc, DimensionQuickCommandConfig config)
        {
            DimensionType? dimensionType = FindDimensionType(doc, config);
            if (dimensionType == null)
            {
                ShowTypeNotFound(config, "тип размера");
                return Result.Cancelled;
            }

            if (!TrySetDefaultLinearDimensionType(doc, dimensionType))
            {
                return Result.Cancelled;
            }

            return TryPostCommand(uiapp, PostableCommand.AlignedDimension, "линейного размера");
        }

        private static Result ExecuteDetailItem(UIApplication uiapp, Document doc, DimensionQuickCommandConfig config)
        {
            FamilySymbol? symbol = FindDetailItemType(doc, config);
            if (symbol == null)
            {
                ShowTypeNotFound(config, "тип элемента узла");
                return Result.Cancelled;
            }

            if (!TrySetDefaultDetailItemType(doc, symbol))
            {
                return Result.Cancelled;
            }

            return TryPostCommand(uiapp, PostableCommand.DetailComponent, "элемента узла");
        }

        private static DimensionType? FindDimensionType(Document doc, DimensionQuickCommandConfig config)
        {
            if (config.DimensionTypeElementId > 0)
            {
                var id = new ElementId(config.DimensionTypeElementId);
                if (doc.GetElement(id) is DimensionType byId && IsLinear(byId))
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeUniqueId)
                && doc.GetElement(config.DimensionTypeUniqueId) is DimensionType byUniqueId
                && IsLinear(byUniqueId))
            {
                return byUniqueId;
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeName))
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(DimensionType))
                    .Cast<DimensionType>()
                    .FirstOrDefault(type =>
                        IsLinear(type)
                        && string.Equals(type.Name, config.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase));
            }

            return null;
        }

        private static FamilySymbol? FindDetailItemType(Document doc, DimensionQuickCommandConfig config)
        {
            if (config.DimensionTypeElementId > 0)
            {
                var id = new ElementId(config.DimensionTypeElementId);
                if (doc.GetElement(id) is FamilySymbol byId && IsDetailItem(byId))
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeUniqueId)
                && doc.GetElement(config.DimensionTypeUniqueId) is FamilySymbol byUniqueId
                && IsDetailItem(byUniqueId))
            {
                return byUniqueId;
            }

            if (!string.IsNullOrWhiteSpace(config.DimensionTypeName))
            {
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(symbol =>
                        IsDetailItem(symbol)
                        && string.Equals(symbol.Name, config.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase));
            }

            return null;
        }

        private static bool TrySetDefaultLinearDimensionType(Document doc, DimensionType dimensionType)
        {
            ElementId typeId = dimensionType.Id;
            const ElementTypeGroup group = ElementTypeGroup.LinearDimensionType;

            if (!doc.IsDefaultElementTypeIdValid(group, typeId))
            {
                TaskDialog.Show("Быстрая команда", $"Тип \"{dimensionType.Name}\" нельзя назначить типом линейного размера по умолчанию.");
                return false;
            }

            try
            {
                using (var transaction = new Transaction(doc, "Set quick linear dimension type"))
                {
                    transaction.Start();
                    doc.SetDefaultElementTypeId(group, typeId);
                    transaction.Commit();
                }

                return true;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException || ex is Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                TaskDialog.Show("Быстрая команда", "Не удалось назначить тип размера текущим.\n\n" + ex.Message);
                return false;
            }
        }

        private static bool TrySetDefaultDetailItemType(Document doc, FamilySymbol symbol)
        {
            ElementId categoryId = new ElementId(BuiltInCategory.OST_DetailComponents);
            if (!doc.IsDefaultFamilyTypeIdValid(categoryId, symbol.Id))
            {
                TaskDialog.Show("Быстрая команда", $"Тип \"{symbol.Name}\" нельзя назначить типом элементов узлов по умолчанию.");
                return false;
            }

            try
            {
                using (var transaction = new Transaction(doc, "Set quick detail item type"))
                {
                    transaction.Start();
                    doc.SetDefaultFamilyTypeId(categoryId, symbol.Id);
                    transaction.Commit();
                }

                return true;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException || ex is Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                TaskDialog.Show("Быстрая команда", "Не удалось назначить тип элемента узла текущим.\n\n" + ex.Message);
                return false;
            }
        }

        private static Result TryPostCommand(UIApplication uiapp, PostableCommand command, string commandName)
        {
            RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(command);
            if (commandId == null)
            {
                TaskDialog.Show("Быстрая команда", $"Системная команда {commandName} недоступна в этой версии Revit.");
                return Result.Cancelled;
            }

            try
            {
                uiapp.PostCommand(commandId);
                return Result.Succeeded;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.InvalidOperationException || ex is Autodesk.Revit.Exceptions.ArgumentException)
            {
                TaskDialog.Show(
                    "Быстрая команда",
                    $"Не удалось запустить системную команду {commandName}.\n\nВозможно, Revit уже находится внутри другой команды или текущий вид не поддерживает эту операцию.\n\n" + ex.Message);
                return Result.Cancelled;
            }
        }

        private static bool IsLinear(DimensionType type)
        {
            return type.StyleType == DimensionStyleType.Linear
                || type.StyleType == DimensionStyleType.LinearFixed;
        }

        private static bool IsDetailItem(FamilySymbol symbol)
        {
            return symbol.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DetailComponents;
        }

        private static void ShowTypeNotFound(DimensionQuickCommandConfig config, string typeLabel)
        {
            TaskDialog.Show(
                "Быстрая команда",
                $"Не найден {typeLabel} \"{config.DimensionTypeName}\".\n\nОткройте менеджер и обновите настройку слота БК{config.SlotNumber}.");
        }
    }
}
