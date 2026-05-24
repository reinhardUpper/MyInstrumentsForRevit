using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MyInstrumentsForRevit.Filters;

namespace MyInstrumentsForRevit.Windows
{
    public partial class FilterSearchWindow : Window
    {
        private readonly IReadOnlyList<FilterItem> _filters;

        public FilterSearchWindow(IReadOnlyList<FilterItem> filters)
        {
            InitializeComponent();
            _filters = filters;
            ApplyFilter();
            SearchTextBox.Focus();
        }

        public FilterItem? SelectedFilter { get; private set; }

        public bool MakeVisible { get; private set; }

        private void OnSearchTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void OnHideClick(object sender, RoutedEventArgs e)
        {
            Complete(false);
        }

        private void OnShowClick(object sender, RoutedEventArgs e)
        {
            Complete(true);
        }

        private void Complete(bool makeVisible)
        {
            SelectedFilter = FilterListBox.SelectedItem as FilterItem;
            if (SelectedFilter == null)
            {
                StatusTextBlock.Text = "Выберите фильтр из списка.";
                return;
            }

            MakeVisible = makeVisible;
            DialogResult = true;
            Close();
        }

        private void ApplyFilter()
        {
            string query = SearchTextBox.Text ?? string.Empty;
            List<FilterItem> filtered = _filters
                .Where(filter => string.IsNullOrWhiteSpace(query)
                    || filter.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Take(200)
                .ToList();

            FilterListBox.ItemsSource = filtered;
            if (filtered.Count > 0)
            {
                FilterListBox.SelectedIndex = 0;
            }

            StatusTextBlock.Text = filtered.Count == 0
                ? "Фильтры не найдены."
                : "Найдено: " + filtered.Count;
        }
    }
}

