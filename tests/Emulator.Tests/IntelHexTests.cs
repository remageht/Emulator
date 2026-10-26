using System;
using System.IO;
using Emulator;

namespace Emulator.Tests
{
    public static class IntelHexTests
    {
        public static void RunAll()
        {
            Console.WriteLine("[TEST SUITE] IntelHexTests");
            Test_Parse_RepositoryArrinxHex_SingleLine();
            Test_Parse_StandardMultiLineCrlf();
            Test_Parse_StandardMultiLineLf();
            Test_Parse_InvalidChecksum_Throws();
            Test_Parse_RecordType01_Eof();
            Test_Parse_RecordTypes02And04_ExtendedAddressing();
            Test_Load_AddressOutOfRange_Throws();
            Console.WriteLine("  All IntelHexTests PASSED.\n");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception("Assertion failed: " + message);
        }

        public static void Test_Parse_RepositoryArrinxHex_SingleLine()
        {
            // The exact line from hex_to_csv/arrinx.hex
            string content = ":0F8300002100823EFF06FF3C7723B8DA07837621:0000000000";
            var records = IntelHex.Parse(content);

            Assert(records.Count == 2, $"Expected 2 records, got {records.Count}");
            Assert(records[0].RecordType == 0x00, "Record 0 should be data (00)");
            Assert(records[0].Address == 0x8300, $"Expected address 0x8300, got 0x{records[0].Address:X4}");
            Assert(records[0].ByteCount == 15, $"Expected 15 bytes, got {records[0].ByteCount}");
            Assert(records[0].Data[0] == 0x21, "Byte 0 should be 0x21 (LXI H)");

            Assert(records[1].RecordType == 0x00, "Record 1 type");
            Assert(records[1].ByteCount == 0, "Record 1 byte count should be 0");

            byte[] mem = new byte[65536];
            int loaded = IntelHex.Load(content, (addr, val) => mem[addr] = val, out int minA, out int maxA, verifyChecksum: true);
            Assert(loaded == 15, $"Expected 15 bytes loaded, got {loaded}");
            Assert(minA == 0x8300 && maxA == 0x830E, $"Address range mismatch: 0x{minA:X4} - 0x{maxA:X4}");
            Console.WriteLine("  [PASS] Test_Parse_RepositoryArrinxHex_SingleLine");
        }

        public static void Test_Parse_StandardMultiLineCrlf()
        {
            // Standard multi-line Intel HEX with \r\n (previously failed in original But_VVOD_Click)
            string hex =
                ":048000003E013E02FD\r\n" +
                ":048004008032008145\r\n" +
                ":00000001FF\r\n";

            var records = IntelHex.Parse(hex);
            Assert(records.Count == 3, $"Expected 3 records, got {records.Count}");
            Assert(records[0].ByteCount == 4 && records[0].Address == 0x8000, "Record 0");
            Assert(records[1].ByteCount == 4 && records[1].Address == 0x8004, "Record 1");
            Assert(records[2].RecordType == 0x01, "Record 2 should be EOF");

            byte[] mem = new byte[65536];
            int count = IntelHex.Load(hex, (addr, val) => mem[addr] = val, out int minA, out int maxA);
            Assert(count == 8, $"Expected 8 bytes loaded, got {count}");
            Assert(mem[0x8000] == 0x3E && mem[0x8001] == 0x01, "Byte check record 0");
            Assert(mem[0x8004] == 0x80 && mem[0x8005] == 0x32, "Byte check record 1");
            Console.WriteLine("  [PASS] Test_Parse_StandardMultiLineCrlf");
        }

        public static void Test_Parse_StandardMultiLineLf()
        {
            // Standard multi-line with \n
            string hex =
                ":048000003E013E02FD\n" +
                ":048004008032008145\n" +
                ":00000001FF\n";

            var records = IntelHex.Parse(hex);
            Assert(records.Count == 3, $"Expected 3 records, got {records.Count}");

            byte[] mem = new byte[65536];
            int count = IntelHex.Load(hex, (addr, val) => mem[addr] = val, out _, out _);
            Assert(count == 8, $"Expected 8 bytes loaded, got {count}");
            Console.WriteLine("  [PASS] Test_Parse_StandardMultiLineLf");
        }

        public static void Test_Parse_InvalidChecksum_Throws()
        {
            // Valid line 1, corrupt checksum in line 2: 45 changed to 00
            string badHex =
                ":048000003E013E02FD\r\n" +
                ":048004008032008100\r\n" +
                ":00000001FF\r\n";

            bool threw = false;
            try
            {
                IntelHex.Parse(badHex, verifyChecksum: true);
            }
            catch (FormatException ex)
            {
                threw = true;
                Assert(ex.Message.Contains("line 2"), $"Error message should mention line 2, got: {ex.Message}");
            }
            Assert(threw, "Parser should throw on invalid checksum");
            Console.WriteLine("  [PASS] Test_Parse_InvalidChecksum_Throws");
        }

        public static void Test_Parse_RecordType01_Eof()
        {
            string hex = ":00000001FF\r\n";
            var records = IntelHex.Parse(hex);
            Assert(records.Count == 1, "Expected 1 record");
            Assert(records[0].RecordType == 1, "Record type 1");
            Assert(records[0].ByteCount == 0, "Byte count 0");
            Console.WriteLine("  [PASS] Test_Parse_RecordType01_Eof");
        }

        public static void Test_Parse_RecordTypes02And04_ExtendedAddressing()
        {
            // Type 02: Extended Segment Address (0x1000 -> base = 0x1000 << 4 = 0x10000)
            // Type 04: Extended Linear Address (0x0001 -> base = 0x0001 << 16 = 0x10000)
            string hex02 = ":020000020100FB\r\n:01000000FF00\r\n"; // base = 0x0100 << 4 = 0x1000
            var records02 = IntelHex.Parse(hex02);
            Assert(records02[0].RecordType == 0x02, "Record type 02");

            int writtenAddr = -1;
            IntelHex.Load(hex02, (addr, val) => writtenAddr = addr, out _, out _);
            Assert(writtenAddr == 0x1000, $"Expected addr 0x1000, got 0x{writtenAddr:X4}");
            Console.WriteLine("  [PASS] Test_Parse_RecordTypes02And04_ExtendedAddressing");
        }

        public static void Test_Load_AddressOutOfRange_Throws()
        {
            // Address exceeds 64KB (0x10000) via type 04
            string hex = ":020000040001F9\r\n:01000000FF00\r\n";
            bool threw = false;
            try
            {
                IntelHex.Load(hex, (addr, val) => { }, out _, out _);
            }
            catch (FormatException ex)
            {
                threw = true;
                Assert(ex.Message.Contains("exceeds 64KB"), $"Expected message about 64KB, got: {ex.Message}");
            }
            Assert(threw, "Load should throw when address exceeds 64KB");
            Console.WriteLine("  [PASS] Test_Load_AddressOutOfRange_Throws");
        }
    }
}
