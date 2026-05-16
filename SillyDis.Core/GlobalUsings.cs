// GlobalUsings.cs — namespace bridges for SillyDis.Core
//
// Purpose: The CSharpDis7 PDU source files reference `DISnet.DataStreamUtilities`
// but the actual stream utility classes (from CsharpDis6) live in `OpenDis.Core`.
// This file creates the missing namespace + Endian enum so all 174 PDU files
// compile without modification.

global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;

// ── Endian enum (used by DataStream/DataInputStream/DataOutputStream) ─────────
namespace OpenDis.Core
{
    public enum Endian { Big, Little }
}

// ── DISnet.DataStreamUtilities shim ──────────────────────────────────────────
// Re-exposes OpenDis.Core stream classes under the namespace expected by DIS v7 files.
namespace DISnet.DataStreamUtilities
{
    public class DataInputStream : OpenDis.Core.DataInputStream
    {
        public DataInputStream() : base() { }
        public DataInputStream(OpenDis.Core.Endian endian) : base(endian) { }
        public DataInputStream(byte[] ds, OpenDis.Core.Endian endian = OpenDis.Core.Endian.Big) : base(ds, endian) { }
    }

    public class DataOutputStream : OpenDis.Core.DataOutputStream
    {
        public DataOutputStream() : base() { }
        public DataOutputStream(OpenDis.Core.Endian endian) : base(endian) { }
    }
}
