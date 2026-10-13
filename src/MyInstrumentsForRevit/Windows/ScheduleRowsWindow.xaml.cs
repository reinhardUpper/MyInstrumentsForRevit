using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MyInstrumentsForRevit.ScheduleRows;

namespace MyInstrumentsForRevit.Windows
{
    public partial class ScheduleRowsWindow : Window
    {
        private readonly Action<IReadOnlyCollection<ScheduleRow>> _requestSelection;
        private readonly Action _disposeController;
        internal ScheduleRowsWindow(
            string scheduleName,
            IReadOnlyList<string> headers,
            IReadOnlyList<ScheduleRow> rows,
            Action<IReadOnlyCollection<ScheduleRow>> requestSelection,
            Action disposeController)
        {
            InitializeComponent();
            _requestSelection = requestSelection;
            _disposeController = disposeController;
            Title = "Строки: " + scheduleName;
            DescriptionTextBlock.Text = "Выберите одну или несколько строк, затем нажмите «Выбрать в модели».";
            var table = new DataTable();
            table.Columns.Add("_row", typeof(ScheduleRow));
            foreach (string header in headers)
            {
                string name = header; int suffix = 2;
                while (table.Columns.Contains(name)) name = header + " (" + suffix++ + ")";
                table.Columns.Add(name, typeof(string));
            }
            foreach (ScheduleRow scheduleRow in rows)
            {
                DataRow row = table.NewRow(); row["_row"] = scheduleRow;
                for (int column = 0; column < scheduleRow.Cells.Count; column++) row[column + 1] = scheduleRow.Cells[column];
                table.Rows.Add(row);
            }
            RowsGrid.AutoGeneratingColumn += OnAutoGeneratingColumn;
            RowsGrid.ItemsSource = table.DefaultView;
            StatusTextBlock.Text = "Строк: " + rows.Count;
        }
        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "_row") e.Cancel = true;
        }
        public void SetStatus(string status)
        {
            StatusTextBlock.Text = status;
        }
        private void OnSelectInModelClick(object sender, RoutedEventArgs e)
        {
            List<ScheduleRow> rows = RowsGrid.SelectedItems.OfType<DataRowView>()
                .Select(row => row["_row"] as ScheduleRow).Where(row => row != null).Cast<ScheduleRow>().ToList();
            if (rows.Count == 0)
            {
                SetStatus("Выберите хотя бы одну строку.");
                return;
            }
            SetStatus("Выбираю элементы в модели...");
            _requestSelection(rows);
        }
        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _disposeController();
        }
    }
}
