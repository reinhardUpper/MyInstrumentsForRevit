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
        private List<NamedElementInfo> dimensionTypes = new List<NamedElementInfo>();
        private DimensionQuickCommandConfig? selectedConfig;

        public DimensionQuickCommandManagerWindow(UIApplication uiapp)
        {
            InitializeComponent();
            this.uiapp = uiapp;
            CommandsGrid.ItemsSource = configs;
            SlotComboBox.ItemsSource = Enumerable.Range(1, 2).Select(number => new SlotInfo(number)).ToList();
            LoadDimensionTypes();
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

        private void LoadDimensionTypes()
        {
            Document? doc = Document;
            if (doc == null)
            {
                dimensionTypes = new List<NamedElementInfo>();
                DimensionTypeComboBox.ItemsSource = dimensionTypes;
                return;
            }

            dimensionTypes = DimensionTypeCollector.GetDimensionTypes(doc);
            DimensionTypeComboBox.ItemsSource = dimensionTypes;
            StatusTextBlock.Text = dimensionTypes.Count == 0
                ? "В документе не найдены типы линейных размеров."
                : $"Типов размеров: {dimensionTypes.Count}";
        }

        private void OnCommandSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            selectedConfig = CommandsGrid.SelectedItem as DimensionQuickCommandConfig;
            FillEditor(selectedConfig);
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string displayName = DisplayNameTextBox.Text.Trim();
            var selectedType = DimensionTypeComboBox.SelectedItem as NamedElementInfo;
            var selectedSlot = SlotComboBox.SelectedItem as SlotInfo;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                TaskDialog.Show("Менеджер размерных команд", "Введите имя команды.");
                return;
            }

            if (selectedType == null)
            {
                TaskDialog.Show("Менеджер размерных команд", "Выберите тип размера.");
                return;
            }

            if (selectedSlot == null)
            {
                TaskDialog.Show("Менеджер размерных команд", "Выберите слот быстрой команды.");
                return;
            }

            DimensionQuickCommandConfig? occupied = configs.FirstOrDefault(config =>
                config.SlotNumber == selectedSlot.Number
                && (selectedConfig == null || config.Id != selectedConfig.Id));

            if (occupied != null)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "Менеджер размерных команд",
                    $"\u0421\u043B\u043E\u0442 \u0411\u041A{selectedSlot.Number} \u0443\u0436\u0435 \u0437\u0430\u043D\u044F\u0442 \u043A\u043E\u043C\u0430\u043D\u0434\u043E\u0439 \"{occupied.DisplayName}\".\n\n\u0417\u0430\u043C\u0435\u043D\u0438\u0442\u044C?",
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
                TaskDialog.Show("Менеджер размерных команд", "Выберите команду для удаления.");
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
                TaskDialog.Show("Менеджер размерных команд", "Выберите команду для запуска.");
                return;
            }

            DimensionQuickCommandExecutor.Execute(uiapp, config);
            Close();
        }

        private void OnRefreshTypesClick(object sender, RoutedEventArgs e)
        {
            LoadDimensionTypes();
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
            SlotComboBox.SelectedItem = SlotComboBox.Items.Cast<SlotInfo>().FirstOrDefault(slot => slot.Number == config.SlotNumber);
            DimensionTypeComboBox.SelectedItem = dimensionTypes.FirstOrDefault(type => type.IntegerId == config.DimensionTypeElementId)
                ?? dimensionTypes.FirstOrDefault(type => string.Equals(type.UniqueId, config.DimensionTypeUniqueId, StringComparison.OrdinalIgnoreCase))
                ?? dimensionTypes.FirstOrDefault(type => string.Equals(type.Name, config.DimensionTypeName, StringComparison.CurrentCultureIgnoreCase));
        }

        private void ClearEditor()
        {
            DisplayNameTextBox.Text = string.Empty;
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
                DisplayName = $"\u0411\u041A{number}";
            }

            public int Number { get; }

            public string DisplayName { get; }
        }
    }
}
