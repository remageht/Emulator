using System;
using System.Threading;
using System.Threading.Tasks;
using Emulator.Core.Devices;

namespace MikroLab.Studio
{
    /// <summary>
    /// Non-blocking-ish clock for the monitor 0400h delay routine. The legacy
    /// implementation sleeps on the execution thread; the UI keeps reading the
    /// last published snapshot, so a bounded sleep does not freeze the window.
    /// </summary>
    internal sealed class StudioClock : IClock
    {
        public void Delay(int milliseconds)
        {
            if (milliseconds <= 0) return;
            Thread.Sleep(Math.Min(milliseconds, 1500));
        }
    }

    /// <summary>Simple sound output for monitor 0300h and PlayTone calls.</summary>
    internal sealed class StudioSound : ISound
    {
        public void PlayTone(uint frequency, uint duration)
        {
            int f = (int)Math.Max(37, Math.Min(32767, frequency));
            int d = (int)Math.Max(10, Math.Min(500, duration));
            Task.Run(() =>
            {
                try { Console.Beep(f, d); } catch { /* audio unavailable */ }
            });
        }

        public void PlayTestTone()
        {
            Task.Run(() =>
            {
                try
                {
                    Console.Beep(660, 110);
                    Console.Beep(880, 150);
                }
                catch
                {
                    // Audio is best-effort only.
                }
            });
        }
    }
}
