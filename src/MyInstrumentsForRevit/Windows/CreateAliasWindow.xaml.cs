using System.Linq;
using System.Windows;
using MyInstrumentsForRevit.CommandLine;

namespace MyInstrumentsForRevit.Windows
{
    public partial class CreateAliasWindow : Window
    {
        public CreateAliasWindow()
        {
            InitializeComponent();
            CommandComboBox.ItemsSource = CommandRegistry.BaseCommands
                .OrderBy(command => command.Name)
                .ToList();
            AliasTextBox.Focus();
        }

        public string Alias { get; private set; } = string.Empty;

        public string CommandName { get; private set; } = string.Empty;

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            Alias = (AliasTextBox.Text ?? string.Empty).Trim();
            CommandName = CommandComboBox.SelectedItem is RegisteredCommand command
                ? command.Name
                : (CommandComboBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(Alias))
            {
                MessageBox.Show("Введите alias.", "Создать alias", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(CommandName))
            {
                MessageBox.Show("Выберите команду.", "Создать alias", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}

