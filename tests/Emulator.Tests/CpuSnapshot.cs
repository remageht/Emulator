using System;
using System.Collections.Generic;
using System.Text;

namespace Emulator.Tests
{
    public class CpuSnapshot
    {
        public byte A { get; set; }
        public byte B { get; set; }
        public byte C { get; set; }
        public byte D { get; set; }
        public byte E { get; set; }
        public byte H { get; set; }
        public byte L { get; set; }
        public byte M { get; set; }
        public int PC { get; set; }

        public bool FlagZ { get; set; }
        public bool FlagC { get; set; }
        public bool FlagS { get; set; }
        public bool FlagAC { get; set; }
        public bool FlagP { get; set; }

        public int CallStackDepth { get; set; }
        public Dictionary<int, byte> ModifiedMemory { get; } = new Dictionary<int, byte>();

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"A\":\"{A:X2}\",\"B\":\"{B:X2}\",\"C\":\"{C:X2}\",\"D\":\"{D:X2}\",\"E\":\"{E:X2}\",\"H\":\"{H:X2}\",\"L\":\"{L:X2}\",\"M\":\"{M:X2}\",\"PC\":\"{PC:X4}\",");
            sb.Append($"\"Z\":{(FlagZ ? "true" : "false")},\"C\":{(FlagC ? "true" : "false")},\"S\":{(FlagS ? "true" : "false")},\"AC\":{(FlagAC ? "true" : "false")},\"P\":{(FlagP ? "true" : "false")},");
            sb.Append($"\"CallStackDepth\":{CallStackDepth},\"Memory\":{{");
            bool first = true;
            foreach (var kvp in ModifiedMemory)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key:X4}\":\"{kvp.Value:X2}\"");
                first = false;
            }
            sb.Append("}}");
            return sb.ToString();
        }

        public bool Matches(CpuSnapshot other)
        {
            if (other == null) return false;
            if (A != other.A || B != other.B || C != other.C || D != other.D) return false;
            if (E != other.E || H != other.H || L != other.L || M != other.M) return false;
            if (PC != other.PC) return false;
            if (FlagZ != other.FlagZ || FlagC != other.FlagC || FlagS != other.FlagS || FlagAC != other.FlagAC || FlagP != other.FlagP) return false;
            if (CallStackDepth != other.CallStackDepth) return false;
            if (ModifiedMemory.Count != other.ModifiedMemory.Count) return false;
            foreach (var kvp in ModifiedMemory)
            {
                if (!other.ModifiedMemory.TryGetValue(kvp.Key, out byte val) || val != kvp.Value)
                    return false;
            }
            return true;
        }
    }
}
