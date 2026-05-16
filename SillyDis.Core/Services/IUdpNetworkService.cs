using System;
using System.Threading.Tasks;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Abstraction for joining a UDP multicast group and listening for DIS PDUs.
    /// Uses a Channel-based producer-consumer pipeline internally.
    /// </summary>
    public interface IUdpNetworkService : IAsyncDisposable, IDisposable
    {
        bool IsListening { get; }

        /// <summary>
        /// Total raw UDP packets silently dropped because the internal channel
        /// was at capacity (BoundedChannelFullMode.DropOldest).
        /// </summary>
        long DroppedPacketCount { get; }

        /// <summary>Joins the multicast group and begins the producer-consumer pipeline.</summary>
        Task StartListeningAsync(NetworkProfile profile, Action<PduItem> onPduReceived);

        /// <summary>Stops the pipeline and leaves the multicast group.</summary>
        Task StopListeningAsync();

        /// <summary>Broadcasts a raw PDU byte array onto the active network profile's address/port.</summary>
        Task BroadcastPduAsync(byte[] pduBytes);
    }
}
