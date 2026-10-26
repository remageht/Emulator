namespace Emulator.Core.Cpu
{
    public interface ICpu
    {
        CpuState State { get; }
        CpuProfile Profile { get; }

        /// <summary>
        /// Executes exactly one instruction at State.PC.
        /// Returns true if execution should continue, false if HLT was reached or unsupported command.
        /// </summary>
        bool Step();

        void Reset();
    }
}
