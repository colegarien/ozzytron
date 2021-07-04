using SFML.Graphics;
using SFML.Window;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ozzytron
{
    class Program
    {
        #region editor theming
        public static Dictionary<string, Color> colors = new Dictionary<string, Color>
        {
            { "white", new Color(255, 255, 255)},
            { "blue", new Color(0, 0, 187)},
            { "red", new Color(187, 0, 0)},
            { "green", new Color(0, 187, 0)},
            { "black", new Color(0, 0, 0)},
        };
        static Color GetForeground()
        {
            return colors["white"];
        }
        static Color GetBackground()
        {
            return colors["blue"];
        }
        static Color GetHighlightBackground()
        {
            return colors["white"];
        }
        static Color GetHighlightForeground()
        {
            return colors["blue"];
        }
        static Color GetFlagOff()
        {
            return colors["red"];
        }
        static Color GetFlagOn()
        {
            return colors["green"];
        }
        #endregion

        public static uint numberOfColumns = 80;
        public static uint numberOfRows = 40;

        public static uint fontSize = 16;
        public static uint characterWidth = 0;
        public static uint characterHeight = 0;
        static RenderTexture InitializeScreen(Font font)
        {
            fontSize = VideoMode.DesktopMode.Width / numberOfColumns;
            characterWidth = fontSize; // because it's a monospace font is the only reason this math works
            characterHeight = (uint)(font.GetLineSpacing(fontSize)); // because it's a monospace font is the only reason this math works

            return new RenderTexture(numberOfColumns * characterWidth, numberOfRows * characterHeight);
        }

        static void Main(string[] args)
        {
            Font font = new Font("kongtext.ttf");
            RenderTexture screen = InitializeScreen(font);

            RenderWindow window = new RenderWindow(new VideoMode(1000, 500), "Ozzytron");
            window.Closed += (sender, e) =>
            {
                ((Window)sender).Close();
            };
            window.Resized += (sender, e) =>
            {
                window.SetView(new View(new FloatRect(0, 0, e.Width, e.Height)));
            };

            var bus = new Bus();
            bus.ReadProgram("Programs\\simple_addition.ozm");


            int cpuIsTrappedThreshold = 20;
            int repeatedPcCount = 0;
            ushort prevPc = bus._cpu.PC;

            var executionPaused = true;
            var takeStep = false;

            var enterIsDown = false;
            var spaceBarIsDown = false;
            var leftShiftIsDown = false;
            var rightShiftIsDown = false;
            var leftArrowIsDown = false;
            var upArrowIsDown = false;
            var downArrowIsDown = false;

            var PCs = new List<ushort>();
            ushort baseMemoryViewAddress = 0;

            Stopwatch stopWatch = new Stopwatch();
            float deltaTime = 0f;
            while (window.IsOpen)
            {
                deltaTime = stopWatch.ElapsedMilliseconds / 1000f;
                stopWatch.Restart();

                // Event Handling
                window.DispatchEvents();
                #region gather key inputs
                if (!spaceBarIsDown && Keyboard.IsKeyPressed(Keyboard.Key.Space) && window.HasFocus())
                {
                    spaceBarIsDown = true;
                    takeStep = true;
                }
                else if (spaceBarIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.Space))
                {
                    spaceBarIsDown = false;
                }
                if (!leftShiftIsDown && Keyboard.IsKeyPressed(Keyboard.Key.LShift) && window.HasFocus())
                {
                    leftShiftIsDown = true;
                }
                else if (leftShiftIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.LShift))
                {
                    leftShiftIsDown = false;
                }
                if (!rightShiftIsDown && Keyboard.IsKeyPressed(Keyboard.Key.RShift) && window.HasFocus())
                {
                    rightShiftIsDown = true;
                }
                else if (rightShiftIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.RShift))
                {
                    rightShiftIsDown = false;
                }

                if (!enterIsDown && Keyboard.IsKeyPressed(Keyboard.Key.Enter) && window.HasFocus())
                {
                    enterIsDown = true;
                    executionPaused = !executionPaused;
                }
                else if (enterIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.Enter))
                {
                    enterIsDown = false;
                }

                if (!leftArrowIsDown && Keyboard.IsKeyPressed(Keyboard.Key.Left) && window.HasFocus())
                {
                    leftArrowIsDown = true;
                    // do thing
                    if (PCs.Count > 0)
                    {
                        bus._cpu.PC = PCs[PCs.Count - 1];
                        PCs.RemoveAt(PCs.Count - 1);
                    }
                }
                else if (leftArrowIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.Left))
                {
                    leftArrowIsDown = false;
                }

                if (!upArrowIsDown && Keyboard.IsKeyPressed(Keyboard.Key.Up) && window.HasFocus())
                {
                    upArrowIsDown = true;
                    // do thing
                    var increment = 0x10;
                    if (rightShiftIsDown)
                        increment = 0x1000;
                    else if(leftShiftIsDown)
                        increment = 0x100;
                    baseMemoryViewAddress = (ushort)(baseMemoryViewAddress - increment);
                }
                else if (upArrowIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.Up))
                {
                    upArrowIsDown = false;
                }

                if (!downArrowIsDown && Keyboard.IsKeyPressed(Keyboard.Key.Down) && window.HasFocus())
                {
                    downArrowIsDown = true;
                    // do thing
                    var increment = 0x10;
                    if (rightShiftIsDown)
                        increment = 0x1000;
                    else if (leftShiftIsDown)
                        increment = 0x100;
                    baseMemoryViewAddress = (ushort)(baseMemoryViewAddress + increment);
                }
                else if (downArrowIsDown && !Keyboard.IsKeyPressed(Keyboard.Key.Down))
                {
                    downArrowIsDown = false;
                }
                #endregion

                // Update Logic Here
                if (!executionPaused || takeStep)
                {
                    takeStep = false; // to ensure if taking a step, only a single step is taken
                    bus._cpu.step();

                    #region PC Trapped Check
                    // track the PC not moving
                    if (bus._cpu.PC == prevPc)
                    {
                        repeatedPcCount++;
                    }
                    else
                    {
                        repeatedPcCount = 0;
                        prevPc = bus._cpu.PC;
                        PCs.Add(bus._cpu.PC);
                    }
                    if (cpuIsTrappedThreshold <= repeatedPcCount)
                    {
                        // pause execution if CPU is trapped
                        executionPaused = true;
                    }
                    #endregion
                }

                // Draw Code Here
                window.Clear();
                screen.Clear(GetBackground());

                DrawMemory(screen, font, bus, baseMemoryViewAddress, (ushort)(baseMemoryViewAddress + 0x0080));

                DrawProgram(screen, font, bus);
                DrawRegisters(screen, font, bus);

                DrawInstructions(screen, font, executionPaused);

                screen.Display();
                var screenSprite = new Sprite(screen.Texture);
                screenSprite.Scale = new SFML.System.Vector2f(window.Size.X / screenSprite.GetLocalBounds().Width, window.Size.Y / screenSprite.GetLocalBounds().Height);

                window.Draw(screenSprite);
                window.Display();

                // On Escape Pressed, quit
                if (window.HasFocus() && Keyboard.IsKeyPressed(Keyboard.Key.Escape))
                    window.Close();
            }
        }

        private static void DrawString(RenderTarget target, Font font, uint column, uint row, string text, Color color, Color? back = null)
        {
            var textObject = new Text(text, font);
            textObject.Position = new SFML.System.Vector2f(column * characterWidth, row * characterHeight);
            textObject.CharacterSize = fontSize;
            textObject.FillColor = color;

            if (back != null)
            {
                var lines = text.Split("\r\n");
                for (var i = 0; i < lines.Length; i++)
                {
                    var r = new RectangleShape(new SFML.System.Vector2f(lines[i].Length * characterWidth, characterHeight));
                    r.Position = new SFML.System.Vector2f(textObject.Position.X, textObject.Position.Y + (i * characterHeight));
                    r.FillColor = (Color)back;
                    target.Draw(r);
                }
            }

            target.Draw(textObject);
        }

        private static void DrawProgram(RenderTarget target, Font font, Bus bus)
        {
            uint x = 0;
            uint y = 12;

            var currentAddress = bus._cpu.PC;
            var stopAddress = (ushort)(bus._cpu.PC + 16);
            DrawString(target, font, x, y++, "--Program--", GetForeground());

            // dissamble this program space
            while (currentAddress <= stopAddress)
            {
                var isCurrentInstruction = currentAddress == bus._cpu.PC;
                var line = "$" + currentAddress.ToString("X4") + "  | ";

                var opcode = bus.read(currentAddress, true);
                var operation = bus._cpu.opCodeLookup[opcode];
                ushort lowByte = 0x00;
                ushort highByte = 0x00;
                if (operation.Size > 1)
                    lowByte = bus.read((ushort)(currentAddress + 1), true);
                if (operation.Size > 2)
                    highByte = bus.read((ushort)(currentAddress + 2), true);
                currentAddress += operation.Size.ToByte(); // move disassembler past current instruction

                line += operation.Mnemonic + " ";

                if (operation.Mnemonic.Substring(0, 3) == "BBR" || operation.Mnemonic.Substring(0, 3) == "BBS")
                {
                    // BBR[0-7] and BBS[0-7] are 4 Byte Instructions that use ZP0 and REL for doing the branch
                    var relativeAdjustment = highByte;
                    if (relativeAdjustment.IsNegative())
                        relativeAdjustment |= 0xFF00;
                    line += "$" + lowByte.ToString("X2") + " {ZP0}, $" + highByte.ToString("X2") + " [$" + ((ushort)(currentAddress + relativeAdjustment)).ToString("X4") + "] {REL}";
                }
                else if (operation.Address == bus._cpu.REL)
                {
                    var relativeAdjustment = lowByte;
                    if (relativeAdjustment.IsNegative())
                        relativeAdjustment |= 0xFF00;
                    line += "$" + lowByte.ToString("X2") + " [$" + ((ushort)(currentAddress + relativeAdjustment)).ToString("X4") + "] {REL}";
                }
                else if (operation.Address == bus._cpu.IMP || operation.Address == bus._cpu.IMA || operation.Address == bus._cpu.IMS)
                {
                    line += " {IMP}";
                }
                else if (operation.Address == bus._cpu.IMM)
                {
                    line += "#$" + lowByte.ToString("X2") + " {IMM}";
                }
                else if (operation.Address == bus._cpu.ZP0)
                {
                    line += "$" + lowByte.ToString("X2") + " {ZP0}";
                }
                else if (operation.Address == bus._cpu.ZPX)
                {
                    line += "$" + lowByte.ToString("X2") + ", X {ZPX}";
                }
                else if (operation.Address == bus._cpu.ZPY)
                {
                    line += "$" + lowByte.ToString("X2") + ", Y {ZPY}";
                }
                else if (operation.Address == bus._cpu.ZPI)
                {
                    line += "($00" + lowByte.ToString("X2") + ") {ZPI}";
                }
                else if (operation.Address == bus._cpu.ZIX)
                {
                    line += "($" + lowByte.ToString("X2") + ", X) {ZIX}";
                }
                else if (operation.Address == bus._cpu.ZIY)
                {
                    line += "($" + lowByte.ToString("X2") + "), Y {ZIY}";
                }
                else if (operation.Address == bus._cpu.ABS)
                {
                    line += "$" + ((ushort)(highByte << 8) | lowByte).ToString("X4") + " {ABS}";
                }
                else if (operation.Address == bus._cpu.IAB)
                {
                    line += "($" + ((ushort)(highByte << 8) | lowByte).ToString("X4") + " + X) {IAB}";
                }
                else if (operation.Address == bus._cpu.ABX)
                {
                    line += "$" + ((ushort)(highByte << 8) | lowByte).ToString("X4") + ", X {ABX}";
                }
                else if (operation.Address == bus._cpu.ABY)
                {
                    line += "$" + ((ushort)(highByte << 8) | lowByte).ToString("X4") + ", Y {ABY}";
                }
                else if (operation.Address == bus._cpu.IND)
                {
                    line += "($" + ((ushort)(highByte << 8) | lowByte).ToString("X4") + ") {IND}";
                }

                if (isCurrentInstruction)
                {
                    DrawString(target, font, x, y++, line, GetHighlightForeground(), GetHighlightBackground());
                }
                else
                {
                    DrawString(target, font, x, y++, line, GetForeground());
                }
            }
        }

        private static void DrawRegisters(RenderTarget target, Font font, Bus bus)
        {
            DrawString(target, font, 60, 0, "Status:", GetForeground());
            DrawString(target, font, 62, 1, "N", bus._cpu.GetStatusBit(bus._cpu.NBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 64, 1, "V", bus._cpu.GetStatusBit(bus._cpu.VBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 66, 1, "_", bus._cpu.GetStatusBit(bus._cpu.UBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 68, 1, "B", bus._cpu.GetStatusBit(bus._cpu.BBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 70, 1, "D", bus._cpu.GetStatusBit(bus._cpu.DBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 72, 1, "I", bus._cpu.GetStatusBit(bus._cpu.IBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 74, 1, "Z", bus._cpu.GetStatusBit(bus._cpu.ZBit) == 0 ? GetFlagOff() : GetFlagOn());
            DrawString(target, font, 76, 1, "C", bus._cpu.GetStatusBit(bus._cpu.CBit) == 0 ? GetFlagOff() : GetFlagOn());

            DrawString(target, font, 60, 3, "A : $" + bus._cpu.A.ToString("X4") + " [" + bus._cpu.A + "]", GetForeground());
            DrawString(target, font, 60, 4, "X : $" + bus._cpu.X.ToString("X4") + " [" + bus._cpu.X + "]", GetForeground());
            DrawString(target, font, 60, 5, "Y : $" + bus._cpu.Y.ToString("X4") + " [" + bus._cpu.Y + "]", GetForeground());
            DrawString(target, font, 60, 6, "SP: $" + bus._cpu.S.ToString("X4"), GetForeground());
            DrawString(target, font, 60, 7, "PC: $" + bus._cpu.PC.ToString("X4"), GetForeground());
        }

        private static void DrawMemory(RenderTarget target, Font font, Bus bus, ushort start, ushort end)
        {
            // different between start and end, shift over 4 bits to get number of rows (0x0080 - 0x0000 is 0x0080, shift over 4 bits to get 0x0008)
            var rows = (((end & 0xFFF0) - (start & 0xFFF0)) >> 4) + 1;
            if (start > end)
                rows += 0x1000; // if wrapping around memory boundary make sure math is good
            DrawRamSegment(target, font, bus, start, rows);
        }

        private static void DrawRamSegment(RenderTarget target, Font font, Bus bus, ushort startAddress, int rows)
        {
            if (rows <= 0)
            {
                return;
            }
            uint x = 0;
            uint y = 0;

            ushort lineBase = (ushort)(startAddress & 0xFFF0);

            DrawString(target, font, x, y++, "MEMORY |  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F |", GetForeground());
            DrawString(target, font, x, y++, "-------+-------------------------------------------------|", GetForeground());

            for (var row = 0; row < rows; row++)
            {
                var containsProgramCounter = false;

                string line = "$" + lineBase.ToString("X4") + "  | ";
                for (var i = 0; i <= 15; i++)
                {
                    line += bus._ram[(ushort)(lineBase + (ushort)i)].ToString("X2") + " ";

                    containsProgramCounter = containsProgramCounter || (lineBase <= bus._cpu.PC && bus._cpu.PC <= (lineBase + i));
                }
                lineBase += 16;
                line += "|";

                if (containsProgramCounter)
                {
                    DrawString(target, font, x, y++, line, GetHighlightForeground(), GetHighlightBackground());
                }
                else
                {
                    DrawString(target, font, x, y++, line, GetForeground());
                }
            }
        }

        private static void DrawInstructions(RenderTarget target, Font font, bool executionPaused)
        {
            DrawString(target, font, 0, numberOfRows - 4, "SPACE BAR to step" + "\r\n" + "ENTER to " + (executionPaused ? "START" : "STOP") + " execution", GetForeground());
        }

    }
}
