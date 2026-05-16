using System;
using System.Text;
using SillyDis.Core.Services;

namespace SillyDis.Core.Models
{
    /// <summary>
    /// Analog of SillyRabbitMQ's MessageItem.
    /// Represents a single captured DIS PDU with decoded fields, SISO-resolved
    /// descriptions, and a pre-computed hex dump for the inspector panel.
    /// </summary>
    public class PduItem
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>DIS Exercise ID from the PDU header.</summary>
        public byte ExerciseId { get; set; }

        /// <summary>DIS PDU Type identifier (e.g. 1 = EntityState, 2 = Fire, etc.).</summary>
        public byte PduType { get; set; }

        /// <summary>Human-readable PDU type name from SisoEnumService.</summary>
        public string PduTypeName { get; set; } = string.Empty;

        /// <summary>Entity ID formatted as "Site.App.Entity" if available.</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>SISO-resolved platform description, e.g. "M1A2 Abrams".</summary>
        public string EntityTypeName { get; set; } = string.Empty;

        /// <summary>SISO-resolved force affiliation, e.g. "Friendly" / "Opposing" / "Neutral".</summary>
        public string ForceIdName { get; set; } = string.Empty;

        /// <summary>Raw numeric force ID (preserved for filtering).</summary>
        public byte ForceId { get; set; }

        /// <summary>The raw UDP payload bytes.</summary>
        public byte[]? RawBytes { get; set; }

        /// <summary>Length of the raw UDP payload in bytes.</summary>
        public int Length => RawBytes?.Length ?? 0;

        /// <summary>
        /// JSON-formatted representation of the decoded PDU fields.
        /// Shown in the JSON tab of the inspector pane.
        /// </summary>
        public string FormattedPayload { get; set; } = string.Empty;

        /// <summary>
        /// Pre-computed hex dump of the raw bytes (16 bytes per row, with offset and ASCII sidebar).
        /// Always populated regardless of PDU type. Shown in the Hex tab of the inspector pane.
        /// </summary>
        public string HexDump { get; set; } = string.Empty;

        // ── Static factory helpers ─────────────────────────────────────────────

        /// <summary>
        /// Builds a structured hex dump string in the classic format:
        ///   0000  4B 07 01 00 ...  K.....
        /// </summary>
        public static string BuildHexDump(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return "(empty)";

            const int bytesPerRow = 16;
            var sb = new StringBuilder();

            for (int i = 0; i < bytes.Length; i += bytesPerRow)
            {
                // Offset
                sb.Append($"{i:X4}  ");

                // Hex bytes
                var rowLen = Math.Min(bytesPerRow, bytes.Length - i);
                for (int j = 0; j < bytesPerRow; j++)
                {
                    if (j < rowLen)
                        sb.Append($"{bytes[i + j]:X2} ");
                    else
                        sb.Append("   "); // padding for last row
                    if (j == 7) sb.Append(' '); // extra space at midpoint
                }

                // ASCII sidebar
                sb.Append(' ');
                for (int j = 0; j < rowLen; j++)
                {
                    char c = (char)bytes[i + j];
                    sb.Append(c is >= ' ' and <= '~' ? c : '.');
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
