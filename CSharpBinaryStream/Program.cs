using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CSharpBinaryStream
{
    public class BinaryStream
    {
        private byte[] buffer;
        private int writingPos;
        private int readingPos;
        private bool isBigEndian;

        public static Dictionary<int, Dictionary<int, int[]>> BitShiftMapTable = new Dictionary<int, Dictionary<int, int[]>>
        {
            {16, new Dictionary<int, int[]> { {0, new int[] {8, 0}}, {1, new int[] {0, 8}} }},
            {24, new Dictionary<int, int[]> { {0, new int[] {16, 8, 0}}, {1, new int[] {0, 8, 16}} }},
            {32, new Dictionary<int, int[]> { {0, new int[] {24, 16, 8, 0}}, {1, new int[] {0, 8, 16, 24}} }}
        };

        public static Dictionary<int, Dictionary<int, int[]>> LimitsTable = new Dictionary<int, Dictionary<int, int[]>>
        {
            {8, new Dictionary<int, int[]> { {0, new int[] {0x00, 0xff}}, {1, new int[] {-(0x80), 0x7f}} }},
            {16, new Dictionary<int, int[]> { {0, new int[] {0x00, 0xffff}}, {1, new int[] {-(0x8000), 0x7fff}} }},
            {24, new Dictionary<int, int[]> { {0, new int[] {0x00, 0xffffff}}, {1, new int[] {-(0x800000), 0x7fffff}} }},
            {32, new Dictionary<int, int[]> { {0, new int[] {0x00, 0xfffffff}}, {1, new int[] {-(0x8000000), 0x7fffffff}} }}
        };

        public static int signInt(int bitSize, int value)
        {
            if (bitSize == 8)
            {
                return (value + (1 << 7)) % (1 << 8) - (1 << 7);
            }
            return value < (1 << (bitSize - 1)) ? value : value - (1 << bitSize);
        }

        public static int limit(int value, int bitSize, bool signed)
        {
            int retValue = value;
            Dictionary<int, int[]> limitTable = LimitsTable[bitSize];
            if (!signed)
            {
                if (retValue < limitTable[0][0])
                {
                    retValue = limitTable[0][0];
                }
                else if (retValue > limitTable[0][1])
                {
                    retValue &= limitTable[0][1];
                }
            }
            else
            {
                retValue = signInt(bitSize, retValue);
            }
            return retValue;
        }

        public static void writeValueIntoBytes(byte[] bytes, int bitSize, bool bigEndian, int value)
        {
            Dictionary<int, int[]> shiftMap = BitShiftMapTable[bitSize];
            int[] byteArray = bigEndian ? shiftMap[0] : shiftMap[1];
            int i = 0;
            foreach (int v in byteArray)
            {
                bytes[i] = (byte)(value >> v);
                i++;
            }
        }

        public static int readValueFromBytes(byte[] bytes, int bitSize, bool bigEndian)
        {
            Dictionary<int, int[]> shiftMap = BitShiftMapTable[bitSize];
            int[] byteArray = bigEndian ? shiftMap[0] : shiftMap[1];
            int value = 0;
            int i = 0;
            foreach (int v in byteArray)
            {
                value |= bytes[i] << v;
                i++;
            }
            return value;
        }

        public BinaryStream(byte[] buffer, int writingPos, int readingPos, bool bigEndain)
        {
            this.buffer = buffer;
            this.writingPos = writingPos;
            this.readingPos = readingPos;
            this.isBigEndian = bigEndain;
        }

        public static BinaryStream allocate(int size, bool bigEndain)
        {
            return new BinaryStream(new byte[size], 0, 0, bigEndain);
        }

        public void write(byte[] value)
        {

            int bytesSize = value.Length;
            Array.Copy(value, 0, buffer, writingPos, bytesSize);
            writingPos += bytesSize;
        }

        public byte[] read(int size)
        {
            readingPos += size;
            byte[] subArray = new byte[size];
            Array.Copy(buffer, readingPos - size, subArray, 0, size);
            return subArray;
        }

        public bool eos()
        {
            return readingPos >= buffer.Length;
        }

        public void rewind()
        {
            writingPos = 0;
            readingPos = 0;
        }

        public void reset()
        {
            buffer = new byte[0];
            writingPos = 0;
            readingPos = 0;
        }

        public void swapEndian()
        {
            isBigEndian = !isBigEndian;
        }

        public void writeInt8(int value, bool signed)
        {
            byte[] temp = new byte[] { (byte)limit(value, 8, signed) };
            write(temp);
        }

        public void writeBool(bool value)
        {
            writeInt8(value ? 1 : 0, false);
        }

        public void writeInt16(int value, bool signed)
        {
            byte[] temp = new byte[2];
            value = limit(value, 16, signed);
            writeValueIntoBytes(temp, 16, isBigEndian, value);
            write(temp);
        }

        public void writeInt24(int value, bool signed)
        {
            byte[] temp = new byte[3];
            value = limit(value, 24, signed);
            writeValueIntoBytes(temp, 24, isBigEndian, value);
            write(temp);
        }

        public void writeInt32(int value, bool signed)
        {
            byte[] temp = new byte[4];
            value = limit(value, 32, signed);
            writeValueIntoBytes(temp, 32, isBigEndian, value);
            write(temp);
        }

        public void writeInt64(long value, bool signed)
        {
            writeInt32((int)(value >> 32), signed);
            writeInt32((int)(value & 0xFFFFFFFF), signed);
        }

        public void writeFloat(float value)
        {
            writeInt32(BitConverter.ToInt32(BitConverter.GetBytes(value), 0), true);
        }

        public void writeDouble(double value)
        {
            writeInt64(BitConverter.DoubleToInt64Bits(value), true);
        }

        public void writeVarInt(int value)
        {
            for (int i = 0; i < 5; i++)
            {
                int toWrite = value & 0x7F;
                value >>= 7;
                if (value != 0)
                {
                    writeInt8(toWrite | 0x80, false);
                }
                else
                {
                    writeInt8(toWrite, false);
                    break;
                }
            }
        }

        public void writeVarLong(long value)
        {
            writeVarInt((int)(value >> 32));
            writeVarInt((int)(value & 0xFFFFFFFF));
        }

        public void writeZigZag32(int value)
        {
            writeVarInt((value << 1) ^ (value >> 31));
        }

        public void writeZigZag64(long value)
        {
            writeZigZag32((int)(value >> 32));
            writeZigZag32((int)(value & 0xFFFFFFFF));
        }

        public bool readBool()
        {
            return readInt16(false) == 1;
        }

        public int readInt16(bool signed)
        {
            int value = readValueFromBytes(read(2), 16, isBigEndian);
            return limit(value, 16, signed);
        }

        public int readInt24(bool signed)
        {
            int value = readValueFromBytes(read(3), 24, isBigEndian);
            return limit(value, 24, signed);
        }

        public int readInt32(bool signed)
        {
            int value = readValueFromBytes(read(4), 32, isBigEndian);
            return limit(value, 32, signed);
        }

        public long readInt64()
        {
            long high = readInt32(false);
            long low = readInt32(false);
            return (high << 32) | (low & 0xFFFFFFFF);
        }

        public float readFloat()
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(readInt32(true)), 0);
        }

        public double readDouble()
        {
            return BitConverter.Int64BitsToDouble(readInt64());
        }

        public byte[] readRemaining()
        {
            return read(buffer.Length - readingPos);
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            BinaryStream binaryStream = BinaryStream.allocate(20, true);

            // Write some sample data to the BinaryStream
            binaryStream.writeInt8(42, false);
            binaryStream.writeBool(true);
            binaryStream.writeFloat(3.14f);

            // Read back and print the sample data
            Console.WriteLine("Int16 Value: " + binaryStream.readInt16(false));
            Console.WriteLine("Bool Value: " + binaryStream.readBool());
            Console.WriteLine("Float Value: " + binaryStream.readFloat());
        }
    }
}