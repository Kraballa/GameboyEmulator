using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Input
    {
        private Memory Memory;

        public Input(Memory memory)
        {
            Memory = memory;
            Memory[Memory.IO] = 0b00001111;
            Memory.MemoryAccessCallback.Add(Memory.IO, GetInputCallback);
        }

        private void GetInputCallback(byte written)
        {
            if ((written & 0b100000) != 0) //select button keys
            {
                byte data = 0b00001111;
                if (KInput.CheckPressed(Keys.A))
                    data &= 0b11111110;
                if (KInput.CheckPressed(Keys.B))
                    data &= 0b11111101;
                if (KInput.CheckPressed(Keys.Enter)) //start
                    data &= 0b11111011;
                if (KInput.CheckPressed(Keys.Escape)) //select
                    data &= 0b11110111;
                Memory[Memory.IO] = data;
            }
            else if ((written & 0b10000) != 0) //select direction keys
            {
                byte data = 0b00001111;
                if (KInput.CheckPressed(Keys.Right))
                    data &= 0b11111110;
                if (KInput.CheckPressed(Keys.Left))
                    data &= 0b11111101;
                if (KInput.CheckPressed(Keys.Up))
                    data &= 0b11111011;
                if (KInput.CheckPressed(Keys.Down))
                    data &= 0b11110111;
                Memory[Memory.IO] = data;
            }
        }
    }
}
