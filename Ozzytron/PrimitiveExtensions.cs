using System;
using System.Collections.Generic;
using System.Text;

namespace Ozzytron
{
    public static class PrimitiveExtensions
    {
        public static byte ToByte(this ushort someNumber)
        {
            return (byte)(someNumber & 0x00FF);
        }
        public static byte ToByte(this short someNumber)
        {
            return (byte)(someNumber & 0x00FF);
        }
        public static byte ToByte(this uint someNumber)
        {
            return (byte)(someNumber & 0x00FF);
        }
        public static byte ToByte(this int someNumber)
        {
            return (byte)(someNumber & 0x00FF);
        }
        public static byte ToByte(this long someNumber)
        {
            return (byte)(someNumber & 0x00FF);
        }

        public static bool IsZero(this byte theByte)
        {
            return theByte == 0x00;
        }

        public static bool IsNegative(this byte theByte)
        {
            return (theByte & 0x80) != 0;
        }

        public static bool IsNegative(this ushort someNumber)
        {
            return (someNumber & 0x80) != 0;
        }

        public static bool IsNegative(this int someNumber)
        {
            return (someNumber & 0x80) != 0;
        }

        public static bool IsBitOn(this byte theByte, byte theBit)
        {
            return (theByte & theBit) > 0x00;
        }

        public static byte SetBit(this byte theByte, byte theBit, bool on)
        {
            return on
                ? (byte)(theByte | theBit)
                : (byte)(theByte & (byte)~theBit);
        }

        public static bool IsBit0On(this byte theByte)
        {
            return theByte.IsBitOn(0b00000001);
        }
        public static bool IsBit1On(this byte theByte)
        {
            return theByte.IsBitOn(0b00000010);
        }
        public static bool IsBit2On(this byte theByte)
        {
            return theByte.IsBitOn(0b00000100);
        }
        public static bool IsBit3On(this byte theByte)
        {
            return theByte.IsBitOn(0b00001000);
        }
        public static bool IsBit4On(this byte theByte)
        {
            return theByte.IsBitOn(0b00010000);
        }
        public static bool IsBit5On(this byte theByte)
        {
            return theByte.IsBitOn(0b00100000);
        }
        public static bool IsBit6On(this byte theByte)
        {
            return theByte.IsBitOn(0b01000000);
        }
        public static bool IsBit7On(this byte theByte)
        {
            return theByte.IsBitOn(0b10000000);
        }
        public static bool IsBit0Off(this byte theByte)
        {
            return !theByte.IsBit0On();
        }
        public static bool IsBit1Off(this byte theByte)
        {
            return !theByte.IsBit1On();
        }
        public static bool IsBit2Off(this byte theByte)
        {
            return !theByte.IsBit2On();
        }
        public static bool IsBit3Off(this byte theByte)
        {
            return !theByte.IsBit3On();
        }
        public static bool IsBit4Off(this byte theByte)
        {
            return !theByte.IsBit4On();
        }
        public static bool IsBit5Off(this byte theByte)
        {
            return !theByte.IsBit5On();
        }
        public static bool IsBit6Off(this byte theByte)
        {
            return !theByte.IsBit6On();
        }
        public static bool IsBit7Off(this byte theByte)
        {
            return !theByte.IsBit7On();
        }

        public static byte SetBit0(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00000001, on);
        }
        public static byte SetBit1(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00000010, on);
        }
        public static byte SetBit2(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00000100, on);
        }
        public static byte SetBit3(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00001000, on);
        }
        public static byte SetBit4(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00010000, on);
        }
        public static byte SetBit5(this byte theByte, bool on)
        {
            return theByte.SetBit(0b00100000, on);
        }
        public static byte SetBit6(this byte theByte, bool on)
        {
            return theByte.SetBit(0b01000000, on);
        }
        public static byte SetBit7(this byte theByte, bool on)
        {
            return theByte.SetBit(0b10000000, on);
        }
    }
}
