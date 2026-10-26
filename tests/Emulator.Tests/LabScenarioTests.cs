using System;
using Emulator;

namespace Emulator.Tests
{
    public static class LabScenarioTests
    {
        public static void RunAll()
        {
            Console.WriteLine("[TEST SUITE] LabScenarioTests (Verified against Crimean Federal University manual)");
            Test_Lab1_Table1_MviRegisterLoads();
            Test_Lab1_Table2_LxiRegisterPairs();
            Test_Lab1_Table3_RegisterTransferChain();
            Test_Lab2_Table1_AdditionSequence_Result_FF();
            Test_Lab3_Table1_LogicFunction_And_Missing_CMA();
            Test_Lab4_LoopAddition_And_Missing_NOP();
            Test_Lab5_Ppi_Tumblers_To_Lamps();
            Console.WriteLine("  All LabScenarioTests PASSED.\n");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Lab scenario assertion failed: " + message);
        }

        public static void Test_Lab1_Table1_MviRegisterLoads()
        {
            // Lab 1, Table 1 (manual page 10):
            // 800 MVI A, 0 (3E 00)
            // 802 MVI B, 1 (06 01)
            // 804 MVI C, 2 (0E 02)
            // 806 MVI D, 3 (16 03)
            // 808 MVI E, 4 (1E 04)
            // 80A MVI H, 5 (26 05)
            // 80C MVI L, 6 (2E 06)
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x800);
                byte[] code = new byte[] {
                    0x3E, 0x00,
                    0x06, 0x01,
                    0x0E, 0x02,
                    0x16, 0x03,
                    0x1E, 0x04,
                    0x26, 0x05,
                    0x2E, 0x06
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x800 + i, code[i]);

