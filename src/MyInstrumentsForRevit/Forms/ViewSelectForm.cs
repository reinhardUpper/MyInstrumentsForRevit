using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MyInstrumentsForRevit.Views;

namespace MyInstrumentsForRevit.Forms
{
    internal sealed class ViewSelectForm : Form
    {
        private readonly ComboBox _comboBox;
        private readonly IReadOnlyList<PlacedViewOption> _options;

        public ViewSelectForm(IReadOnlyList<PlacedViewOption> options)
        {
            _options = options;

            Text = "Копирование вида на текущий лист";
            Size = new Size(560, 180);
            MinimumSize = new Size(560, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;

            var label = new Label
            {
                Text = "Выберите размещенный план или вид-узел:",
                Location = new Point(16, 16),
                Size = new Size(510, 20)
            };

            _comboBox = new ComboBox
            {
                Location = new Point(16, 44),
                Size = new Size(510, 24),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            foreach (PlacedViewOption option in _options.OrderBy(option => option.ToString(), StringComparer.CurrentCultureIgnoreCase))
            {
                _comboBox.Items.Add(option);
            }

            if (_comboBox.Items.Count > 0)
            {
                _comboBox.SelectedIndex = 0;
            }

            var okButton = new Button
            {
                Text = "Скопировать и разместить",
                Location = new Point(334, 92),
                Size = new Size(190, 28),
                DialogResult = DialogResult.OK
            };
            okButton.Click += OnOkClick;

            var cancelButton = new Button
            {
                Text = "Отмена",
                Location = new Point(232, 92),
                Size = new Size(90, 28),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(label);
            Controls.Add(_comboBox);
            Controls.Add(cancelButton);
            Controls.Add(okButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public PlacedViewOption? SelectedOption { get; private set; }

        private void OnOkClick(object sender, EventArgs e)
        {
            SelectedOption = _comboBox.SelectedItem as PlacedViewOption
                ?? FindOptionByText(_comboBox.Text);

            if (SelectedOption == null)
            {
                MessageBox.Show(
                    "Выберите вид из списка.",
                    "Копирование вида",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                DialogResult = DialogResult.None;
            }
        }

        private PlacedViewOption? FindOptionByText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return _options.FirstOrDefault(option =>
                option.ToString().IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }
    }
}
