using System;
using System.Collections.Generic;
using System.IO;

namespace Emulator
{
    public class IntelHexRecord
    {
        public int LineNumber { get; set; }
        public int ByteCount { get; set; }
        public int Address { get; set; }
        public int RecordType { get; set; }
        public byte[] Data { get; set; }
        public byte Checksum { get; set; }
    }

    public static class IntelHex
    {
        /// <summary>
        /// Parses an Intel HEX string (multi-line CRLF/LF or concatenated single-line format).
        /// Returns a list of parsed valid records.
        /// Throws FormatException with line number and details on error.
        /// </summary>
        public static List<IntelHexRecord> Parse(string text, bool verifyChecksum = true)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var records = new List<IntelHexRecord>();
            int index = 0;
            int length = text.Length;
            int lineNumber = 1;

            while (index < length)
            {
                // Advance past any whitespace or newline
                while (index < length && text[index] != ':')
                {
                    if (text[index] == '\n')
                        lineNumber++;
                    index++;
                }

                if (index >= length)
                    break;

                int recordStartIndex = index;
                int currentLine = lineNumber;
                index++; // Skip ':'

                // Need at least 8 hex chars: LL (2) + AAAA (4) + TT (2)
                if (index + 8 > length)
                    throw new FormatException($"Intel HEX error at line {currentLine}: truncated record header after ':'");

                int byteCount = ParseHexByte(text, ref index, currentLine, "byte count");
                int addrHigh = ParseHexByte(text, ref index, currentLine, "address high");
                int addrLow = ParseHexByte(text, ref index, currentLine, "address low");
                int address = (addrHigh << 8) | addrLow;
                int recordType = ParseHexByte(text, ref index, currentLine, "record type");

                // Data bytes (byteCount * 2) + Checksum (2)
                if (index + (byteCount * 2) + 2 > length)
                    throw new FormatException($"Intel HEX error at line {currentLine}: record specifies {byteCount} bytes, but file is truncated.");

                byte[] data = new byte[byteCount];
                int calculatedSum = byteCount + addrHigh + addrLow + recordType;

                for (int i = 0; i < byteCount; i++)
                {
                    byte b = (byte)ParseHexByte(text, ref index, currentLine, $"data byte {i}");
                    data[i] = b;
                    calculatedSum += b;
                }

                byte checksum = (byte)ParseHexByte(text, ref index, currentLine, "checksum");
                calculatedSum += checksum;

                if (verifyChecksum && (calculatedSum & 0xFF) != 0)
                {
                    byte expected = (byte)((-(calculatedSum - checksum)) & 0xFF);
                    throw new FormatException($"Intel HEX checksum error at line {currentLine}: calculated checksum {expected:X2}, got {checksum:X2}");
                }

                records.Add(new IntelHexRecord
                {
                    LineNumber = currentLine,
                    ByteCount = byteCount,
                    Address = address,
                    RecordType = recordType,
                    Data = data,
                    Checksum = checksum
                });

                if (recordType == 0x01) // EOF
                {
                    // EOF reached. Any subsequent records or trailing chars are handled on next loop iteration.
                }
            }

            return records;
        }

        private static int ParseHexByte(string s, ref int index, int line, string fieldName)
        {
            if (index + 2 > s.Length)
                throw new FormatException($"Intel HEX error at line {line}: unexpected end of input while reading {fieldName}");

            int hi = HexDigit(s[index++], line, fieldName);
            int lo = HexDigit(s[index++], line, fieldName);
            return (hi << 4) | lo;
        }

        private static int HexDigit(char c, int line, string fieldName)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            throw new FormatException($"Intel HEX error at line {line}: invalid hex character '{c}' in {fieldName}");
        }

        /// <summary>
        /// Loads parsed records into an address-to-byte action.
        /// Handles record types 00 (data), 01 (EOF), 02 (extended segment), 04 (extended linear).
        /// </summary>
        public static int Load(string hexContent, Action<int, byte> writeByte, out int minAddress, out int maxAddress, bool verifyChecksum = true)
        {
            var records = Parse(hexContent, verifyChecksum);
            int totalBytesLoaded = 0;
            int baseAddress = 0;
            minAddress = int.MaxValue;
            maxAddress = int.MinValue;

            foreach (var rec in records)
            {
                switch (rec.RecordType)
                {
                    case 0x00: // Data
                        int targetAddr = baseAddress + rec.Address;
                        for (int i = 0; i < rec.Data.Length; i++)
                        {
                            int addr = targetAddr + i;
                            if (addr < 0 || addr > 0xFFFF)
                                throw new FormatException($"Intel HEX error at line {rec.LineNumber}: address 0x{addr:X} exceeds 64KB boundary (0xFFFF)");

                            writeByte(addr, rec.Data[i]);
                            totalBytesLoaded++;

                            if (addr < minAddress) minAddress = addr;
                            if (addr > maxAddress) maxAddress = addr;
                        }
                        break;

                    case 0x01: // EOF
                        break;

                    case 0x02: // Extended Segment Address: base = data << 4
                        if (rec.Data.Length != 2)
                            throw new FormatException($"Intel HEX error at line {rec.LineNumber}: type 02 record must have 2 data bytes, got {rec.Data.Length}");
                        baseAddress = ((rec.Data[0] << 8) | rec.Data[1]) << 4;
                        break;

                    case 0x04: // Extended Linear Address: base = data << 16
                        if (rec.Data.Length != 2)
                            throw new FormatException($"Intel HEX error at line {rec.LineNumber}: type 04 record must have 2 data bytes, got {rec.Data.Length}");
                        baseAddress = ((rec.Data[0] << 8) | rec.Data[1]) << 16;
                        break;

                    case 0x03: // Start Segment Address (ignored for simple 8080 loader)
                    case 0x05: // Start Linear Address (ignored for simple 8080 loader)
                        break;

                    default:
                        throw new FormatException($"Intel HEX error at line {rec.LineNumber}: unsupported record type {rec.RecordType:X2}");
                }
            }

            if (totalBytesLoaded == 0)
            {
                minAddress = 0;
                maxAddress = 0;
            }

            return totalBytesLoaded;
        }

        /// <summary>
        /// Loads hex content into legacy CMemory instance.
        /// </summary>
        public static int LoadToMemory(string hexContent, CMemory memory, bool verifyChecksum = true)
        {
            if (memory == null)
                throw new ArgumentNullException(nameof(memory));

            return Load(hexContent, (addr, b) =>
            {
                int highNibble = (b >> 4) & 0x0F;
                int lowNibble = b & 0x0F;
                memory.setMemoryValue(addr, highNibble, lowNibble);
            }, out _, out _, verifyChecksum);
        }
    }
}
