using System;
using System.Collections.Generic;
using System.IO;

namespace Ozzytron
{
    public class Bus
    {
        public W65C02S _cpu = new W65C02S();
        public IDictionary<ushort, byte> _ram = new Dictionary<ushort, byte>();

        public Bus()
        {
            _cpu.ConnectBus(this);
            for (ushort a = 0x0000; a < 0xFFFF; a++)
                _ram[a] = 0x00;
            _ram[0xFFFF] = 0x00; // if the loop went up-to 0xFFFF a buffer overflow would cause it to loop forever lol
        }

        public void LoadProgram(ushort loadCodeAddress, string codeString, ushort programStartAddress)
        {
            // Load Code into Memory
            ushort nOffset = loadCodeAddress;
            foreach (var byteString in codeString.ToUpper().Split(' '))
            {
                _ram[nOffset++] = byte.Parse(byteString, System.Globalization.NumberStyles.HexNumber);
            }

            // Set Reset Vector
            _ram[W65C02S.RESET_VECTOR_LOW_ADDRESS] = (byte)(programStartAddress & 0xFF); // get the low 8-bits from program start address
            _ram[W65C02S.RESET_VECTOR_HIGH_ADDRESS] = (byte)(programStartAddress >> 8); // get the high 8-bits from program start address

            // Reset
            _cpu.reset();
        }

        public void LoadProgram(ushort loadCodeAddress, byte[] code, ushort programStartAddress)
        {
            // Load Code into Memory
            ushort nOffset = loadCodeAddress;
            foreach (byte codeByte in code)
            {
                _ram[nOffset++] = codeByte;
            }

            // Set Reset Vector
            _ram[W65C02S.RESET_VECTOR_LOW_ADDRESS] = (byte)(programStartAddress & 0xFF); // get the low 8-bits from program start address
            _ram[W65C02S.RESET_VECTOR_HIGH_ADDRESS] = (byte)(programStartAddress >> 8); // get the high 8-bits from program start address

            // Reset
            _cpu.reset();
        }

        public void ReadProgram(string filePath)
        {
            // assemble program from file
            var program = File.ReadAllText(filePath);
            var code = _cpu.assemble(program, true, 0x00);

            ushort nOffset = 0x00;
            foreach (byte codeByte in code)
            {
                _ram[nOffset] = codeByte;
                nOffset++;
            }

            // Reset
            _cpu.reset();
        }


        public void write(ushort address, byte data)
        {
            if (address >= 0x0000 && address <= 0xFFFF)
                _ram[address] = data;
        }

        public byte read(ushort address, bool bReadOnly = false)
        {
            if (address >= 0x0000 && address <= 0xFFFF)
                return _ram[address];

            return 0x00;
        }
    }
}
