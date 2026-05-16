using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Loads the SISO-REF-010 XML enumeration database at startup and exposes
    /// human-readable lookups for entity types, force IDs, PDU types, and more.
    ///
    /// The XML is embedded as a resource in SillyDis.Core so no file-system path
    /// assumptions are made.
    /// </summary>
    public static class SisoEnumService
    {
        // Key: "kind:domain:country:cat:subcat:specific:extra"  Value: description
        private static readonly Dictionary<string, string> _entityTypes = new();

        // Key: forceId byte    Value: "Friendly" | "Opposing" | "Neutral" | ...
        private static readonly Dictionary<byte, string> _forceIds = new();

        // Key: pduType byte    Value: "Entity State" etc.
        private static readonly Dictionary<byte, string> _pduTypes = new();

        private static bool _loaded;
        private static readonly object _lock = new();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Resolves a 7-tuple entity type to a human-readable SISO description.</summary>
        public static string ResolveEntityType(
            byte kind, byte domain, ushort country,
            byte cat, byte subcat, byte specific, byte extra)
        {
            EnsureLoaded();
            // Walk from most-specific to least-specific, returning the first hit.
            var keys = new[]
            {
                $"{kind}:{domain}:{country}:{cat}:{subcat}:{specific}:{extra}",
                $"{kind}:{domain}:{country}:{cat}:{subcat}:{specific}:0",
                $"{kind}:{domain}:{country}:{cat}:{subcat}:0:0",
                $"{kind}:{domain}:{country}:{cat}:0:0:0",
                $"{kind}:{domain}:{country}:0:0:0:0",
            };
            foreach (var k in keys)
                if (_entityTypes.TryGetValue(k, out var desc)) return desc;
            return $"{kind}:{domain}:{country}:{cat}:{subcat}:{specific}:{extra}";
        }

        /// <summary>Resolves a force ID byte to a human-readable name.</summary>
        public static string ResolveForceId(byte forceId)
        {
            EnsureLoaded();
            return _forceIds.TryGetValue(forceId, out var name) ? name : $"Force {forceId}";
        }

        /// <summary>Resolves a PDU type byte to a human-readable PDU name.</summary>
        public static string ResolvePduType(byte pduType)
        {
            EnsureLoaded();
            return _pduTypes.TryGetValue(pduType, out var name) ? name : $"PDU {pduType}";
        }

        // ── Loader ─────────────────────────────────────────────────────────────

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                LoadFromEmbeddedResource();
                SeedStaticEnums();
                _loaded = true;
            }
        }

        private static void LoadFromEmbeddedResource()
        {
            var asm      = Assembly.GetExecutingAssembly();
            var resName  = "SillyDis.Core.Resources.SISO-REF-010.xml";

            using var stream = asm.GetManifestResourceStream(resName)
                ?? throw new InvalidOperationException($"Embedded resource '{resName}' not found.");

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
            using var reader = XmlReader.Create(stream, settings);

            // Parser state
            byte   curKind    = 0;
            byte   curDomain  = 0;
            ushort curCountry = 0;
            byte   curCat     = 0;
            byte   curSubcat  = 0;
            byte   curSpecific = 0;

            bool insideCet = false; // <cet uid="30" name="Entity Types">

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                switch (reader.LocalName)
                {
                    case "cet":
                        insideCet = reader.GetAttribute("name") == "Entity Types";
                        break;

                    case "entity" when insideCet:
                        curKind    = ParseByte(reader.GetAttribute("kind"));
                        curDomain  = ParseByte(reader.GetAttribute("domain"));
                        curCountry = ParseUShort(reader.GetAttribute("country"));
                        curCat     = 0; curSubcat = 0; curSpecific = 0;
                        break;

                    case "category" when insideCet:
                        curCat     = ParseByte(reader.GetAttribute("value"));
                        curSubcat  = 0; curSpecific = 0;
                        var catDesc = reader.GetAttribute("description") ?? string.Empty;
                        Store(curKind, curDomain, curCountry, curCat, 0, 0, 0, catDesc);
                        break;

                    case "subcategory" when insideCet:
                        curSubcat   = ParseByte(reader.GetAttribute("value"));
                        curSpecific = 0;
                        var subDesc = reader.GetAttribute("description") ?? string.Empty;
                        Store(curKind, curDomain, curCountry, curCat, curSubcat, 0, 0, subDesc);
                        break;

                    case "specific" when insideCet:
                        curSpecific = ParseByte(reader.GetAttribute("value"));
                        var specDesc = reader.GetAttribute("description") ?? string.Empty;
                        Store(curKind, curDomain, curCountry, curCat, curSubcat, curSpecific, 0, specDesc);
                        break;

                    case "extra" when insideCet:
                        var extraVal  = ParseByte(reader.GetAttribute("value"));
                        var extraDesc = reader.GetAttribute("description") ?? string.Empty;
                        Store(curKind, curDomain, curCountry, curCat, curSubcat, curSpecific, extraVal, extraDesc);
                        break;
                }
            }
        }

        private static void Store(byte k, byte d, ushort c, byte cat, byte sub, byte spec, byte extra, string desc)
        {
            if (string.IsNullOrWhiteSpace(desc)) return;
            var key = $"{k}:{d}:{c}:{cat}:{sub}:{spec}:{extra}";
            _entityTypes.TryAdd(key, desc);
        }

        /// <summary>Seeds enums that are too small to warrant XML parsing.</summary>
        private static void SeedStaticEnums()
        {
            // DIS Force IDs
            _forceIds[0] = "Other";
            _forceIds[1] = "Friendly";
            _forceIds[2] = "Opposing";
            _forceIds[3] = "Neutral";
            _forceIds[4] = "Friendly 2";
            _forceIds[5] = "Opposing 2";
            _forceIds[6] = "Neutral 2";
            _forceIds[7] = "Friendly 3";
            _forceIds[8] = "Opposing 3";
            _forceIds[9] = "Neutral 3";

            // DIS PDU Types (IEEE 1278.1-2012, Table 4)
            _pduTypes[1]  = "Entity State";
            _pduTypes[2]  = "Fire";
            _pduTypes[3]  = "Detonation";
            _pduTypes[4]  = "Collision";
            _pduTypes[11] = "Create Entity";
            _pduTypes[12] = "Remove Entity";
            _pduTypes[13] = "Start / Resume";
            _pduTypes[14] = "Stop / Freeze";
            _pduTypes[15] = "Acknowledge";
            _pduTypes[16] = "Action Request";
            _pduTypes[17] = "Action Response";
            _pduTypes[18] = "Data Query";
            _pduTypes[19] = "Set Data";
            _pduTypes[20] = "Data";
            _pduTypes[21] = "Event Report";
            _pduTypes[22] = "Comment";
            _pduTypes[23] = "Electromagnetic Emissions";
            _pduTypes[24] = "Designator";
            _pduTypes[25] = "Transmitter";
            _pduTypes[26] = "Signal";
            _pduTypes[27] = "Receiver";
            _pduTypes[28] = "IFF";
            _pduTypes[29] = "UnderwaterAcoustic";
            _pduTypes[41] = "Minefield State";
            _pduTypes[42] = "Minefield Query";
            _pduTypes[43] = "Minefield Data";
            _pduTypes[44] = "Minefield Response NACK";
            _pduTypes[70] = "Environmental Process";
            _pduTypes[71] = "Grid Data";
            _pduTypes[72] = "Point Object State";
            _pduTypes[73] = "Linear Object State";
            _pduTypes[74] = "Areal Object State";
            _pduTypes[200] = "Announce Object";
            _pduTypes[201] = "Delete Object";
            _pduTypes[202] = "Describe Application";
            _pduTypes[203] = "Describe Event";
            _pduTypes[204] = "Describe Object";
            _pduTypes[205] = "Request Event";
            _pduTypes[206] = "Request Object";
        }

        private static byte ParseByte(string? s) =>
            byte.TryParse(s, out var v) ? v : (byte)0;

        private static ushort ParseUShort(string? s) =>
            ushort.TryParse(s, out var v) ? v : (ushort)0;
    }
}
