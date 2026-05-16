using System;
using DISnet;
using DISnet.DataStreamUtilities;
using Newtonsoft.Json;
using SillyDis.Core.Models;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Wraps the OpenDIS (DISnet) library to decode raw UDP byte arrays into PduItem objects.
    /// Populates SISO-resolved names and a pre-computed hex dump on every item.
    /// </summary>
    public static class DisParserService
    {
        public static PduItem Parse(byte[] bytes)
        {
            var item = new PduItem
            {
                RawBytes  = bytes,
                Timestamp = DateTime.Now,
                HexDump   = PduItem.BuildHexDump(bytes)
            };

            if (bytes.Length < 12)
            {
                item.PduTypeName     = "Unknown";
                item.FormattedPayload = $"// Packet too short ({bytes.Length} bytes) to be a DIS PDU.";
                return item;
            }

            try
            {
                // DIS header: byte[0]=ProtocolVersion, [1]=ExerciseID, [2]=PDUType
                item.ExerciseId = bytes[1];
                item.PduType    = bytes[2];
                item.PduTypeName = SisoEnumService.ResolvePduType(item.PduType);

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
                    _  => null
                };

                // Enrich entity-bearing PDUs with SISO-resolved descriptions
                if (pdu is EntityStatePdu espdu)
                {
                    var eid = espdu.EntityID;
                    item.EntityId = $"{eid.SimulationAddress.Site}.{eid.SimulationAddress.Application}.{eid.EntityNumber}";

                    item.ForceId    = espdu.ForceId;
                    item.ForceIdName = SisoEnumService.ResolveForceId(espdu.ForceId);

                    var et = espdu.EntityType;
                    item.EntityTypeName = SisoEnumService.ResolveEntityType(
                        et.EntityKind, et.Domain, et.Country,
                        et.Category, et.Subcategory, et.Specific, et.Extra);
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
                    : item.HexDump;
            }
            catch (Exception ex)
            {
                item.FormattedPayload = $"// Parse error: {ex.Message}\n{item.HexDump}";
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
    }
}
