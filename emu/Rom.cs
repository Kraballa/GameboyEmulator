using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GB.emu
{
    public class Rom
    {
        public static Rom Empty => new Rom();

        public byte[] Data;

        public Rom(string path)
        {
            if (!File.Exists(path))
            {
                throw new Exception("error, file not found");
            }

            if (new FileInfo(path).Length > 8388608) //2^23 = 8.388.608 -> 8MiB. max gameboy size and safety measure
            {
                throw new Exception("error, file too big");
            }

            Data = File.ReadAllBytes(path);
        }

        private Rom()
        {
            Data = new byte[1] { 0x76 }; //halt
        }
    }
}
