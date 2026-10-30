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

        public SampleProgram(string name, string code)
        {
            Name = name ?? string.Empty;
            Code = code ?? string.Empty;
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
            new SampleProgram("Біжучі вогні", RunningLight),
            new SampleProgram("Сума 1…5", SumOneToFive),
            new SampleProgram("Перемикачі → лампи", TumblersToLamps),
            new SampleProgram("Регістри (лаб. 1)", RegisterLab)
        };

        public static string Default => All[0].Code;

        public const string NewTemplate =
@"; MikroLab Studio · новий проєкт
; ORG задає адресу завантаження програми.

        ORG 8000h

START:  MVI A, 00h
        OUT 01h
        HLT

        END
";

        public const string RunningLight =
@"; MikroLab Studio · приклад: біжучі вогні
; Лампи підключені до порту 01h (ВВ55, порт A)
;
; F7 — зібрати, F5 — пуск, F6 — пауза, F10 — крок,
; F9 — точка зупинки, Ctrl+R — скидання.

        ORG 8000h

START:  MVI A, 01h        ; початковий вогник
        MVI B, 08h        ; вісім позицій

SHIFT:  OUT 01h           ; вивести A на лампи
        CALL DELAY        ; невелика затримка
        RLC               ; зсунути вогник ліворуч
        DCR B
        JNZ SHIFT
        JMP START         ; нескінченний цикл

; Порожній цикл затримки, ~2–5 мс
DELAY:  MVI C, 0C0h
DLOOP:  DCR C
        JNZ DLOOP
        RET

        END
";

        public const string SumOneToFive =
@"; MikroLab Studio · приклад: сума 1+2+3+4+5
; Результат (0Fh) записується в пам'ять 8200h
; і виводиться на лампи порту 01h.

        ORG 8000h

        MVI A, 00h        ; акумулятор = 0
        MVI B, 05h        ; доданок = 5

LOOP:   ADD B             ; A = A + B
        DCR B             ; B = B - 1
        JNZ LOOP          ; повторювати, доки B != 0

        STA 8200h         ; зберегти суму в пам'ять
        OUT 01h           ; показати суму на лампах
        HLT               ; зупинити процесор

        END
";

        public const string TumblersToLamps =
@"; MikroLab Studio · приклад: перемикачі → лампи
; IN 0FAh читає стан трьох перемикачів,
; OUT 01h виводить його на світлодіоди.
;
; Перемикачі керуються на вкладці «Пристрої».

        ORG 8000h

LOOP:   IN 0FAh           ; SA1..SA3 → A
        OUT 01h           ; A → лампи
        JMP LOOP

        END
";

        public const string RegisterLab =
@"; MikroLab Studio · лабораторна 1: регістри
; Завантаження регістрів і пересилання даних.

        ORG 8000h

        MVI A, 11h
        MVI B, 22h
        MVI C, 33h
        MVI D, 44h
        MVI E, 55h
        MVI H, 80h
        MVI L, 00h
        MOV M, A          ; 8000h ← A

        MVI A, 00h
        MOV A, B
        MOV B, C
        MOV C, D
        MOV D, E

        STA 8201h         ; зберегти A
        MOV A, M          ; A ← 8000h
        STA 8202h
        HLT

        END
";
    }
}
