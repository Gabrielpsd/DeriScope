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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DerivativeMonitor
{
    /// <summary>
    /// Interaction logic for AlertWindow.xaml
    /// </summary>
    public partial class AlertWindow : Window
    {
        public ObservableCollection<Alert> Alerts { get; }
        public AlertWindow(ObservableCollection<Alert> alerts)
        {
            InitializeComponent();

            Alerts = alerts;
            DataContext = this;
        }

        private void ClearAlertQueue(object sender, RoutedEventArgs e)
        {
            Alerts.Clear();
        }
    }
}
