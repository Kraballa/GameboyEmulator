using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum LCDCReg
    {
        DisplayEnable = 0b10000000,
        WinTileMapDispSel = 0b01000000,
        WindowDisplayEnable = 0b00100000, //window
        BGWinTileDataSel = 0b00010000,
        BGTileMapDisplaySel = 0b00001000,
        OBJSize = 0b00000100, //0 = 8x8, 1 = 8x16
        OBJDisplayEnable = 0b00000010, //sprites
        BGDisplayEnable = 0b00000001 //backgrounds / tiles
    }

    public enum LCDSReg
    {
        LYCEQLYInterrupt = 0b01000000,
        Mode2OAMInterrupt = 0b00100000,
        Mode1VBlkInterrupt = 0b00010000,
        Mode0HBlkInterrupt = 0b00001000,
        Coincidence = 0b00000100,
        Mode = 0b00000011
    }

    public class Display
    {
        public const ushort LCDC = 0xFF40; //lcd control register
        public const ushort LCDS = 0xFF41; //lcd status register
        public const ushort SCY = 0xFF42; //bg map scroll y
        public const ushort SCX = 0xFF43; //bg map scroll x
        public const ushort LY = 0xFF44; // [0;153], vertical line to which data is transferred.
        public const ushort LYC = 0xFF45;
        public const ushort WY = 0xFF4A; // window y position
        public const ushort WX = 0xFF4B; // window x position - 7
        public const ushort DMA = 0xFF46; // DMA transfer start address
        public const ushort VRAMBank = 0xFF4F; // select either VRAM Bank 0 or 1

        private const int MODE2BOUNDS = 456 - 80;
        private const int MODE3BOUNDS = 456 - 80 - 172;

        //need access to memory to write to and read from
        private Memory Memory;
        private int ScanlineCounter = 0;


        public Display(Memory memory)
        {
            Memory = memory;
        }

        public int GetMode()
        {
            return Memory[LCDS] & (byte)LCDSReg.Mode;
        }

        public void UpdateGraphics(int cycles)
        {
            SetLCDStatus();

            if ((Memory[LCDC] & (byte)LCDCReg.DisplayEnable) != 0)
            {
                ScanlineCounter -= cycles;
            }
            else
            {
                return;
            }

            if (ScanlineCounter <= 0)
            {
                Memory[LY]++;
                ScanlineCounter = 456;

                if (Memory[LY] == 144)
                    CPU.Instance.RequestInterrupt(InterruptType.VBLANK);

                if (Memory[LY] > 153)
                {
                    Memory[LY] = 0;
                }
                else if (Memory[LY] < 144)
                {
                    RenderScanline();
                }
            }
        }

        private void SetLCDStatus()
        {
            if ((Memory[LCDC] & (byte)LCDCReg.DisplayEnable) == 0)
            {
                ScanlineCounter = 456;
                Memory[LY] = 0;
                Memory[LCDS] = (byte)((Memory[LCDS] & 0b11111100) + 1);
                return;
            }

            byte currentLine = Memory[LY];
            byte currentMode = (byte)(Memory[LCDS] & (byte)LCDSReg.Mode);
            byte newMode;
            bool needInterrupt = false;

            if (currentLine >= 144)
            {
                SetNewMode(newMode = 1);
                needInterrupt = (Memory[LCDS] & (byte)LCDSReg.Mode1VBlkInterrupt) != 0;
            }
            else if (ScanlineCounter >= MODE2BOUNDS)
            {
                SetNewMode(newMode = 2);
                needInterrupt = (Memory[LCDS] & (byte)LCDSReg.Mode2OAMInterrupt) != 0;
            }
            else if (ScanlineCounter >= MODE3BOUNDS)
            {
                SetNewMode(newMode = 3);
            }
            else
            {
                SetNewMode(newMode = 0);
                needInterrupt = (Memory[LCDS] & (byte)LCDSReg.Mode0HBlkInterrupt) != 0;
            }

            if (needInterrupt && (newMode != currentMode))
            {
                CPU.Instance.RequestInterrupt(InterruptType.LCD);
            }

            if (Memory[LY] == Memory[LYC])
            {
                Memory[LCDS] |= (byte)LCDSReg.Coincidence;
                if ((Memory[LCDS] & (byte)LCDSReg.LYCEQLYInterrupt) != 0)
                    CPU.Instance.RequestInterrupt(InterruptType.LCD);
            }
            else
            {
                Memory[LCDS] &= 0b11111011;
            }
        }

        private void RenderScanline()
        {
            //Controller.Instance.GraphicsDevice.SetRenderTarget(RenderTargets.ScreenBuffer);
            Render.Begin();
            Memory[LCDC] = 0xFF;
            if ((Memory[LCDC] & (byte)LCDCReg.BGDisplayEnable) != 0)
            {
                RenderTiles();
            }

            if ((Memory[LCDC] & (byte)LCDCReg.OBJDisplayEnable) != 0)
            {
                RenderSprites();
            }
            Render.End();
        }

        private void RenderTiles()
        {
            ushort tileData;
            ushort backgroundMemory;
            bool unsig = true;
            bool usingWindow = false;

            byte winX = (byte)(Memory[WX] - 7);
            byte winY = Memory[WY];
            byte scrollX = Memory[SCX];
            byte scrollY = Memory[SCY];

            if ((Memory[LCDC] & (byte)LCDCReg.WindowDisplayEnable) != 0)
            {
                //draw window
                if (winY <= Memory[LY])
                    usingWindow = true;
            }

            if ((Memory[LCDC] & (byte)LCDCReg.WinTileMapDispSel) != 0)
            {
                tileData = Memory.VRAM;
            }
            else
            {
                tileData = 0x9C00;
                unsig = true;
            }

            if (!usingWindow)
            {
                if ((Memory[LCDC] & (byte)LCDCReg.BGTileMapDisplaySel) != 0)
                {
                    backgroundMemory = 0x9C00;
                }
                else
                {
                    backgroundMemory = 0x9800;
                }
            }
            else
            {
                if ((Memory[LCDC] & (byte)LCDCReg.WinTileMapDispSel) != 0)
                {
                    backgroundMemory = 0x9C00;
                }
                else
                {
                    backgroundMemory = 0x9800;
                }
            }

            byte yPos;
            if (!usingWindow)
                yPos = (byte)(scrollY + Memory[LY]);
            else
                yPos = (byte)(Memory[LY] - winY);
            ushort tileRow = (ushort)((yPos / 8) * 32);

            for (int pixelX = 0; pixelX < 160; pixelX++)
            {
                int pixelY = Memory[LY];
                //make sure we're not trying to render a pixel outside the window
                if (pixelY < 0 || pixelY > 143 || pixelX < 0 || pixelX > 159)
                    continue;

                byte xPos;
                if (usingWindow && pixelX >= winX)
                {
                    xPos = (byte)(pixelX - winX);
                }
                else
                {
                    xPos = (byte)(pixelX + scrollX);
                }
                ushort tileColumn = (ushort)(xPos / 8);
                ushort tileAddress = (ushort)(backgroundMemory + tileRow + tileColumn);
                ushort tileId = Memory[tileAddress]; //TODO figure out the whole signed unsigned byte thing

                ushort tileLocation = tileData;
                if (unsig)
                    tileLocation += (ushort)(tileId * 16);
                else
                    tileLocation += (ushort)((tileId + 128) * 16);

                byte line = (byte)((yPos % 8) * 2);
                byte data1 = Memory[(ushort)(tileLocation + line)];
                byte data2 = Memory[(ushort)(tileLocation + line + 1)];

                RenderTile(xPos, pixelX, pixelY, data1, data2);
            }
        }

        private void RenderTile(byte xPos, int pixelX, int pixelY, byte data1, byte data2)
        {
            int colourBit = xPos % 8;
            colourBit -= 7;
            colourBit *= -1;

            int colourNum = (data2 & (1 << colourBit)) >> colourBit;
            colourNum <<= 1;
            colourNum |= (data1 & (1 << colourBit)) >> colourBit;
            Color color = DecodeColor(colourNum, 0xFF47);

            Render.Point(new Vector2(pixelX, pixelY), color);
        }

        public void RenderDebugTiles(int x, int y)
        {
            for (int i = 0; i < 192; i++)
            {
                byte[] data = new byte[16];
                for (int j = 0; j < 16; j++)
                {
                    data[j] = Memory[(ushort)(i + j + 0x8000)];
                }
                RenderDebugTile(i % 8 + x, i / 8 + y, data);
            }
        }

        //interpret 16 bytes of data as a tile
        private void RenderDebugTile(int tileX, int tileY, byte[] data)
        {
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int index = (data[y * 2] & (1 << x)) << 1;
                    index |= data[y * 2 + 1] & (1 << x);
                    Color color = DecodeColor(index, 0xFF47);
                    Render.Point(new Vector2(tileX * 8 + x, tileY * 8 + y), color);
                }
            }
        }

        private void RenderSprites()
        {
            bool wideSprites = (Memory[LCDC] & (byte)LCDCReg.OBJSize) != 0;

            for (int sprite = 0; sprite < 40; sprite++)
            {
                byte index = (byte)(sprite * 4);
                byte sprX = (byte)(Memory[(ushort)(Memory.OAM + index + 1)] - 8);
                byte sprY = (byte)(Memory[(ushort)(Memory.OAM + index)] - 16);
                byte tileLocation = Memory[(ushort)(Memory.OAM + index + 2)];
                byte attributes = Memory[(ushort)(Memory.OAM + index + 3)];

                bool xFlip = (attributes & (1 << 5)) != 0;
                bool yFlip = (attributes & (1 << 6)) != 0;

                int pixelY = Memory[LY];

                int ySize = wideSprites ? 16 : 8;
                if ((pixelY >= sprY) && (pixelY < (sprY + ySize)))
                {
                    int line = pixelY - sprY;
                    if (yFlip)
                    {
                        line -= ySize;
                        line *= -1;
                    }

                    line *= 2;
                    ushort memAddress = (ushort)((Memory.VRAM + (tileLocation * 16)) + line);
                    byte data1 = Memory[memAddress];
                    byte data2 = Memory[(ushort)(memAddress + 1)];

                    for (int tilePixel = 7; tilePixel >= 0; tilePixel--)
                    {
                        int colorBit = tilePixel;
                        if (xFlip)
                        {
                            colorBit -= 7;
                            colorBit *= -1;
                        }

                        int colorNum = (data2 & (1 << colorBit)) >> colorBit;
                        colorNum <<= 1;
                        colorNum |= (data1 & (1 << colorBit)) >> colorBit;

                        ushort colorAddress;
                        if ((attributes & (1 << 4)) != 0)
                            colorAddress = 0xFF49;
                        else
                            colorAddress = 0xFF48;

                        Color color = DecodeColor(colorNum, colorAddress);
                        if (color == Color.White) //white is transparent
                            continue;

                        int xPix = -tilePixel;
                        xPix += 7;

                        int pixelX = sprX + xPix;
                        //make sure we're not trying to render a pixel outside the window
                        if (pixelY < 0 || pixelY > 143 || pixelX < 0 || pixelX > 159)
                            continue;
                        Render.Point(new Vector2(pixelX, pixelY), color);
                    }
                }
            }
        }

        /// <summary>
        /// the gameboy has 4 colors. white is also used for transparency. sprites have a 1 byte palette of which 2 bits each denote one colour.
        /// we have to decode the palette using the index and then map it onto an actual color.
        /// </summary>
        private Color DecodeColor(int num, ushort address)
        {
            byte palette = Memory[address];
            int hi = 0;
            int lo = 0;

            switch (num)
            {
                case 0: hi = 1; lo = 0; break;
                case 1: hi = 3; lo = 2; break;
                case 2: hi = 5; lo = 4; break;
                case 3: hi = 7; lo = 6; break;
            }

            int color;
            color = (palette & (1 << hi)) >> hi;
            color <<= 1;
            color |= (palette & (1 << lo)) >> lo;

            return ColorFromIndex(color);
        }

        private Color ColorFromIndex(int color)
        {
            switch (color)
            {
                case 0: return Palette.White;
                case 1: return Palette.LightGray;
                case 2: return Palette.DarkGray;
                default: return Palette.Black;
            }
        }

        private void SetNewMode(byte mode)
        {
            Memory[LCDS] &= 0b11111100;
            Memory[LCDS] |= mode;
        }
    }
}
