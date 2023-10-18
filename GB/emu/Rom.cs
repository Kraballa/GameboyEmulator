using GB.emu.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GB.emu
{
    public struct Header
    {
        public string Title;
        public byte Type;
        public byte ROMSize;
    }

    public class Rom
    {
        public static Rom Empty => new Rom();
        public Header Header;

        private byte[] mem;

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
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("loading {0}", info.Name);
            Console.ForegroundColor = ConsoleColor.Gray;
            mem = File.ReadAllBytes(path);

            ParseCartridgeHeader();
        }

        private Rom()
        {
            mem = new byte[0x4000];

            mem[0x100] = 0x76; //halt
            string title = "EMPTY";
            for (int i = 0; i < 16; i++)
            {
                if (i < title.Length)
                    mem[0x134 + i] = (byte)title[i];
                else
                    mem[0x134 + i] = 0x0;
            }
            mem[0x147] = 0x0; //cartridge type
            mem[0x148] = 0x0; //amount of rom banks. here 0
            mem[0x149] = 0x0; //size of external ram. here 0
            mem[0x14A] = 0x1; //destination code

            Console.WriteLine("loading empty rom");
        }

        public byte this[ushort index]
        {
            get
            {
                //TODO: Rom Banks, only allow access to BANK0
                if (index >= MMU.BANK1)
                    return 0x00;
                return mem[index];
            }
        }

        private void ParseCartridgeHeader()
        {
            char[] title = new char[16];
            for (int i = 0; i < title.Length; i++)
            {
                title[i] = (char)mem[i + 0x134];
            }

            Header = new Header()
            {
                Title = new string(title),
                Type = mem[0x147],
                ROMSize = mem[0x148]
            };
            Header.Title = Header.Title.Replace("\0", null);
        }
    }
}
