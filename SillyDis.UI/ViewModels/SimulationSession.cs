using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SillyDis.Core.Models;
using SillyDis.Core.Services;

namespace SillyDis.UI.ViewModels
{
    /// <summary>
    /// Represents one active capture tab.
    /// Owns filter state, the PDU list, pause/resume + buffering,
    /// entity tracking for the tactical map, and per-type telemetry histories.
    /// </summary>
    public partial class SimulationSession : ObservableObject
    {
        private readonly IUdpNetworkService _networkService;

        // ── Header ────────────────────────────────────────────────────────────

        private string _header = "New Session";
        private bool   _isAutoUpdatingHeader;
        private bool   _isCustomHeader;

        public string Header
        {
            get => _header;
            set
            {
                if (SetProperty(ref _header, value) && !_isAutoUpdatingHeader)
                    _isCustomHeader = true;
            }
        }

        // ── Filters ───────────────────────────────────────────────────────────

        [ObservableProperty] private byte   _targetExerciseId  = 0;    // 0 = any
        [ObservableProperty] private string _targetPduType     = "Any";
        [ObservableProperty] private string _targetEntityId    = string.Empty;
        [ObservableProperty] private string _targetRegexFilter = string.Empty;

        // ── PDU List ──────────────────────────────────────────────────────────

        [ObservableProperty] private ObservableCollection<PduItem> _pdus = new();
        [ObservableProperty] private PduItem? _selectedPdu;

        // ── Exercise ID Auto-Discovery ────────────────────────────────────────

        /// <summary>
        /// Sorted set of every unique Exercise ID observed in live traffic.
        /// Bound to the Exercise ID ComboBox in the filter bar so users never
        /// have to type an ID by hand.
        /// </summary>
        public ObservableCollection<byte> ObservedExerciseIds { get; } = new();
        private readonly HashSet<byte> _seenExerciseIds = new();

        // ── Dropped packet pass-through (from network service) ────────────────

        public long DroppedPacketCount => _networkService.DroppedPacketCount;

        // ── Pause / Buffer ────────────────────────────────────────────────────

        public enum PauseModeOption { Drop, Buffer }

        [ObservableProperty] private bool            _isPaused;
        [ObservableProperty] private PauseModeOption _pauseMode = PauseModeOption.Buffer;
        [ObservableProperty] private int             _maxPauseBufferSize = 10_000;
        [ObservableProperty] private int             _pauseBufferCount;

        private readonly Queue<PduItem> _pauseBuffer = new();

        // ── Entity Tracking (tactical map) ────────────────────────────────────

        public ObservableCollection<EntityTrack> TrackedEntities { get; } = new();

        // Key: "Site.App.Entity"
        private readonly Dictionary<string, EntityTrack> _trackIndex = new();

        // Stale-check timer interval
        private const int StaleThresholdSeconds = 5;

        // ── Telemetry ─────────────────────────────────────────────────────────

        // Total PDUs/sec (60-second ring)
        public double[] TelemetryHistory { get; } = new double[60];
        private int _pduCountThisSecond;

        // Per-type PDUs/sec (top 8 types tracked by byte key)
        public Dictionary<byte, double[]> TypeHistories { get; } = new();
        private readonly Dictionary<byte, int> _typeCountsThisSecond = new();

        private readonly System.Timers.Timer _telemetryTimer;

        // ── Constructor ───────────────────────────────────────────────────────

        public SimulationSession(IUdpNetworkService networkService)
        {
            _networkService = networkService;

            _telemetryTimer = new System.Timers.Timer(1000);
            _telemetryTimer.Elapsed += OnTelemetryTick;
            _telemetryTimer.Start();
        }

        public void Cleanup()
        {
            _telemetryTimer.Stop();
            _telemetryTimer.Dispose();
        }

        // ── Property changed hooks ────────────────────────────────────────────

        partial void OnTargetExerciseIdChanged(byte value)  => UpdateHeader();
        partial void OnTargetPduTypeChanged(string value)   => UpdateHeader();

        private void UpdateHeader()
        {
            if (_isCustomHeader) return;
            _isAutoUpdatingHeader = true;
            Header = $"Ex:{(TargetExerciseId == 0 ? "*" : TargetExerciseId.ToString())} | {TargetPduType}";
            _isAutoUpdatingHeader = false;
        }

        // ── Commands ──────────────────────────────────────────────────────────

        [RelayCommand]
        private void ClearPdus() => Pdus.Clear();

        [RelayCommand]
        private void TogglePause()
        {
            IsPaused = !IsPaused;

            if (!IsPaused && _pauseBuffer.Count > 0)
                DrainPauseBuffer();
        }

        [RelayCommand]
        private void ClearFilter()
        {
            TargetExerciseId  = 0;
            TargetPduType     = "Any";
            TargetEntityId    = string.Empty;
            TargetRegexFilter = string.Empty;
            IsPaused          = false;
            _pauseBuffer.Clear();
            PauseBufferCount  = 0;
            _seenExerciseIds.Clear();
            ObservedExerciseIds.Clear();
        }

