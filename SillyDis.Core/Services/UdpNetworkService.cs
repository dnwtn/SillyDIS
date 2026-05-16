using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Concrete UDP multicast listener/broadcaster.
    /// Analog of RabbitMQService — owns the socket lifetime.
    /// </summary>
    public class UdpNetworkService : IUdpNetworkService
    {
        private UdpClient? _udpClient;
        private NetworkProfile? _activeProfile;
        private Action<PduItem>? _onPduReceived;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private bool _disposed;

        public bool IsListening => _listenTask != null && !_listenTask.IsCompleted;

        public async Task StartListeningAsync(NetworkProfile profile, Action<PduItem> onPduReceived)
        {
            await StopListeningAsync();

            _activeProfile = profile;
            _onPduReceived = onPduReceived;

            var localIp = string.IsNullOrWhiteSpace(profile.LocalInterfaceIp)
                ? IPAddress.Any
                : IPAddress.Parse(profile.LocalInterfaceIp);

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(localIp, profile.Port));

            // Join multicast if a group address is specified
            if (!string.IsNullOrWhiteSpace(profile.MulticastAddress))
            {
                var multicastIp = IPAddress.Parse(profile.MulticastAddress);
                _udpClient.JoinMulticastGroup(multicastIp, localIp);
            }

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        }

        public async Task StopListeningAsync()
        {
            if (_cts != null)
            {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }

            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            if (_listenTask != null)
            {
                try { await _listenTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                _listenTask = null;
            }
        }

        public async Task BroadcastPduAsync(byte[] pduBytes)
        {
            if (_activeProfile == null) throw new InvalidOperationException("Not connected to a network profile.");

            using var sender = new UdpClient();
            var target = string.IsNullOrWhiteSpace(_activeProfile.MulticastAddress)
                ? IPAddress.Broadcast
                : IPAddress.Parse(_activeProfile.MulticastAddress);

            await sender.SendAsync(pduBytes, pduBytes.Length, new IPEndPoint(target, _activeProfile.Port));
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(ct);
                    var pdu = DisParserService.Parse(result.Buffer);
                    _onPduReceived?.Invoke(pdu);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Socket closed or transient error — exit loop
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _udpClient?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await StopListeningAsync();
            Dispose();
        }
    }
}
