using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Enumerates real, active network interfaces suitable for UDP socket binding.
    /// </summary>
    public static class NetworkInterfaceService
    {
        /// <summary>
        /// Returns all UP, non-loopback interfaces that have at least one IPv4 unicast address.
        /// Sorted by description for stable display ordering.
        /// </summary>
        public static IReadOnlyList<NicInfo> GetAvailableNics()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic =>
                    nic.GetIPProperties()
                       .UnicastAddresses
                       .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                       .Select(a => new NicInfo(
                           Name:        nic.Name,
                           Description: nic.Description,
                           IpAddress:   a.Address.ToString())))
                .OrderBy(n => n.Description)
                .ToList();
        }
    }
}
