using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SillyDis.Core.Models;
using SillyDis.Core.Services;

namespace SillyDis.UI.ViewModels
{
    /// <summary>
    /// Root view model. Analog of SillyRabbitMQ's MainViewModel.
    /// Manages network profiles, simulation sessions, and the single
    /// active UDP connection.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUdpNetworkService _networkService;
        private readonly ProfileManager _profileManager;

        [ObservableProperty]
        private ObservableCollection<NetworkProfile> _profiles = new();

        [ObservableProperty]
        private NetworkProfile? _selectedProfile;

        [ObservableProperty]
        private ObservableCollection<SimulationSession> _sessions = new();

        [ObservableProperty]
        private SimulationSession? _selectedSession;

        [ObservableProperty]
        private bool _isListening;

        // Dialog state
        [ObservableProperty]
        private bool _isDialogOpen;

        [ObservableProperty]
        private NetworkProfile _editingProfile = new();

        public MainViewModel(IUdpNetworkService networkService, ProfileManager profileManager)
        {
            _networkService = networkService;
            _profileManager = profileManager;

            LoadProfiles();
            AddSession();
        }

        private void LoadProfiles()
        {
            var loaded = _profileManager.LoadProfiles();
            if (loaded.Count == 0)
            {
                loaded.Add(new NetworkProfile { Name = "Local Loopback", MulticastAddress = "239.1.2.3", Port = 3000 });
                _profileManager.SaveProfiles(loaded);
            }
            foreach (var p in loaded)
                Profiles.Add(p);
        }

        partial void OnSelectedProfileChanged(NetworkProfile? value)
        {
            if (value != null && !IsDialogOpen)
                ConnectCommand.ExecuteAsync(null);
        }

        // ── Profile CRUD ──────────────────────────────────────────────────────────

        [RelayCommand]
        private void AddProfile()
        {
            EditingProfile = new NetworkProfile();
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void EditProfile(NetworkProfile profile)
        {
            if (profile == null) return;
            EditingProfile = new NetworkProfile
            {
                Id              = profile.Id,
                Name            = profile.Name,
                MulticastAddress = profile.MulticastAddress,
                Port            = profile.Port,
                LocalInterfaceIp = profile.LocalInterfaceIp
            };
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void DeleteProfile(NetworkProfile profile)
        {
            if (profile == null) return;
            Profiles.Remove(profile);
            _profileManager.SaveProfiles(Profiles.ToList());
        }

        [RelayCommand]
        private void SaveProfile()
        {
            var existing = Profiles.FirstOrDefault(p => p.Id == EditingProfile.Id);
            if (existing != null)
                Profiles[Profiles.IndexOf(existing)] = EditingProfile;
            else
                Profiles.Add(EditingProfile);

            _profileManager.SaveProfiles(Profiles.ToList());
            IsDialogOpen = false;
        }

        [RelayCommand]
        private void CancelProfile() => IsDialogOpen = false;

        // ── Session Management ────────────────────────────────────────────────────

        [RelayCommand]
        private void AddSession()
        {
            var session = new SimulationSession(_networkService);
            Sessions.Add(session);
            SelectedSession = session;
        }

        [RelayCommand]
        private void CloseSession(SimulationSession session)
        {
            if (session == null) return;
            session.Cleanup();
            Sessions.Remove(session);
            if (Sessions.Count == 0)
                AddSession();
        }

        // ── Network Commands ──────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (SelectedProfile == null) return;

            foreach (var p in Profiles) p.Status = NetworkStatus.Idle;
            SelectedProfile.Status = NetworkStatus.Listening;

            try
            {
                await _networkService.StartListeningAsync(SelectedProfile, OnPduReceived);
                IsListening = _networkService.IsListening;
                SelectedProfile.Status = NetworkStatus.Listening;

                foreach (var s in Sessions)
                {
                    s.Pdus.Clear();
                    s.IsPaused = false;
                }
            }
            catch (Exception ex)
            {
                IsListening = false;
                SelectedProfile.Status = NetworkStatus.Failed;
                MessageBox.Show($"Failed to start listening: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DisconnectAsync()
        {
            await _networkService.StopListeningAsync();
            IsListening = false;
            if (SelectedProfile != null)
                SelectedProfile.Status = NetworkStatus.Idle;
        }

        // ── PDU Spoof & Re-Broadcast ──────────────────────────────────────────────

        [RelayCommand]
        private async Task RebroadcastPduAsync(byte[]? pduBytes)
        {
            if (pduBytes == null || pduBytes.Length == 0)
            {
                MessageBox.Show("No PDU bytes to broadcast.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _networkService.BroadcastPduAsync(pduBytes);
                MessageBox.Show($"PDU ({pduBytes.Length} bytes) re-broadcast successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Re-broadcast failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void OnPduReceived(Core.Models.PduItem pdu)
        {
            // Fan-out to all active sessions — each session applies its own filters
            foreach (var session in Sessions)
                session.OnPduReceived(pdu);
        }
    }
}
