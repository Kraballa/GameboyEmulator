using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum InterruptType
    {
        VBLANK = 0b0001,
        LCD = 0b0010,
        TIMER = 0b0100,
        SERIAL = 0b1000,
        JOYPAD = 0b10000
    }
}
