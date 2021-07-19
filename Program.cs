using Microsoft.Xna.Framework;
using System;

namespace GB
{
    class Program
    {
        static void Main(string[] args)
        {
            ushort value = 0xFFFF;
            byte value2 = (byte)value;

            byte[] values = new byte[2];
            values[0] = 0xff;

            Console.WriteLine("short value: {0}, byte value: {1}", value, value2);
            Console.WriteLine("byte shifted: {0}", value2 << 8);
            Console.WriteLine("byte 3: {0}, added 1: {1}", values[0], (byte)(values[0] + 1));
            using (var g = new Controller())
                g.Run();
        }
    }
}
