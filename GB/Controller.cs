using GB.emu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GB
{
    public class Controller : Game
    {
        public static Controller Instance;

        private GraphicsDeviceManager Graphics;

        public CPU CPU;

        public Controller() : base()
        {
            Instance = this;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Graphics = new GraphicsDeviceManager(this);
            CPU = new CPU();
            CPU.OCHandleMode = OCHandleMode.NOTHING;
        }

        protected override void Initialize()
        {
            base.Initialize();

            Graphics.PreferMultiSampling = true;
            Graphics.PreferredBackBufferWidth = 1280;
            Graphics.PreferredBackBufferHeight = 720;
            Graphics.ApplyChanges();

            Render.Initialize(GraphicsDevice);
            KInput.Initialize();
            MInput.Initialize();
        }

        int count = 0;
        Stopwatch Stopwatch = new Stopwatch();

        protected override void Update(GameTime gameTime)
        {
            MInput.Update();
            KInput.Update();


            Stopwatch.Start();
            CPU.Step();
            Stopwatch.Stop();
            count++;
            if (count == 60)
            {
                Window.Title = string.Format("average cycle time: {0}ms / 16.667ms", (double)Stopwatch.ElapsedMilliseconds / 60d);
                Stopwatch.Reset();
                count = 0;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            Render.Begin();
            //render...
            Render.End();
            base.Draw(gameTime);
        }
    }
}
