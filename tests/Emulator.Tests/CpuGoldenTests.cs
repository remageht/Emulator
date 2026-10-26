using System;
using System.Collections.Generic;
using Emulator;

namespace Emulator.Tests
{
    public static class CpuGoldenTests
    {
        public static void RunAll()
        {
            Console.WriteLine("[TEST SUITE] CpuGoldenTests (Baseline Instruction Snapshots)");
            Test_Mov_AllRegisters();
            Test_Mvi_AllRegisters();
            Test_Add_Sub_AllRegisters();
            Test_Inr_Dcr_AllRegisters();
            Test_Logic_Ana_Xra_Ora_AllRegisters();
            Test_Cmp_AllRegisters();
            Test_Branching_Jmp_Jnz_Jz_Jnc_Jc();
            Test_Call_Ret_Stack();
            Console.WriteLine("  All CpuGoldenTests PASSED.\n");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Golden test failed: " + message);
        }

        public static void Test_Mov_AllRegisters()
        {
            // Test MOV r1, r2 for all combinations
            using (var harness = new LegacyHarness())
            {
                // Set HL to address 0x8200 and set memory [0x8200] = 0xAA
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_H, 0x82);
                harness.SetRegister(CInterpreter.REG_L, 0x00);
                harness.WriteByte(0x8200, 0xAA);

                // Set initial register values
                harness.SetRegister(CInterpreter.REG_B, 0x10);
                harness.SetRegister(CInterpreter.REG_C, 0x20);
                harness.SetRegister(CInterpreter.REG_D, 0x30);
                harness.SetRegister(CInterpreter.REG_E, 0x40);
                harness.SetRegister(CInterpreter.REG_A, 0x70);

                // MOV A, B (opcode 0x78 = 01 111 000)
                harness.WriteByte(0x8000, 0x78);
                harness.SetPC(0x8000);
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x10, $"MOV A, B failed: got A=0x{snap.A:X2}");
                Assert(snap.PC == 0x8001, "MOV advances PC by 1");

                // MOV B, M (opcode 0x46 = 01 000 110)
                harness.WriteByte(0x8001, 0x46);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.B == 0xAA, $"MOV B, M failed: got B=0x{snap.B:X2}");

