using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB
{
    public static class RenderTargets
    {
        //buffer for the gameboy to render onto
        public static RenderTarget2D ScreenBuffer;

        public static void Initialize(GraphicsDevice gd)
        {
            if (Config.RenderDebugTiles)
            {
                ScreenBuffer = new RenderTarget2D(gd, Config.ScreenWidth + 256, 256);
            }
            else
            {
                ScreenBuffer = new RenderTarget2D(gd, Config.ScreenWidth, Config.ScreenHeight);
            }
        }
    }
}
