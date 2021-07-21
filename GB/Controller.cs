using GB.emu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GB
{
    public class Controller : Game
    {
        //amount of frames to take the average of
        const int NumFrames = 30;

        public static Controller Instance;

        public CPU CPU;

        private GraphicsDeviceManager Graphics;
        private string Title;

        public Controller() : base()
        {
            Instance = this;
            IsMouseVisible = true;
            Graphics = new GraphicsDeviceManager(this);
            Title = "Gameboy";
            Window.Title = Title;
        }

        public void LoadRom(Rom rom)
        {
            CPU = new CPU(rom);
            CPU.OCHandleMode = OCHandleMode.NOTHING;
        }

        public void LoadRom(string path)
        {
            Rom rom = new Rom(path);
            LoadRom(rom);
        }

        protected override void Initialize()
        {
            base.Initialize();

            Graphics.PreferMultiSampling = true;
            Graphics.PreferredBackBufferWidth = Config.ScreenWidth * Config.Scale;
            Graphics.PreferredBackBufferHeight = Config.ScreenHeight * Config.Scale;
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
            if (count == NumFrames)
            {
                float executionTime = Stopwatch.ElapsedMilliseconds / (float)NumFrames / 16.6667f;
                float speed = 100 / Math.Max(1, executionTime);

                Window.Title = Title + string.Format(" - speed: {0,4}%, exetime: {1,4}%", speed, executionTime * 100);
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
