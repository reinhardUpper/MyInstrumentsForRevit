using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MyInstrumentsForRevit.ScheduleRows;

namespace MyInstrumentsForRevit.Windows
{
    public partial class ScheduleRowsWindow : Window
    {
        private readonly Action<ScheduleRow> _selectElements;
        internal ScheduleRowsWindow(string scheduleName, IReadOnlyList<string> headers, IReadOnlyList<ScheduleRow> rows, Action<ScheduleRow> selectElements)
        {
            InitializeComponent();
            _selectElements = selectElements;
            Title = "Строки: " + scheduleName;
            DescriptionTextBlock.Text = "Выберите строку — элементы этой позиции будут выделены в модели.";
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
        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataRowView? rowView = RowsGrid.SelectedItem as DataRowView;
            ScheduleRow? row = rowView?["_row"] as ScheduleRow;
            if (row == null) return;
            _selectElements(row);
            StatusTextBlock.Text = row.ElementIds.Count > 0 ? "Выделено элементов: " + row.ElementIds.Count : "Не удалось сопоставить эту строку с элементами модели.";
        }
    }
}
