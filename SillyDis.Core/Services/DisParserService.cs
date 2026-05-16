using System;
using System.IO;
using System.Text;
using DISnet;
using DISnet.DataStreamUtilities;
using Newtonsoft.Json;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Wraps the OpenDIS (DISnet) library to decode raw UDP byte arrays into PduItem objects.
    /// Uses reflection on the PDU header to instantiate the correct typed PDU class (DIS v7).
    /// </summary>
    public static class DisParserService
    {
        public static PduItem Parse(byte[] bytes)
        {
            var item = new PduItem
            {
                RawBytes = bytes,
                Timestamp = DateTime.Now
            };

            if (bytes.Length < 12)
            {
                item.FormattedPayload = $"// Packet too short ({bytes.Length} bytes) to be a DIS PDU.";
                return item;
            }

            try
            {
                // Byte 2 = PDU Type, Byte 1 = Exercise ID (DIS header layout)
                item.ExerciseId = bytes[1];
                item.PduType    = bytes[2];

                // Unmarshal using OpenDIS DataInputStream
                object? pdu = item.PduType switch
                {
                    1  => UnmarshalAs<EntityStatePdu>(bytes),
                    2  => UnmarshalAs<FirePdu>(bytes),
                    3  => UnmarshalAs<DetonationPdu>(bytes),
                    4  => UnmarshalAs<CollisionPdu>(bytes),
                    11 => UnmarshalAs<CreateEntityPdu>(bytes),
                    12 => UnmarshalAs<RemoveEntityPdu>(bytes),
                    20 => UnmarshalAs<DataPdu>(bytes),
                    21 => UnmarshalAs<SetDataPdu>(bytes),
                    22 => UnmarshalAs<EventReportPdu>(bytes),
                    23 => UnmarshalAs<CommentPdu>(bytes),
                    24 => UnmarshalAs<ElectronicEmissionsPdu>(bytes),
                    25 => UnmarshalAs<DesignatorPdu>(bytes),
                    26 => null, // TransmitterPdu — add as needed
                    _  => null
                };

                if (pdu is EntityStatePdu espdu)
                {
                    var eid = espdu.EntityID;
                    item.EntityId = $"{eid.SimulationAddress.Site}.{eid.SimulationAddress.Application}.{eid.EntityNumber}";
                    item.ForceId  = espdu.ForceId.ToString();
                }
                else if (pdu is FirePdu fire)
                {
                    var eid = fire.FiringEntityID;
                    item.EntityId = $"{eid.SimulationAddress.Site}.{eid.SimulationAddress.Application}.{eid.EntityNumber}";
                }
                else if (pdu is DetonationPdu det)
                {
                    var eid = det.ExplodingEntityID;
                    item.EntityId = $"{eid.SimulationAddress.Site}.{eid.SimulationAddress.Application}.{eid.EntityNumber}";
                }

                item.FormattedPayload = pdu != null
                    ? JsonConvert.SerializeObject(pdu, Formatting.Indented)
                    : BuildHexDump(bytes);
            }
            catch (Exception ex)
            {
                item.FormattedPayload = $"// Parse error: {ex.Message}\n{BuildHexDump(bytes)}";
            }

            return item;
        }

        private static T UnmarshalAs<T>(byte[] bytes) where T : PduSuperclass, new()
        {
            var pdu = new T();
            var dis = new DataInputStream(bytes);
            pdu.Unmarshal(dis);
            return pdu;
        }

        private static string BuildHexDump(byte[] bytes)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Raw hex dump ({bytes.Length} bytes):");
            for (int i = 0; i < bytes.Length; i += 16)
            {
                sb.Append($"{i:X4}  ");
                int len = Math.Min(16, bytes.Length - i);
                for (int j = 0; j < len; j++)
                    sb.Append($"{bytes[i + j]:X2} ");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