        // ── PDU Ingestion ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by MainViewModel for every incoming PDU.
        /// Applies filters, handles pause buffering, and dispatches accepted items to the UI thread.
        /// </summary>
        public void OnPduReceived(PduItem pdu)
        {
            // Always count for telemetry regardless of filters/pause state
            System.Threading.Interlocked.Increment(ref _pduCountThisSecond);
            lock (_typeCountsThisSecond)
            {
                _typeCountsThisSecond.TryGetValue(pdu.PduType, out var tc);
                _typeCountsThisSecond[pdu.PduType] = tc + 1;
            }

            // Always update entity tracking for ESPDUs (even when paused)
            if (pdu.PduType == 1 && !string.IsNullOrEmpty(pdu.EntityId))
                UpdateEntityTrack(pdu);

            // Auto-discover exercise IDs — add any new ID to the sorted observable list
            if (_seenExerciseIds.Add(pdu.ExerciseId))
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Insert sorted
                    int i = 0;
                    while (i < ObservedExerciseIds.Count && ObservedExerciseIds[i] < pdu.ExerciseId) i++;
                    ObservedExerciseIds.Insert(i, pdu.ExerciseId);
                });
            }

            // Apply filters
            if (!PassesFilters(pdu)) return;

            if (IsPaused)
            {
                HandlePausedIngest(pdu);
                return;
            }

            DispatchToGrid(pdu);
        }

        private bool PassesFilters(PduItem pdu)
        {
            if (TargetExerciseId != 0 && pdu.ExerciseId != TargetExerciseId) return false;
            if (TargetPduType != "Any" && pdu.PduTypeName != TargetPduType) return false;
            if (!string.IsNullOrWhiteSpace(TargetEntityId) && !pdu.EntityId.Contains(TargetEntityId)) return false;

            if (!string.IsNullOrWhiteSpace(TargetRegexFilter))
            {
                try
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(
                            pdu.FormattedPayload, TargetRegexFilter,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        return false;
                }
                catch { /* invalid regex — pass through */ }
            }

            return true;
        }

        private void HandlePausedIngest(PduItem pdu)
        {
            if (PauseMode == PauseModeOption.Drop) return;

            if (_pauseBuffer.Count >= MaxPauseBufferSize)
                _pauseBuffer.Dequeue(); // oldest drops when buffer is full

            _pauseBuffer.Enqueue(pdu);
            PauseBufferCount = _pauseBuffer.Count;
        }

        private void DrainPauseBuffer()
        {
            var batch = _pauseBuffer.ToArray();
            _pauseBuffer.Clear();
            PauseBufferCount = 0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var pdu in batch)
                    Pdus.Insert(0, pdu);
                TrimGrid();
            });
        }

        private void DispatchToGrid(PduItem pdu)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pdus.Insert(0, pdu);
                TrimGrid();
            });
        }

        private void TrimGrid()
        {
            while (Pdus.Count > 5000)
                Pdus.RemoveAt(Pdus.Count - 1);
        }

        // ── Entity Tracking ───────────────────────────────────────────────────

        private void UpdateEntityTrack(PduItem pdu)
        {
            // ECEF → Geodetic conversion requires the raw PDU — we decode it inline
            // from the JSON payload to avoid re-parsing. For now we parse from
            // the EntityId string and zero-out coordinates if ECEF isn't available.
            // (Full ECEF upsert is wired in MainWindow.xaml.cs via the map layer.)
            if (!_trackIndex.TryGetValue(pdu.EntityId, out var track))
            {
                track = new EntityTrack { EntityId = pdu.EntityId };
                _trackIndex[pdu.EntityId] = track;
                Application.Current.Dispatcher.Invoke(() => TrackedEntities.Add(track));
            }

            // Coordinates will be updated by the EntityMapLayer when it processes
            // the full ESPDU object from FormattedPayload.
            track.EntityTypeName = pdu.EntityTypeName;
            track.ForceIdName    = pdu.ForceIdName;
            track.ForceId        = pdu.ForceId;
            track.LastSeen       = pdu.Timestamp;
            track.IsStale        = false;
        }

        // ── Telemetry Tick ────────────────────────────────────────────────────

        private void OnTelemetryTick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // Rotate total history
            Array.Copy(TelemetryHistory, 1, TelemetryHistory, 0, TelemetryHistory.Length - 1);
            TelemetryHistory[^1] = System.Threading.Interlocked.Exchange(ref _pduCountThisSecond, 0);

            // Rotate per-type histories
            lock (_typeCountsThisSecond)
            {
                foreach (var kvp in _typeCountsThisSecond)
                {
                    if (!TypeHistories.TryGetValue(kvp.Key, out var hist))
                    {
                        hist = new double[60];
                        TypeHistories[kvp.Key] = hist;
                    }
                    Array.Copy(hist, 1, hist, 0, hist.Length - 1);
                    hist[^1] = kvp.Value;
                }
                _typeCountsThisSecond.Clear();
            }

            // Mark stale entities
            var now = DateTime.Now;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var track in TrackedEntities)
                    track.IsStale = (now - track.LastSeen).TotalSeconds > StaleThresholdSeconds;
            });
        }
    }
}
