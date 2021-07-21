using GB.emu;
using Microsoft.Xna.Framework;
using System;

namespace GB
{
    class Program
    {
        static void Main(string[] args)
        {
            Controller cont;
            if (args.Length >= 1)
            {
                cont = new Controller();
                cont.LoadRom(args[0]);
            }
            else
            {
                cont = new Controller();
                cont.LoadRom(Rom.Empty);
            }
            cont.Run();
            cont.Dispose();
        }
    }
}
