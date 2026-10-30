using System;

namespace Emulator.Core.Assembler
{
    /// <summary>
    /// A named assembler example that can be loaded by any host.
    /// </summary>
    public sealed class SampleProgram
    {
        public string Name { get; }
        public string Code { get; }
        public Cpu.CpuProfile RecommendedProfile { get; }

        public SampleProgram(string name, string code, Cpu.CpuProfile recommendedProfile = Cpu.CpuProfile.Extended)
        {
            Name = name ?? string.Empty;
            Code = code ?? string.Empty;
            RecommendedProfile = recommendedProfile;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Ready-to-run programs shared by the Studio UI, tests and automation.
    /// All examples start at 8000h and only use documented 8080 instructions.
    /// </summary>
    public static class SamplePrograms
    {
        public static readonly SampleProgram[] All =
        {
            new SampleProgram("Лаб. 1 · Регистры РОН (Legacy)", Lab1_Registers, Cpu.CpuProfile.Legacy),
            new SampleProgram("Лаб. 2 · Арифметика (Legacy)", Lab2_Arithmetic, Cpu.CpuProfile.Legacy),
            new SampleProgram("Лаб. 3 · Логика и CMA (Newest)", Lab3_Logic, Cpu.CpuProfile.Extended),
            new SampleProgram("Лаб. 4 · Циклы и массив (Newest)", Lab4_Loops, Cpu.CpuProfile.Extended),
            new SampleProgram("Лаб. 5 · Тумблеры → Лампы (Newest)", Lab5_PpiLamps, Cpu.CpuProfile.Extended),
            new SampleProgram("Лаб. 5 · Бегущие огни (Newest)", RunningLight, Cpu.CpuProfile.Extended),
            new SampleProgram("Пример · Сумма 1…5", SumOneToFive, Cpu.CpuProfile.Extended)
        };

        public static string Default => All[0].Code;

        public const string NewTemplate =
@"; MikroLab Studio · новый проект
; ORG задает адрес загрузки программы в память.

        ORG 8000h

START:  MVI A, 00h
        OUT 01h
        HLT

        END
";

        public const string RunningLight =
@"; MikroLab Studio · пример: бегущие огни
; Лампы подключены к порту 01h (ВВ55, порт A)
;
; F7 — собрать, F5 — пуск, F6 — пауза, F10 — шаг,
; F9 — точка останова, Ctrl+R — сброс.

        ORG 8000h

START:  MVI A, 01h        ; начальный огонек
        MVI B, 08h        ; восемь позиций

SHIFT:  OUT 01h           ; вывести A на лампы
        CALL DELAY        ; небольшая задержка
        RLC               ; сдвиг огонька влево
        DCR B
        JNZ SHIFT
        JMP START         ; бесконечный цикл

; Цикл задержки, ~2–5 мс
DELAY:  MVI C, 0C0h
DLOOP:  DCR C
        JNZ DLOOP
        RET

        END
";

        public const string SumOneToFive =
@"; MikroLab Studio · пример: сумма 1+2+3+4+5
; Результат (0Fh) сохраняется в память 8200h
; и выводится на лампы порта 01h.

        ORG 8000h

        MVI A, 00h        ; аккумулятор = 0
        MVI B, 05h        ; слагаемое = 5

LOOP:   ADD B             ; A = A + B
        DCR B             ; B = B - 1
        JNZ LOOP          ; повторять, пока B != 0

        STA 8200h         ; сохранить сумму в память
        OUT 01h           ; показать сумму на лампах
        HLT               ; остановка процессора

        END
";

        public const string TumblersToLamps =
@"; MikroLab Studio · пример: тумблеры → лампы
; IN 0FAh считывает состояние тумблеров,
; OUT 01h выводит его на светодиоды.
;
; Тумблеры переключаются на вкладке «Устройства».

        ORG 8000h

LOOP:   IN 0FAh           ; SA1..SA3 → A
        OUT 01h           ; A → лампы
        JMP LOOP

        END
";

        public const string Lab1_Registers =
@"; Лабораторная работа №1 (Дмитренко П.С., КФУ, стр. 10)
; Загрузка РОН, регистровых пар LXI и пересылка MOV.
; Рекомендуемый профиль: Legacy (Лаб. 1–4)

        ORG 8000h

        ; Таблица 1: Загрузка РОН константами
        MVI A, 00h
        MVI B, 01h
        MVI C, 02h
        MVI D, 03h
        MVI E, 04h
        MVI H, 05h
        MVI L, 06h

        ; Таблица 2: Загрузка регистровых пар
        LXI B, 3132h
        LXI D, 3334h
        LXI H, 3536h

        ; Таблица 3: Цепочка пересылок через аккумулятор
        MVI A, 0FFh
        MOV B, A
        MOV C, B
        MOV D, C
        MOV E, D
        MOV H, E
        MOV L, H
        HLT

        END
";

        public const string Lab2_Arithmetic =
@"; Лабораторная работа №2 (Дмитренко П.С., КФУ, стр. 14)
; Арифметические операции: (A) = (A) + (B) + (M) + 1 = FFh
; Рекомендуемый профиль: Legacy (Лаб. 1–4)

        ORG 8000h

        MVI A, 45h        ; A = 45h
        MVI B, 0ABh       ; B = ABh
        LXI H, 8200h      ; ячейка памяти M (8200h)
        MVI M, 0Eh        ; [8200h] = 0Eh

        ADD B             ; A = 45h + ABh = F0h
        ADD M             ; A = F0h + 0Eh = FEh
        INR A             ; A = FEh + 1   = FFh
        STA 8201h         ; сохранить результат FFh
        HLT

        END
";

        public const string Lab3_Logic =
@"; Лабораторная работа №3 (Дмитренко П.С., КФУ, стр. 18-19)
; Логические операции: A = NOT(((A v B v M) * C) ^ D)
; Рекомендуемый профиль: Newest (требуется команда CMA)

        ORG 8000h

        MVI A, 25h        ; A = 25h
        MVI B, 3Ah        ; B = 3Ah
        MVI C, 2Eh        ; C = 2Eh
        MVI D, 16h        ; D = 16h
        LXI H, 8200h      ; указатель M (8200h)
        MVI M, 28h        ; [8200h] = 28h

        ORA B             ; A = 25h OR 3Ah = 3Fh
        ORA M             ; A = 3Fh OR 28h = 3Fh
        ANA C             ; A = 3Fh AND 2Eh = 2Eh
        XRA D             ; A = 2Eh XOR 16h = 38h
        CMA               ; A = NOT(38h) = C7h
        STA 8201h         ; сохранить результат
        HLT

        END
";

        public const string Lab4_Loops =
@"; Лабораторная работа №4 (Дмитренко П.С., КФУ, стр. 25)
; Циклы: вычисление суммы элементов массива в памяти
; Рекомендуемый профиль: Newest (требуются команды LDA/STA)

        ORG 8000h

        ; Инициализация тестового массива из 3 элементов: 10, 20, 30
        LXI H, 8200h
        MVI M, 03h        ; длина массива N = 3
        INX H
        MVI M, 0Ah        ; элемент 1: 10
        INX H
        MVI M, 14h        ; элемент 2: 20
        INX H
        MVI M, 1Eh        ; элемент 3: 30

        ; Алгоритм суммирования по таблице 1 (стр. 25)
        LDA 8200h         ; загрузить длину ряда чисел в АКМ
        MOV B, A          ; загрузить длину в счетчик B
        SUB A             ; A = 0
        LXI H, 8201h      ; указатель на первый элемент

M2:     ADD M             ; прибавить элемент массива к A
        DCR B             ; уменьшить счетчик
        JZ M1             ; если все элементы сложены — выход
        INX H             ; перейти к следующему элементу
        JMP M2            ; повторить цикл

M1:     STA 8204h         ; сохранить итоговую сумму (3Ch = 60)
        HLT

        END
";

        public const string Lab5_PpiLamps =
@"; Лабораторная работа №5 (Дмитренко П.С., КФУ, стр. 31)
; Взаимодействие с портами ввода-вывода (КР580ВВ55А / i8255 PPI)
; Рекомендуемый профиль: Newest (Лаб. 5+)
;
; Порт FBh — РУС (Регистр Управляющего Слова ВВ55): 81h
; Порт FAh — Порт ввода тумблеров (SA1..SA3)
; Порт F9h — Порт вывода на светодиодные индикаторы (HL1..HL8)

        ORG 8000h

        MVI A, 81h        ; управляющее слово ВВ55:
                          ; Порт A=вывод, B=вывод, C_младший=ввод
        OUT 0FBh          ; записать РУС в ВВ55

START:  IN 0FAh           ; считать состояние тумблеров в аккумулятор
        OUT 0F9h          ; вывести состояние на светодиоды
        JMP START         ; непрерывный цикл обновления

        END
";

        public const string RegisterLab = Lab1_Registers;
    }
}