                for (int i = 0; i < 7; i++)
                    harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x00, $"A expected 0x00, got {snap.A:X2}");
                Assert(snap.B == 0x01, $"B expected 0x01, got {snap.B:X2}");
                Assert(snap.C == 0x02, $"C expected 0x02, got {snap.C:X2}");
                Assert(snap.D == 0x03, $"D expected 0x03, got {snap.D:X2}");
                Assert(snap.E == 0x04, $"E expected 0x04, got {snap.E:X2}");
                Assert(snap.H == 0x05, $"H expected 0x05, got {snap.H:X2}");
                Assert(snap.L == 0x06, $"L expected 0x06, got {snap.L:X2}");
            }
            Console.WriteLine("  [PASS] Test_Lab1_Table1_MviRegisterLoads (A=0, B=1, C=2, D=3, E=4, H=5, L=6)");
        }

        public static void Test_Lab1_Table2_LxiRegisterPairs()
        {
            // Lab 1, Table 2 (manual page 10):
            // 800 LXI B, 3132 (01 32 31)
            // 803 LXI D, 3334 (11 34 33)
            // 806 LXI H, 3536 (21 36 35)
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x800);
                byte[] code = new byte[] {
                    0x01, 0x32, 0x31,
                    0x11, 0x34, 0x33,
                    0x21, 0x36, 0x35
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x800 + i, code[i]);

                for (int i = 0; i < 3; i++)
                    harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.B == 0x31 && snap.C == 0x32, $"BC mismatch: {snap.B:X2} {snap.C:X2}");
                Assert(snap.D == 0x33 && snap.E == 0x34, $"DE mismatch: {snap.D:X2} {snap.E:X2}");
                Assert(snap.H == 0x35 && snap.L == 0x36, $"HL mismatch: {snap.H:X2} {snap.L:X2}");
            }
            Console.WriteLine("  [PASS] Test_Lab1_Table2_LxiRegisterPairs (B=31 C=32 D=33 E=34 H=35 L=36)");
        }

        public static void Test_Lab1_Table3_RegisterTransferChain()
        {
            // Lab 1, Table 3 (manual page 10):
            // 800 MVI A, FF (3E FF)
            // 802 MOV B, A  (47)
            // 803 MOV C, B  (48)
            // 804 MOV D, C  (51)
            // 805 MOV E, D  (5A)
            // 806 MOV H, E  (63)
            // 807 MOV L, H  (6C)
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x800);
                byte[] code = new byte[] {
                    0x3E, 0xFF,
                    0x47,
                    0x48,
                    0x51,
                    0x5A,
                    0x63,
                    0x6C
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x800 + i, code[i]);

                for (int i = 0; i < 7; i++)
                    harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0xFF && snap.B == 0xFF && snap.C == 0xFF && snap.D == 0xFF &&
                       snap.E == 0xFF && snap.H == 0xFF && snap.L == 0xFF, "All registers must equal 0xFF");
            }
            Console.WriteLine("  [PASS] Test_Lab1_Table3_RegisterTransferChain (All registers A..L = FF)");
        }

        public static void Test_Lab2_Table1_AdditionSequence_Result_FF()
        {
            // Lab 2, Table 1 (manual page 14):
            // Expression: (A) = (A) + (B) + (M) + 1
            // 800 MVI A, 45 (3E 45) -> A = 45
            // 802 MVI B, AB (06 AB) -> B = AB
            // 804 LXI H, 0900 (21 00 09) -> HL = 0900
            // 807 MVI M, 0E (36 0E) -> [0900] = 0E
            // 809 ADD B (80) -> A = 45 + AB = F0
            // 80A ADD M (86) -> A = F0 + 0E = FE
            // 80B INR A (3C) -> A = FE + 1 = FF
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x800);
                byte[] code = new byte[] {
                    0x3E, 0x45,
                    0x06, 0xAB,
                    0x21, 0x00, 0x09,
                    0x36, 0x0E,
                    0x80,
                    0x86,
                    0x3C
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x800 + i, code[i]);

                // Step 1: MVI A, 45
                harness.Step();
                Assert(harness.CaptureSnapshot().A == 0x45, "A after MVI A,45");

                // Step 2: MVI B, AB
                harness.Step();
                Assert(harness.CaptureSnapshot().B == 0xAB, "B after MVI B,AB");

                // Step 3: LXI H, 0900
                harness.Step();

                // Step 4: MVI M, 0E
                harness.Step();
                Assert(harness.ReadByte(0x0900) == 0x0E, "[0900] after MVI M,0E");

                // Step 5: ADD B
                harness.Step();
                Assert(harness.CaptureSnapshot().A == 0xF0, "A after ADD B (expected F0)");

                // Step 6: ADD M
                harness.Step();
                Assert(harness.CaptureSnapshot().A == 0xFE, "A after ADD M (expected FE)");

                // Step 7: INR A
                harness.Step();
                Assert(harness.CaptureSnapshot().A == 0xFF, "A after INR A (expected FF)");
            }
            Console.WriteLine("  [PASS] Test_Lab2_Table1_AdditionSequence_Result_FF ((A)+(B)+(M)+1 = FF confirmed)");
        }

        public static void Test_Lab3_Table1_LogicFunction_And_Missing_CMA()
        {
            // Lab 3, Table 1 (manual page 18-19):
            // A = ((A v B v M) * C) ^ D, then NOT A (CMA)
            // 800 MVI A, 25 (3E 25)
            // 802 MVI B, 3A (06 3A)
            // 804 MVI C, 2E (0E 2E)
            // 806 MVI D, 16 (16 16)
            // 808 LXI H, 0900 (21 00 09)
            // 80B MVI M, 28 (36 28)
            // 80D ORA B (B0) -> 25 | 3A = 3F
            // 80E ORA M (B6) -> 3F | 28 = 3F
            // 80F ANA C (A1) -> 3F & 2E = 2E
            // 810 XRA D (AA) -> 2E ^ 16 = 38
            // 811 CMA (2F) -> MISSING in legacy!
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x800);
                byte[] code = new byte[] {
                    0x3E, 0x25,
                    0x06, 0x3A,
                    0x0E, 0x2E,
                    0x16, 0x16,
                    0x21, 0x00, 0x09,
                    0x36, 0x28,
                    0xB0,
                    0xB6,
                    0xA1,
                    0xAA,
                    0x2F // CMA
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x800 + i, code[i]);

                // Run up to XRA D (10 instructions)
                for (int i = 0; i < 10; i++)
                    harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x38, $"Intermediate result before CMA: expected 0x38, got 0x{snap.A:X2}");

                // Now execute CMA (0x2F) in legacy
                bool okCma = harness.Step();
                Assert(!okCma, "CMA (0x2F) is missing in legacy CPU, step must return false");
            }
            Console.WriteLine("  [PASS] Test_Lab3_Table1_LogicFunction_And_Missing_CMA (A=38h computed, CMA unsupported in legacy confirmed)");
        }

        public static void Test_Lab4_LoopAddition_And_Missing_NOP()
        {
            // Lab 4, Table 1 (manual page 25): Sum of array elements
            // In manual: 8111 is 00 NOP (Ml: NOP)
            // In legacy: NOP is unsupported command!
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8100);
                // Data length at 0x8200 = 3
                harness.WriteByte(0x8200, 3);
                // Array at 0x8201..0x8203 = 10, 20, 30 -> sum = 60 (0x3C)
                harness.WriteByte(0x8201, 10);
                harness.WriteByte(0x8202, 20);
                harness.WriteByte(0x8203, 30);

                // 8100: LDA 8200h (3A 00 82)
                harness.WriteByte(0x8100, 0x3A); harness.WriteByte(0x8101, 0x00); harness.WriteByte(0x8102, 0x82);
                // 8103: MOV B, A (47)
                harness.WriteByte(0x8103, 0x47);
                // 8104: SUB A (97)
                harness.WriteByte(0x8104, 0x97);
                // 8105: LXI H, 8201h (21 01 82)
                harness.WriteByte(0x8105, 0x21); harness.WriteByte(0x8106, 0x01); harness.WriteByte(0x8107, 0x82);
                // M2 (8108): ADD M (86)
                harness.WriteByte(0x8108, 0x86);
                // 8109: DCR B (05)
                harness.WriteByte(0x8109, 0x05);
                // 810A: JZ 8111h (CA 11 81)
                harness.WriteByte(0x810A, 0xCA); harness.WriteByte(0x810B, 0x11); harness.WriteByte(0x810C, 0x81);
                // 810D: INX H (23)
                harness.WriteByte(0x810D, 0x23);
                // 810E: JMP 8108h (C3 08 81)
                harness.WriteByte(0x810E, 0xC3); harness.WriteByte(0x810F, 0x08); harness.WriteByte(0x8110, 0x81);
                // 8111: NOP (00)
                harness.WriteByte(0x8111, 0x00);

                // Run loop until PC reaches 0x8111 (or halts/fails)
                int steps = 0;
                while (harness.GetPC() != 0x8111 && steps < 100)
                {
                    bool ok = harness.Step();
                    if (!ok) break;
                    steps++;
                }

                Assert(harness.GetPC() == 0x8111, $"Loop completed, PC=0x{harness.GetPC():X4}");
                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 60, $"Sum of 10+20+30 expected 60, got {snap.A}");

                // At 0x8111, NOP (0x00) is unsupported in legacy:
                bool okNop = harness.Step();
                Assert(!okNop, "NOP (00h) at end of loop is unsupported in legacy");
            }
            Console.WriteLine("  [PASS] Test_Lab4_LoopAddition_And_Missing_NOP (Sum=60 computed, NOP 00h unsupported in legacy confirmed)");
        }

        public static void Test_Lab5_Ppi_Tumblers_To_Lamps()
        {
            // Lab 5, Table 1 (manual page 31):
            // 8000 MVI A, 81 (3E 81)
            // 8002 OUT FB (D3 FB) - program PPI
            // 8004 IN FA (DB FA) - read tumblers
            // 8006 OUT F9 (D3 F9) - write lamps
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                // Set tumblers: CB1=on, CB2=off, CB3=on -> 1 | 9 | 3 = 13 (0x0D)
                harness.Memory.PPI.CB1.Checked = true;
                harness.Memory.PPI.CB2.Checked = false;
                harness.Memory.PPI.CB3.Checked = true;

                byte[] code = new byte[] {
                    0x3E, 0x81,
                    0xD3, 0xFB,
                    0xDB, 0xFA,
                    0xD3, 0xF9
                };

                for (int i = 0; i < code.Length; i++)
                    harness.WriteByte(0x8000 + i, code[i]);

                for (int i = 0; i < 4; i++)
                    harness.Step();

                var snap = harness.CaptureSnapshot();
                // In legacy: formula is 1 | (9*CB1) | (5*CB2) | (3*CB3) = 1 | 9 | 0 | 3 = 11 (binary 1011)
                Assert(snap.A == 11, $"Tumbler read A expected 11, got {snap.A}");
                // In legacy: OUT F9 sets CDigitValue = 11 and writes bits to PPI lamps!
                Assert(harness.Memory.CDigitValue == 11, $"Lamps value expected 11, got {harness.Memory.CDigitValue}");
            }
            Console.WriteLine("  [PASS] Test_Lab5_Ppi_Tumblers_To_Lamps (Tumblers 11 written to lamps 11)");
        }
    }
}
