using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Concrete UDP multicast listener/broadcaster.
    ///
    /// Uses a two-stage producer-consumer pipeline via System.Threading.Channels:
    ///   Producer task  — receives raw byte[] from UdpClient and writes to a bounded Channel.
    ///   Consumer task  — reads byte[], calls DisParserService.Parse, invokes the callback.
    ///
    /// This ensures network I/O is never blocked by PDU parsing / JSON serialization,
    /// and gives a clean back-pressure mechanism under heavy load.
    /// </summary>
    public class UdpNetworkService : IUdpNetworkService
    {
        private const int ChannelCapacity = 50_000;

        private UdpClient?                    _udpClient;
        private NetworkProfile?               _activeProfile;
        private Action<PduItem>?              _onPduReceived;
        private CancellationTokenSource?      _cts;
        private Task?                         _producerTask;
        private Task?                         _consumerTask;
        private Channel<byte[]>?              _channel;
        private long                          _droppedPacketCount;
        private bool                          _disposed;

        public bool IsListening =>
            _producerTask != null && !_producerTask.IsCompleted;

        public long DroppedPacketCount =>
            Interlocked.Read(ref _droppedPacketCount);

        // ── Connect ────────────────────────────────────────────────────────────

        public async Task StartListeningAsync(NetworkProfile profile, Action<PduItem> onPduReceived)
        {
            await StopListeningAsync();

            _activeProfile    = profile;
            _onPduReceived    = onPduReceived;
            _droppedPacketCount = 0;

            var localIp = string.IsNullOrWhiteSpace(profile.LocalInterfaceIp)
                ? IPAddress.Any
                : IPAddress.Parse(profile.LocalInterfaceIp);

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(localIp, profile.Port));

            if (!string.IsNullOrWhiteSpace(profile.MulticastAddress))
            {
                var multicastIp = IPAddress.Parse(profile.MulticastAddress);
                _udpClient.JoinMulticastGroup(multicastIp, localIp);
            }

            // Bounded channel — DropOldest silently discards the oldest unprocessed
            // packet when at capacity; _droppedPacketCount tracks the total.
            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode     = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

            _cts          = new CancellationTokenSource();
            _producerTask = Task.Run(() => ProduceAsync(_cts.Token), _cts.Token);
            _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        }

        // ── Disconnect ─────────────────────────────────────────────────────────

        public async Task StopListeningAsync()
        {
            if (_cts != null)
            {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }

            _channel?.Writer.TryComplete();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            foreach (var t in new[] { _producerTask, _consumerTask })
            {
                if (t != null)
                {
                    try { await t.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                    catch { }
                }
            }

            _producerTask = null;
            _consumerTask = null;
            _channel      = null;
        }

        // ── Broadcast ──────────────────────────────────────────────────────────

        public async Task BroadcastPduAsync(byte[] pduBytes)
        {
            if (_activeProfile == null)
                throw new InvalidOperationException("Not connected to a network profile.");

            using var sender = new UdpClient();
            var target = string.IsNullOrWhiteSpace(_activeProfile.MulticastAddress)
                ? IPAddress.Broadcast
                : IPAddress.Parse(_activeProfile.MulticastAddress);

            await sender.SendAsync(pduBytes, pduBytes.Length,
                new IPEndPoint(target, _activeProfile.Port));
        }

        // ── Producer (network I/O thread) ──────────────────────────────────────

        private async Task ProduceAsync(CancellationToken ct)
        {
            if (_channel == null || _udpClient == null) return;
            var writer = _channel.Writer;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(ct);

                    // TryWrite returns false when the channel is full and DropOldest
                    // has already discarded an entry — we count it as dropped.
                    if (!writer.TryWrite(result.Buffer))
                        Interlocked.Increment(ref _droppedPacketCount);
                }
                catch (OperationCanceledException) { break; }
                catch { break; }
            }

            writer.TryComplete();
        }

        // ── Consumer (parse + callback thread) ────────────────────────────────

        private async Task ConsumeAsync(CancellationToken ct)
        {
            if (_channel == null) return;
            var reader = _channel.Reader;

            await foreach (var bytes in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    var pdu = DisParserService.Parse(bytes);
                    _onPduReceived?.Invoke(pdu);
                }
                catch { /* swallow parse errors — malformed PDU */ }
            }
        }

        // ── Dispose ────────────────────────────────────────────────────────────

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
