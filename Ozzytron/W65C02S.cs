using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ozzytron
{

    public class W65C02S
    {
        public Bus _bus;
        public IDictionary<byte, Operation> opCodeLookup;

        public bool isWaiting = false;
        public bool isStopped = false;

        public const ushort NMI_VECTOR_LOW_ADDRESS = 0xFFFA; // Address to read the low byte of the nmi vector
        public const ushort NMI_VECTOR_HIGH_ADDRESS = 0xFFFB; // Address to read the high byte of the nmi vector
        public const ushort RESET_VECTOR_LOW_ADDRESS = 0xFFFC; // Address to read the low byte of the reset vector
        public const ushort RESET_VECTOR_HIGH_ADDRESS = 0xFFFD; // Address to read the high byte of the reset vector
        public const ushort IRQ_VECTOR_LOW_ADDRESS = 0xFFFE; // Address to read the low byte of the irq/brk vector
        public const ushort IRQ_VECTOR_HIGH_ADDRESS = 0xFFFF; // Address to read the high byte of the irq/brk vector

        public byte A = 0x00; // Accumulator A
        public byte Y = 0x00; // Index Register Y
        public byte X = 0x00; // Index Register X

        public ushort PC = 0x0000; // Program Counter PC
        public byte S = 0x00; // Stack Pointer S

        public byte P = 0b00100000; // Processor Status Register "P" (only Unused is defaulted to 1)
        public byte CBit = 0b00000001; // Carry, 1=True, 0th bit
        public byte ZBit = 0b00000010; // Zero, 1=True, 1st bit
        public byte IBit = 0b00000100; // IRQB disable, 1=True, 2nd bit
        public byte DBit = 0b00001000; // Decimal mode, 1=True, 3rd bit
        public byte BBit = 0b00010000; // BRK command, 1=BRK, 0=IRQB, 4th bit
        public byte UBit = 0b00100000; // Unused, 5th bit
        public byte VBit = 0b01000000; // Overflow, 1=True, 6th bit
        public byte NBit = 0b10000000; // Negative, 1=True, 7th bit


        public byte currentOpCode = 0xEA;
        public int cycles = 0;

        public ushort currentAbsoluteAddress = 0x0000; // current address based on addressesing mode (except for implied modes)
        public ushort currentRelativeAddress = 0x0000; // offset for Program Counter if branching occurs (only used for branching operations)
        public byte currentWorkingValue = 0x00; // current "working" value based on current operation and addressing mode

        public void ConnectBus(Bus bus)
        {
            _bus = bus;
            opCodeLookup = new Dictionary<byte, Operation>
            {
                { 0x00, new Operation { Mnemonic="BRK", OpCode=0x00, Operate=BRK, Address=IMS, MinimumCycles=7, Size=1 } },
                { 0x01, new Operation { Mnemonic="ORA", OpCode=0x01, Operate=ORA, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0x02, new Operation { Mnemonic="???", OpCode=0x02, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x03, new Operation { Mnemonic="???", OpCode=0x03, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x04, new Operation { Mnemonic="TSB", OpCode=0x04, Operate=TSB, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x05, new Operation { Mnemonic="ORA", OpCode=0x05, Operate=ORA, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x06, new Operation { Mnemonic="ASL", OpCode=0x06, Operate=ASL, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x07, new Operation { Mnemonic="RMB0", OpCode=0x07, Operate=RMB0, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x08, new Operation { Mnemonic="PHP", OpCode=0x08, Operate=PHP, Address=IMS, MinimumCycles=3, Size=1 } },
                { 0x09, new Operation { Mnemonic="ORA", OpCode=0x09, Operate=ORA, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0x0A, new Operation { Mnemonic="ASL", OpCode=0x0A, Operate=ASL, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x0B, new Operation { Mnemonic="???", OpCode=0x0B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x0C, new Operation { Mnemonic="TSB", OpCode=0x0C, Operate=TSB, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x0D, new Operation { Mnemonic="ORA", OpCode=0x0D, Operate=ORA, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x0E, new Operation { Mnemonic="ASL", OpCode=0x0E, Operate=ASL, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x0F, new Operation { Mnemonic="BBR0", OpCode=0x0F, Operate=BBR0, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x10, new Operation { Mnemonic="BPL", OpCode=0x10, Operate=BPL, Address=REL, MinimumCycles=2, Size=2 } },
                { 0x11, new Operation { Mnemonic="ORA", OpCode=0x11, Operate=ORA, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0x12, new Operation { Mnemonic="ORA", OpCode=0x12, Operate=ORA, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0x13, new Operation { Mnemonic="???", OpCode=0x13, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x14, new Operation { Mnemonic="TRB", OpCode=0x14, Operate=TRB, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x15, new Operation { Mnemonic="ORA", OpCode=0x15, Operate=ORA, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x16, new Operation { Mnemonic="ASL", OpCode=0x16, Operate=ASL, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0x17, new Operation { Mnemonic="RMB1", OpCode=0x17, Operate=RMB1, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x18, new Operation { Mnemonic="CLC", OpCode=0x18, Operate=CLC, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x19, new Operation { Mnemonic="ORA", OpCode=0x19, Operate=ORA, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0x1A, new Operation { Mnemonic="INC", OpCode=0x1A, Operate=INC, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x1B, new Operation { Mnemonic="???", OpCode=0x1B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x1C, new Operation { Mnemonic="TRB", OpCode=0x1C, Operate=TRB, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x1D, new Operation { Mnemonic="ORA", OpCode=0x1D, Operate=ORA, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0x1E, new Operation { Mnemonic="ASL", OpCode=0x1E, Operate=ASL, Address=ABX, MinimumCycles=6, Size=3 } },
                { 0x1F, new Operation { Mnemonic="BBR1", OpCode=0x1F, Operate=BBR1, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x20, new Operation { Mnemonic="JSR", OpCode=0x20, Operate=JSR, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x21, new Operation { Mnemonic="AND", OpCode=0x21, Operate=AND, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0x22, new Operation { Mnemonic="???", OpCode=0x22, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x23, new Operation { Mnemonic="???", OpCode=0x23, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x24, new Operation { Mnemonic="BIT", OpCode=0x24, Operate=BIT, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x25, new Operation { Mnemonic="AND", OpCode=0x25, Operate=AND, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x26, new Operation { Mnemonic="ROL", OpCode=0x26, Operate=ROL, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x27, new Operation { Mnemonic="RMB2", OpCode=0x27, Operate=RMB2, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x28, new Operation { Mnemonic="PLP", OpCode=0x28, Operate=PLP, Address=IMS, MinimumCycles=4, Size=1 } },
                { 0x29, new Operation { Mnemonic="AND", OpCode=0x29, Operate=AND, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0x2A, new Operation { Mnemonic="ROL", OpCode=0x2A, Operate=ROL, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x2B, new Operation { Mnemonic="???", OpCode=0x2B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x2C, new Operation { Mnemonic="BIT", OpCode=0x2C, Operate=BIT, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x2D, new Operation { Mnemonic="AND", OpCode=0x2D, Operate=AND, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x2E, new Operation { Mnemonic="ROL", OpCode=0x2E, Operate=ROL, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x2F, new Operation { Mnemonic="BBR2", OpCode=0x2F, Operate=BBR2, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x30, new Operation { Mnemonic="BMI", OpCode=0x30, Operate=BMI, Address=REL, MinimumCycles=2, Size=2 } },
                { 0x31, new Operation { Mnemonic="AND", OpCode=0x31, Operate=AND, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0x32, new Operation { Mnemonic="AND", OpCode=0x32, Operate=AND, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0x33, new Operation { Mnemonic="???", OpCode=0x33, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x34, new Operation { Mnemonic="BIT", OpCode=0x34, Operate=BIT, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x35, new Operation { Mnemonic="AND", OpCode=0x35, Operate=AND, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x36, new Operation { Mnemonic="ROL", OpCode=0x36, Operate=ROL, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0x37, new Operation { Mnemonic="RMB3", OpCode=0x37, Operate=RMB3, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x38, new Operation { Mnemonic="SEC", OpCode=0x38, Operate=SEC, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x39, new Operation { Mnemonic="AND", OpCode=0x39, Operate=AND, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0x3A, new Operation { Mnemonic="DEC", OpCode=0x3A, Operate=DEC, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x3B, new Operation { Mnemonic="???", OpCode=0x3B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x3C, new Operation { Mnemonic="BIT", OpCode=0x3C, Operate=BIT, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0x3D, new Operation { Mnemonic="AND", OpCode=0x3D, Operate=AND, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0x3E, new Operation { Mnemonic="ROL", OpCode=0x3E, Operate=ROL, Address=ABX, MinimumCycles=6, Size=3 } },
                { 0x3F, new Operation { Mnemonic="BBR3", OpCode=0x3F, Operate=BBR3, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x40, new Operation { Mnemonic="RTI", OpCode=0x40, Operate=RTI, Address=IMS, MinimumCycles=6, Size=1 } },
                { 0x41, new Operation { Mnemonic="EOR", OpCode=0x41, Operate=EOR, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0x42, new Operation { Mnemonic="???", OpCode=0x42, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x43, new Operation { Mnemonic="???", OpCode=0x43, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x44, new Operation { Mnemonic="???", OpCode=0x44, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x45, new Operation { Mnemonic="EOR", OpCode=0x45, Operate=EOR, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x46, new Operation { Mnemonic="LSR", OpCode=0x46, Operate=LSR, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x47, new Operation { Mnemonic="RMB4", OpCode=0x47, Operate=RMB4, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x48, new Operation { Mnemonic="PHA", OpCode=0x48, Operate=PHA, Address=IMS, MinimumCycles=3, Size=1 } },
                { 0x49, new Operation { Mnemonic="EOR", OpCode=0x49, Operate=EOR, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0x4A, new Operation { Mnemonic="LSR", OpCode=0x4A, Operate=LSR, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x4B, new Operation { Mnemonic="???", OpCode=0x4B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x4C, new Operation { Mnemonic="JMP", OpCode=0x4C, Operate=JMP, Address=ABS, MinimumCycles=3, Size=3 } },
                { 0x4D, new Operation { Mnemonic="EOR", OpCode=0x4D, Operate=EOR, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x4E, new Operation { Mnemonic="LSR", OpCode=0x4E, Operate=LSR, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x4F, new Operation { Mnemonic="BBR4", OpCode=0x4F, Operate=BBR4, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x50, new Operation { Mnemonic="BVC", OpCode=0x50, Operate=BVC, Address=REL, MinimumCycles=2, Size=2 } },
                { 0x51, new Operation { Mnemonic="EOR", OpCode=0x51, Operate=EOR, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0x52, new Operation { Mnemonic="EOR", OpCode=0x52, Operate=EOR, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0x53, new Operation { Mnemonic="???", OpCode=0x53, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x54, new Operation { Mnemonic="???", OpCode=0x54, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x55, new Operation { Mnemonic="EOR", OpCode=0x55, Operate=EOR, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x56, new Operation { Mnemonic="LSR", OpCode=0x56, Operate=LSR, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0x57, new Operation { Mnemonic="RMB5", OpCode=0x57, Operate=RMB5, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x58, new Operation { Mnemonic="CLI", OpCode=0x58, Operate=CLI, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x59, new Operation { Mnemonic="EOR", OpCode=0x59, Operate=EOR, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0x5A, new Operation { Mnemonic="PHY", OpCode=0x5A, Operate=PHY, Address=IMS, MinimumCycles=3, Size=1 } },
                { 0x5B, new Operation { Mnemonic="???", OpCode=0x5B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x5C, new Operation { Mnemonic="???", OpCode=0x5C, Operate=NOP, Address=IMP, MinimumCycles=2, Size=3 } },
                { 0x5D, new Operation { Mnemonic="EOR", OpCode=0x5D, Operate=EOR, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0x5E, new Operation { Mnemonic="LSR", OpCode=0x5E, Operate=LSR, Address=ABX, MinimumCycles=6, Size=3 } },
                { 0x5F, new Operation { Mnemonic="BBR5", OpCode=0x5F, Operate=BBR5, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x60, new Operation { Mnemonic="RTS", OpCode=0x60, Operate=RTS, Address=IMS, MinimumCycles=6, Size=1 } },
                { 0x61, new Operation { Mnemonic="ADC", OpCode=0x61, Operate=ADC, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0x62, new Operation { Mnemonic="???", OpCode=0x62, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x63, new Operation { Mnemonic="???", OpCode=0x63, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x64, new Operation { Mnemonic="STZ", OpCode=0x64, Operate=STZ, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x65, new Operation { Mnemonic="ADC", OpCode=0x65, Operate=ADC, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x66, new Operation { Mnemonic="ROR", OpCode=0x66, Operate=ROR, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x67, new Operation { Mnemonic="RMB6", OpCode=0x67, Operate=RMB6, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x68, new Operation { Mnemonic="PLA", OpCode=0x68, Operate=PLA, Address=IMS, MinimumCycles=4, Size=1 } },
                { 0x69, new Operation { Mnemonic="ADC", OpCode=0x69, Operate=ADC, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0x6A, new Operation { Mnemonic="ROR", OpCode=0x6A, Operate=ROR, Address=IMA, MinimumCycles=2, Size=1 } },
                { 0x6B, new Operation { Mnemonic="???", OpCode=0x6B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x6C, new Operation { Mnemonic="JMP", OpCode=0x6C, Operate=JMP, Address=IND, MinimumCycles=6, Size=3 } },
                { 0x6D, new Operation { Mnemonic="ADC", OpCode=0x6D, Operate=ADC, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x6E, new Operation { Mnemonic="ROR", OpCode=0x6E, Operate=ROR, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0x6F, new Operation { Mnemonic="BBR6", OpCode=0x6F, Operate=BBR6, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x70, new Operation { Mnemonic="BVS", OpCode=0x70, Operate=BVS, Address=REL, MinimumCycles=2, Size=2 } },
                { 0x71, new Operation { Mnemonic="ADC", OpCode=0x71, Operate=ADC, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0x72, new Operation { Mnemonic="ADC", OpCode=0x72, Operate=ADC, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0x73, new Operation { Mnemonic="???", OpCode=0x73, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x74, new Operation { Mnemonic="STZ", OpCode=0x74, Operate=STZ, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x75, new Operation { Mnemonic="ADC", OpCode=0x75, Operate=ADC, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x76, new Operation { Mnemonic="ROR", OpCode=0x76, Operate=ROR, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0x77, new Operation { Mnemonic="RMB7", OpCode=0x77, Operate=RMB7, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x78, new Operation { Mnemonic="SEI", OpCode=0x78, Operate=SEI, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x79, new Operation { Mnemonic="ADC", OpCode=0x79, Operate=ADC, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0x7A, new Operation { Mnemonic="PLY", OpCode=0x7A, Operate=PLY, Address=IMS, MinimumCycles=4, Size=1 } },
                { 0x7B, new Operation { Mnemonic="???", OpCode=0x7B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x7C, new Operation { Mnemonic="JMP", OpCode=0x7C, Operate=JMP, Address=IAB, MinimumCycles=6, Size=3 } },
                { 0x7D, new Operation { Mnemonic="ADC", OpCode=0x7D, Operate=ADC, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0x7E, new Operation { Mnemonic="ROR", OpCode=0x7E, Operate=ROR, Address=ABX, MinimumCycles=6, Size=3 } },
                { 0x7F, new Operation { Mnemonic="BBR7", OpCode=0x7F, Operate=BBR7, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x80, new Operation { Mnemonic="BRA", OpCode=0x80, Operate=BRA, Address=REL, MinimumCycles=3, Size=2 } },
                { 0x81, new Operation { Mnemonic="STA", OpCode=0x81, Operate=STA, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0x82, new Operation { Mnemonic="???", OpCode=0x82, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0x83, new Operation { Mnemonic="???", OpCode=0x83, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x84, new Operation { Mnemonic="STY", OpCode=0x84, Operate=STY, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x85, new Operation { Mnemonic="STA", OpCode=0x85, Operate=STA, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x86, new Operation { Mnemonic="STX", OpCode=0x86, Operate=STX, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0x87, new Operation { Mnemonic="SMB0", OpCode=0x87, Operate=SMB0, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x88, new Operation { Mnemonic="DEY", OpCode=0x88, Operate=DEY, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x89, new Operation { Mnemonic="BIT", OpCode=0x89, Operate=BIT, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0x8A, new Operation { Mnemonic="TXA", OpCode=0x8A, Operate=TXA, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x8B, new Operation { Mnemonic="???", OpCode=0x8B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x8C, new Operation { Mnemonic="STY", OpCode=0x8C, Operate=STY, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x8D, new Operation { Mnemonic="STA", OpCode=0x8D, Operate=STA, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x8E, new Operation { Mnemonic="STX", OpCode=0x8E, Operate=STX, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x8F, new Operation { Mnemonic="BBS0", OpCode=0x8F, Operate=BBS0, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0x90, new Operation { Mnemonic="BCC", OpCode=0x90, Operate=BCC, Address=REL, MinimumCycles=2, Size=2 } },
                { 0x91, new Operation { Mnemonic="STA", OpCode=0x91, Operate=STA, Address=ZIY, MinimumCycles=6, Size=2 } },
                { 0x92, new Operation { Mnemonic="STA", OpCode=0x92, Operate=STA, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0x93, new Operation { Mnemonic="???", OpCode=0x93, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x94, new Operation { Mnemonic="STY", OpCode=0x94, Operate=STY, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x95, new Operation { Mnemonic="STA", OpCode=0x95, Operate=STA, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0x96, new Operation { Mnemonic="STX", OpCode=0x96, Operate=STX, Address=ZPY, MinimumCycles=4, Size=2 } },
                { 0x97, new Operation { Mnemonic="SMB1", OpCode=0x97, Operate=SMB1, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0x98, new Operation { Mnemonic="TYA", OpCode=0x98, Operate=TYA, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x99, new Operation { Mnemonic="STA", OpCode=0x99, Operate=STA, Address=ABY, MinimumCycles=5, Size=3 } },
                { 0x9A, new Operation { Mnemonic="TXS", OpCode=0x9A, Operate=TXS, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x9B, new Operation { Mnemonic="???", OpCode=0x9B, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0x9C, new Operation { Mnemonic="STZ", OpCode=0x9C, Operate=STZ, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0x9D, new Operation { Mnemonic="STA", OpCode=0x9D, Operate=STA, Address=ABX, MinimumCycles=5, Size=3 } },
                { 0x9E, new Operation { Mnemonic="STZ", OpCode=0x9E, Operate=STZ, Address=ABX, MinimumCycles=5, Size=3 } },
                { 0x9F, new Operation { Mnemonic="BBS1", OpCode=0x9F, Operate=BBS1, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xA0, new Operation { Mnemonic="LDY", OpCode=0xA0, Operate=LDY, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xA1, new Operation { Mnemonic="LDA", OpCode=0xA1, Operate=LDA, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0xA2, new Operation { Mnemonic="LDX", OpCode=0xA2, Operate=LDX, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xA3, new Operation { Mnemonic="???", OpCode=0xA3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xA4, new Operation { Mnemonic="LDY", OpCode=0xA4, Operate=LDY, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xA5, new Operation { Mnemonic="LDA", OpCode=0xA5, Operate=LDA, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xA6, new Operation { Mnemonic="LDX", OpCode=0xA6, Operate=LDX, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xA7, new Operation { Mnemonic="SMB2", OpCode=0xA7, Operate=SMB2, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xA8, new Operation { Mnemonic="TAY", OpCode=0xA8, Operate=TAY, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xA9, new Operation { Mnemonic="LDA", OpCode=0xA9, Operate=LDA, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xAA, new Operation { Mnemonic="TAX", OpCode=0xAA, Operate=TAX, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xAB, new Operation { Mnemonic="???", OpCode=0xAB, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xAC, new Operation { Mnemonic="LDY", OpCode=0xAC, Operate=LDY, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xAD, new Operation { Mnemonic="LDA", OpCode=0xAD, Operate=LDA, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xAE, new Operation { Mnemonic="LDX", OpCode=0xAE, Operate=LDX, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xAF, new Operation { Mnemonic="BBS2", OpCode=0xAF, Operate=BBS2, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xB0, new Operation { Mnemonic="BCS", OpCode=0xB0, Operate=BCS, Address=REL, MinimumCycles=2, Size=2 } },
                { 0xB1, new Operation { Mnemonic="LDA", OpCode=0xB1, Operate=LDA, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0xB2, new Operation { Mnemonic="LDA", OpCode=0xB2, Operate=LDA, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0xB3, new Operation { Mnemonic="???", OpCode=0xB3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xB4, new Operation { Mnemonic="LDY", OpCode=0xB4, Operate=LDY, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0xB5, new Operation { Mnemonic="LDA", OpCode=0xB5, Operate=LDA, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0xB6, new Operation { Mnemonic="LDX", OpCode=0xB6, Operate=LDX, Address=ZPY, MinimumCycles=4, Size=2 } },
                { 0xB7, new Operation { Mnemonic="SMB3", OpCode=0xB7, Operate=SMB3, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xB8, new Operation { Mnemonic="CLV", OpCode=0xB8, Operate=CLV, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xB9, new Operation { Mnemonic="LDA", OpCode=0xB9, Operate=LDA, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0xBA, new Operation { Mnemonic="TSX", OpCode=0xBA, Operate=TSX, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xBB, new Operation { Mnemonic="???", OpCode=0xBB, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xBC, new Operation { Mnemonic="LDY", OpCode=0xBC, Operate=LDY, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0xBD, new Operation { Mnemonic="LDA", OpCode=0xBD, Operate=LDA, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0xBE, new Operation { Mnemonic="LDX", OpCode=0xBE, Operate=LDX, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0xBF, new Operation { Mnemonic="BBS3", OpCode=0xBF, Operate=BBS3, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xC0, new Operation { Mnemonic="CPY", OpCode=0xC0, Operate=CPY, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xC1, new Operation { Mnemonic="CMP", OpCode=0xC1, Operate=CMP, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0xC2, new Operation { Mnemonic="???", OpCode=0xC2, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0xC3, new Operation { Mnemonic="???", OpCode=0xC3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xC4, new Operation { Mnemonic="CPY", OpCode=0xC4, Operate=CPY, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xC5, new Operation { Mnemonic="CMP", OpCode=0xC5, Operate=CMP, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xC6, new Operation { Mnemonic="DEC", OpCode=0xC6, Operate=DEC, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xC7, new Operation { Mnemonic="SMB4", OpCode=0xC7, Operate=SMB4, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xC8, new Operation { Mnemonic="INY", OpCode=0xC8, Operate=INY, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xC9, new Operation { Mnemonic="CMP", OpCode=0xC9, Operate=CMP, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xCA, new Operation { Mnemonic="DEX", OpCode=0xCA, Operate=DEX, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xCB, new Operation { Mnemonic="WAI", OpCode=0xCB, Operate=WAI, Address=IMP, MinimumCycles=3, Size=1 } },
                { 0xCC, new Operation { Mnemonic="CPY", OpCode=0xCC, Operate=CPY, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xCD, new Operation { Mnemonic="CMP", OpCode=0xCD, Operate=CMP, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xCE, new Operation { Mnemonic="DEC", OpCode=0xCE, Operate=DEC, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0xCF, new Operation { Mnemonic="BBS4", OpCode=0xCF, Operate=BBS4, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xD0, new Operation { Mnemonic="BNE", OpCode=0xD0, Operate=BNE, Address=REL, MinimumCycles=2, Size=2 } },
                { 0xD1, new Operation { Mnemonic="CMP", OpCode=0xD1, Operate=CMP, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0xD2, new Operation { Mnemonic="CMP", OpCode=0xD2, Operate=CMP, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0xD3, new Operation { Mnemonic="???", OpCode=0xD3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xD4, new Operation { Mnemonic="???", OpCode=0xD4, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0xD5, new Operation { Mnemonic="CMP", OpCode=0xD5, Operate=CMP, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0xD6, new Operation { Mnemonic="DEC", OpCode=0xD6, Operate=DEC, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0xD7, new Operation { Mnemonic="SMB5", OpCode=0xD7, Operate=SMB5, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xD8, new Operation { Mnemonic="CLD", OpCode=0xD8, Operate=CLD, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xD9, new Operation { Mnemonic="CMP", OpCode=0xD9, Operate=CMP, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0xDA, new Operation { Mnemonic="PHX", OpCode=0xDA, Operate=PHX, Address=IMS, MinimumCycles=3, Size=1 } },
                { 0xDB, new Operation { Mnemonic="STP", OpCode=0xDB, Operate=STP, Address=IMP, MinimumCycles=3, Size=1 } },
                { 0xDC, new Operation { Mnemonic="???", OpCode=0xDC, Operate=NOP, Address=IMP, MinimumCycles=2, Size=3 } },
                { 0xDD, new Operation { Mnemonic="CMP", OpCode=0xDD, Operate=CMP, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0xDE, new Operation { Mnemonic="DEC", OpCode=0xDE, Operate=DEC, Address=ABX, MinimumCycles=7, Size=3 } },
                { 0xDF, new Operation { Mnemonic="BBS5", OpCode=0xDF, Operate=BBS5, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xE0, new Operation { Mnemonic="CPX", OpCode=0xE0, Operate=CPX, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xE1, new Operation { Mnemonic="SBC", OpCode=0xE1, Operate=SBC, Address=ZIX, MinimumCycles=6, Size=2 } },
                { 0xE2, new Operation { Mnemonic="???", OpCode=0xE2, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0xE3, new Operation { Mnemonic="???", OpCode=0xE3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xE4, new Operation { Mnemonic="CPX", OpCode=0xE4, Operate=CPX, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xE5, new Operation { Mnemonic="SBC", OpCode=0xE5, Operate=SBC, Address=ZP0, MinimumCycles=3, Size=2 } },
                { 0xE6, new Operation { Mnemonic="INC", OpCode=0xE6, Operate=INC, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xE7, new Operation { Mnemonic="SMB6", OpCode=0xE7, Operate=SMB6, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xE8, new Operation { Mnemonic="INX", OpCode=0xE8, Operate=INX, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xE9, new Operation { Mnemonic="SBC", OpCode=0xE9, Operate=SBC, Address=IMM, MinimumCycles=2, Size=2 } },
                { 0xEA, new Operation { Mnemonic="NOP", OpCode=0xEA, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xEB, new Operation { Mnemonic="???", OpCode=0xEB, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xEC, new Operation { Mnemonic="CPX", OpCode=0xEC, Operate=CPX, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xED, new Operation { Mnemonic="SBC", OpCode=0xED, Operate=SBC, Address=ABS, MinimumCycles=4, Size=3 } },
                { 0xEE, new Operation { Mnemonic="INC", OpCode=0xEE, Operate=INC, Address=ABS, MinimumCycles=6, Size=3 } },
                { 0xEF, new Operation { Mnemonic="BBS6", OpCode=0xEF, Operate=BBS6, Address=ZP0, MinimumCycles=5, Size=3 } },
                { 0xF0, new Operation { Mnemonic="BEQ", OpCode=0xF0, Operate=BEQ, Address=REL, MinimumCycles=2, Size=2 } },
                { 0xF1, new Operation { Mnemonic="SBC", OpCode=0xF1, Operate=SBC, Address=ZIY, MinimumCycles=5, Size=2 } },
                { 0xF2, new Operation { Mnemonic="SBC", OpCode=0xF2, Operate=SBC, Address=ZPI, MinimumCycles=5, Size=2 } },
                { 0xF3, new Operation { Mnemonic="???", OpCode=0xF3, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xF4, new Operation { Mnemonic="???", OpCode=0xF4, Operate=NOP, Address=IMP, MinimumCycles=2, Size=2 } },
                { 0xF5, new Operation { Mnemonic="SBC", OpCode=0xF5, Operate=SBC, Address=ZPX, MinimumCycles=4, Size=2 } },
                { 0xF6, new Operation { Mnemonic="INC", OpCode=0xF6, Operate=INC, Address=ZPX, MinimumCycles=6, Size=2 } },
                { 0xF7, new Operation { Mnemonic="SMB7", OpCode=0xF7, Operate=SMB7, Address=ZP0, MinimumCycles=5, Size=2 } },
                { 0xF8, new Operation { Mnemonic="SED", OpCode=0xF8, Operate=SED, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xF9, new Operation { Mnemonic="SBC", OpCode=0xF9, Operate=SBC, Address=ABY, MinimumCycles=4, Size=3 } },
                { 0xFA, new Operation { Mnemonic="PLX", OpCode=0xFA, Operate=PLX, Address=IMS, MinimumCycles=4, Size=1 } },
                { 0xFB, new Operation { Mnemonic="???", OpCode=0xFB, Operate=NOP, Address=IMP, MinimumCycles=2, Size=1 } },
                { 0xFC, new Operation { Mnemonic="???", OpCode=0xFC, Operate=NOP, Address=IMP, MinimumCycles=2, Size=3 } },
                { 0xFD, new Operation { Mnemonic="SBC", OpCode=0xFD, Operate=SBC, Address=ABX, MinimumCycles=4, Size=3 } },
                { 0xFE, new Operation { Mnemonic="INC", OpCode=0xFE, Operate=INC, Address=ABX, MinimumCycles=7, Size=3 } },
                { 0xFF, new Operation { Mnemonic="BBS7", OpCode=0xFF, Operate=BBS7, Address=ZP0, MinimumCycles=5, Size=3 } },
            };
        }

        #region Programming Commands
        public byte[] assemble(string program, bool fillRam = false, byte fillerByte = 0x00)
        {
            /** Features:
             * -- 0. parse instructions -> "lda #$29" or "lda $29" or "lda $0801" 
             * -- 1. set location to assemble at -> ".org $8000"
             * -- 2. comments -> "; ignore everything after semi-colon"
             * -- 3. create data -> ".byte $80" or ".word $8080"
             * -- 4. create labels -> "some_label:" (creates a label for 16-bit address in the code)
             * -- 4.5 be able to reference labels before they are declared (likely requires two passes todo)
             * -- 5. crate constants -> "some_constant=" (creates a name for some 16-bit value in the code)
            */

            var variableNameRegex = @"^[a-zA-Z][a-zA-Z0-9_]*";
            var byteRegex = @"\$[0-9a-fA-F][0-9a-fA-F]";
            var wordRegex = @"\$[0-9a-fA-F][0-9a-fA-F][0-9a-fA-F][0-9a-fA-F]";
            var wordOrVariableRegex = @$"({wordRegex}|{variableNameRegex})";

            ushort address = 0x0000;
            var byteVariables = new Dictionary<string, byte>();
            var wordVariables = new Dictionary<string, ushort>();

            var addressingNeedingWordVariableResolved = new Dictionary<ushort, string>();
            var addressingNeedingBranchingVariableResolved = new Dictionary<ushort, string>();

            var listing = "";
            var programByAddress = new Dictionary<ushort, byte>();

            program = program.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", " ").ToUpper().Trim(); // normalize line endings and whitespace
            program = Regex.Replace(program, "[ ]+", " ").Trim(); // replace multiple adjacent white-spaces with a single one
            var lines = program.Split("\n");
            foreach (var line in lines)
            {
                var commentIndex = line.IndexOf(";");
                var trimmedLine = commentIndex != -1 ? line.Substring(0, commentIndex).Trim() : line.Trim();
                listing += $"${address.ToString("X4")} |     {(commentIndex != -1 ? line.Substring(0, commentIndex) : line).Replace(" ", "\t")}{(commentIndex != -1 ? line.Substring(commentIndex).Trim() : "")}\n";
                if (trimmedLine.Length == 0)
                {
                    continue;
                }

                // apply any reference labels or constant values in the line
                foreach (var variableValueKV in wordVariables)
                    trimmedLine = trimmedLine.Replace(variableValueKV.Key, "$" + variableValueKV.Value.ToString("X4")).Trim();
                foreach (var variableValueKV in byteVariables)
                    trimmedLine = trimmedLine.Replace(variableValueKV.Key, "$" + variableValueKV.Value.ToString("X2")).Trim();

                if (trimmedLine.IndexOf(".ORG") == 0)
                {
                    // move address to specific address
                    address = ushort.Parse(trimmedLine.Substring(trimmedLine.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                    continue;
                }
                else if (trimmedLine.IndexOf(":") != -1)
                {
                    // track a label
                    wordVariables[trimmedLine.Substring(0, trimmedLine.IndexOf(":")).Trim()] = address;
                    continue;
                }
                else if (trimmedLine.IndexOf("=") != -1)
                {
                    if (Regex.Match(trimmedLine, wordRegex).Success)
                        wordVariables[trimmedLine.Substring(0, trimmedLine.IndexOf("=")).Trim()] = ushort.Parse(trimmedLine.Substring(trimmedLine.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                    if (Regex.Match(trimmedLine, byteRegex).Success)
                        byteVariables[trimmedLine.Substring(0, trimmedLine.IndexOf("=")).Trim()] = byte.Parse(trimmedLine.Substring(trimmedLine.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    
                    continue;
                }
                else if (trimmedLine.IndexOf(".BYTE") == 0)
                {
                    programByAddress[address] = byte.Parse(trimmedLine.Substring(trimmedLine.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    continue;
                }
                else if (trimmedLine.IndexOf(".WORD") == 0)
                {
                    if (Regex.Match(trimmedLine, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(trimmedLine.Substring(trimmedLine.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);

                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = trimmedLine.Replace(".WORD", "").Trim();
                        address++;
                        address++;
                    }

                    continue;
                }

                // Parse operations
                var pieces = trimmedLine.Split(" ").Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (pieces.Length == 0)
                    continue;

                var opcodeMnemonic = pieces[0];
                var possibleOperations = opCodeLookup.Values.Where(o => o.Mnemonic.Equals(opcodeMnemonic)).ToArray();
                if (possibleOperations.Length == 0)
                    continue;


                var addessingString = pieces.Length >= 2 ? pieces[1] : "";
                if (opcodeMnemonic.Substring(0, 3) == "BBR" || opcodeMnemonic.Substring(0, 3) == "BBS")
                {
                    // if (opcodeMnemonic.Substring(0, 3) == "BBR" || opcodeMnemonic.Substring(0, 3) == "BBS") $c0,$c0
                    var operation = possibleOperations.Where(o => o.Mnemonic == opcodeMnemonic).FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    // resolve relative address
                    var relativeAddressingString = addessingString.Substring(addessingString.IndexOf(",") + 1).Trim();
                    if (Regex.Match(relativeAddressingString, @$"{byteRegex}$").Success)
                    {
                        programByAddress[address] = byte.Parse(relativeAddressingString.Substring(relativeAddressingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else if (Regex.Match(relativeAddressingString, @$"{wordRegex}$").Success)
                    {
                        var absoluteAddress = ushort.Parse(relativeAddressingString.Substring(relativeAddressingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        programByAddress[address] = (byte)((absoluteAddress - (address + 1)) & 0xFF);
                    }
                    else if (Regex.Match(relativeAddressingString, $@"{variableNameRegex}$").Success)
                    {
                        addressingNeedingBranchingVariableResolved[address] = relativeAddressingString;
                    }
                    else
                    {
                        // something bad has happened
                    }
                    address++;

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
                else if ((new string[] { "BPL", "BMI", "BVC", "BVS", "BRA", "BCC", "BCS", "BNE", "BEQ" }).Contains(opcodeMnemonic))
                {
                    // else if (REL) $c0 or label
                    var operation = possibleOperations.Where(o => o.Mnemonic == opcodeMnemonic).FirstOrDefault();
                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    // resolve relative address
                    if (Regex.Match(addessingString, @$"{byteRegex}$").Success)
                    {
                        programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else if (Regex.Match(addessingString, @$"{wordRegex}$").Success)
                    {
                        var absoluteAddress = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        programByAddress[address] = (byte)((absoluteAddress - (address + 1)) & 0xFF);
                    }
                    else if (Regex.Match(addessingString, $@"{variableNameRegex}$").Success)
                    {
                        addressingNeedingBranchingVariableResolved[address] = addessingString;
                    }
                    else
                    {
                        // something bad has happened
                    }
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (addessingString.Trim().Length == 0)
                {
                    // else if (IMP || IMA || IMS) -- no addressing
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "IMP" || o.Address.Method.Name == "IMA" || o.Address.Method.Name == "IMS").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    address += (ushort)(operation.Size - 1); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\#{byteRegex}$").Success)
                {
                    // else if (IMM) #$c0
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "IMM").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{byteRegex}$").Success)
                {
                    // else if (ZP0) $c0
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZP0").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{byteRegex}\,X$").Success)
                {
                    // else if (ZPX) $c0,X
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZPX").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{byteRegex}\,Y$").Success)
                {
                    // else if (ZPY) $c0,Y
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZPY").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\({byteRegex}\)$").Success)
                {
                    // else if (ZPI) ($c0)
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZPI").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\({byteRegex}\,X\)$").Success)
                {
                    // else if (ZIX) ($c0,X)
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZIX").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\({byteRegex}\)\,Y$").Success)
                {
                    // else if (ZIY) ($c0),Y
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ZIY").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;
                    programByAddress[address] = byte.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 2), System.Globalization.NumberStyles.HexNumber);
                    address++;

                    address += (ushort)(operation.Size - 2); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{wordOrVariableRegex}$").Success)
                {
                    // else if (ABS) $c000
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ABS").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    if (Regex.Match(addessingString, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = addessingString;
                        address++;
                        address++;
                    }

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\({wordOrVariableRegex}\,X\)$").Success)
                {
                    // else if (IAB) ($c000,X)
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "IAB").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    if (Regex.Match(addessingString, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = addessingString.Replace("(", "").Replace(",X)", "");
                        address++;
                        address++;
                    }

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{wordOrVariableRegex}\,X$").Success)
                {
                    // else if (ABX) $c000,X
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ABX").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    if (Regex.Match(addessingString, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = addessingString.Replace(",X", "");
                        address++;
                        address++;
                    }

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"{wordOrVariableRegex}\,Y$").Success)
                {
                    // else if (ABY) $c000,Y
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "ABY").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    if (Regex.Match(addessingString, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = addessingString.Replace(",Y", "");
                        address++;
                        address++;
                    }

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
                else if (Regex.Match(addessingString, @$"\({wordOrVariableRegex}\)$").Success)
                {
                    // else if (IND) ($c000)
                    var operation = possibleOperations.Where(o => o.Address.Method.Name == "IND").FirstOrDefault();

                    if (operation.Mnemonic == null)
                        continue;

                    programByAddress[address] = operation.OpCode;
                    address++;

                    if (Regex.Match(addessingString, @$".*{wordRegex}.*").Success)
                    {
                        var word = ushort.Parse(addessingString.Substring(addessingString.IndexOf("$") + 1, 4), System.Globalization.NumberStyles.HexNumber);
                        // write low byte then high byte
                        programByAddress[address] = (word).ToByte();
                        address++;
                        programByAddress[address] = (word >> 8).ToByte();
                        address++;
                    }
                    else
                    {
                        addressingNeedingWordVariableResolved[address] = addessingString.Replace("(", "").Replace(")", "");
                        address++;
                        address++;
                    }

                    address += (ushort)(operation.Size - 3); // to handle any weird instructions sizes
                }
            }

            foreach (var addressVariableKV in addressingNeedingWordVariableResolved)
            {
                var addressToFill = addressVariableKV.Key;
                var stringToResolve = addressVariableKV.Value;

                if (byteVariables.ContainsKey(stringToResolve))
                {
                    programByAddress[addressToFill] = byteVariables[stringToResolve];
                    addressToFill++;
                    // to avoid word variables being used in byte-specific scenarios causing opcodes to be overwritten on accident
                    if (!programByAddress.ContainsKey(addressToFill))
                        programByAddress[addressToFill] = 0x00;
                }
                else if (wordVariables.ContainsKey(stringToResolve))
                {
                    var word = wordVariables[stringToResolve];
                    programByAddress[addressToFill] = (word).ToByte();
                    addressToFill++;
                    // to avoid word variables being used in byte-specific scenarios causing opcodes to be overwritten on accident
                    if (!programByAddress.ContainsKey(addressToFill))
                        programByAddress[addressToFill] = (word >> 8).ToByte();
                }
            }

            foreach (var addressVariableKV in addressingNeedingBranchingVariableResolved)
            {
                var addressToFill = addressVariableKV.Key;
                var stringToResolve = addressVariableKV.Value;

                if (byteVariables.ContainsKey(stringToResolve))
                {
                    programByAddress[addressToFill] = byteVariables[stringToResolve];
                }
                else if (wordVariables.ContainsKey(stringToResolve))
                {
                    var word = wordVariables[stringToResolve];
                    programByAddress[addressToFill] = (byte)((word - (addressToFill + 1)) & 0xFF);
                }
            }

            Console.WriteLine(listing);

            var programBytes = new List<byte>();
            for (ushort a = 0x0000; a <= 0xFFFF; a++)
            {
                if (programByAddress.ContainsKey(a))
                    programBytes.Add(programByAddress[a]);
                else if (fillRam)
                    programBytes.Add(fillerByte);

                if (a == 0xFFFF) // otherwise ushort will overflow and this will be an infinite loop
                    break;
            }
            return programBytes.ToArray();
        }

        #endregion

        #region External CPU Commands
        public void reset()
        {
            isWaiting = isStopped = false;
            A = X = Y = 0;
            S = 0xFF;
            P = (byte)(0x00 | UBit);

            currentAbsoluteAddress = 0x0000;
            currentRelativeAddress = 0x0000;
            currentWorkingValue = 0x00;

            // Read program counter from RESET Vector
            ushort low = _bus.read(RESET_VECTOR_LOW_ADDRESS);
            ushort high = _bus.read(RESET_VECTOR_HIGH_ADDRESS);
            PC = (ushort)((high << 8) | low);

            cycles = 7;
        }

        public void irq()
        {
            isWaiting = false;

            if (GetStatusBit(IBit) == 0)
            {
                PushProgramCounterToStack();

                SetStatusBit(BBit, false);
                SetStatusBit(UBit, true);
                PushByteToStack(P);
                SetStatusBit(DBit, false);
                SetStatusBit(IBit, true);

                // Read program counter from IRQ Vector
                ushort low = _bus.read(IRQ_VECTOR_LOW_ADDRESS);
                ushort high = _bus.read(IRQ_VECTOR_HIGH_ADDRESS);
                PC = (ushort)((high << 8) | low);

                cycles = 7;
            }
        }

        public void nmi()
        {
            isWaiting = false;
            PushProgramCounterToStack();

            SetStatusBit(BBit, false);
            SetStatusBit(UBit, true);
            PushByteToStack(P);
            SetStatusBit(DBit, false);
            SetStatusBit(IBit, true);

            // Read program counter from NMI Vector
            ushort low = _bus.read(NMI_VECTOR_LOW_ADDRESS);
            ushort high = _bus.read(NMI_VECTOR_HIGH_ADDRESS);
            PC = (ushort)((high << 8) | low);

            cycles = 8;
        }

        public void clock()
        {
            if (!(isWaiting || isStopped) && complete())
            {
                currentOpCode = _bus.read(PC);
                PC++;

                // Ensure Unused bit is 1
                SetStatusBit(UBit, true);

                // Get base number of cycles
                var operation = opCodeLookup[currentOpCode];
                cycles = operation.MinimumCycles;


                var pageBoundaryCrossed = operation.Address();
                var operationIsSensitiveToPageCrossing = operation.Operate();
                
                // add additional cycle for crossing page boundary
                if(pageBoundaryCrossed && operationIsSensitiveToPageCrossing)
                    cycles++;

                // Ensure Unused bit is 1
                SetStatusBit(UBit, true);
            }

            if (cycles > 0)
                cycles--;
        }

        public void step()
        {
            do
            {
                clock();
            } while (!complete());
        }

        public byte fetchCurrentWorkingValue()
        {
            var addressingMode = opCodeLookup[currentOpCode].Address;

            // Implied Addressing modes don't have anything to load
            if (!(addressingMode == IMP || addressingMode == IMA || addressingMode == IMS))
                currentWorkingValue = _bus.read(currentAbsoluteAddress);

            return currentWorkingValue;
        }

        public bool complete()
        {
            // is not mid-operation
            return cycles == 0;
        }
        #endregion

        #region Operation Functions
        /// -+---- OPERATIONS ----+-
        /// ADC : ADd memory to accumulator with Carry
        public bool ADC()
        {
            fetchCurrentWorkingValue();
            if (GetStatusBit(DBit) == 1)
            {
                cycles++; // decimal mode adds a cycle
                var newValue = currentWorkingValue + A + (GetStatusBit(CBit));


                SetStatusBit(VBit, !(A ^ newValue).IsNegative() && (A ^ currentWorkingValue).IsNegative());

                // in Decimal mode you read the hex weird, 0x01 = 1, 0x20 = 20, 0x99 = 99,
                newValue = int.Parse(currentWorkingValue.ToString("x")) + int.Parse(A.ToString("x")) + (GetStatusBit(CBit));

                if (newValue > 99)
                {
                    SetStatusBit(CBit, true);
                    newValue -= 100;
                }
                else
                {
                    SetStatusBit(CBit, false);
                }

                newValue = (int)Convert.ToInt64(string.Concat("0x", newValue), 16);
                SetStatusBit(ZBit, newValue.ToByte().IsZero());
                SetStatusBit(NBit, newValue.IsNegative());

                A = newValue.ToByte();
            }
            else
            {
                ushort temp = (ushort)(A + currentWorkingValue + GetStatusBit(CBit));

                SetStatusBit(CBit, temp > 255);
                SetStatusBit(ZBit, temp.ToByte().IsZero());
                SetStatusBit(VBit, (~(A ^ currentWorkingValue) & (A ^ temp)).IsNegative());
                SetStatusBit(NBit, temp.IsNegative());

                A = temp.ToByte();
            }

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// AND : "AND" memory with accumulator
        public bool AND()
        {
            fetchCurrentWorkingValue();
            A = (byte)(A & currentWorkingValue);
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// ASL : Arithmetic    Shift    one    bit    Left,    memory    or accumulator
        public bool ASL()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(currentWorkingValue << 1);
            SetStatusBit(CBit, (temp & 0xFF00) > 0);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.IsNegative());
            if (opCodeLookup[currentOpCode].Address == IMA)
                A = temp.ToByte();
            else
                _bus.write(currentAbsoluteAddress, temp.ToByte());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// BBR : Branch on Bit Reset
        public bool BBR0()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit0Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR1()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit1Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR2()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit2Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR3()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit3Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR4()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit4Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR5()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit5Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR6()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit6Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBR7()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit7Off())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BBS : Branch of Bit Set
        public bool BBS0()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit0On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS1()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit1On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS2()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit2On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS3()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit3On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS4()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit4On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS5()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit5On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS6()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit6On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        public bool BBS7()
        {
            fetchCurrentWorkingValue(); // load value from Zero Page
            REL(); // Load up relative address for branching
            if (currentWorkingValue.IsBit7On())
            {
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);
                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BCC : Branch on Carry Clear (Pc=0)
        public bool BCC()
        {
            if (GetStatusBit(CBit) == 0)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BCS : Branch on Carry Set (Pc=1)
        public bool BCS()
        {
            if (GetStatusBit(CBit) == 1)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BEQ : Branch if EQual (Pz=1)
        public bool BEQ()
        {
            if (GetStatusBit(ZBit) == 1)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BIT : BIt Test
        public bool BIT()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(A & currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            if (opCodeLookup[currentOpCode].Address != IMM) // Cool 65C02 thing
            {
                SetStatusBit(NBit, (currentWorkingValue & (1 << 7)) != 0);
                SetStatusBit(VBit, (currentWorkingValue & (1 << 6)) != 0);
            }

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// BMI : Branch if result MInus (Pn=1)
        public bool BMI()
        {
            if (GetStatusBit(NBit) == 1)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BNE : Branch if Not Equal (Pz=0), i.e. if Zero Bit is off then move the Program Counter
        public bool BNE()
        {
            if (GetStatusBit(ZBit) == 0)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BPL : Branch if result PLus (Pn=0)
        public bool BPL()
        {
            if (GetStatusBit(NBit) == 0)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BRA : BRanch Always
        public bool BRA()
        {
            currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

            if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                cycles++;

            PC = currentAbsoluteAddress;

            return false;
        }
        /// BRK : BReaK instruction
        public bool BRK()
        {
            PC++; // Move past "padding byte" so return address is correct on stack

            PushProgramCounterToStack();

            PushByteToStack((byte)(P | BBit));

            SetStatusBit(DBit, false); // this is a 65C02 thing
            SetStatusBit(IBit, true);

            PC = (ushort)(_bus.read(IRQ_VECTOR_LOW_ADDRESS) | (_bus.read(IRQ_VECTOR_HIGH_ADDRESS) << 8));

            return false;
        }
        /// BVC : Branch on oVerflow Clear (Pv=0)
        public bool BVC()
        {
            if (GetStatusBit(VBit) == 0)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// BVS : Branch on oVerflow Set (Pv=1)
        public bool BVS()
        {
            if (GetStatusBit(VBit) == 1)
            {
                cycles++;
                currentAbsoluteAddress = (ushort)(PC + currentRelativeAddress);

                if ((currentAbsoluteAddress & 0xFF00) != (PC & 0xFF00)) // crossed page boundary
                    cycles++;

                PC = currentAbsoluteAddress;
            }

            return false;
        }
        /// CLC : CLear Cary flag, i.e. set Carry Bit to off
        public bool CLC()
        {
            SetStatusBit(CBit, false);

            return false;
        }
        /// CLD : CLear Decimal mode
        public bool CLD()
        {
            SetStatusBit(DBit, false);

            return false;
        }
        /// CLI : CLear Interrupt disable bit
        public bool CLI()
        {
            SetStatusBit(IBit, false);

            return false;
        }
        /// CLV : CLear oVerflow flag
        public bool CLV()
        {
            SetStatusBit(VBit, false);

            return false;
        }
        /// CMP : CoMPare memory and accumulator
        public bool CMP()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(A - currentWorkingValue);
            SetStatusBit(CBit, A >= currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// CPX : ComPare memory and X register
        public bool CPX()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(X - currentWorkingValue);
            SetStatusBit(CBit, X >= currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());

            return false;
        }
        /// CPY : ComPare memory and Y register
        public bool CPY()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(Y - currentWorkingValue);
            SetStatusBit(CBit, Y >= currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());

            return false;
        }
        /// DEC : DECrement memory or accumulate by one
        public bool DEC()
        {
            byte temp = (byte)(fetchCurrentWorkingValue() - 1);
            if (opCodeLookup[currentOpCode].Address == IMA)
            {
                A = temp;
            }
            else
            {
                _bus.write(currentAbsoluteAddress, temp);
            }

            SetStatusBit(ZBit, temp.IsZero());
            SetStatusBit(NBit, temp.IsNegative());

            return false;
        }
        /// DEX : DEcrement X by one
        public bool DEX()
        {
            X--;
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            return false;
        }
        /// DEY : DEcrement Y by one
        public bool DEY()
        {
            Y--;
            SetStatusBit(ZBit, Y.IsZero());
            SetStatusBit(NBit, Y.IsNegative());

            return false;
        }
        /// EOR : "Exclusive OR" memory with accumulate
        public bool EOR()
        {
            fetchCurrentWorkingValue();
            A = (byte)(A ^ currentWorkingValue);
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// INC : INCrement memory or accumulate by one
        public bool INC()
        {
            byte temp = (byte)(fetchCurrentWorkingValue() + 1);
            if (opCodeLookup[currentOpCode].Address == IMA)
                A = temp;
            else
                _bus.write(currentAbsoluteAddress, temp);

            SetStatusBit(ZBit, temp.IsZero());
            SetStatusBit(NBit, temp.IsNegative());

            return false;
        }
        /// INX : INcrement X register by one
        public bool INX()
        {
            X++;
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            return false;
        }
        /// INY : INcrement Y register by one
        public bool INY()
        {
            Y++;
            SetStatusBit(ZBit, Y.IsZero());
            SetStatusBit(NBit, Y.IsNegative());

            return false;
        }
        /// JMP : JuMP to new location
        public bool JMP()
        {
            PC = currentAbsoluteAddress;

            return false;
        }
        /// JSR : Jump  to  new  location  Saving  Return  (Jump  to SubRoutine)
        public bool JSR()
        {
            PC--; // point PC at JSR instruction since RTS will move the PC forward on return
            PushProgramCounterToStack();

            PC = currentAbsoluteAddress;
            return false;
        }
        /// LDA : LoaD Accumulator with memory
        public bool LDA()
        {
            A = fetchCurrentWorkingValue();
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// LDX : LoaD the X register with memory
        public bool LDX()
        {
            X = fetchCurrentWorkingValue();
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// LDY : LoaD the Y register with memory
        public bool LDY()
        {
            Y = fetchCurrentWorkingValue();
            SetStatusBit(ZBit, Y.IsZero());
            SetStatusBit(NBit, Y.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// LSR : Logical    Shift    one    bit    Right    memory    or accumulator
        public bool LSR()
        {
            fetchCurrentWorkingValue();
            SetStatusBit(CBit, (currentWorkingValue & 0x0001) != 0);
            ushort temp = (byte)(currentWorkingValue >> 1);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());
            if (opCodeLookup[currentOpCode].Address == IMA)
                A = temp.ToByte();
            else
                _bus.write(currentAbsoluteAddress, temp.ToByte());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// NOP : No OPeration
        public bool NOP()
        {
            // some unused codes are different sized bytes
            if (currentOpCode == 0x02
                || currentOpCode == 0x22
                || currentOpCode == 0x42
                || currentOpCode == 0x62
                || currentOpCode == 0x82
                || currentOpCode == 0xC2
                || currentOpCode == 0xE2
                || currentOpCode == 0x44
                || currentOpCode == 0x54
                || currentOpCode == 0xD4
                || currentOpCode == 0xF4)
            {
                PC++;
            }

            if (currentOpCode == 0x5C
                || currentOpCode == 0xDC
                || currentOpCode == 0xFC)
            {
                PC++;
                PC++;
            }

            return false;
        }
        /// ORA : "OR" memory with Accumulator
        public bool ORA()
        {
            fetchCurrentWorkingValue();
            A = (byte)(A | currentWorkingValue);
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// PHA : PusH Accumulator on stack
        public bool PHA()
        {
            PushByteToStack(A);

            return false;
        }
        /// PHP : PusH Processor status on stack
        public bool PHP()
        {
            PushByteToStack((byte)(P | BBit | UBit));
            SetStatusBit(BBit, false);
            SetStatusBit(UBit, false);

            return false;
        }
        /// PHX : PusH X register on stack
        public bool PHX()
        {
            PushByteToStack(X);

            return false;
        }
        /// PHY : PusH Y register on stack
        public bool PHY()
        {
            PushByteToStack(Y);

            return false;
        }
        /// PLA : PuLl Accumulator from stack
        public bool PLA()
        {
            A = PullByteFromStack();
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            return false;
        }
        /// PLP : PuLl Processor status from stack
        public bool PLP()
        {
            P = PullByteFromStack();
            SetStatusBit(UBit, true);

            return false;
        }
        /// PLX : PuLl X register from stack
        public bool PLX()
        {
            X = PullByteFromStack();
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            return false;
        }
        /// PLY : PuLl Y register from stack
        public bool PLY()
        {
            Y = PullByteFromStack();
            SetStatusBit(ZBit, Y.IsZero());
            SetStatusBit(NBit, Y.IsNegative());

            return false;
        }
        /// RMB : Reset Memory Bit
        public bool RMB0()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit0(false));

            return false;
        }
        public bool RMB1()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit1(false));

            return false;
        }
        public bool RMB2()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit2(false));

            return false;
        }
        public bool RMB3()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit3(false));

            return false;
        }
        public bool RMB4()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit4(false));

            return false;
        }
        public bool RMB5()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit5(false));

            return false;
        }
        public bool RMB6()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit6(false));

            return false;
        }
        public bool RMB7()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit7(false));

            return false;
        }
        /// SMB : Set Memory Bit
        public bool SMB0()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit0(true));

            return false;
        }
        public bool SMB1()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit1(true));

            return false;
        }
        public bool SMB2()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit2(true));

            return false;
        }
        public bool SMB3()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit3(true));

            return false;
        }
        public bool SMB4()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit4(true));

            return false;
        }
        public bool SMB5()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit5(true));

            return false;
        }
        public bool SMB6()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit6(true));

            return false;
        }
        public bool SMB7()
        {
            fetchCurrentWorkingValue();
            _bus.write(currentAbsoluteAddress, currentWorkingValue.SetBit7(true));

            return false;
        }
        /// ROL : ROtate one bit Left memory or accumulator
        public bool ROL()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)((ushort)(currentWorkingValue << 1) | GetStatusBit(CBit));
            SetStatusBit(CBit, (temp & 0xFF00) != 0);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());
            if (opCodeLookup[currentOpCode].Address == IMA)
                A = temp.ToByte();
            else
                _bus.write(currentAbsoluteAddress, temp.ToByte());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// ROR : ROtate one bit Right memory or accumulator
        public bool ROR()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)((ushort)(GetStatusBit(CBit) << 7) | (currentWorkingValue >> 1));
            SetStatusBit(CBit, (currentWorkingValue & 0x01) != 0);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            SetStatusBit(NBit, temp.ToByte().IsNegative());
            if (opCodeLookup[currentOpCode].Address == IMA)
                A = temp.ToByte();
            else
                _bus.write(currentAbsoluteAddress, temp.ToByte());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// RTI : ReTurn from Interrupt
        public bool RTI()
        {
            P = PullByteFromStack();
            P &= (byte)~BBit;
            P &= (byte)~UBit;

            PC = PullAddressFromStack();

            return false;
        }
        /// RTS : ReTurn from Subroutine
        public bool RTS()
        {
            PC = PullAddressFromStack();
            PC++;

            return false;
        }
        /// SBC : SuBtract memory from accumulator withborrow (Carry bit)
        public bool SBC()
        {
            fetchCurrentWorkingValue();
            var a = A;
            if (GetStatusBit(DBit) == 1)
            {
                cycles++; // decimal mode adds a cycle

                uint tmp = (uint)(A - currentWorkingValue - (GetStatusBit(CBit) == 1 ? 0 : 1));
                if (((A & 0x0F) - (GetStatusBit(CBit) == 1 ? 0 : 1)) < (currentWorkingValue & 0x0F))
                {
                    tmp -= 6;
                }

                if (tmp > 0x99)
                {
                    // wrap negative numbers around
                    tmp -= 0x60;
                }

                SetStatusBit(CBit, (tmp < 0x100));
                A = tmp.ToByte();
            }
            else
            {
                ushort value = (ushort)(currentWorkingValue ^ 0x00FF);

                ushort temp = (ushort)(A + value + GetStatusBit(CBit));
                SetStatusBit(CBit, (temp & 0xFF00) != 0);
                A = temp.ToByte();
            }

            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());
            SetStatusBit(VBit, ((a ^ A) & ~(currentWorkingValue ^ A)).IsNegative());

            // this operation is senstive to a page boundary being crossed
            return true;
        }
        /// SEC : SEt Carry
        public bool SEC()
        {
            SetStatusBit(CBit, true);

            return false;
        }
        /// SED : SEt Decimal mode
        public bool SED()
        {
            SetStatusBit(DBit, true);

            return false;
        }
        /// SEI : SEt Interrupt disable status
        public bool SEI()
        {
            SetStatusBit(IBit, true);

            return false;
        }
        /// STA : STore Accumulator in memory
        public bool STA()
        {
            _bus.write(currentAbsoluteAddress, A);

            return false;
        }
        /// STP : SToP mode
        public bool STP()
        {
            isStopped = true;

            return false;
        }
        /// STX : STore the X register in memory
        public bool STX()
        {
            _bus.write(currentAbsoluteAddress, X);

            return false;
        }
        /// STY : STore the Y register in memory
        public bool STY()
        {
            _bus.write(currentAbsoluteAddress, Y);

            return false;
        }
        /// STZ : STore Zero in memory
        public bool STZ()
        {
            _bus.write(currentAbsoluteAddress, 0x00);

            return false;
        }
        /// TAX : Transfer the Accumulator to the X register
        public bool TAX()
        {
            X = A;
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            return false;
        }
        /// TAY : Transfer the Accumulator to the Y register
        public bool TAY()
        {
            Y = A;
            SetStatusBit(ZBit, Y.IsZero());
            SetStatusBit(NBit, Y.IsNegative());

            return false;
        }
        /// TRB : Test and Reset memory Bit
        public bool TRB()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(A & currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());
            _bus.write(currentAbsoluteAddress, (byte)(currentWorkingValue & (A ^ 0xFF)));

            return false;
        }
        /// TSB : Test and Set memory Bit
        public bool TSB()
        {
            fetchCurrentWorkingValue();
            ushort temp = (ushort)(A & currentWorkingValue);
            SetStatusBit(ZBit, temp.ToByte().IsZero());

            _bus.write(currentAbsoluteAddress, (byte)(currentWorkingValue | A));

            return false;
        }
        /// TSX : Transfer the Stack pointer to the X register
        public bool TSX()
        {
            X = S;
            SetStatusBit(ZBit, X.IsZero());
            SetStatusBit(NBit, X.IsNegative());

            return false;
        }
        /// TXA : Transferthe X register to the Accumulator
        public bool TXA()
        {
            A = X;
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            return false;
        }
        /// TXS : Transfer the X register to the Stack pointer register
        public bool TXS()
        {
            S = X;

            return false;
        }
        /// TYA : Transfer Y register to the Accumulator
        public bool TYA()
        {
            A = Y;
            SetStatusBit(ZBit, A.IsZero());
            SetStatusBit(NBit, A.IsNegative());

            return false;
        }
        /// WAI : WAit for Interrupt
        public bool WAI()
        {
            isWaiting = true;
            SetStatusBit(BBit, true);

            return false;
        }
        #endregion

        #region Addressing Functions
        /// -+---- ADDRESSING MODES ----+-
        /// Absolute -> the full 16-bits are givien
        /// Indexed -> the address get's offset by something
        /// Indirect -> the address is a pointer to where the address is stored
        /// Related -> for branching, offset from teh PC
        /// Implied -> addressing is implcilit for the specific operation (either uses the registers or the stack pointer)
        /// Zero Page -> points to address in page zero (a high-byte of 0x00 is assumed)
        /// 
        /// These return "true" if they cross a page boundary and have potential to add an extra cycle (depending on the operation in context)
        /// 
        /// -+---- ADDRESSING MODES ----+-
        /// 
        /// Absolute a : read 16-bit address in little-endian ( i.e Low byte then High byte )
        public bool ABS()
        {
            ushort low = _bus.read(PC);
            PC++;
            ushort high = _bus.read(PC);
            PC++;

            currentAbsoluteAddress = (ushort)((high << 8) | low);

            return false;
        }
        /// Absolute Indexed Indirect (A, X) : read the 16-bit address + register X to get the location of a pointer, use the vector of the pointer
        public bool IAB()
        {
            ushort pointerLow = _bus.read(PC);
            PC++;
            ushort pointerHigh = _bus.read(PC);
            PC++;

            ushort pointer = (ushort)(((pointerHigh << 8) | pointerLow) + X);
            currentAbsoluteAddress = (ushort)((_bus.read((ushort)(pointer + 1)) << 8) | _bus.read((ushort)(pointer + 0)));

            return false;
        }
        /// Absolute Indexed With X - A,X : read the 16-bit address and add register X (note crossing the page boundary adds a cycle)
        public bool ABX()
        {
            ushort low = _bus.read(PC);
            PC++;
            ushort high = _bus.read(PC);
            PC++;

            currentAbsoluteAddress = (ushort)((high << 8) | low);
            currentAbsoluteAddress += X;

            // return true if crossed page boundary, this mode has potential to add an extra cycle
            return (currentAbsoluteAddress & 0xFF00) != (high << 8);
        }
        /// Absolute Indexed With Y - A, Y :  read the 16-bit address and add register Y (note crossing the page boundary adds a cycle)
        public bool ABY()
        {
            ushort low = _bus.read(PC);
            PC++;
            ushort high = _bus.read(PC);
            PC++;

            currentAbsoluteAddress = (ushort)((high << 8) | low);
            currentAbsoluteAddress += Y;

            // return true if crossed page boundary, this mode has potential to add an extra cycle
            return (currentAbsoluteAddress & 0xFF00) != (high << 8);
        }
        /// Absolute Indirect (A) : read the 16-bit address to get the location of a pointer, use the vector of the pointer
        public bool IND()
        {
            ushort pointerLow = _bus.read(PC);
            PC++;
            ushort pointerHigh = _bus.read(PC);
            PC++;

            ushort pointer = (ushort)((pointerHigh << 8) | pointerLow);
            currentAbsoluteAddress = (ushort)((_bus.read((ushort)(pointer + 1)) << 8) | _bus.read((ushort)(pointer + 0)));

            return false;
        }
        /// Accumulator A : the address is implicity assumed to point at the A register
        public bool IMA()
        {
            currentWorkingValue = A; // just have to make sure 'fetching' data pulls from the A register
            return false;
        }
        /// Immediate Addressing # : the byte immediately after the opcode is the appropriate address
        public bool IMM()
        {
            currentAbsoluteAddress = PC++;
            return false;
        }
        /// Implied I : the address is implicitly defined by the instruction (we don't gotsta doing anything)
        public bool IMP()
        {
            return false;
        }
        /// Program Counter Relative R : used for branch operations, allows setting the address relative -128 to +127 bytes of the Program Counter
        public bool REL()
        {
            currentRelativeAddress = _bus.read(PC);
            PC++;
            if (currentRelativeAddress.IsNegative())
                currentRelativeAddress |= 0xFF00; // adjust the high so the PC will wrap around / actually move backwards
            return false;
        }
        /// Stack S :  the address used will be based off of the stack pointer
        public bool IMS()
        {
            return false; // this is generally only used by teh Stack operations so those implemenations can just use the stack pointer directly
        }
        /// Zero Page ZP : read low byte that points to an address within the zero-th page (i.e. 0x0000 - 0x00FF)
        public bool ZP0()
        {
            currentAbsoluteAddress = _bus.read(PC);
            PC++;
            currentAbsoluteAddress &= 0x00FF; // paranoid clamping to ensure address stay in the zero-th page
            return false;
        }
        /// Zero Page Indexed Indirect (ZP, X) : read in a low byte of an address in the zero page and offset by X register, this pointer is a pointer to the real address
        public bool ZIX()
        {
            ushort pointerBaseLowByte = _bus.read(PC);
            PC++;

            // Have to do it like this so we can keep this pointing to an address within the Zero Page
            ushort pointerToLowByte = (pointerBaseLowByte + X).ToByte();
            ushort pointerToHighByte = (pointerBaseLowByte + X + 1).ToByte();

            ushort low = _bus.read(pointerToLowByte);
            ushort high = _bus.read(pointerToHighByte);

            currentAbsoluteAddress = (ushort)((high << 8) | low);

            return false;
        }
        /// Zero Page Indexed With X - ZP, X : read low byte and offset by X Register to get an address within the zero-th page (i.e. 0x0000 - 0x00FF)
        public bool ZPX()
        {
            currentAbsoluteAddress = (ushort)(_bus.read(PC) + X);
            PC++;
            currentAbsoluteAddress &= 0x00FF; // paranoid clamping to ensure address stay in the zero-th page
            return false;
        }
        /// Zero Page Indexed With Y - ZP, Y : read low byte and offset by Y Register to get an address within the zero-th page (i.e. 0x0000 - 0x00FF)
        public bool ZPY()
        {
            currentAbsoluteAddress = (ushort)(_bus.read(PC) + Y);
            PC++;
            currentAbsoluteAddress &= 0x00FF; // paranoid clamping to ensure address stay in the zero-th page
            return false;
        }
        /// Zero Page Indirect (ZP) : read loy byte to read in a pointer from the zero-th page
        public bool ZPI()
        {
            ushort pointerLowByte = _bus.read(PC);
            PC++;

            // Claming to ensure address is read from within the zero-th page
            ushort low = _bus.read(pointerLowByte.ToByte());
            ushort high = _bus.read((pointerLowByte + 1).ToByte());

            currentAbsoluteAddress = (ushort)((high << 8) | low);

            // return true if crossed page boundary, this mode has potential to add an extra cycle
            return (currentAbsoluteAddress & 0xFF00) != (high << 8);
        }
        /// Zero Page Indirect Indexed With Y (ZP), Y : read low byte to read in a pointer from the zero-th page, offset the address the pointer points to by Y Register
        public bool ZIY()
        {
            ushort pointerLowByte = _bus.read(PC);
            PC++;

            // Claming to ensure address is read from within the zero-th page
            ushort low = _bus.read(pointerLowByte.ToByte());
            ushort high = _bus.read((pointerLowByte + 1).ToByte());

            currentAbsoluteAddress = (ushort)((high << 8) | low);
            currentAbsoluteAddress += Y;

            // return true if crossed page boundary, this mode has potential to add an extra cycle
            return (currentAbsoluteAddress & 0xFF00) != (high << 8);
        }
        #endregion

        #region Helper Functions
        /// Helper Functions
        protected void PushProgramCounterToStack()
        {
            // Push Program Count into Stack little-endian style
            PushByteToStack((PC >> 8).ToByte());
            PushByteToStack(PC.ToByte());
        }

        protected void PushByteToStack(byte theByte)
        {
            _bus.write((ushort)(0x0100 + S), theByte);
            S--;
        }

        protected ushort PullAddressFromStack()
        {
            ushort address = PullByteFromStack();
            address |= (ushort)(PullByteFromStack() << 8);
            return address;
        }

        protected byte PullByteFromStack()
        {
            S++;
            return _bus.read((ushort)(0x0100 + S));
        }

        private void SetStatusBit(byte bit, bool on)
        {
            P = P.SetBit(bit, on);
        }
        public byte GetStatusBit(byte bit)
        {
            return (byte)(P.IsBitOn(bit) ? 0x01 : 0x00);
        }
        #endregion
    }

    public struct Operation
    {
        public string Mnemonic { get; set; }
        public byte OpCode { get; set; }
        public Func<bool> Operate { get; set; }
        public Func<bool> Address { get; set; }
        public int MinimumCycles { get; set; }
        public int Size { get; set; }
    }
}
