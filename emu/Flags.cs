using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public enum Flags
    {
        ZERO = (1 << 7),
        SUB = (1 << 6),
        HCARRY = (1 << 5),
        CARRY = (1 << 4)
    }
}
