using System;

namespace Emulator.Tests
{
    class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  MicroLab KR580VM80A Emulator - Stage 0 Test Suite");
            Console.WriteLine("=================================================\n");

            int failureCount = 0;

            try
            {
                IntelHexTests.RunAll();
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] IntelHexTests: {ex.Message}\n{ex.StackTrace}\n");
                Console.ResetColor();
            }

            try
            {
                QuirkProbe.RunAll();
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] QuirkProbe: {ex.Message}\n{ex.StackTrace}\n");
                Console.ResetColor();
            }

            try
            {
                CpuGoldenTests.RunAll();
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] CpuGoldenTests: {ex.Message}\n{ex.StackTrace}\n");
                Console.ResetColor();
            }

            try
            {
                LabScenarioTests.RunAll();
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] LabScenarioTests: {ex.Message}\n{ex.StackTrace}\n");
                Console.ResetColor();
            }

            Console.WriteLine("=================================================");
            if (failureCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ALL TEST SUITES PASSED (0 failures)");
                Console.ResetColor();
                Console.WriteLine("=================================================");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  TEST SUITE COMPLETED WITH {failureCount} FAILURES");
                Console.ResetColor();
                Console.WriteLine("=================================================");
                return 1;
            }
        }
    }
}
