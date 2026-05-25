using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MyRevitTools.DimensionQuickCommands
{
    public partial class DimensionQuickCommandManagerWindow : Window
    {
        private readonly UIApplication uiapp;
        private readonly ObservableCollection<DimensionQuickCommandConfig> configs = new ObservableCollection<DimensionQuickCommandConfig>();
        private List<NamedElementInfo> availableTypes = new List<NamedElementInfo>();
        private DimensionQuickCommandConfig? selectedConfig;

        public DimensionQuickCommandManagerWindow(UIApplication uiapp)
        {
            InitializeComponent();
            this.uiapp = uiapp;
            CommandsGrid.ItemsSource = configs;
            SlotComboBox.ItemsSource = Enumerable.Range(1, 4).Select(number => new SlotInfo(number)).ToList();
            LoadAvailableTypes();
            LoadConfigs();
            ClearEditor();
        }

        private Document? Document => uiapp.ActiveUIDocument?.Document;

        private void LoadConfigs()
        {
            configs.Clear();
            foreach (DimensionQuickCommandConfig config in DimensionQuickCommandStorage.Load())
            {
                configs.Add(config);
            }

            StatusTextBlock.Text = $"Загружено команд: {configs.Count}";
        }

        private void LoadAvailableTypes()
        {
            Document? doc = Document;
            if (doc == null)
            {
                availableTypes = new List<NamedElementInfo>();
                DimensionTypeComboBox.ItemsSource = availableTypes;
                return;
            }

            availableTypes = DimensionTypeCollector.GetAvailableTypes(doc);
            DimensionTypeComboBox.ItemsSource = availableTypes;
            StatusTextBlock.Text = availableTypes.Count == 0
                ? "В документе не найдены типы размеров и элементов узлов."
                : $"Типов пресетов: {availableTypes.Count}";
        }

        private void OnCommandSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedConfig = CommandsGrid.SelectedItem as DimensionQuickCommandConfig;
            FillEditor(selectedConfig);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string displayName = DisplayNameTextBox.Text.Trim();
            string hotkeyText = HotkeyTextBox.Text.Trim();
            var selectedType = DimensionTypeComboBox.SelectedItem as NamedElementInfo;
            var selectedSlot = SlotComboBox.SelectedItem as SlotInfo;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                TaskDialog.Show("Менеджер быстрых команд", "Введите имя команды.");
                return;
            }

            if (selectedType == null)
            {
                TaskDialog.Show("Менеджер быстрых команд", "Выберите тип размера или элемента узла.");
                return;
            }

            if (selectedSlot == null)
            {
                TaskDialog.Show("Менеджер быстрых команд", "Выберите слот быстрой команды.");
                return;
            }

            DimensionQuickCommandConfig? occupied = configs.FirstOrDefault(config =>
                config.SlotNumber == selectedSlot.Number
                && (selectedConfig == null || config.Id != selectedConfig.Id));

            if (occupied != null)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "Менеджер быстрых команд",
                    $"Слот БК{selectedSlot.Number} уже занят командой \"{occupied.DisplayName}\".\n\nЗаменить?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (result != TaskDialogResult.Yes)
                {
                    return;
                }

                configs.Remove(occupied);
            }

            DateTime now = DateTime.Now;
            if (selectedConfig == null)
            {
                selectedConfig = new DimensionQuickCommandConfig
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now
                };
                configs.Add(selectedConfig);
            }

            selectedConfig.DisplayName = displayName;
            selectedConfig.HotkeyText = hotkeyText;
            selectedConfig.CommandKind = selectedType.Kind;
            selectedConfig.DimensionTypeName = selectedType.Name;
            selectedConfig.DimensionTypeUniqueId = selectedType.UniqueId;
            selectedConfig.DimensionTypeElementId = selectedType.IntegerId;
            selectedConfig.SlotNumber = selectedSlot.Number;
            selectedConfig.UpdatedAt = now;

            SaveConfigs();
            CommandsGrid.Items.Refresh();
            CommandsGrid.SelectedItem = selectedConfig;
            StatusTextBlock.Text = $"Сохранено: {selectedConfig.DisplayName}";
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (selectedConfig == null)
            {
                TaskDialog.Show("Менеджер быстрых команд", "Выберите команду для удаления.");
                return;
            }

            DimensionQuickCommandConfig removed = selectedConfig;
            configs.Remove(removed);
            selectedConfig = null;
            SaveConfigs();
            ClearEditor();
            StatusTextBlock.Text = $"Удалено: {removed.DisplayName}";
        }

        private void OnRunClick(object sender, RoutedEventArgs e)
        {
            DimensionQuickCommandConfig? config = selectedConfig;
            if (config == null)
            {
                TaskDialog.Show("Менеджер быстрых команд", "Выберите команду для запуска.");
                return;
            }

            DimensionQuickCommandExecutor.Execute(uiapp, config);
            Close();
        }

        private void OnRefreshTypesClick(object sender, RoutedEventArgs e)
        {
            LoadAvailableTypes();
        }

        private void OnNewClick(object sender, RoutedEventArgs e)
        {
            CommandsGrid.SelectedItem = null;
            selectedConfig = null;
            ClearEditor();
        }

        private void FillEditor(DimensionQuickCommandConfig? config)
        {
            if (config == null)
            {
                ClearEditor();
                return;
            }

            DisplayNameTextBox.Text = config.DisplayName;
            HotkeyTextBox.Text = config.HotkeyText;
            SlotComboBox.SelectedItem = SlotComboBox.Items.Cast<SlotInfo>().FirstOrDefault(slot => slot.Number == config.SlotNumber);
            string kind = QuickCommandKind.Normalize(config.CommandKind);
            DimensionTypeComboBox.SelectedItem = availableTypes.FirstOrDefault(type =>
                    type.IntegerId == config.DimensionTypeElementId
                    && string.Equals(type.Kind, kind, StringComparison.OrdinalIgnoreCase))
                ?? availableTypes.FirstOrDefault(type =>
                    string.Equals(type.UniqueId, config.DimensionTypeUniqueId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(type.Kind, kind, StringComparison.OrdinalIgnoreCase))
                ?? availableTypes.FirstOrDefault(type =>
                    string.Equals(type.Name, config.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase)
                    && string.Equals(type.Kind, kind, StringComparison.OrdinalIgnoreCase));
        }

        private void ClearEditor()
        {
            DisplayNameTextBox.Text = string.Empty;
            HotkeyTextBox.Text = string.Empty;
            DimensionTypeComboBox.SelectedItem = null;
            SlotComboBox.SelectedItem = null;
        }

        private void SaveConfigs()
        {
            DimensionQuickCommandStorage.Save(configs.ToList());
            DimensionQuickCommandStorage.InvalidateCache();
        }

        private sealed class SlotInfo
        {
            public SlotInfo(int number)
            {
                Number = number;
                DisplayName = $"БК{number}";
            }

            public int Number { get; }

            public string DisplayName { get; }
        }
    }
}
