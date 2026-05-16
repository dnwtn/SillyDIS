using System;
using System.Threading.Tasks;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Analog of IMessageService.
    /// Abstraction for joining a UDP multicast group and listening for DIS PDUs.
    /// </summary>
    public interface IUdpNetworkService : IAsyncDisposable, IDisposable
    {
        bool IsListening { get; }

        /// <summary>Joins the multicast group defined in the profile and begins receiving UDP datagrams.</summary>
        Task StartListeningAsync(NetworkProfile profile, Action<PduItem> onPduReceived);

        /// <summary>Stops receiving and leaves the multicast group.</summary>
        Task StopListeningAsync();

        /// <summary>Broadcasts a raw PDU byte array onto the current network profile's multicast address/port.</summary>
        Task BroadcastPduAsync(byte[] pduBytes);
    }
}
