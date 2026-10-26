using System;
using System.Collections.Generic;
using Emulator;

namespace Emulator.Tests
{
    public sealed class LegacyHarness : IDisposable
    {
        public CPPI Ppi;
        public CMemory Memory;
        public CInterpreter Cpu;
        public ProgramLogic FormStub;

        private readonly HashSet<int> _trackedAddresses = new HashSet<int>();

        public LegacyHarness()
        {
            FormStub = new ProgramLogic();
            Ppi = FormStub.PPI;
            Memory = new CMemory(ref Ppi);
            Cpu = new CInterpreter(ref Memory)
            {
                PLRoutine = FormStub,
                bMainAllertActive = false // Disable modal message boxes
            };
            Ppi.setMemory(ref Memory);

            // Connect memory and interpreter to FormStub so UI update calls succeed
            typeof(ProgramLogic).GetField("memory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(FormStub, Memory);
            typeof(ProgramLogic).GetField("interpreter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(FormStub, Cpu);
        }

        public void Reset(ushort startAddress = 0x8000)
        {
            _trackedAddresses.Clear();
            Cpu.ScallStack.Clear();

            // Clear registers in memory 83E4h..83EBh
            for (int r = 0x83E4; r <= 0x83EB; r++)
            {
                Memory.setMemoryValue(r, 0, 0);
            }

            Memory.flagReg.AC = false;
            Memory.flagReg.C = false;
            Memory.flagReg.P = false;
            Memory.flagReg.S = false;
            Memory.flagReg.Z = false;

            SetPC(startAddress);
        }

        public void SetPC(int address)
        {
            var method = typeof(CInterpreter).GetMethod("setCurAddr");
            if (method != null)
                method.Invoke(Cpu, new object[] { address });
            else
            {
                var field = typeof(CInterpreter).GetField("curAddr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field.SetValue(Cpu, address);
            }
        }

        public int GetPC()
        {
            var field = typeof(CInterpreter).GetField("curAddr", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (int)field.GetValue(Cpu);
        }

        public void SetRegister(int regNum, byte value, bool setFlags = false)
        {
            Cpu.setRegister(regNum, value, setFlags);
        }

        public byte GetRegister(int regNum)
        {
            return (byte)Cpu.getRegister(regNum);
        }

        public void WriteByte(int addr, byte value)
        {
            Memory.setMemoryValue(addr, (value >> 4) & 0x0F, value & 0x0F);
            _trackedAddresses.Add(addr);
        }

        public byte ReadByte(int addr)
        {
            int s = 0, l = 0;
            Memory.getMemoryValue(addr, ref s, ref l);
            return (byte)((s << 4) | l);
        }

        public bool Step()
        {
            int pc = GetPC();
            int cmd = ReadByte(pc);
            int op1 = ReadByte(pc + 1);
            int op2 = ReadByte(pc + 2);
            return Cpu.interpretCommand(cmd, op1, op2);
        }

        public CpuSnapshot CaptureSnapshot()
        {
            var snap = new CpuSnapshot
            {
                A = GetRegister(CInterpreter.REG_A),
                B = GetRegister(CInterpreter.REG_B),
                C = GetRegister(CInterpreter.REG_C),
                D = GetRegister(CInterpreter.REG_D),
                E = GetRegister(CInterpreter.REG_E),
                H = GetRegister(CInterpreter.REG_H),
                L = GetRegister(CInterpreter.REG_L),
                M = GetRegister(CInterpreter.REG_M),
                PC = GetPC(),
                FlagZ = Memory.flagReg.Z,
                FlagC = Memory.flagReg.C,
                FlagS = Memory.flagReg.S,
                FlagAC = Memory.flagReg.AC,
                FlagP = Memory.flagReg.P,
                CallStackDepth = Cpu.ScallStack.Count
            };

            foreach (var addr in _trackedAddresses)
            {
                snap.ModifiedMemory[addr] = ReadByte(addr);
            }

            return snap;
        }

        public void TrackAddress(int address)
        {
            _trackedAddresses.Add(address);
        }

        public void Dispose()
        {
            FormStub?.Dispose();
        }
    }
}
