using System;
using System.IO;
using Emulator;

namespace Emulator.Tests
{
    public static class QuirkProbe
    {
        public static void RunAll()
        {
            Console.WriteLine("[TEST SUITE] QuirkProbe (Verifying Legacy CPU Deviations & Bugs)");
            Test_Xchg_Quirk();
            Test_Ldax_Stax_De_Quirk();
            Test_Dad_Quirk();
            Test_Lhld_Shld_Quirk();
            Test_Lxi_Sp_Quirk();
            Test_Mov_Mvi_Flag_Corruption();
            Test_Inr_Dcr_Carry_Quirk();
            Test_Inx_Dcx_ClearFlags_Quirk();
            Test_Rc_D8_Quirk();
            Test_In_Fa_Quirk();
            Test_Seggg_Monitor_Quirk();
            Test_Unsupported_Commands_In_Legacy();
            Console.WriteLine("  All QuirkProbe tests PASSED (Legacy behavior accurately confirmed).\n");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Quirk verification failed: " + message);
        }

        public static void Test_Xchg_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: setRegPair shifts val >> 4 and & 15 instead of >> 8!
                // getRegPair(1) is C:D; getRegPair(2) is D:E.
                harness.SetRegister(CInterpreter.REG_C, 0x11);
                harness.SetRegister(CInterpreter.REG_D, 0x12);
                harness.SetRegister(CInterpreter.REG_E, 0x34);
                harness.SetRegister(CInterpreter.REG_H, 0x56);
                harness.SetRegister(CInterpreter.REG_L, 0x78);

                harness.WriteByte(0x8000, 0xEB); // XCHG
                bool ok = harness.Step();
                Assert(ok, "XCHG executed");