                // MOV M, A (opcode 0x77 = 01 110 111)
                harness.SetRegister(CInterpreter.REG_A, 0x55);
                harness.WriteByte(0x8002, 0x77);
                harness.Step();
                byte memVal = harness.ReadByte(0x8200);
                Assert(memVal == 0x55, $"MOV M, A failed: got [0x8200]=0x{memVal:X2}");
            }
            Console.WriteLine("  [PASS] Test_Mov_AllRegisters");
        }

        public static void Test_Mvi_AllRegisters()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_H, 0x82);
                harness.SetRegister(CInterpreter.REG_L, 0x50);

                // MVI B..A (8 instructions)
                byte[] regs = new byte[] { 0x06, 0x0E, 0x16, 0x1E, 0x26, 0x2E, 0x36, 0x3E };
                byte[] values = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x82, 0x50, 0x77, 0x88 };

                int pc = 0x8000;
                for (int i = 0; i < 8; i++)
                {
                    harness.WriteByte(pc, regs[i]);
                    harness.WriteByte(pc + 1, values[i]);
                    pc += 2;
                }

                harness.SetPC(0x8000);
                for (int i = 0; i < 8; i++)
                {
                    bool ok = harness.Step();
                    Assert(ok, $"MVI step {i} executed");
                }

                var snap = harness.CaptureSnapshot();
                Assert(snap.B == 0x11 && snap.C == 0x22 && snap.D == 0x33 && snap.E == 0x44, "MVI B..E");
                Assert(snap.H == 0x82 && snap.L == 0x50, "MVI H, L");
                Assert(snap.A == 0x88, "MVI A");
                Assert(harness.ReadByte(0x8250) == 0x77, "MVI M (memory at HL)");
            }
            Console.WriteLine("  [PASS] Test_Mvi_AllRegisters");
        }

        public static void Test_Add_Sub_AllRegisters()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_A, 0x10);
                harness.SetRegister(CInterpreter.REG_B, 0x25);

                // ADD B (0x80)
                harness.WriteByte(0x8000, 0x80);
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x35, $"ADD B: expected 0x35, got 0x{snap.A:X2}");
                Assert(snap.FlagZ == false && snap.FlagC == false, "ADD B flags");

                // SUB B (0x90) -> 0x35 - 0x25 = 0x10
                harness.WriteByte(0x8001, 0x90);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x10, $"SUB B: expected 0x10, got 0x{snap.A:X2}");

                // SUB A (0x97) -> 0x10 - 0x10 = 0x00
                harness.WriteByte(0x8002, 0x97);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x00, $"SUB A: expected 0x00, got 0x{snap.A:X2}");
                Assert(snap.FlagZ == true, "SUB A sets Z=true");
            }
            Console.WriteLine("  [PASS] Test_Add_Sub_AllRegisters");
        }

        public static void Test_Inr_Dcr_AllRegisters()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_C, 0x05);

                // INR C (0x0C)
                harness.WriteByte(0x8000, 0x0C);
                harness.Step();
                var snap = harness.CaptureSnapshot();
                Assert(snap.C == 0x06, $"INR C: expected 6, got {snap.C}");

                // DCR C (0x0D)
                harness.WriteByte(0x8001, 0x0D);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.C == 0x05, $"DCR C: expected 5, got {snap.C}");
            }
            Console.WriteLine("  [PASS] Test_Inr_Dcr_AllRegisters");
        }

        public static void Test_Logic_Ana_Xra_Ora_AllRegisters()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_A, 0x0F);
                harness.SetRegister(CInterpreter.REG_B, 0x33);

                // ORA B (0xB0): 0x0F | 0x33 = 0x3F
                harness.WriteByte(0x8000, 0xB0);
                harness.Step();
                var snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x3F, $"ORA B: expected 0x3F, got 0x{snap.A:X2}");

                // ANA B (0xA0): 0x3F & 0x33 = 0x33
                harness.WriteByte(0x8001, 0xA0);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x33, $"ANA B: expected 0x33, got 0x{snap.A:X2}");

                // XRA B (0xA8): 0x33 ^ 0x33 = 0x00
                harness.WriteByte(0x8002, 0xA8);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.A == 0x00, $"XRA B: expected 0x00, got 0x{snap.A:X2}");
                Assert(snap.FlagZ == true, "XRA B with equal sets Z=true");
            }
            Console.WriteLine("  [PASS] Test_Logic_Ana_Xra_Ora_AllRegisters");
        }

        public static void Test_Cmp_AllRegisters()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_A, 0x10);
                harness.SetRegister(CInterpreter.REG_B, 0x10);

                // CMP B (0xB8) when A == B -> Z=true, C=false
                harness.WriteByte(0x8000, 0xB8);
                harness.Step();
                var snap = harness.CaptureSnapshot();
                Assert(snap.FlagZ == true && snap.FlagC == false, "CMP B equal");
                Assert(snap.A == 0x10, "CMP must not change A");

                // CMP C (0xB9) when A < C -> C=true
                harness.SetRegister(CInterpreter.REG_C, 0x20);
                harness.WriteByte(0x8001, 0xB9);
                harness.Step();
                snap = harness.CaptureSnapshot();
                Assert(snap.FlagC == true, "CMP C when A < C must set C=true");
            }
            Console.WriteLine("  [PASS] Test_Cmp_AllRegisters");
        }

        public static void Test_Branching_Jmp_Jnz_Jz_Jnc_Jc()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);

                // JMP 8010h (0xC3 0x10 0x80)
                harness.WriteByte(0x8000, 0xC3);
                harness.WriteByte(0x8001, 0x10);
                harness.WriteByte(0x8002, 0x80);
                harness.Step();
                Assert(harness.GetPC() == 0x8010, "JMP should set PC=0x8010");

                // JZ 8020h with Z=false -> should not branch, PC advances by 3
                harness.Memory.flagReg.Z = false;
                harness.WriteByte(0x8010, 0xCA);
                harness.WriteByte(0x8011, 0x20);
                harness.WriteByte(0x8012, 0x80);
                harness.Step();
                Assert(harness.GetPC() == 0x8013, "JZ should advance by 3 if Z=false");

                // JZ 8020h with Z=true -> should branch to 0x8020
                harness.Memory.flagReg.Z = true;
                harness.WriteByte(0x8013, 0xCA);
                harness.WriteByte(0x8014, 0x20);
                harness.WriteByte(0x8015, 0x80);
                harness.Step();
                Assert(harness.GetPC() == 0x8020, "JZ should branch to 0x8020 when Z=true");
            }
            Console.WriteLine("  [PASS] Test_Branching_Jmp_Jnz_Jz_Jnc_Jc");
        }

        public static void Test_Call_Ret_Stack()
        {
            using (var harness = new LegacyHarness())
            {
                harness.Reset(0x8000);
                harness.SetRegister(CInterpreter.REG_B, 0x55);

                // CALL 8100h (0xCD 0x00 0x81)
                harness.WriteByte(0x8000, 0xCD);
                harness.WriteByte(0x8001, 0x00);
                harness.WriteByte(0x8002, 0x81);
                harness.Step();

                Assert(harness.GetPC() == 0x8100, "CALL should set PC=0x8100");
                Assert(harness.Cpu.ScallStack.Count == 1, "ScallStack depth should be 1");
                Assert(harness.Cpu.ScallStack.Peek().iRetAddres == 0x8003, "Return address should be 0x8003");

                // Under routine, change B to 0x99
                harness.SetRegister(CInterpreter.REG_B, 0x99);

                // RET (0xC9)
                harness.WriteByte(0x8100, 0xC9);
                harness.Step();

                var snap = harness.CaptureSnapshot();
                Assert(snap.PC == 0x8003, $"RET should return to 0x8003, got 0x{snap.PC:X4}");
                // In legacy: RET restores all registers saved by CALL!
                Assert(snap.B == 0x55, $"Legacy RET restores B to 0x55, got 0x{snap.B:X2}");
            }
            Console.WriteLine("  [PASS] Test_Call_Ret_Stack");
        }
    }
}
