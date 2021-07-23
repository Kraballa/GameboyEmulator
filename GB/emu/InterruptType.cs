using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum InterruptType
    {
        VBlank = 0b0001,
        LCD = 0b0010,
        Timer = 0b0100,
        Joypad = 0b1000
    }
}
