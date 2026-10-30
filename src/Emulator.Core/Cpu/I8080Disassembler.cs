using System;

namespace Emulator.Core.Cpu
{
    public struct DisassembledInstruction
    {
        public ushort Address;
        public byte Opcode;
        public int Length;
        public string Mnemonic;
        public string Description;

        public override string ToString() => Mnemonic;
    }

    public static class I8080Disassembler
    {
        private struct OpcodeInfo
        {
            public string Format;
            public byte Length;
            public string Description;

            public OpcodeInfo(string format, byte length, string desc)
            {
                Format = format;
                Length = length;
                Description = desc;
            }
        }

        private static readonly OpcodeInfo[] Table = BuildTable();

        public static int GetInstructionLength(byte opcode)
        {
            return Table[opcode].Length;
        }

        public static DisassembledInstruction Disassemble(byte[] memory, ushort address)
        {
            if (memory == null || memory.Length == 0)
            {
                return new DisassembledInstruction
                {
                    Address = address,
                    Opcode = 0,
                    Length = 1,
                    Mnemonic = "NOP",
                    Description = "No operation"
                };
            }

            int memLen = memory.Length;
            byte op = memory[address % memLen];
            OpcodeInfo info = Table[op];
            int len = info.Length;

            byte b1 = (byte)(address + 1 < memLen ? memory[(address + 1) % memLen] : 0);
            byte b2 = (byte)(address + 2 < memLen ? memory[(address + 2) % memLen] : 0);
            ushort w16 = (ushort)(b1 | (b2 << 8));

            string mnemonic;
            if (len == 1)
            {
                mnemonic = info.Format;
            }
            else if (len == 2)
            {
                mnemonic = string.Format(info.Format, b1);
            }
            else
            {
                mnemonic = string.Format(info.Format, w16);
            }

            return new DisassembledInstruction
            {
                Address = address,
                Opcode = op,
                Length = len,
                Mnemonic = mnemonic,
                Description = info.Description
            };
        }

