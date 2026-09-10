using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DerivativeMonitor
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public AppConfig draftConfig { get; }
        public ObservableCollection<ParameterRow> CallParameterRows { get; } = new();
        public ObservableCollection<ParameterRow> PutParameterRows { get; } = new();

        public IEnumerable<string> AvailableCodes =>
                CallParameterRows.Concat(PutParameterRows)
                    .Select(r => r.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct();
        public SettingsWindow(AppConfig draft)
        {
            InitializeComponent();
            draftConfig = draft;
            DataContext = draft;

            Logger.Log("SettingsWindow initialized with draft config.");
            // dictionary -> editable rows (this is the draft copy, so edits are temporary)
            foreach (var kv in draft.CallParametersToMonitor)
            {
                string format = draft.CallFieldFormats.TryGetValue(kv.Value, out var f) ? f : "text";
                CallParameterRows.Add(new ParameterRow { Parameter = kv.Key, Value = kv.Value, Format = format });
            }
            foreach (var kv in draft.PutParametersToMonitor)
            {
                string format = draft.PutFieldFormats.TryGetValue(kv.Value, out var f) ? f : "text";
                PutParameterRows.Add(new ParameterRow { Parameter = kv.Key, Value = kv.Value, Format = format });
            }
            // hand the rows to the grids (simplest way — no RelativeSource needed)
            CallParametersGrid.ItemsSource = CallParameterRows;
            PutParametersGrid.ItemsSource = PutParameterRows;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Log("Saving settigns config...");
                draftConfig.CallParametersToMonitor = CallParameterRows
                .Where(r => !string.IsNullOrWhiteSpace(r.Parameter))
                .ToDictionary(r => r.Parameter, r => r.Value);

                draftConfig.PutParametersToMonitor = PutParameterRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Parameter))
                    .ToDictionary(r => r.Parameter, r => r.Value);

                draftConfig.CallFieldFormats = CallParameterRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Value))
                    .ToDictionary(r => r.Value, r => r.Format);

                draftConfig.PutFieldFormats = PutParameterRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Value))
                    .ToDictionary(r => r.Value, r => r.Format);


                ConfigManager.Validate(draftConfig);

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
        }

        private void CallParametersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }

    public class ParameterRow
    {
        public string Parameter { get; set; } = "";  // the dictionary Key
        public string Value { get; set; } = "";       // the dictionary Value (RTD code)

        public string Format { get; set; } = "number";      // the format type for this parameter (money, percent, etc.)}
    }

}