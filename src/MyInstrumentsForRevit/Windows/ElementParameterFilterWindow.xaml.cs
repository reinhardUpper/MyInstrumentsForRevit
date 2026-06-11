using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyInstrumentsForRevit.Filters;

namespace MyInstrumentsForRevit.Windows
{
    public partial class ElementParameterFilterWindow : Window
    {
        private readonly IReadOnlyList<ElementParameterFilterCandidate> _parameters;

        public ElementParameterFilterWindow(IReadOnlyList<ElementParameterFilterCandidate> parameters)
        {
            InitializeComponent();
            _parameters = parameters;
            ApplyFilter();
            SearchTextBox.Focus();
        }

        public ElementParameterFilterCandidate? SelectedParameter { get; private set; }

        public bool IsolateSimilar { get; private set; }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatus();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            Complete(true);
            e.Handled = true;
        }

        private void OnIsolateClick(object sender, RoutedEventArgs e)
        {
            Complete(true);
        }

        private void OnHideClick(object sender, RoutedEventArgs e)
        {
            Complete(false);
        }

        private void Complete(bool isolateSimilar)
        {
            var selected = ParameterListBox.SelectedItem as ElementParameterFilterCandidate;
            if (selected == null)
            {
                StatusTextBlock.Text = "Выберите параметр.";
                return;
            }

            SelectedParameter = selected;
            IsolateSimilar = isolateSimilar;
            DialogResult = true;
        }

        private void ApplyFilter()
        {
            string query = (SearchTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            List<ElementParameterFilterCandidate> filtered = _parameters
                .Where(parameter => query.Length == 0 || parameter.SearchText.Contains(query))
                .OrderBy(parameter => parameter.Name)
                .ThenBy(parameter => parameter.DisplayValue)
                .ToList();

            ParameterListBox.ItemsSource = filtered;
            if (filtered.Count > 0)
            {
                ParameterListBox.SelectedIndex = 0;
            }

            StatusTextBlock.Text = filtered.Count == 0
                ? "Параметры не найдены."
                : "Найдено: " + filtered.Count;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (ParameterListBox.SelectedItem is ElementParameterFilterCandidate selected)
            {
                StatusTextBlock.Text = selected.Name + " = " + selected.DisplayValue;
            }
        }
    }
}
