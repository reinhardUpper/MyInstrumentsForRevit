using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using MyInstrumentsForRevit.CommandLine;

namespace MyInstrumentsForRevit.Windows
{
    public partial class CommandInputWindow : Window
    {
        private readonly UIApplication _uiApplication;

        public CommandInputWindow(UIApplication uiApplication)
        {
            InitializeComponent();
            _uiApplication = uiApplication;
            RefreshSuggestions();
            CommandTextBox.Focus();
        }

        private void OnCommandTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshSuggestions();
        }

        private void OnCommandKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteCurrentCommand();
            }
            else if (e.Key == Key.Down && SuggestionsListBox.Items.Count > 0)
            {
                SuggestionsListBox.Focus();
                SuggestionsListBox.SelectedIndex = Math.Max(0, SuggestionsListBox.SelectedIndex);
            }
        }

        private void OnSuggestionDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelectedSuggestion();
        }

        private void OnSuggestionsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteSelectedSuggestion();
            }
        }

        private void ExecuteCurrentCommand()
        {
            try
            {
                if (CommandRegistry.TryExecute(CommandTextBox.Text, _uiApplication, out string error))
                {
                    DialogResult = true;
                    Close();
                    return;
                }

                StatusTextBlock.Text = error;
            }
            catch (Exception exception)
            {
                StatusTextBlock.Text = exception.Message;
            }
        }

        private void RefreshSuggestions()
        {
            string query = CommandTextBox.Text ?? string.Empty;
            var suggestions = CommandRegistry.AllCommands
                .Where(command => string.IsNullOrWhiteSpace(query)
                    || command.SearchText.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                .Take(30)
                .ToList();

            SuggestionsListBox.ItemsSource = suggestions;
            if (suggestions.Count > 0)
            {
                SuggestionsListBox.SelectedIndex = 0;
            }
        }

        private void ExecuteSelectedSuggestion()
        {
            if (SuggestionsListBox.SelectedItem is RegisteredCommand command)
            {
                CommandTextBox.Text = command.Name;
                CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
                ExecuteCurrentCommand();
            }
        }
    }
}
