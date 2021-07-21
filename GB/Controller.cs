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
        public static Controller Instance;

        private GraphicsDeviceManager Graphics;

        public CPU CPU;

        private string Title = "";

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
            if (count == 60)
            {
                Window.Title = Title + string.Format(" - {0}%", Math.Round(Stopwatch.ElapsedMilliseconds / 60d / 16.6667d * 100));
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
