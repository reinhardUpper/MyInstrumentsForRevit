using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.Filters;
using MyInstrumentsForRevit.Graphics;

namespace MyInstrumentsForRevit.CommandLine
{
    internal static class CommandRegistry
    {
        private static readonly Dictionary<string, RegisteredCommand> Commands =
            new Dictionary<string, RegisteredCommand>(StringComparer.OrdinalIgnoreCase);

        static CommandRegistry()
        {
            Register("help", "Список команд", "Показать список доступных команд.", ShowHelp);
            Register("alias.path", "Путь к alias'ам", "Показать путь к пользовательскому файлу alias'ов.", ShowAliasPath);
            Register("alias.reload", "Перечитать alias'ы", "Перечитать пользовательский файл alias'ов.", ReloadAliases);
            Register("filters.refresh", "Обновить фильтры", "Обновить кэш фильтров проекта.", RefreshFilters);
            Register("rebar.toggle", "Вкл/выкл арматуру", "Включить или выключить категорию несущей арматуры на активном виде.", ToggleRebar);
            Register("view.3d", "Настроить 3D вид", "Применить стандартную настройку активного 3D вида.", Configure3DView);

            // Добавляй свои быстрые команды здесь:
            // Register("my.command", "Описание команды.", uiApplication => { /* Revit API logic */ });
        }

        public static IReadOnlyCollection<RegisteredCommand> AllCommands
        {
            get
            {
                CommandAliasService.EnsureLoaded();
                return Commands.Values
                    .Concat(CommandAliasService.BuildAliasCommands(Commands))
                    .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public static IReadOnlyCollection<RegisteredCommand> BaseCommands =>
            Commands.Values.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToList();

        public static bool HasBaseCommand(string name)
        {
            return Commands.ContainsKey(name);
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

            CommandAliasService.EnsureLoaded();
            string resolvedCommandName = CommandAliasService.Resolve(commandName);

            if (!Commands.TryGetValue(resolvedCommandName, out RegisteredCommand command))
            {
                error = "Команда не найдена: " + commandName;
                return false;
            }

            command.Execute(uiApplication);
            error = string.Empty;
            return true;
        }

        private static void ShowHelp(UIApplication uiApplication)
        {
            string commands = string.Join(
                Environment.NewLine,
                AllCommands.Select(command => command.Name + " - " + command.Description));

            TaskDialog.Show("Командная строка", commands);
        }

        private static void ShowAliasPath(UIApplication uiApplication)
        {
            CommandAliasService.EnsureLoaded();
            TaskDialog.Show("Командная строка", CommandAliasService.AliasFilePath);
        }

        private static void ReloadAliases(UIApplication uiApplication)
        {
            CommandAliasService.Reload();
            TaskDialog.Show("Командная строка", "Alias'ы перечитаны: " + CommandAliasService.CurrentAliases.Count);
        }

        private static void RefreshFilters(UIApplication uiApplication)
        {
            Document document = GetDocument(uiApplication);
            int count = FilterCacheService.Refresh(document).Count;
            TaskDialog.Show("Командная строка", "Кэш фильтров обновлен.\nНайдено фильтров: " + count);
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
                transaction.Commit();
            }
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
