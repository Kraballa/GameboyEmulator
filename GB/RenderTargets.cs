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
            ScreenBuffer = new RenderTarget2D(gd, Config.ScreenWidth, Config.ScreenHeight);
        }
    }
}
