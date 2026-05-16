using System;

namespace SillyDis.Core.Models
{
    /// <summary>
    /// Analog of SillyRabbitMQ's MessageItem.
    /// Represents a single captured DIS PDU from the network.
    /// </summary>
    public class PduItem
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>DIS Exercise ID from the PDU header.</summary>
        public byte ExerciseId { get; set; }

        /// <summary>DIS PDU Type identifier (e.g. 1 = EntityState, 2 = Fire, etc.).</summary>
        public byte PduType { get; set; }

        /// <summary>Human-readable PDU type name resolved from PduType.</summary>
        public string PduTypeName => ResolvePduTypeName(PduType);

        /// <summary>Entity ID formatted as "Site.App.Entity" if available.</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>Force/side affiliation if available (e.g. "Friendly", "Opposing").</summary>
        public string ForceId { get; set; } = string.Empty;

        /// <summary>The raw UDP payload bytes.</summary>
        public byte[]? RawBytes { get; set; }

        /// <summary>Length of the raw UDP payload in bytes.</summary>
        public int Length => RawBytes?.Length ?? 0;

        /// <summary>JSON-formatted representation of the decoded PDU fields, built by DisParserService.</summary>
        public string FormattedPayload { get; set; } = string.Empty;

        private static string ResolvePduTypeName(byte pduType) => pduType switch
        {
            1  => "Entity State",
            2  => "Fire",
            3  => "Detonation",
            4  => "Collision",
            11 => "Create Entity",
            12 => "Remove Entity",
            20 => "Data",
            21 => "Set Data",
            22 => "Event Report",
            23 => "Comment",
            24 => "Electromagnetic Emissions",
            25 => "Designator",
            26 => "Transmitter",
            27 => "Signal",
            28 => "Receiver",
            _  => $"Type {pduType}"
        };
    }
}
