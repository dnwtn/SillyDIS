namespace SillyDis.Core.Models
{
    /// <summary>
    /// Represents a single physical or virtual network interface available
    /// for binding. Populated by NetworkInterfaceService.
    /// </summary>
    public record NicInfo(string Name, string Description, string IpAddress)
    {
        public override string ToString() => $"{Description} ({IpAddress})";
    }
}
