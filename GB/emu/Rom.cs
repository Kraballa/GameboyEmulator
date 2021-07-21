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
                throw new Exception(string.Format("error, rom file not found: {0}", path));
            }

            FileInfo info = new FileInfo(path);
            if (info.Length > 8388608) //2^23 = 8.388.608 -> 8MiB. max gameboy size and safety measure
            {
                throw new Exception("error, file too big");
            }
            Console.WriteLine("loading {0}", info.Name);
            Data = File.ReadAllBytes(path);
        }

        private Rom()
        {
            Data = new byte[0x14F];

            Data[0x100] = 0x76; //halt
            Data[0x147] = 0x0; //cartridge type
            Data[0x148] = 0x0; //amount of rom banks. here 0
            Data[0x149] = 0x0; //size of external ram. here 0
            Data[0x14A] = 0x1; //destination code

            //TODO: extend rom header with title, manufacturing info etc.
        }
    }
}
