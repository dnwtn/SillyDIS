using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SillyDis.Core.Models
{
    public enum NetworkStatus
    {
        Idle,
        Listening,
        Failed
    }

    /// <summary>
    /// Replaces ConnectionProfile from SillyRabbitMQ.
    /// Stores a UDP multicast/unicast network configuration.
    /// </summary>
    public partial class NetworkProfile : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _name = "New Profile";

        /// <summary>Multicast group address (e.g. 239.1.2.3) or "" for unicast/any.</summary>
        [ObservableProperty]
        private string _multicastAddress = "239.1.2.3";

        [ObservableProperty]
        private int _port = 3000;

        /// <summary>IP of the local NIC to bind to; empty string binds to all interfaces.</summary>
        [ObservableProperty]
        private string _localInterfaceIp = string.Empty;

        [ObservableProperty]
        [System.Text.Json.Serialization.JsonIgnore]
        private NetworkStatus _status = NetworkStatus.Idle;
    }
}
