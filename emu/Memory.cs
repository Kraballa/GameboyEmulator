using System;
using System.Collections.Generic;
using System.Text;

namespace GB.emu
{
    public class Memory
    {
        private byte[] mem = new byte[32];

        public byte this[uint index]
        {
            get => mem[index];
            set => mem[index] = value;
        }
    }
}
