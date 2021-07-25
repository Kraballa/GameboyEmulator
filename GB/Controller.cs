using GB.emu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        private int Count = 0;
        private Stopwatch Stopwatch = new Stopwatch();

        public Controller() : base()
        {
            Instance = this;
            IsMouseVisible = true;
            Graphics = new GraphicsDeviceManager(this);
        }

        public void LoadRom(Rom rom)
        {
            CPU = new CPU(rom);
            CPU.OCErrorMode = OCErrorMode.ERROR;
            Title = CPU.Rom.Header.Title;
            Window.Title = Title;
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
            RenderTargets.Initialize(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            MInput.Update();
            KInput.Update();
            Stopwatch.Start();
            CPU.Step();
            Stopwatch.Stop();
            Count++;
            if (Count == NumFrames)
            {
                float executionTime = Stopwatch.ElapsedMilliseconds / (float)NumFrames / 16.6667f;
                float speed = 100 / Math.Max(1, executionTime);

                Window.Title = Title + string.Format(" - speed: {0,4}%, exetime: {1,4}%", speed, executionTime * 100);
                Stopwatch.Reset();
                Count = 0;
            }

            if (KInput.CheckPressed(Keys.F1))
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(CPU.FlagsToString() + " - PC: 0x{0:X} [0x{1:X}] - rom: {2}", CPU.Regs.PC, CPU.Memory[CPU.Regs.PC], CPU.Rom.Header.Title);
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.SetRenderTarget(null);
            Render.Begin();
            Render.SpriteBatch.Draw(RenderTargets.ScreenBuffer, Vector2.Zero, null, Color.White, 0, Vector2.Zero, Config.Scale, SpriteEffects.None, 0);
            Render.End();
            base.Draw(gameTime);
        }
    }
}
