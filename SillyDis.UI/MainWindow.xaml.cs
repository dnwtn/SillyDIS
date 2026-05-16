using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Folding;
using Mapsui;
using Mapsui.Tiling;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using ScottPlot;
using SillyDis.Core.Models;
using SillyDis.Core.Services;
using SillyDis.UI.Helpers;
using SillyDis.UI.ViewModels;

namespace SillyDis.UI
{
    public partial class MainWindow : Window
    {
        private FoldingManager?     _foldingManager;
        private BraceFoldingStrategy? _foldingStrategy;
        private DispatcherTimer?    _plotTimer;

        // Per-type ScottPlot signal series  key = PDU type byte
        private readonly Dictionary<byte, ScottPlot.Plottables.Signal> _typeSignals = new();
        private ScottPlot.Plottables.Signal? _totalSignal;

        // Tactical map layer (rebuilt on session switch)
        private EntityMapLayer? _mapLayer;
        private Mapsui.UI.Wpf.MapControl? _activeMapControl;

        // PDU-type colour wheel (cycles through the palette for new types)
        private static readonly Color[] TypeColors =
        {
            new(0,   191, 255),  // blue   — Entity State
            new(255, 107,  53),  // orange — Fire/Det
            new(0,   255, 136),  // green  — Create/Remove
            new(255, 179,   0),  // amber  — Sim Mgmt
            new(200,  80, 255),  // purple — EE
            new(255, 200,   0),  // yellow — Designator
            new(80,  200, 255),  // cyan   — Transmitter
            new(255, 100, 100),  // red    — other
        };
        private int _colorIndex;

        public MainWindow()
        {
            InitializeComponent();
            var vm = App.Current.Services.GetRequiredService<MainViewModel>();
            DataContext = vm;

            // AvalonEdit folding
            _foldingManager  = FoldingManager.Install(PduEditor.TextArea);
            _foldingStrategy = new BraceFoldingStrategy();

            // Configure ScottPlot
            ConfigurePlot();

            // Poll timer: refresh plot + stale entities every 100 ms
            _plotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _plotTimer.Tick += OnPlotTick;
            _plotTimer.Start();

            // Populate NIC dropdown whenever the dialog opens
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsDialogOpen) && vm.IsDialogOpen)
                    PopulateNicComboBox();
            };
        }

        // ── ScottPlot ─────────────────────────────────────────────────────────

        private void ConfigurePlot()
        {
            var plot = TelemetryPlot.Plot;
            plot.DataBackground.Color = ScottPlot.Color.FromHex("#0A0E1A");
            plot.FigureBackground.Color = ScottPlot.Color.FromHex("#111827");
            plot.Axes.Color(ScottPlot.Color.FromHex("#4A5568"));
            plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#1E2D45");
            plot.Axes.Bottom.Label.Text = "seconds ago";
            plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#111827");
            plot.Legend.FontColor = ScottPlot.Color.FromHex("#94A3B8");
        }

        private void OnPlotTick(object? sender, EventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var session = vm?.SelectedSession;
            if (session == null) return;

            // Sync total signal
            if (_totalSignal == null)
            {
                _totalSignal = TelemetryPlot.Plot.Add.Signal(session.TelemetryHistory);
                _totalSignal.Color = ScottPlot.Color.FromHex("#00FF88");
                _totalSignal.LegendText = "Total";
                _totalSignal.LineWidth = 2;
            }

            // Sync per-type signals
            lock (session.TypeHistories)
            {
                foreach (var kvp in session.TypeHistories)
                {
                    if (!_typeSignals.TryGetValue(kvp.Key, out var sig))
                    {
                        var col = TypeColors[_colorIndex++ % TypeColors.Length];
                        sig = TelemetryPlot.Plot.Add.Signal(kvp.Value);
                        sig.Color = col;
                        sig.LegendText = SisoEnumService.ResolvePduType(kvp.Key);
                        sig.LineWidth = 1.5f;
                        _typeSignals[kvp.Key] = sig;
                    }
                }
            }

            TelemetryPlot.Plot.Axes.AutoScale();
            TelemetryPlot.Refresh();

            // Refresh map if visible
            _activeMapControl?.Map.RefreshData();
        }

        // ── Session / Tab switching ────────────────────────────────────────────

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl tc) return;

            // Inner sub-tab switch (Traffic / Tactical)
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem ti && ti.Header?.ToString()?.Contains("TACTICAL") == true)
            {
                _activeMapControl = FindVisualChild<Mapsui.UI.Wpf.MapControl>(ti);
                if (_activeMapControl != null && _activeMapControl.Map == null)
                    InitMap(_activeMapControl);

                var vm2 = DataContext as MainViewModel;
                if (vm2?.SelectedSession != null && _activeMapControl != null)
                    RebuildMapLayer(vm2.SelectedSession);
                return;
            }

            // Outer session tab switch
            TelemetryPlot.Plot.Clear();
            _totalSignal = null;
            _typeSignals.Clear();
            _colorIndex = 0;

            var vm = DataContext as MainViewModel;
            if (vm?.SelectedSession != null)
                UpdatePduEditor(vm.SelectedSession.SelectedPdu);
            else
                UpdatePduEditor(null);

            RebuildMapLayer(vm?.SelectedSession);
            TelemetryPlot.Refresh();
        }

        private static void InitMap(Mapsui.UI.Wpf.MapControl mapControl)
        {
            mapControl.Map = new Map();
            mapControl.Map.Layers.Add(OpenStreetMap.CreateTileLayer("SillyDis/1.0"));
            mapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), mapControl.Map.Navigator.Resolutions[3]);
        }

        private void RebuildMapLayer(ViewModels.SimulationSession? session)
        {
            _mapLayer?.Dispose();
            _mapLayer = null;

            if (session == null || _activeMapControl == null) return;

            _mapLayer = new EntityMapLayer(session.TrackedEntities);
            _activeMapControl.Map.Layers.Add(_mapLayer);
        }

        // ── ListView selection → editors ──────────────────────────────────────

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is PduItem pdu)
                UpdatePduEditor(pdu);
        }

        private void UpdatePduEditor(PduItem? pdu)
        {
            PduEditor.Text = pdu?.FormattedPayload ?? string.Empty;
            HexEditor.Text = pdu?.HexDump ?? string.Empty;

            if (_foldingManager != null && PduEditor.Document.TextLength > 0)
                _foldingStrategy?.UpdateFoldings(_foldingManager, PduEditor.Document);
        }

        // ── Re-Broadcast ──────────────────────────────────────────────────────

        private void RebroadcastButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.SelectedSession?.SelectedPdu?.RawBytes is byte[] bytes)
                vm.RebroadcastPduCommand.Execute(bytes);
            else
                MessageBox.Show("No PDU selected.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ── NIC ComboBox ──────────────────────────────────────────────────────

        private void PopulateNicComboBox()
        {
            var nics = NetworkInterfaceService.GetAvailableNics();
            NicComboBox.ItemsSource = nics;
            NicComboBox.DisplayMemberPath = null; // uses ToString() = "Description (IP)"
        }

        private void NicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] is SillyDis.Core.Models.NicInfo nic)
            {
                var vm = DataContext as MainViewModel;
                if (vm != null) vm.EditingProfile.LocalInterfaceIp = nic.IpAddress;
            }
        }

        // ── Visual tree helper ────────────────────────────────────────────────

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}