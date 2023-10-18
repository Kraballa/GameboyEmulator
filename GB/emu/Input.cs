using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Input
    {
        private MMU Memory;

        public Input()
        {
            Memory = CPU.Instance.Memory;
            Memory.Mem[MMU.IO] = 0b00001111;
        }

        public void GetInputCallback(byte written)
        {
            Console.WriteLine("reading input...");
            byte data = (byte)(0x0F | written);
            if ((written & 0b100000) == 0) //select button keys
            {
                data = 0b00001111;
                if (KInput.CheckPressed(Keys.A))
                    data &= 0b11111110;
                if (KInput.CheckPressed(Keys.B))
                    data &= 0b11111101;
                if (KInput.CheckPressed(Keys.Enter)) //start
                    data &= 0b11111011;
                if (KInput.CheckPressed(Keys.Escape)) //select
                    data &= 0b11110111;
            }
            else if ((written & 0b10000) == 0) //select direction keys
            {
                data = 0b00001111;
                if (KInput.CheckPressed(Keys.Right))
                    data &= 0b11111110;
                if (KInput.CheckPressed(Keys.Left))
                    data &= 0b11111101;
                if (KInput.CheckPressed(Keys.Up))
                    data &= 0b11111011;
                if (KInput.CheckPressed(Keys.Down))
                    data &= 0b11110111;
            }
            //one or more input bits changed from high to low
            if ((Memory[MMU.IO] & data & 0x0F) < (Memory[MMU.IO] & 0x0F))
            {
                CPU.Instance.RequestInterrupt(InterruptType.JOYPAD);
            }
            Memory.Mem[MMU.IO] = data;
        }
    }
}
