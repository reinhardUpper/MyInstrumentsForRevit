using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyInstrumentsForRevit.Windows
{
    public partial class RebarAnchorageWindow : Window
    {
        private static readonly int[] Diameters = { 10, 12, 16, 20, 25, 28, 32, 36 };
        private static readonly string[] Concretes = { "B25", "B30", "B35", "B40", "B50" };
        private static readonly double[] Alphas = { 0.9, 1.0, 1.2, 2.0 };

        private static readonly Dictionary<string, int[]> AnchorByConcrete = new Dictionary<string, int[]>
        {
            ["B25"] = new[] { 450, 500, 700, 850, 1050, 1200, 1350, 1650 },
            ["B30"] = new[] { 400, 450, 600, 750, 950, 1100, 1250, 1550 },
            ["B35"] = new[] { 350, 400, 550, 700, 850, 950, 1100, 1350 },
            ["B40"] = new[] { 350, 400, 500, 650, 800, 900, 1000, 1250 },
            ["B50"] = new[] { 300, 350, 450, 550, 700, 800, 900, 1100 }
        };

        private static readonly Dictionary<string, int[]> Lap12ByConcrete = new Dictionary<string, int[]>
        {
            ["B25"] = new[] { 500, 600, 800, 1000, 1250, 1400, 1600, 2000 },
            ["B30"] = new[] { 500, 550, 750, 950, 1150, 1300, 1500, 1850 },
            ["B35"] = new[] { 450, 500, 650, 850, 1050, 1150, 1300, 1600 },
            ["B40"] = new[] { 400, 450, 600, 750, 950, 1050, 1200, 1500 },
            ["B50"] = new[] { 350, 400, 550, 700, 850, 950, 1050, 1350 }
        };

        private static readonly Dictionary<string, int[]> Lap20ByConcrete = new Dictionary<string, int[]>
        {
            ["B25"] = new[] { 850, 1000, 1350, 1700, 2100, 2350, 2700, 3350 },
            ["B30"] = new[] { 800, 950, 1250, 1550, 1900, 2150, 2450, 3050 },
            ["B35"] = new[] { 700, 850, 1100, 1350, 1700, 1900, 2150, 2700 },
            ["B40"] = new[] { 650, 750, 1000, 1250, 1600, 1750, 2000, 2500 },
            ["B50"] = new[] { 550, 700, 900, 1100, 1400, 1550, 1750, 2200 }
        };

        private readonly RebarAnchorageSettings _settings;
        private int _selectedDiameter;
        private bool _isUpdating;

        public RebarAnchorageWindow()
        {
            InitializeComponent();

            _settings = RebarAnchorageSettings.Load();
            _selectedDiameter = NormalizeDiameter(_settings.Diameter);
            ConcreteComboBox.ItemsSource = Concretes;

            ApplySettings();
            RefreshCurrentValue();
            Focus();
        }

        private string SelectedConcrete => ConcreteComboBox.SelectedItem as string ?? Concretes[0];

        private int SelectedDiameter
        {
            get
            {
                return _selectedDiameter;
            }
        }

        private double SelectedAlpha
        {
            get
            {
                if (Alpha09RadioButton.IsChecked == true)
                {
                    return 0.9;
                }

                if (Alpha12RadioButton.IsChecked == true)
                {
                    return 1.2;
                }

                if (Alpha20RadioButton.IsChecked == true)
                {
                    return 2.0;
                }

                return 1.0;
            }
        }

        private void ApplySettings()
        {
            _isUpdating = true;

            ConcreteComboBox.SelectedItem = Concretes.Contains(_settings.Concrete)
                ? _settings.Concrete
                : "B25";

            SetAlpha(_settings.Alpha);

            _isUpdating = false;
        }

        private void SetAlpha(double alpha)
        {
            Alpha09RadioButton.IsChecked = Math.Abs(alpha - 0.9) < 0.01;
            Alpha10RadioButton.IsChecked = Math.Abs(alpha - 1.0) < 0.01 || !Alphas.Contains(alpha);
            Alpha12RadioButton.IsChecked = Math.Abs(alpha - 1.2) < 0.01;
            Alpha20RadioButton.IsChecked = Math.Abs(alpha - 2.0) < 0.01;
        }

        private void RefreshCurrentValue()
        {
            int selectedDiameter = NormalizeDiameter(SelectedDiameter);
            _isUpdating = true;
            _selectedDiameter = selectedDiameter;
            _isUpdating = false;

            UpdateSummary();
            SaveSettings();
        }

        private static int NormalizeDiameter(int diameter)
        {
            return Diameters.Contains(diameter) ? diameter : Diameters[0];
        }

        private static int GetAnchorageLength(string concrete, double alpha, int diameterIndex)
        {
            if (Math.Abs(alpha - 1.2) < 0.01)
            {
                return Lap12ByConcrete[concrete][diameterIndex];
            }

            if (Math.Abs(alpha - 2.0) < 0.01)
            {
                return Lap20ByConcrete[concrete][diameterIndex];
            }

            int baseLength = AnchorByConcrete[concrete][diameterIndex];
            if (Math.Abs(alpha - 0.9) < 0.01)
            {
                return RoundUpTo(baseLength * 0.9, 50);
            }

            return baseLength;
        }

        private static int RoundUpTo(double value, int step)
        {
            return (int)(Math.Ceiling(value / step) * step);
        }

        private static string GetAlphaDescription(double alpha)
        {
            if (Math.Abs(alpha - 0.9) < 0.01)
            {
                return "\u0430\u043D\u043A\u0435\u0440\u043E\u0432\u043A\u0430 \u043F\u0440\u0438 \u0441\u0436\u0430\u0442\u0438\u0438";
            }

            if (Math.Abs(alpha - 1.2) < 0.01)
            {
                return "\u043D\u0430\u0445\u043B\u0435\u0441\u0442 100% \u0432 \u0440\u0430\u0441\u0447\u0435\u0442\u043D\u043E\u043C \u0441\u0435\u0447\u0435\u043D\u0438\u0438";
            }

            if (Math.Abs(alpha - 2.0) < 0.01)
            {
                return "\u043D\u0430\u0445\u043B\u0435\u0441\u0442 100% \u0432 \u0440\u0430\u0441\u0447\u0435\u0442\u043D\u043E\u043C \u0441\u0435\u0447\u0435\u043D\u0438\u0438";
            }

            return "\u0430\u043D\u043A\u0435\u0440\u043E\u0432\u043A\u0430 \u043F\u0440\u0438 \u0440\u0430\u0441\u0442\u044F\u0436\u0435\u043D\u0438\u0438";
        }

        private void OnConcreteSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                RefreshCurrentValue();
            }
        }

        private void OnAlphaChecked(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating && IsLoaded)
            {
                RefreshCurrentValue();
            }
        }

        private void OnPreviousDiameterClick(object sender, RoutedEventArgs e)
        {
            MoveDiameter(-1);
        }

        private void OnNextDiameterClick(object sender, RoutedEventArgs e)
        {
            MoveDiameter(1);
        }

        private void OnCurrentValueMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CopySelectedLength();
        }

        private void OnCopyClick(object sender, RoutedEventArgs e)
        {
            CopySelectedLength();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
            {
                CopySelectedLength();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                MoveDiameter(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                MoveDiameter(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                MoveConcrete(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                MoveConcrete(1);
                e.Handled = true;
            }
        }

        private void MoveDiameter(int offset)
        {
            int currentIndex = Array.IndexOf(Diameters, _selectedDiameter);
            int nextIndex = Clamp(currentIndex + offset, 0, Diameters.Length - 1);
            _selectedDiameter = Diameters[nextIndex];
            RefreshCurrentValue();
        }

        private void MoveConcrete(int offset)
        {
            int currentIndex = Array.IndexOf(Concretes, SelectedConcrete);
            int nextIndex = Clamp(currentIndex + offset, 0, Concretes.Length - 1);
            ConcreteComboBox.SelectedIndex = nextIndex;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private void CopySelectedLength()
        {
            AnchorageRow row = GetCurrentRow();

            try
            {
                Clipboard.SetText(row.Length.ToString(CultureInfo.InvariantCulture));
                StatusTextBlock.Text = "\u0421\u043A\u043E\u043F\u0438\u0440\u043E\u0432\u0430\u043D\u043E: " + row.LengthText;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                StatusTextBlock.Text = "\u041D\u0435 \u0443\u0434\u0430\u043B\u043E\u0441\u044C \u0441\u043A\u043E\u043F\u0438\u0440\u043E\u0432\u0430\u0442\u044C \u0437\u043D\u0430\u0447\u0435\u043D\u0438\u0435.";
            }
        }

        private void UpdateSummary()
        {
            AnchorageRow row = GetCurrentRow();

            CurrentDiameterTextBlock.Text = row.DiameterText;
            CurrentDescriptionTextBlock.Text = row.Description;
            CurrentLengthTextBlock.Text = row.LengthText;
            SelectedValueTextBlock.Text = "\u0424 " + row.Diameter + " - " + row.LengthText;
            StatusTextBlock.Text = SelectedConcrete + ", alpha " + SelectedAlpha.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private AnchorageRow GetCurrentRow()
        {
            int diameter = NormalizeDiameter(_selectedDiameter);
            int diameterIndex = Array.IndexOf(Diameters, diameter);
            return new AnchorageRow(
                diameter,
                GetAnchorageLength(SelectedConcrete, SelectedAlpha, diameterIndex),
                GetAlphaDescription(SelectedAlpha));
        }
        private void SaveSettings()
        {
            if (_isUpdating)
            {
                return;
            }

            _settings.Concrete = SelectedConcrete;
            _settings.Diameter = SelectedDiameter;
            _settings.Alpha = SelectedAlpha;
            _settings.Save();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            SaveSettings();
        }

        private sealed class AnchorageRow
        {
            public AnchorageRow(int diameter, int length, string description)
            {
                Diameter = diameter;
                Length = length;
                Description = description;
            }

            public int Diameter { get; }

            public int Length { get; }

            public string Description { get; }

            public string DiameterText => "\u0424 " + Diameter;

            public string LengthText => Length + " \u043C\u043C";
        }

        private sealed class RebarAnchorageSettings
        {
            private static readonly string SettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MyInstrumentsForRevit",
                "RebarAnchorage.settings");

            public string Concrete { get; set; } = "B25";

            public int Diameter { get; set; } = 10;

            public double Alpha { get; set; } = 1.0;

            public static RebarAnchorageSettings Load()
            {
                var settings = new RebarAnchorageSettings();
                if (!File.Exists(SettingsPath))
                {
                    return settings;
                }

                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        ApplyValue(settings, parts[0], parts[1]);
                    }
                }

                return settings;
            }

            public void Save()
            {
                string? directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(SettingsPath, new[]
                {
                    "Concrete=" + Concrete,
                    "Diameter=" + Diameter.ToString(CultureInfo.InvariantCulture),
                    "Alpha=" + Alpha.ToString(CultureInfo.InvariantCulture)
                });
            }

            private static void ApplyValue(RebarAnchorageSettings settings, string key, string value)
            {
                if (string.Equals(key, "Concrete", StringComparison.OrdinalIgnoreCase) && Concretes.Contains(value))
                {
                    settings.Concrete = value;
                }
                else if (string.Equals(key, "Diameter", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int diameter)
                         && Diameters.Contains(diameter))
                {
                    settings.Diameter = diameter;
                }
                else if (string.Equals(key, "Alpha", StringComparison.OrdinalIgnoreCase)
                         && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double alpha)
                         && Alphas.Any(item => Math.Abs(item - alpha) < 0.01))
                {
                    settings.Alpha = alpha;
                }
            }
        }
    }
}


