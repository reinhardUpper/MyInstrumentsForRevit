using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Filters;
using MyInstrumentsForRevit.Graphics;
using MyInstrumentsForRevit.Windows;
using MyInstrumentsForRevit.Commands;
using MyRevitTools.DimensionQuickCommands;

namespace MyInstrumentsForRevit.CommandLine
{
    internal static class CommandRegistry
    {
        private static readonly Dictionary<string, RegisteredCommand> Commands =
            new Dictionary<string, RegisteredCommand>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, RegisteredCommand> RevitCommands =
            new Dictionary<string, RegisteredCommand>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<ElementId>> HiddenLinksByView =
            new Dictionary<string, List<ElementId>>();

        static CommandRegistry()
        {
            Register("help", "Список команд", "Показать список доступных команд.", ShowHelp);
            Register("filters.refresh", "Обновить фильтры", "Обновить кэш фильтров проекта.", RefreshFilters);
            Register("filters.add", "Добавить фильтр на вид", "Открыть поиск фильтра и добавить/переключить его на активном виде.", AddViewFilter);

            Register("graphics.hatches", "Штриховка категорий", "Скрыть или вернуть штриховку основных конструктивных категорий.", ToggleStructuralHatches);
            Register("graphics.rebar_view", "Вид армирования", "Настроить активный вид под армирование.", ConfigureRebarView);
            Register("graphics.formwork_view", "Вид опалубки", "Настроить активный вид под опалубку.", ConfigureFormworkView);
            Register("graphics.revit_links", "Revit связи", "Скрыть или вернуть Revit-связи на активном виде.", ToggleRevitLinks);
            Register("rebar.toggle", "Вкл/выкл арматуру", "Включить или выключить категорию несущей арматуры на активном виде.", ToggleRebar);
            Register("view.3d", "Настроить 3D вид", "Применить стандартную настройку активного 3D вида.", Configure3DView);
            Register("view.grids", "Оси вида", "Подрезать оси на текущем виде и поставить размеры.", ArrangeGridsOnCurrentView);
            Register("sheet.duplicate", "Дубль листа", "Дублировать активный лист.", DuplicateActiveSheet);

            Register("quick.manager", "Менеджер размеров", "Открыть менеджер быстрых пресетов БК1-БК4.", OpenQuickCommandManager);
            Register("quick.bk1", "БК1", "Запустить быстрый пресет БК1.", uiApplication => ExecuteQuickSlot(uiApplication, 1));
            Register("quick.bk2", "БК2", "Запустить быстрый пресет БК2.", uiApplication => ExecuteQuickSlot(uiApplication, 2));
            Register("quick.bk3", "БК3", "Запустить быстрый пресет БК3.", uiApplication => ExecuteQuickSlot(uiApplication, 3));
            Register("quick.bk4", "БК4", "Запустить быстрый пресет БК4.", uiApplication => ExecuteQuickSlot(uiApplication, 4));

            RegisterRevitPostableCommands();
        }