        private static OpcodeInfo[] BuildTable()
        {
            var t = new OpcodeInfo[256];
            string[] regs = { "B", "C", "D", "E", "H", "L", "M", "A" };
            string[] pairs = { "B", "D", "H", "SP" };
            string[] pushPairs = { "B", "D", "H", "PSW" };

            // Default all to NOP
            for (int i = 0; i < 256; i++)
            {
                t[i] = new OpcodeInfo("NOP", 1, "No operation");
            }

            // 00..3F Misc
            t[0x00] = new OpcodeInfo("NOP", 1, "No operation");
            t[0x08] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x10] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x18] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x20] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x28] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x30] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");
            t[0x38] = new OpcodeInfo("NOP*", 1, "Undocumented NOP");

            t[0x07] = new OpcodeInfo("RLC", 1, "Rotate A left circular");
            t[0x0F] = new OpcodeInfo("RRC", 1, "Rotate A right circular");
            t[0x17] = new OpcodeInfo("RAL", 1, "Rotate A left through carry");
            t[0x1F] = new OpcodeInfo("RAR", 1, "Rotate A right through carry");
            t[0x27] = new OpcodeInfo("DAA", 1, "Decimal adjust accumulator");
            t[0x2F] = new OpcodeInfo("CMA", 1, "Complement accumulator");
            t[0x37] = new OpcodeInfo("STC", 1, "Set carry flag");
            t[0x3F] = new OpcodeInfo("CMC", 1, "Complement carry flag");

            t[0x02] = new OpcodeInfo("STAX B", 1, "Store A to (BC)");
            t[0x12] = new OpcodeInfo("STAX D", 1, "Store A to (DE)");
            t[0x0A] = new OpcodeInfo("LDAX B", 1, "Load A from (BC)");
            t[0x1A] = new OpcodeInfo("LDAX D", 1, "Load A from (DE)");

            t[0x22] = new OpcodeInfo("SHLD {0:X4}h", 3, "Store HL direct");
            t[0x2A] = new OpcodeInfo("LHLD {0:X4}h", 3, "Load HL direct");
            t[0x32] = new OpcodeInfo("STA {0:X4}h", 3, "Store A direct");
            t[0x3A] = new OpcodeInfo("LDA {0:X4}h", 3, "Load A direct");

            for (int p = 0; p < 4; p++)
            {
                t[0x01 | (p << 4)] = new OpcodeInfo($"LXI {pairs[p]}, {{0:X4}}h", 3, $"Load {pairs[p]} immediate 16-bit");
                t[0x03 | (p << 4)] = new OpcodeInfo($"INX {pairs[p]}", 1, $"Increment {pairs[p]}");
                t[0x0B | (p << 4)] = new OpcodeInfo($"DCX {pairs[p]}", 1, $"Decrement {pairs[p]}");
                t[0x09 | (p << 4)] = new OpcodeInfo($"DAD {pairs[p]}", 1, $"Add {pairs[p]} to HL");
            }

            for (int r = 0; r < 8; r++)
            {
                t[0x04 | (r << 3)] = new OpcodeInfo($"INR {regs[r]}", 1, $"Increment {regs[r]}");
                t[0x05 | (r << 3)] = new OpcodeInfo($"DCR {regs[r]}", 1, $"Decrement {regs[r]}");
                t[0x06 | (r << 3)] = new OpcodeInfo($"MVI {regs[r]}, {{0:X2}}h", 2, $"Move immediate to {regs[r]}");
            }

            // 40..7F MOV r1, r2 and HLT
            for (int dst = 0; dst < 8; dst++)
            {
                for (int src = 0; src < 8; src++)
                {
                    int code = 0x40 | (dst << 3) | src;
                    if (code == 0x76)
                    {
                        t[code] = new OpcodeInfo("HLT", 1, "Halt processor");
                    }
                    else
                    {
                        t[code] = new OpcodeInfo($"MOV {regs[dst]}, {regs[src]}", 1, $"Move {regs[src]} to {regs[dst]}");
                    }
                }
            }

            // 80..BF ALU
            string[] aluOps = { "ADD", "ADC", "SUB", "SBB", "ANA", "XRA", "ORA", "CMP" };
            for (int op = 0; op < 8; op++)
            {
                for (int r = 0; r < 8; r++)
                {
                    int code = 0x80 | (op << 3) | r;
                    t[code] = new OpcodeInfo($"{aluOps[op]} {regs[r]}", 1, $"{aluOps[op]} {regs[r]} with A");
                }
            }

            // Immediate ALU
            string[] immAlu = { "ADI", "ACI", "SUI", "SBI", "ANI", "XRI", "ORI", "CPI" };
            for (int op = 0; op < 8; op++)
            {
                t[0xC6 | (op << 3)] = new OpcodeInfo($"{immAlu[op]} {{0:X2}}h", 2, $"{immAlu[op]} immediate with A");
            }

            // Branches
            t[0xC3] = new OpcodeInfo("JMP {0:X4}h", 3, "Unconditional jump");
            t[0xCB] = new OpcodeInfo("JMP* {0:X4}h", 3, "Undocumented JMP");
            t[0xC2] = new OpcodeInfo("JNZ {0:X4}h", 3, "Jump if Not Zero");
            t[0xCA] = new OpcodeInfo("JZ {0:X4}h", 3, "Jump if Zero");
            t[0xD2] = new OpcodeInfo("JNC {0:X4}h", 3, "Jump if No Carry");
            t[0xDA] = new OpcodeInfo("JC {0:X4}h", 3, "Jump if Carry");
            t[0xE2] = new OpcodeInfo("JPO {0:X4}h", 3, "Jump if Parity Odd");
            t[0xEA] = new OpcodeInfo("JPE {0:X4}h", 3, "Jump if Parity Even");
            t[0xF2] = new OpcodeInfo("JP {0:X4}h", 3, "Jump if Positive");
            t[0xFA] = new OpcodeInfo("JM {0:X4}h", 3, "Jump if Minus");

            t[0xCD] = new OpcodeInfo("CALL {0:X4}h", 3, "Call subroutine");
            t[0xDD] = new OpcodeInfo("CALL* {0:X4}h", 3, "Undocumented CALL");
            t[0xED] = new OpcodeInfo("CALL* {0:X4}h", 3, "Undocumented CALL");
            t[0xFD] = new OpcodeInfo("CALL* {0:X4}h", 3, "Undocumented CALL");
            t[0xC4] = new OpcodeInfo("CNZ {0:X4}h", 3, "Call if Not Zero");
            t[0xCC] = new OpcodeInfo("CZ {0:X4}h", 3, "Call if Zero");
            t[0xD4] = new OpcodeInfo("CNC {0:X4}h", 3, "Call if No Carry");
            t[0xDC] = new OpcodeInfo("CC {0:X4}h", 3, "Call if Carry");
            t[0xE4] = new OpcodeInfo("CPO {0:X4}h", 3, "Call if Parity Odd");
            t[0xEC] = new OpcodeInfo("CPE {0:X4}h", 3, "Call if Parity Even");
            t[0xF4] = new OpcodeInfo("CP {0:X4}h", 3, "Call if Positive");
            t[0xFC] = new OpcodeInfo("CM {0:X4}h", 3, "Call if Minus");

            t[0xC9] = new OpcodeInfo("RET", 1, "Return from subroutine");
            t[0xD9] = new OpcodeInfo("RET*", 1, "Undocumented RET");
            t[0xC0] = new OpcodeInfo("RNZ", 1, "Return if Not Zero");
            t[0xC8] = new OpcodeInfo("RZ", 1, "Return if Zero");
            t[0xD0] = new OpcodeInfo("RNC", 1, "Return if No Carry");
            t[0xD8] = new OpcodeInfo("RC", 1, "Return if Carry");
            t[0xE0] = new OpcodeInfo("RPO", 1, "Return if Parity Odd");
            t[0xE8] = new OpcodeInfo("RPE", 1, "Return if Parity Even");
            t[0xF0] = new OpcodeInfo("RP", 1, "Return if Positive");
            t[0xF8] = new OpcodeInfo("RM", 1, "Return if Minus");

            // Stack & Regs
            for (int p = 0; p < 4; p++)
            {
                t[0xC5 | (p << 4)] = new OpcodeInfo($"PUSH {pushPairs[p]}", 1, $"Push {pushPairs[p]} onto stack");
                t[0xC1 | (p << 4)] = new OpcodeInfo($"POP {pushPairs[p]}", 1, $"Pop {pushPairs[p]} from stack");
            }

            // Restarts
            for (int r = 0; r < 8; r++)
            {
                t[0xC7 | (r << 3)] = new OpcodeInfo($"RST {r}", 1, $"Restart vector {r * 8:X2}h");
            }

            // Ports & Control
            t[0xDB] = new OpcodeInfo("IN {0:X2}h", 2, "Input from I/O port");
            t[0xD3] = new OpcodeInfo("OUT {0:X2}h", 2, "Output to I/O port");
            t[0xE3] = new OpcodeInfo("XTHL", 1, "Exchange top of stack with HL");
            t[0xEB] = new OpcodeInfo("XCHG", 1, "Exchange DE with HL");
            t[0xE9] = new OpcodeInfo("PCHL", 1, "Load PC from HL");
            t[0xF9] = new OpcodeInfo("SPHL", 1, "Load SP from HL");
            t[0xFB] = new OpcodeInfo("EI", 1, "Enable interrupts");
            t[0xF3] = new OpcodeInfo("DI", 1, "Disable interrupts");

            return t;
        }
    }
}
