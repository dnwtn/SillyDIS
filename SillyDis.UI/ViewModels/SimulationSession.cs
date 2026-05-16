using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SillyDis.Core.Models;
using SillyDis.Core.Services;

namespace SillyDis.UI.ViewModels
{
    /// <summary>
    /// Analog of EavesdropSession.
    /// Represents one active listening "tab" — owns its filter state,
    /// captured PDU list, pause state, and per-second telemetry ring buffer.
    /// </summary>
    public partial class SimulationSession : ObservableObject
    {
        private readonly IUdpNetworkService _networkService;

        private string _header = "New Session";
        private bool _isAutoUpdatingHeader;
        private bool _isCustomHeader;

        public string Header
        {
            get => _header;
            set
            {
                if (SetProperty(ref _header, value))
                {
                    if (!_isAutoUpdatingHeader)
                        _isCustomHeader = true;
                }
            }
        }

        [ObservableProperty]
        private byte _targetExerciseId = 1;

        [ObservableProperty]
        private string _targetPduType = "Any";

        [ObservableProperty]
        private string _targetEntityId = string.Empty;

        [ObservableProperty]
        private string _targetRegexFilter = string.Empty;

        [ObservableProperty]
        private ObservableCollection<PduItem> _pdus = new();

        [ObservableProperty]
        private PduItem? _selectedPdu;

        [ObservableProperty]
        private bool _isPaused;

        // Telemetry: 60-second rolling window (PDUs/sec)
        public double[] TelemetryHistory { get; } = new double[60];
        private int _pduCountThisSecond;
        private readonly System.Timers.Timer _telemetryTimer;

        public SimulationSession(IUdpNetworkService networkService)
        {
            _networkService = networkService;

            _telemetryTimer = new System.Timers.Timer(1000);
            _telemetryTimer.Elapsed += (s, e) =>
            {
                Array.Copy(TelemetryHistory, 1, TelemetryHistory, 0, TelemetryHistory.Length - 1);
                TelemetryHistory[^1] = System.Threading.Interlocked.Exchange(ref _pduCountThisSecond, 0);
            };
            _telemetryTimer.Start();
        }

        public void Cleanup()
        {
            _telemetryTimer.Stop();
            _telemetryTimer.Dispose();
        }

        partial void OnTargetExerciseIdChanged(byte value) => UpdateHeader();
        partial void OnTargetPduTypeChanged(string value) => UpdateHeader();

        private void UpdateHeader()
        {
            if (_isCustomHeader) return;
            _isAutoUpdatingHeader = true;
            Header = $"Ex:{TargetExerciseId} | {TargetPduType}";
            _isAutoUpdatingHeader = false;
        }

        [RelayCommand]
        private void ClearPdus() => Pdus.Clear();

        [RelayCommand]
        private void TogglePause() => IsPaused = !IsPaused;

        [RelayCommand]
        private void ClearFilter()
        {
            TargetExerciseId = 1;
            TargetPduType    = "Any";
            TargetEntityId   = string.Empty;
            TargetRegexFilter = string.Empty;
            IsPaused = false;
        }

        /// <summary>
        /// Called by MainViewModel when a raw PDU arrives from the network service.
        /// Applies filters and dispatches to the UI thread.
        /// </summary>
        public void OnPduReceived(PduItem pdu)
        {
            if (IsPaused) return;

            System.Threading.Interlocked.Increment(ref _pduCountThisSecond);

            // Exercise ID filter
            if (pdu.ExerciseId != TargetExerciseId && TargetExerciseId != 0) return;

            // PDU type filter
            if (TargetPduType != "Any" && pdu.PduTypeName != TargetPduType) return;

            // Entity ID filter
            if (!string.IsNullOrWhiteSpace(TargetEntityId) && !pdu.EntityId.Contains(TargetEntityId)) return;

            // Regex payload filter
            if (!string.IsNullOrWhiteSpace(TargetRegexFilter))
            {
                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(
                            pdu.FormattedPayload, TargetRegexFilter,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return;
                }
                catch { /* invalid regex — pass through */ }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Pdus.Insert(0, pdu);
                if (Pdus.Count > 2000)
                    Pdus.RemoveAt(Pdus.Count - 1);
            });
        }
    }
}
