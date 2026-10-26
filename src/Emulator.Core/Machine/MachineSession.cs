using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Emulator.Core.Devices;

namespace Emulator.Core.Machine
{
    public sealed class MachineSession : IDisposable
    {
        private readonly Machine _machine;
        private readonly object _lock = new object();
        private Thread _runThread;
        private volatile bool _isRunning;
        private volatile bool _shouldStop;
        private volatile bool _isDisposed;

        public Machine Machine => _machine;
        public RunState State => _machine.State;

        public event Action<MachineSnapshot> SnapshotUpdated;
        public event Action<string> ExecutionError;
        public event Action<ushort, byte> UnsupportedCommand;

        public bool ThrottleLikeOriginal { get; set; } = true;
        public int InstructionsPerSlice { get; set; } = 2000;

        public MachineSession(Machine machine)
        {
            _machine = machine ?? throw new ArgumentNullException(nameof(machine));
            if (_machine.Diagnostics == null)
            {
                _machine.Diagnostics = new SessionDiagnostics(this);
            }
        }

        private class SessionDiagnostics : IDiagnostics
        {
            private readonly MachineSession _session;
            public SessionDiagnostics(MachineSession session) => _session = session;
            public void ReportError(string message) => _session.ExecutionError?.Invoke(message);
            public void ReportUnsupported(int pc, byte opcode) => _session.UnsupportedCommand?.Invoke((ushort)pc, opcode);
        }

        public void Run()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                _machine.State = RunState.Running;
                _isRunning = true;
                _shouldStop = false;

                _runThread = new Thread(RunWorker)
                {
                    IsBackground = true,
                    Name = "MachineExecutionLoop"
                };
                _runThread.Start();
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (!_isRunning) return;
                _shouldStop = true;
            }

            _runThread?.Join(500);
            lock (_lock)
            {
                _machine.State = RunState.Paused;
                _isRunning = false;
            }
            PublishSnapshot();
        }

        public void Step()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Pause();
                }

                _machine.State = RunState.Running;
                bool ok = _machine.Step();
                _machine.State = ok ? RunState.Paused : RunState.Halted;
            }
            PublishSnapshot();
        }

        public void Reset(ResetKind kind = ResetKind.Soft)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    _shouldStop = true;
                }
            }

            _runThread?.Join(500);

            lock (_lock)
            {
                _isRunning = false;
                _machine.Reset(kind);
            }
            PublishSnapshot();
        }

        private void RunWorker()
        {
            var stopwatch = Stopwatch.StartNew();
            long lastSnapshotTime = 0;

            try
            {
                while (!_shouldStop && !_isDisposed)
                {
                    bool canContinue = true;
                    int count = 0;

                    lock (_lock)
                    {
                        for (int i = 0; i < InstructionsPerSlice; i++)
                        {
                            if (_shouldStop) break;

                            canContinue = _machine.Step();
                            count++;
                            if (!canContinue)
                            {
                                _machine.State = RunState.Halted;
                                break;
                            }
                        }
                    }

                    long now = stopwatch.ElapsedMilliseconds;
                    if (now - lastSnapshotTime >= 33) // ~30 Hz
                    {
                        PublishSnapshot();
                        lastSnapshotTime = now;
                    }

                    if (!canContinue)
                        break;

                    if (ThrottleLikeOriginal)
                    {
                        // Sleep 5ms every slice of instructions to match original responsiveness & timing
                        Thread.Sleep(5);
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            }
            catch (Exception ex)
            {
                ExecutionError?.Invoke(ex.Message);
            }
            finally
            {
                lock (_lock)
                {
                    _isRunning = false;
                    if (_machine.State == RunState.Running)
                        _machine.State = RunState.Paused;
                }
                PublishSnapshot();
            }
        }

        public void PublishSnapshot()
        {
            MachineSnapshot snap;
            lock (_lock)
            {
                snap = _machine.Capture();
            }
            SnapshotUpdated?.Invoke(snap);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _shouldStop = true;
            _runThread?.Join(500);
        }
    }
}