        public static IReadOnlyCollection<RegisteredCommand> AllCommands
        {
            get
            {
                return GetExecutableCommands().Values
                    .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public static IReadOnlyCollection<RegisteredCommand> BaseCommands =>
            GetExecutableCommands().Values.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToList();

        public static bool HasBaseCommand(string name)
        {
            return GetExecutableCommands().ContainsKey(name);
        }

        public static void Register(string name, string description, Action<UIApplication> execute)
        {
            Commands[name] = new RegisteredCommand(name, description, execute);
        }

        public static void Register(string name, string displayName, string description, Action<UIApplication> execute)
        {
            Commands[name] = new RegisteredCommand(name, displayName, description, execute);
        }

        public static bool TryExecute(string input, UIApplication uiApplication, out string error)
        {
            string commandName = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(commandName))
            {
                error = "Введите имя команды.";
                return false;
            }

            Dictionary<string, RegisteredCommand> executableCommands = GetExecutableCommands();

            if (!executableCommands.TryGetValue(commandName, out RegisteredCommand command))
            {
                error = "Команда не найдена: " + commandName;
                return false;
            }

            command.Execute(uiApplication);
            error = string.Empty;
            return true;
        }

        private static Dictionary<string, RegisteredCommand> GetExecutableCommands()
        {
            return Commands
                .Concat(RevitCommands)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static void RegisterRevitPostableCommands()
        {
            foreach (PostableCommand postableCommand in Enum.GetValues(typeof(PostableCommand)).Cast<PostableCommand>())
            {
                string commandName = postableCommand.ToString();
                string registryName = "revit." + commandName;
                RevitCommands[registryName] = new RegisteredCommand(
                    registryName,
                    "Revit: " + commandName,
                    "Стандартная команда Revit PostableCommand.",
                    uiApplication => PostRevitCommand(uiApplication, postableCommand));
            }
        }

        private static void PostRevitCommand(UIApplication uiApplication, PostableCommand postableCommand)
        {
            RevitCommandId commandId = RevitCommandId.LookupPostableCommandId(postableCommand);
            if (commandId == null)
            {
                TaskDialog.Show("Командная строка", "Команда Revit недоступна: " + postableCommand);
                return;
            }

            try
            {
                uiApplication.PostCommand(commandId);
            }
            catch (Exception exception) when (exception is Autodesk.Revit.Exceptions.ArgumentException || exception is Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                TaskDialog.Show("Командная строка", "Не удалось запустить команду Revit: " + postableCommand + "\n\n" + exception.Message);
            }
        }

        private static void ShowHelp(UIApplication uiApplication)
        {
            string commands = string.Join(
                Environment.NewLine,
                AllCommands.Select(command => command.Name + " - " + command.Description));

            TaskDialog.Show("Командная строка", commands);
        }

        private static void RefreshFilters(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            int count = FilterCacheService.Refresh(document).Count;
            TaskDialog.Show("Командная строка", "Кэш фильтров обновлен.\nНайдено фильтров: " + count);
        }

        private static void AddViewFilter(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            if (!ViewFilterApplicator.CanUseFilters(view))
            {
                TaskDialog.Show("Командная строка", "Активный вид не поддерживает фильтры.");
                return;
            }

            if (!FilterCacheService.HasFiltersFor(document))
            {
                FilterCacheService.Refresh(document);
            }

            if (FilterCacheService.Filters.Count == 0)
            {
                TaskDialog.Show("Командная строка", "В проекте не найдено существующих фильтров.");
                return;
            }

            var window = new FilterSearchWindow(
                FilterCacheService.Filters,
                view,
                (selectedFilter, makeVisible) => ApplyCommandLineFilterVisibility(document, view, selectedFilter, makeVisible));
            window.ShowDialog();

            if (window.SelectedFilter != null && !FilterCacheService.Exists(document, window.SelectedFilter))
            {
                TaskDialog.Show("Командная строка", "Фильтр был удален или переименован. Обновите список фильтров.");
                return;
            }

        }

        private static bool ApplyCommandLineFilterVisibility(Document document, View view, FilterItem selectedFilter, bool makeVisible)
        {
            if (!FilterCacheService.Exists(document, selectedFilter))
            {
                TaskDialog.Show("Командная строка", "Фильтр был удален или переименован. Обновите список фильтров.");
                return false;
            }

            using (var transaction = new Transaction(document, "Command line: apply view filter"))
            {
                transaction.Start();
                ViewFilterApplicator.ApplyVisibility(view, selectedFilter.Id, makeVisible);
                transaction.Commit();
            }

            return true;
        }

        private static void ToggleStructuralHatches(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            View targetView = ViewGraphicsService.GetGraphicsTargetView(document, view);

            using (var transaction = new Transaction(document, "Command line: toggle structural hatches"))
            {
                transaction.Start();
                if (CategoryGraphicsStateStore.HasSavedState(document, targetView))
                {
                    CategoryGraphicsStateStore.Restore(document, targetView);
                }
                else
                {
                    CategoryGraphicsStateStore.Save(document, targetView, StructuralGraphicsCategories.MainCategories);
                    ViewGraphicsService.HideStructuralCategoryPatterns(document, targetView);
                }

                transaction.Commit();
            }
        }

        private static void ConfigureRebarView(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            using (var transaction = new Transaction(document, "Command line: configure rebar view"))
            {
                transaction.Start();
                ViewGraphicsService.ApplyStructuralCategorySettings(document, view, 2, true);
                ViewGraphicsService.SetCategoriesHidden(document, view, StructuralGraphicsCategories.RebarCategories, false);
                transaction.Commit();
            }
        }

        private static void ConfigureFormworkView(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            using (var transaction = new Transaction(document, "Command line: configure formwork view"))
            {
                transaction.Start();
                ViewGraphicsService.ApplyStructuralCategorySettings(document, view, 4, true);
                ViewGraphicsService.SetCategoriesHidden(document, view, StructuralGraphicsCategories.RebarCategories, true);
                ViewGraphicsService.HideLinksAndImportedCategories(document, view);
                transaction.Commit();
            }
        }

        private static void ToggleRevitLinks(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            string key = BuildViewKey(document, view);

            using (var transaction = new Transaction(document, "Command line: toggle Revit links"))
            {
                transaction.Start();

                if (HiddenLinksByView.TryGetValue(key, out List<ElementId> savedIds))
                {
                    List<ElementId> idsToUnhide = savedIds.Where(id => document.GetElement(id) != null).ToList();
                    if (idsToUnhide.Count > 0)
                    {
                        view.UnhideElements(idsToUnhide);
                    }

                    HiddenLinksByView.Remove(key);
                    transaction.Commit();
                    return;
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
                    TaskDialog.Show("Командная строка", "На активном виде нет видимых Revit-связей.");
                    return;
                }

                HiddenLinksByView[key] = visibleLinkIds;
                view.HideElements(visibleLinkIds);
                transaction.Commit();
            }
        }

        private static void ToggleRebar(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View view = document.ActiveView;
            Category category = Category.GetCategory(document, BuiltInCategory.OST_Rebar);
            if (category == null)
            {
                TaskDialog.Show("Командная строка", "Категория несущей арматуры не найдена.");
                return;
            }

            if (!view.CanCategoryBeHidden(category.Id))
            {
                TaskDialog.Show("Командная строка", "Активный вид не позволяет управлять видимостью арматуры.");
                return;
            }

            bool shouldHide = !view.GetCategoryHidden(category.Id);
            using (var transaction = new Transaction(document, "Command line: toggle rebar"))
            {
                transaction.Start();
                ViewGraphicsService.SetCategoriesHidden(document, view, StructuralGraphicsCategories.RebarCategories, shouldHide);
                transaction.Commit();
            }
        }

        private static void Configure3DView(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            View3D? view3D = document.ActiveView as View3D;
            if (view3D == null || view3D.IsTemplate)
            {
                TaskDialog.Show("Командная строка", "Откройте обычный 3D вид и повторите команду.");
                return;
            }

            using (var transaction = new Transaction(document, "Command line: configure 3D view"))
            {
                transaction.Start();
                view3D.DisplayStyle = DisplayStyle.Shading;
                ViewGraphicsService.SetCategoriesHidden(document, view3D, new[]
                {
                    BuiltInCategory.OST_VolumeOfInterest,
                    BuiltInCategory.OST_Levels,
                    BuiltInCategory.OST_Grids
                }, true);
                SetModelCategoriesTransparency(document, view3D, 20);
                transaction.Commit();
            }
        }

        private static void ArrangeGridsOnCurrentView(UIApplication uiApplication)
        {
            ArrangeGridsOnCurrentViewCommand.ExecuteFromCommandLine(uiApplication);
        }

        private static void OpenQuickCommandManager(UIApplication uiApplication)
        {
            var window = new DimensionQuickCommandManagerWindow(uiApplication);
            window.ShowDialog();
        }

        private static void ExecuteQuickSlot(UIApplication uiApplication, int slotNumber)
        {
            DimensionQuickCommandConfig? config = DimensionQuickCommandStorage.Load()
                .FirstOrDefault(item => item.SlotNumber == slotNumber);
            if (config == null)
            {
                TaskDialog.Show("Командная строка", "Для слота БК" + slotNumber + " команда не назначена.");
                return;
            }

            DimensionQuickCommandExecutor.Execute(uiApplication, config);
        }

        private static void DuplicateActiveSheet(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            DuplicateActiveSheetCommand.Duplicate(document);
        }

        private static void SetModelCategoriesTransparency(Document document, View view, int transparency)
        {
            foreach (Category category in document.Settings.Categories)
            {
                if (category == null || category.CategoryType != CategoryType.Model)
                {
                    continue;
                }

                if (!category.get_AllowsVisibilityControl(view))
                {
                    continue;
                }

                OverrideGraphicSettings settings = view.GetCategoryOverrides(category.Id);
                settings.SetSurfaceTransparency(transparency);
                view.SetCategoryOverrides(category.Id, settings);
            }
        }

        private static string BuildViewKey(Document document, View view)
        {
            string documentKey = string.IsNullOrWhiteSpace(document.PathName)
                ? document.GetHashCode().ToString()
                : document.PathName;

            return documentKey + ":" + view.Id.IntegerValue;
        }

        private static Document GetDocument(UIApplication uiApplication)
        {
            UIDocument uiDocument = uiApplication.ActiveUIDocument;
            if (uiDocument == null)
            {
                throw new InvalidOperationException("Нет активного документа.");
            }

            return uiDocument.Document;
        }
    }
}
