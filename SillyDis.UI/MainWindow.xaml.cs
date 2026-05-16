using System;
using System.Windows;
using ICSharpCode.AvalonEdit.Folding;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SillyDis.UI.Helpers;
using SillyDis.UI.ViewModels;

namespace SillyDis.UI
{
    public partial class MainWindow : Window
    {
        private FoldingManager _foldingManager;
        private BraceFoldingStrategy _foldingStrategy;
        private System.Windows.Threading.DispatcherTimer _plotTimer;
        private ScottPlot.Plottables.Signal? _telemetrySignal;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            DataContext = viewModel;

            // AvalonEdit folding
            _foldingManager  = FoldingManager.Install(PduEditor.TextArea);
            _foldingStrategy = new BraceFoldingStrategy();

            // ScottPlot setup
            TelemetryPlot.Plot.Axes.Title.Label.Text = "Throughput (PDUs / Second)";
            TelemetryPlot.Plot.Axes.Bottom.Label.Text = "Seconds";
            TelemetryPlot.Plot.Axes.SetLimitsX(0, 60);

            _plotTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _plotTimer.Tick += PlotTimer_Tick;
            _plotTimer.Start();
        }

        private void PlotTimer_Tick(object? sender, EventArgs e)
        {
            if (_telemetrySignal == null) return;

            var vm = DataContext as MainViewModel;
            if (vm?.SelectedSession == null) return;

            double maxVal = 0;
            foreach (var v in vm.SelectedSession.TelemetryHistory)
                if (v > maxVal) maxVal = v;

            TelemetryPlot.Plot.Axes.SetLimitsY(0, Math.Max(10, maxVal * 1.2));
            TelemetryPlot.Refresh();
        }

        private void ListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Core.Models.PduItem pdu)
            {
                UpdatePduEditor(pdu);
            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source is not System.Windows.Controls.TabControl) return;

            var vm = DataContext as MainViewModel;

            TelemetryPlot.Plot.Clear();
            _telemetrySignal = null;

            if (vm?.SelectedSession != null)
            {
                UpdatePduEditor(vm.SelectedSession.SelectedPdu);
                _telemetrySignal = TelemetryPlot.Plot.Add.Signal(vm.SelectedSession.TelemetryHistory);
            }
            else
            {
                UpdatePduEditor(null);
            }

            TelemetryPlot.Refresh();
        }

        private void UpdatePduEditor(Core.Models.PduItem? pdu)
        {
            PduEditor.Text = pdu?.FormattedPayload ?? string.Empty;
            if (_foldingManager != null)
                _foldingStrategy.UpdateFoldings(_foldingManager, PduEditor.Document);
        }

        private void RebroadcastButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.SelectedSession?.SelectedPdu?.RawBytes is byte[] bytes)
            {
                vm.RebroadcastPduCommand.Execute(bytes);
            }
            else
            {
                MessageBox.Show("No PDU selected to re-broadcast.", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(ThemeToggle.IsChecked == true
                ? BaseTheme.Dark
                : BaseTheme.Light);
            paletteHelper.SetTheme(theme);
        }
    }
}