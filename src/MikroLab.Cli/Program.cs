using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Emulator.Core.Assembler;
using Emulator.Core.Cpu;
using Emulator.Core.Machine;
using Emulator.Core.Memory;

namespace MikroLab.Cli
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0)
            {
                PrintHelp();
                return 0;
            }

            string command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "help":
                case "--help":
                case "-h":
                case "/?":
                    PrintHelp();
                    return 0;

                case "info":
                case "spec":
                    PrintInfo();
                    return 0;

                case "lab":
                    return RunLab(args);

                case "run":
                    return RunProgram(args);

                case "asm":
                case "assemble":
                    return AssembleFile(args);

                case "disasm":
                case "disassemble":
                    return DisassembleFile(args);

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неизвестная команда: " + args[0]);
                    Console.ResetColor();
                    Console.WriteLine("Введите 'mikrolab help' для вывода списка команд.");
                    return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================================");
            Console.WriteLine(" MikroLab CLI — КР580ВМ80А / Intel 8080 Эмулятор (v1.0)");
            Console.WriteLine(" Кроссплатформенная консольная среда (Windows, Linux, macOS)");
            Console.WriteLine("=================================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Использование: mikrolab <команда> [параметры]");
            Console.WriteLine();
            Console.WriteLine("Команды:");
            Console.WriteLine("  run <файл.asm|файл.hex>   Собрать и выполнить программу в памяти");
            Console.WriteLine("  lab <1..5>                Выполнить и проверить лабораторную работу");
            Console.WriteLine("  assemble <файл.asm>       Скомпилировать ассемблерный файл в Intel HEX");
            Console.WriteLine("                            Параметры: [-o out.hex] [--listing]");
            Console.WriteLine("  disasm <файл.hex>         Дизассемблировать машинный код из файла");
            Console.WriteLine("  info                      Показать карту памяти и спецификацию КР580");
            Console.WriteLine("  help                      Вывести эту справку");
            Console.WriteLine();
            Console.WriteLine("Параметры команды run:");
            Console.WriteLine("  --profile legacy|newest   Выбор профиля процессора (по умолчанию: newest)");
            Console.WriteLine("  --steps <N>               Максимальное число шагов (по умолчанию: 100000)");
            Console.WriteLine("  --dump                    Вывести расширенный дамп памяти после останова");
            Console.WriteLine("  --trace                   Пошаговый вывод исполняемых инструкций");
            Console.WriteLine();
            Console.WriteLine("Примеры:");
            Console.WriteLine("  mikrolab lab 1            Запуск лабораторной работы №1 (Регистры РОН)");
            Console.WriteLine("  mikrolab lab 5            Запуск лабораторной работы №5 (Порты ВВ55)");
            Console.WriteLine("  mikrolab run program.asm  Компиляция и запуск пользовательской программы");
            Console.WriteLine();
        }

        private static void PrintInfo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=================================================================");
            Console.WriteLine(" Спецификация микропроцессорной системы КР580ВМ80А / Intel 8080");
            Console.WriteLine(" Методический практикум КФУ им. Вернадского (Дмитренко П.С.)");
            Console.WriteLine("=================================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Карта адресного пространства памяти:");
            Console.WriteLine("  0000h - 07FFh  ПЗУ монитора стенда (2 КБ)");
            Console.WriteLine("  0100h          Точка входа SEGGG (вывод на 7-сегментные индикаторы)");
            Console.WriteLine("  0300h          Точка входа Sound (генерация звукового сигнала)");
            Console.WriteLine("  0400h          Точка входа Delay (программная задержка)");
            Console.WriteLine("  8000h - 83C6h  ОЗУ пользователя (базовый адрес загрузки программ 8000h)");
            Console.WriteLine("  83E4h - 83EBh  Буфер регистров монитора при останове:");
            Console.WriteLine("                 83E4h=L, 83E5h=H, 83E6h=E, 83E7h=D, 83E8h=C, 83E9h=B, 83EAh=PSW, 83EBh=A");
            Console.WriteLine("  83F8h - 83FFh  Регистры 8-разрядного 7-сегментного дисплея");
            Console.WriteLine();
            Console.WriteLine("Порты ввода-вывода (микросхема КР580ВВ55А / i8255 PPI):");
            Console.WriteLine("  Порт FBh       Регистр Управляющего Слова (РУС). Например, 81h для лаб. 5");
            Console.WriteLine("  Порт FAh       Порт ввода состояния тумблеров (SA1..SA3)");
            Console.WriteLine("  Порт F9h       Порт вывода на светодиодные лампы (HL1..HL8)");
            Console.WriteLine();
            Console.WriteLine("Профили выполнения:");
            Console.WriteLine("  Legacy:        Лабораторные 1-4. Воспроизводит буфер регистров монитора и");
            Console.WriteLine("                 исторические особенности микрокода учебного стенда.");
            Console.WriteLine("  Newest:        Лабораторная 5+. Полный комплект из 256 инструкций i8080,");
            Console.WriteLine("                 такт 0.5 мкс (2.0 МГц), полная эмуляция ВВ55А PPI.");
            Console.WriteLine();
        }

        private static int RunLab(string[] args)
        {
            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Укажите номер лабораторной работы: mikrolab lab <1..5>");
                Console.ResetColor();
                return 1;
            }

            string labId = args[1];
            switch (labId)
            {
                case "1":
                    return ExecuteSample("Лабораторная работа №1 (Регистры РОН и LXI)", SamplePrograms.Lab1_Registers, CpuProfile.Legacy,
                        verify: VerifyLab1);
                case "2":
                    return ExecuteSample("Лабораторная работа №2 (Арифметика)", SamplePrograms.Lab2_Arithmetic, CpuProfile.Legacy,
                        verify: VerifyLab2);
                case "3":
                    return ExecuteSample("Лабораторная работа №3 (Логические операции)", SamplePrograms.Lab3_Logic, CpuProfile.Extended,
                        verify: VerifyLab3);
                case "4":
                    return ExecuteSample("Лабораторная работа №4 (Циклы и массив)", SamplePrograms.Lab4_Loops, CpuProfile.Extended,
                        verify: VerifyLab4);
                case "5":
                    return ExecuteSample("Лабораторная работа №5 (Тумблеры → Лампы ВВ55)", SamplePrograms.Lab5_PpiLamps, CpuProfile.Extended,
                        setupMachine: m =>
                        {
                            m.Ppi.Tumbler1 = true;
                            m.Ppi.Tumbler2 = false;
                            m.Ppi.Tumbler3 = true;
                        }, maxSteps: 50, verify: VerifyLab5, expectHalt: false);

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Неизвестный номер лабораторной: " + labId + ". Доступны: 1, 2, 3, 4, 5.");
                    Console.ResetColor();
                    return 1;
            }
        }

        private static int ExecuteSample(
            string title,
            string asmCode,
            CpuProfile profile,
            Action<Machine> setupMachine = null,
            int maxSteps = 100000,
            Func<Machine, string> verify = null,
            bool expectHalt = true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=================================================================");
            Console.WriteLine(" " + title);
            Console.WriteLine(" Профиль: " + (profile == CpuProfile.Legacy ? "Legacy (Лаб. 1-4)" : "Newest / Extended (Лаб. 5+)"));
            Console.WriteLine("=================================================================");
            Console.ResetColor();

            AssembleResult asmResult = I8080Assembler.Assemble(asmCode, 0x8000);
            if (!asmResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибка компиляции ассемблера:");
                foreach (var err in asmResult.Errors)
                {
                    Console.WriteLine("  Строка " + err.LineNumber + ": " + err.Message);
                }
                Console.ResetColor();
                return 1;
            }

            var machine = new Machine(null, null, null, profile);
            machine.Cpu.Reset();
            machine.Display.Reset();

            for (int i = 0x8000; i <= 0x83FF; i++)
            {
                machine.Memory.WriteByte((ushort)i, 0);
            }

            foreach (var chunk in asmResult.Chunks)
            {
                for (int i = 0; i < chunk.Data.Length; i++)
                {
                    machine.Memory.WriteByte((ushort)(chunk.Address + i), chunk.Data[i]);
                }
            }

            machine.Cpu.State.PC = asmResult.StartAddress;
            machine.Cpu.State.SP = 0x83FE;

            setupMachine?.Invoke(machine);

            int steps = ExecuteUntilHalt(machine, maxSteps, false);
            bool halted = machine.State == RunState.Halted;

            PrintMachineState(machine, steps);

            string verificationError = verify?.Invoke(machine);
            if (verificationError != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✘ Проверка результатов не пройдена: " + verificationError);
                Console.ResetColor();
                return 1;
            }

            if (expectHalt && !halted)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✘ Программа не завершилась за " + maxSteps + " шагов (команда HLT не встречена).");
                Console.ResetColor();
                return 1;
            }

            if (!expectHalt && !halted)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Программа работает в цикле; результаты проверены после " + steps + " шагов.");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔ Проверка результатов пройдена (" + steps + " шагов).");
            Console.ResetColor();
            return 0;
        }

        private static string VerifyLab1(Machine machine)
        {
            var s = machine.Cpu.State;
            bool ok = s.A == 0xFF && s.B == 0xFF && s.C == 0xFF && s.D == 0xFF
                      && s.E == 0xFF && s.H == 0xFF && s.L == 0xFF;
            if (ok) return null;

            return string.Format(
                "ожидалось A=B=C=D=E=H=L=FFh, получено A={0:X2}h B={1:X2}h C={2:X2}h D={3:X2}h E={4:X2}h H={5:X2}h L={6:X2}h",
                s.A, s.B, s.C, s.D, s.E, s.H, s.L);
        }

        private static string VerifyLab2(Machine machine)
        {
            byte result = machine.Memory.ReadByte(0x8201);
            if (machine.Cpu.State.A == 0xFF && result == 0xFF) return null;

            return string.Format("ожидалось A=FFh и [8201h]=FFh, получено A={0:X2}h и [8201h]={1:X2}h",
                machine.Cpu.State.A, result);
        }

        private static string VerifyLab3(Machine machine)
        {
            byte result = machine.Memory.ReadByte(0x8201);
            if (machine.Cpu.State.A == 0xC7 && result == 0xC7) return null;

            return string.Format("ожидалось A=C7h и [8201h]=C7h, получено A={0:X2}h и [8201h]={1:X2}h",
                machine.Cpu.State.A, result);
        }

        private static string VerifyLab4(Machine machine)
        {
            byte result = machine.Memory.ReadByte(0x8204);
            if (machine.Cpu.State.A == 0x3C && result == 0x3C) return null;

            return string.Format("ожидалось A=3Ch и [8204h]=3Ch, получено A={0:X2}h и [8204h]={1:X2}h",
                machine.Cpu.State.A, result);
        }

        private static string VerifyLab5(Machine machine)
        {
            if (machine.Ppi.ControlWord == 0x81 && machine.Ppi.Lamps == 0x0B) return null;

            return string.Format("ожидалось РУС=81h и лампы=0Bh, получено РУС={0:X2}h и лампы={1:X2}h",
                machine.Ppi.ControlWord, machine.Ppi.Lamps);
        }

        private static int RunProgram(string[] args)
        {
            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Укажите путь к файлу: mikrolab run <файл.asm|файл.hex>");
                Console.ResetColor();
                return 1;
            }

            string filePath = args[1];
            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Файл не найден: " + filePath);
                Console.ResetColor();
                return 1;
            }

            CpuProfile profile = CpuProfile.Extended;
            int maxSteps = 100000;
            bool dumpMemory = false;
            bool trace = false;

            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                if (arg == "--profile" && i + 1 < args.Length)
                {
                    profile = args[i + 1].ToLowerInvariant() == "legacy" ? CpuProfile.Legacy : CpuProfile.Extended;
                    i++;
                }
                else if (arg == "--steps" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1], out int parsedSteps) || parsedSteps <= 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Параметр --steps требует положительное целое число.");
                        Console.ResetColor();
                        return 1;
                    }
                    maxSteps = parsedSteps;
                    i++;
                }
                else if (arg == "--dump")
                {
                    dumpMemory = true;
                }
                else if (arg == "--trace")
                {
                    trace = true;
                }
            }

            var machine = new Machine(null, null, null, profile);
            machine.Cpu.Reset();
            ushort startAddress = 0x8000;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".hex")
            {
                string hexText = File.ReadAllText(filePath);
                IntelHex.Load(hexText, (addr, val) => machine.Memory.WriteByte((ushort)addr, val), out int minA, out _);
                startAddress = (ushort)minA;
            }
            else
            {
                string source = File.ReadAllText(filePath);
                AssembleResult result = I8080Assembler.Assemble(source, 0x8000);
                if (!result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Ошибка компиляции:");
                    foreach (var err in result.Errors)
                    {
                        Console.WriteLine("  Строка " + err.LineNumber + ": " + err.Message);
                    }
                    Console.ResetColor();
                    return 1;
                }

                foreach (var chunk in result.Chunks)
                {
                    for (int i = 0; i < chunk.Data.Length; i++)
                    {
                        machine.Memory.WriteByte((ushort)(chunk.Address + i), chunk.Data[i]);
                    }
                }
                startAddress = result.StartAddress;
            }

            machine.Cpu.State.PC = startAddress;
            machine.Cpu.State.SP = 0x83FE;

            Console.WriteLine("Запуск программы " + Path.GetFileName(filePath) + " с адреса " + startAddress.ToString("X4") + "h...");
            int steps = ExecuteUntilHalt(machine, maxSteps, trace);
            bool halted = machine.State == RunState.Halted;

            PrintMachineState(machine, steps);

            if (!halted)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Достигнут лимит " + maxSteps + " шагов: команда HLT не встречена.");
                Console.ResetColor();
            }

            if (dumpMemory)
            {
                PrintMemoryDump(machine, 0x8200, 32);
            }

            return 0;
        }

        private static int AssembleFile(string[] args)
        {
            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Укажите входной файл: mikrolab assemble <файл.asm> [-o out.hex]");
                Console.ResetColor();
                return 1;
            }

            string sourcePath = args[1];
            if (!File.Exists(sourcePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Файл не найден: " + sourcePath);
                Console.ResetColor();
                return 1;
            }

            string outputPath = null;
            bool showListing = false;
            for (int i = 2; i < args.Length; i++)
            {
                if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
                {
                    outputPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "--listing")
                {
                    showListing = true;
                }
            }

            string code = File.ReadAllText(sourcePath);
            AssembleResult result = I8080Assembler.Assemble(code, 0x8000);

            if (!result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Ошибок компиляции: " + result.Errors.Count);
                foreach (var err in result.Errors)
                {
                    Console.WriteLine("  Строка " + err.LineNumber + ": " + err.Message);
                }
                Console.ResetColor();
                return 1;
            }

            int totalBytes = 0;
            foreach (var chunk in result.Chunks) totalBytes += chunk.Data.Length;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Успешно собрано! Байт кода: {totalBytes}, символов: {result.Symbols.Count}");
            Console.ResetColor();

            if (result.Symbols.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Таблица символов:");
                foreach (var symbol in result.Symbols.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  {symbol.Key,-20} {symbol.Value:X4}h");
                }
            }

            if (showListing)
            {
                Console.WriteLine();
                Console.WriteLine("Листинг:");
                foreach (var line in result.Listing)
                {
                    string address = line.HasAddress ? line.Address.ToString("X4") + "h" : "     ";
                    string bytes = line.HasAddress ? string.Join(" ", line.Bytes.Select(b => b.ToString("X2"))) : string.Empty;
                    Console.WriteLine($"{line.LineNumber,4}  {address}  {bytes,-12}  {line.Source}");
                }
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                var sb = new StringBuilder();
                foreach (var chunk in result.Chunks)
                {
                    int offset = 0;
                    while (offset < chunk.Data.Length)
                    {
                        int len = Math.Min(16, chunk.Data.Length - offset);
                        byte checksum = (byte)(len + ((chunk.Address + offset) >> 8) + ((chunk.Address + offset) & 0xFF));
                        sb.Append($":{len:X2}{(chunk.Address + offset):X4}00");
                        for (int k = 0; k < len; k++)
                        {
                            byte b = chunk.Data[offset + k];
                            checksum += b;
                            sb.Append($"{b:X2}");
                        }
                        byte cs = (byte)((~checksum + 1) & 0xFF);
                        sb.AppendLine($"{cs:X2}");
                        offset += len;
                    }
                }
                sb.AppendLine(":00000001FF");
                File.WriteAllText(outputPath, sb.ToString());
                Console.WriteLine("Результат сохранен в файл: " + outputPath);
            }

            return 0;
        }

        private static int DisassembleFile(string[] args)
        {
            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Укажите файл Intel HEX: mikrolab disasm <файл.hex>");
                Console.ResetColor();
                return 1;
            }

            string path = args[1];
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Файл не найден: " + path);
                Console.ResetColor();
                return 1;
            }

            string hex = File.ReadAllText(path);
            byte[] ram = new byte[65536];
            IntelHex.Load(hex, (addr, val) => ram[addr] = val, out int minAddr, out int maxAddr);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Дизассемблирование {Path.GetFileName(path)} [{minAddr:X4}h..{maxAddr:X4}h]:");
            Console.ResetColor();

            ushort pc = (ushort)minAddr;
            while (pc <= maxAddr)
            {
                var dis = I8080Disassembler.Disassemble(ram, pc);
                string bytesStr = ram[pc].ToString("X2");
                if (dis.Length >= 2) bytesStr += " " + ram[(pc + 1) & 0xFFFF].ToString("X2");
                if (dis.Length >= 3) bytesStr += " " + ram[(pc + 2) & 0xFFFF].ToString("X2");

                Console.WriteLine($"{pc:X4}h:  {bytesStr,-9}  {dis.Mnemonic}");
                pc += (ushort)dis.Length;
            }

            return 0;
        }

        private static int ExecuteUntilHalt(Machine machine, int maxSteps, bool trace)
        {
            int steps = 0;
            byte[] snapshotBuffer = trace ? new byte[65536] : null;

            while (steps < maxSteps)
            {
                ushort pc = machine.Cpu.State.PC;
                if (machine.Memory.ReadByte(pc) == 0x76) // HLT
                {
                    machine.State = RunState.Halted;
                    break;
                }

                if (trace)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        int address = (pc + k) & 0xFFFF;
                        snapshotBuffer[address] = machine.Memory.ReadByte((ushort)address);
                    }
                    var dis = I8080Disassembler.Disassemble(snapshotBuffer, pc);
                    Console.WriteLine($"[{steps:D5}] {pc:X4}h: {dis.Mnemonic}");
                }

                bool ok = machine.Cpu.Step();
                steps++;
                if (!ok) break;
            }

            return steps;
        }

        private static void PrintMachineState(Machine machine, int steps)
        {
            var cpu = machine.Cpu.State;
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("-----------------------------------------------------------------");
            Console.WriteLine($" Состояние процессора (выполнено шагов: {steps}, тактов: {cpu.Cycles}):");
            Console.WriteLine("-----------------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine($"  A = {cpu.A:X2}h ({cpu.A,3})    BC = {cpu.B:X2}{cpu.C:X2}h (B={cpu.B:X2}h C={cpu.C:X2}h)");
            Console.WriteLine($"  DE = {cpu.D:X2}{cpu.E:X2}h (D={cpu.D:X2}h E={cpu.E:X2}h)    HL = {cpu.H:X2}{cpu.L:X2}h (H={cpu.H:X2}h L={cpu.L:X2}h)");
            Console.WriteLine($"  PC = {cpu.PC:X4}h           SP = {cpu.SP:X4}h");
            Console.WriteLine($"  Флаги: S={B(cpu.Flags.S)} Z={B(cpu.Flags.Z)} AC={B(cpu.Flags.AC)} P={B(cpu.Flags.P)} CY={B(cpu.Flags.C)}");
            Console.WriteLine($"  ВВ55:  Порт A (лампы) = {machine.Ppi.Lamps:X2}h, РУС = {machine.Ppi.ControlWord:X2}h");
            Console.WriteLine("-----------------------------------------------------------------");
        }

        private static void PrintMemoryDump(Machine machine, ushort start, int length)
        {
            Console.WriteLine($"Дамп памяти [{start:X4}h..{(start + length - 1):X4}h]:");
            for (int i = 0; i < length; i += 16)
            {
                ushort addr = (ushort)(start + i);
                Console.Write($"{addr:X4}h: ");
                for (int j = 0; j < 16 && (i + j) < length; j++)
                {
                    byte b = machine.Memory.ReadByte((ushort)(addr + j));
                    Console.Write($"{b:X2} ");
                }
                Console.WriteLine();
            }
        }

        private static int B(bool val) => val ? 1 : 0;
    }
}