                var snap = harness.CaptureSnapshot();
                // In legacy, because of setRegPair(n, val >> 4, val & 15):
                // regPair 1 = C:D, regPair 2 = D:E
                // XCHG produces H=11, L=02, D=23, E=04
                Assert(snap.H == 0x11 && snap.L == 0x02 && snap.D == 0x23 && snap.E == 0x04,
                    $"Legacy XCHG quirk mismatch: H={snap.H:X2}, L={snap.L:X2}, D={snap.D:X2}, E={snap.E:X2}");
                Console.WriteLine("  [PASS] Test_Xchg_Quirk (H=11 L=02 D=23 E=04 confirmed)");
            }
        }

        public static void Test_Ldax_Stax_De_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: getRegPair(1) is C:D, not D:E!
                // Set C=0x82, D=0x00, E=0x99. Target address 0x8200 has value 0x55.
                harness.SetRegister(CInterpreter.REG_C, 0x82);
                harness.SetRegister(CInterpreter.REG_D, 0x00);
                harness.SetRegister(CInterpreter.REG_E, 0x99);
                harness.WriteByte(0x8200, 0x55);

                harness.WriteByte(0x8000, 0x1A); // LDAX D
                harness.Step();

                var snap = harness.CaptureSnapshot();
                // Reads from C:D (0x8200), getting 0x55!
                Assert(snap.A == 0x55, $"Legacy LDAX D should read from C:D, got A=0x{snap.A:X2}");
                Console.WriteLine("  [PASS] Test_Ldax_Stax_De_Quirk (Reads C:D instead of D:E)");
            }
        }

        public static void Test_Dad_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // DAD regPaigNum:
                // this.setRegPair(regPaigNum, this.getRegPair(regPaigNum) + this.getRegPair(2));
                // For DAD D (regPaigNum = 1): modifies pair 1 (DE), not HL!
                harness.SetRegister(CInterpreter.REG_H, 0x10);
                harness.SetRegister(CInterpreter.REG_L, 0x00);
                harness.SetRegister(CInterpreter.REG_D, 0x01);
                harness.SetRegister(CInterpreter.REG_E, 0x23);

                harness.WriteByte(0x8000, 0x19); // DAD D
                harness.Step();

                var snap = harness.CaptureSnapshot();
                // HL is unchanged (10 00), DE is modified to 12 04 due to shift by 4 bug
                Assert(snap.H == 0x10 && snap.L == 0x00, $"HL should be unchanged in legacy DAD D, got H={snap.H:X2} L={snap.L:X2}");
                Assert(snap.D == 0x12 && snap.E == 0x04, $"DE was modified to {snap.D:X2} {snap.E:X2}");
                Console.WriteLine("  [PASS] Test_Dad_Quirk (DAD D modifies DE instead of HL)");
            }
        }

        public static void Test_Lhld_Shld_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: LHLD is:
                // setRegPair(2, 0, getMemory(getWordFromTwoBytes(curOp2, curOp1)))
                // setRegPair(2, sign, low) writes to reg 4 (H) and reg 5 (L):
                // H gets 0, L gets the byte!
                harness.WriteByte(0x8200, 0x34);
                harness.WriteByte(0x8201, 0x12);

                harness.WriteByte(0x8000, 0x2A); // LHLD 8200h
                harness.WriteByte(0x8001, 0x00);
                harness.WriteByte(0x8002, 0x82);
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.H == 0x00 && snap.L == 0x34, $"Legacy LHLD writes H=00 and L=byte, got H={snap.H:X2} L={snap.L:X2}");
                Console.WriteLine("  [PASS] Test_Lhld_Shld_Quirk (LHLD writes H=00, L=byte)");
            }
        }

        public static void Test_Lxi_Sp_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: LXI SP (pair index 3) calls setRegPair(3, op2, op1)
                // setRegister(3*2, ...) = setRegister(6) = M (memory at HL)!
                // setRegister(3*2+1, ...) = setRegister(7) = A!
                harness.SetRegister(CInterpreter.REG_H, 0x82);
                harness.SetRegister(CInterpreter.REG_L, 0x00);

                harness.WriteByte(0x8000, 0x31); // LXI SP, 83FFh
                harness.WriteByte(0x8001, 0xFF);
                harness.WriteByte(0x8002, 0x83);
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0xFF, $"LXI SP in legacy writes low byte to A, got A={snap.A:X2}");
                byte mVal = harness.ReadByte(0x8200);
                Assert(mVal == 0x83, $"LXI SP in legacy writes high byte to [HL], got [8200]={mVal:X2}");
                Console.WriteLine("  [PASS] Test_Lxi_Sp_Quirk (LXI SP writes to M and A)");
            }
        }

        public static void Test_Mov_Mvi_Flag_Corruption()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: setRegister calls updateFlags(val)!
                // So MOV and MVI alter Z, C, S flags.
                harness.SetRegister(CInterpreter.REG_C, 0x00);
                harness.Memory.flagReg.Z = false;

                harness.WriteByte(0x8000, 0x79); // MOV A, C (value 0)
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.FlagZ == true, "MOV A,C with val 0 set Z=true in legacy!");
                Console.WriteLine("  [PASS] Test_Mov_Mvi_Flag_Corruption (MOV alters Z/C/S)");
            }
        }

        public static void Test_Inr_Dcr_Carry_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In standard 8080: INR/DCR do NOT affect C flag.
                // In legacy: updateFlags resets C to false if val in 0..255!
                harness.SetRegister(CInterpreter.REG_A, 0x00);
                harness.Memory.flagReg.C = true;

                harness.WriteByte(0x8000, 0x3C); // INR A (0 -> 1)
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.FlagC == false, "Legacy INR cleared C flag!");

                // DCR 0: (0 - 1 = -1) -> val < 0 -> sets C=true!
                harness.SetRegister(CInterpreter.REG_A, 0x00);
                harness.WriteByte(0x8001, 0x3D); // DCR A
                harness.SetPC(0x8001);
                harness.Step();

                snap = harness.CaptureSnapshot();
                Assert(snap.FlagC == true, "Legacy DCR 0 sets C=true!");
                Console.WriteLine("  [PASS] Test_Inr_Dcr_Carry_Quirk (INR clears C, DCR 0 sets C)");
            }
        }

        public static void Test_Inx_Dcx_ClearFlags_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // In legacy: INX and DCX call clearFags()!
                harness.Memory.flagReg.Z = true;
                harness.Memory.flagReg.C = true;
                harness.Memory.flagReg.S = true;

                harness.WriteByte(0x8000, 0x03); // INX B
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.FlagZ == false && snap.FlagC == false && snap.FlagS == false,
                    "INX called clearFags() and wiped out all flags!");
                Console.WriteLine("  [PASS] Test_Inx_Dcx_ClearFlags_Quirk (INX/DCX clears all flags)");
            }
        }

        public static void Test_Rc_D8_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // 0xD8 in standard 8080 is RC (Return if Carry, 1 byte).
                // In legacy CInterpreter.cs:
                // if (curCmd == 216) { this.setRegister(7, this.getMemory(250 + curOp1)); this.setCurAddr(curAddr + 2); return true; }
                harness.WriteByte(250 + 5, 0x77); // Mem[255] = 0x77

                harness.WriteByte(0x8000, 0xD8); // RC
                harness.WriteByte(0x8001, 0x05); // operand = 5 -> address 255
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x77, $"Legacy RC loaded mem[255] into A, got A=0x{snap.A:X2}");
                Assert(snap.PC == 0x8002, $"Legacy RC advanced PC by 2, got PC=0x{snap.PC:X4}");
                Console.WriteLine("  [PASS] Test_Rc_D8_Quirk (RC D8h acts as 2-byte memory load)");
            }
        }

        public static void Test_In_Fa_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // IN FAh reads tumblers: 1 | (CB1?9:0) | (CB2?5:0) | (CB3?3:0)
                // Let's set CB1 and CB2 checked:
                harness.Memory.PPI.CB1.Checked = true;
                harness.Memory.PPI.CB2.Checked = true;
                harness.Memory.PPI.CB3.Checked = false;

                harness.WriteByte(0x8000, 0xDB); // IN
                harness.WriteByte(0x8001, 0xFA); // FAh
                harness.Step();

                var snap = harness.CaptureSnapshot();
                int expected = 1 | 9 | 5; // 15 = 0x0F
                Assert(snap.A == expected, $"Legacy IN FAh: expected {expected}, got {snap.A}");
                Console.WriteLine("  [PASS] Test_In_Fa_Quirk (IN FAh reads tumblers formula)");
            }
        }

        public static void Test_Seggg_Monitor_Quirk()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // CALL 0100h executes seggg():
                // Reads 4 bytes at 33780..33783 (0x83F4..0x83F7)
                // Writes 8 segment codes to 33784..33791 (0x83F8..0x83FF)
                harness.WriteByte(0x83F4, 0x01); // 0 and 1 -> numArray[0]=64 (0x40), numArray[1]=6 (0x06)
                harness.WriteByte(0x83F5, 0x23);
                harness.WriteByte(0x83F6, 0x45);
                harness.WriteByte(0x83F7, 0x67);

                harness.WriteByte(0x8000, 0xCD); // CALL 0100h
                harness.WriteByte(0x8001, 0x00);
                harness.WriteByte(0x8002, 0x01);
                harness.Step();

                byte d0 = harness.ReadByte(0x83F8);
                byte d1 = harness.ReadByte(0x83F9);
                Assert(d0 == 64, $"SEGGG digit 0: expected 64, got {d0}");
                Assert(d1 == 6, $"SEGGG digit 1: expected 6, got {d1}");
                Console.WriteLine("  [PASS] Test_Seggg_Monitor_Quirk (CALL 0100h SEGGG decodes to 7-seg)");
            }
        }

        public static void Test_Unsupported_Commands_In_Legacy()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // NOP (0x00) is NOT supported in legacy
                harness.WriteByte(0x8000, 0x00);
                bool okNop = harness.Step();
                Assert(!okNop, "NOP must return false (unsupported) in legacy");

                // PUSH B (0xC5)
                harness.WriteByte(0x8000, 0xC5);
                bool okPush = harness.Step();
                Assert(!okPush, "PUSH B must return false (unsupported) in legacy");

                // CPI (0xFE)
                harness.WriteByte(0x8000, 0xFE);
                bool okCpi = harness.Step();
                Assert(!okCpi, "CPI must return false (unsupported) in legacy");

                Console.WriteLine("  [PASS] Test_Unsupported_Commands_In_Legacy (NOP, PUSH, CPI confirmed unsupported)");
            }
        }
    }
}
