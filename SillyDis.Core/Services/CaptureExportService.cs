using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Serializes a captured PDU list to disk in one of two formats:
    ///   • NDJSON  (.ndjson) — one JSON object per line; streamable, grep-able.
    ///   • JSON    (.json)   — a single pretty-printed JSON array; human-readable.
    ///
    /// RawBytes are exported as a Base64 string so the file is self-contained
    /// and can be replayed or re-parsed later without the original network capture.
    /// HexDump is excluded from export (it can be regenerated on load).
    /// </summary>
    public static class CaptureExportService
    {
        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Exports to NDJSON — one record per line.</summary>
        public static void ExportNdjson(IEnumerable<PduItem> pdus, string filePath)
        {
            using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
            foreach (var pdu in pdus)
            {
                var record = ToRecord(pdu);
                writer.WriteLine(JsonConvert.SerializeObject(record, Formatting.None));
            }
        }

        /// <summary>Exports to a single pretty-printed JSON array.</summary>
        public static void ExportJson(IEnumerable<PduItem> pdus, string filePath)
        {
            var records = new List<object>();
            foreach (var pdu in pdus)
                records.Add(ToRecord(pdu));

            File.WriteAllText(filePath,
                JsonConvert.SerializeObject(records, Formatting.Indented),
                Encoding.UTF8);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static object ToRecord(PduItem pdu) => new
        {
            timestamp      = pdu.Timestamp.ToString("o"),   // ISO-8601
            exerciseId     = pdu.ExerciseId,
            pduType        = pdu.PduType,
            pduTypeName    = pdu.PduTypeName,
            entityId       = pdu.EntityId,
            entityTypeName = pdu.EntityTypeName,
            forceId        = pdu.ForceId,
            forceIdName    = pdu.ForceIdName,
            lengthBytes    = pdu.Length,
            rawBytesBase64 = pdu.RawBytes != null ? Convert.ToBase64String(pdu.RawBytes) : null,
            payload        = pdu.FormattedPayload
        };
    }
}
