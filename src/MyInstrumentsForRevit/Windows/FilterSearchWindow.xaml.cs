using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using MyInstrumentsForRevit.Filters;

namespace MyInstrumentsForRevit.Windows
{
    public partial class FilterSearchWindow : Window
    {
        private readonly IReadOnlyList<FilterSearchItem> _filters;
        private readonly Func<FilterItem, bool, bool> _applyVisibility;

        public FilterSearchWindow(
            IReadOnlyList<FilterItem> filters,
            View activeView,
            Func<FilterItem, bool, bool> applyVisibility)
        {
            InitializeComponent();
            _filters = filters
                .Select(filter => new FilterSearchItem(filter, ViewFilterApplicator.GetState(activeView, filter.Id)))
                .OrderBy(filter => filter.State == FilterViewState.NotApplied ? 1 : 0)
                .ThenBy(filter => filter.State == FilterViewState.Hidden ? 1 : 0)
                .ThenBy(filter => filter.Name)
                .ToList();
            _applyVisibility = applyVisibility;

            ApplyFilter();
            SearchTextBox.Focus();
        }

        public FilterItem? SelectedFilter { get; private set; }

        public bool MakeVisible { get; private set; }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionHint();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CompleteToggle();
            e.Handled = true;
        }

        private void OnToggleClick(object sender, RoutedEventArgs e)
        {
            CompleteToggle();
        }

        private void OnHideClick(object sender, RoutedEventArgs e)
        {
            Complete(false);
        }

        private void OnShowClick(object sender, RoutedEventArgs e)
        {
            Complete(true);
        }

        private void CompleteToggle()
        {
            FilterSearchItem? selectedItem = FilterListBox.SelectedItem as FilterSearchItem;
            if (selectedItem == null)
            {
                StatusTextBlock.Text = "Выберите фильтр из списка.";
                return;
            }

            ApplyAction(!selectedItem.IsOnView || !selectedItem.IsVisible);
        }

        private void Complete(bool makeVisible)
        {
            ApplyAction(makeVisible);
        }

        private void ApplyAction(bool makeVisible)
        {
            FilterSearchItem? selectedItem = FilterListBox.SelectedItem as FilterSearchItem;
            if (selectedItem == null)
            {
                StatusTextBlock.Text = "Выберите фильтр из списка.";
                return;
            }

            SelectedFilter = selectedItem.Filter;
            MakeVisible = makeVisible;

            if (!_applyVisibility(selectedItem.Filter, makeVisible))
            {
                return;
            }

            selectedItem.State = makeVisible ? FilterViewState.Visible : FilterViewState.Hidden;
            ApplyFilter(selectedItem.Filter.Id.IntegerValue);
            StatusTextBlock.Text = makeVisible
                ? "Фильтр включен на текущем виде."
                : "Фильтр выключен на текущем виде.";
        }

        private void ApplyFilter(int? selectedFilterId = null)
        {
            string query = SearchTextBox.Text ?? string.Empty;
            List<FilterSearchItem> filtered = _filters
                .Where(filter => string.IsNullOrWhiteSpace(query)
                    || filter.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .OrderBy(filter => filter.State == FilterViewState.NotApplied ? 1 : 0)
                .ThenBy(filter => filter.State == FilterViewState.Hidden ? 1 : 0)
                .ThenBy(filter => filter.Name)
                .Take(200)
                .ToList();

            FilterListBox.ItemsSource = filtered;
            if (filtered.Count > 0)
            {
                int index = selectedFilterId.HasValue
                    ? filtered.FindIndex(filter => filter.Filter.Id.IntegerValue == selectedFilterId.Value)
                    : -1;
                FilterListBox.SelectedIndex = index >= 0 ? index : 0;
            }

            StatusTextBlock.Text = filtered.Count == 0
                ? "Фильтры не найдены."
                : "Найдено: " + filtered.Count;
            UpdateSelectionHint();
        }

        private void UpdateSelectionHint()
        {
            FilterSearchItem? selectedItem = FilterListBox.SelectedItem as FilterSearchItem;
            if (selectedItem == null)
            {
                ToggleButton.Content = "Переключить";
                return;
            }

            if (selectedItem.State == FilterViewState.Visible)
            {
                ToggleButton.Content = "Выключить";
                StatusTextBlock.Text = "Фильтр включен на текущем виде. Enter выключит его.";
            }
            else if (selectedItem.State == FilterViewState.Hidden)
            {
                ToggleButton.Content = "Включить";
                StatusTextBlock.Text = "Фильтр выключен на текущем виде. Enter включит его.";
            }
            else
            {
                ToggleButton.Content = "Добавить";
                StatusTextBlock.Text = "Фильтра нет на текущем виде. Enter добавит и включит его.";
            }
        }
    }
}
