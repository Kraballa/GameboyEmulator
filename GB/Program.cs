using Microsoft.Xna.Framework;
using System;

namespace GB
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var g = new Controller())
                g.Run();
        }
    }
}
