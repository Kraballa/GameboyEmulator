using GB.emu;
using ImGuiNET;
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

        private ImGuiRenderer Renderer;

        public int TargetFPS
        {
            get
            {
                return (int)Math.Round(TargetElapsedTime.TotalMilliseconds * 1000);
            }
            set
            {
                TargetElapsedTime = TimeSpan.FromMilliseconds(1000d / value);
            }
        }

        public CPU CPU;
        public Rom Rom;

        private GraphicsDeviceManager Graphics;
        private string Title;
        private int Count = 0;
        private Stopwatch Stopwatch = new Stopwatch();

        private bool CPURun = false;
        private bool CPUStep = false;

        private IntPtr TextureBufferPtr;
        private IntPtr ScreenBufferPtr;

        public Controller() : base()
        {
            Instance = this;
            IsMouseVisible = true;
            Graphics = new GraphicsDeviceManager(this);
            Graphics.PreferMultiSampling = true;
            Graphics.PreferredBackBufferWidth = 1280;
            Graphics.PreferredBackBufferHeight = 900;
            Graphics.ApplyChanges();
        }

        public void LoadRom(Rom rom)
        {
            Rom = rom;
            CPU = new CPU(rom);
            Title = CPU.Rom.Header.Title;
            Window.Title = Title;
        }

        public void LoadRom(string path)
        {
            Rom = new Rom(path);
            LoadRom(Rom);
        }

        protected void ImGuiLayout()
        {
            ImGui.DockSpaceOverViewport();
            ImGui.Begin("Main Window");
            ImGui.Image(ScreenBufferPtr, new System.Numerics.Vector2(Config.ScreenWidth * Config.Scale, Config.ScreenHeight * Config.Scale));
            ImGui.End();

            if (Config.RenderDebugTiles)
            {
                ImGui.Begin("Texture Buffer");
                ImGui.Image(TextureBufferPtr, new System.Numerics.Vector2(Config.ScreenWidth * Config.Scale, Config.ScreenHeight * Config.Scale));
                ImGui.End();
            }

            ImGui.Begin("Control");
            if (ImGui.Button("Reset")) { LoadRom(Rom); }

            if (ImGui.Button("Load Tetris")) { LoadRom(new Rom("tetris.gb")); }
            ImGui.SameLine();
            if (ImGui.Button("Load cpuinstrs-10")) { LoadRom(new Rom("./individual/10-bit ops.gb")); }

            ImGui.Checkbox("CPU Run", ref CPURun);
            if (ImGui.Button("CPU Step")) { CPUStep = true; }
            string lastInstr;
            if (CPU.LastInstrWasCB)
            {
                lastInstr = $"CB{CPU.LastInstr:X2}";
            }
            else
            {
                lastInstr = $"{CPU.LastInstr:X2}";
            }
            ImGui.Text($"lastInstr: 0x{lastInstr}");
            ImGui.Text($"cpu state: {CPU.GetState()}");
            ImGui.Text($"flags: {CPU.FlagsToString()}");

            if (ImGui.Button("test log"))
            {
                StreamWriter writer = new StreamWriter(File.Create("test.log"));
                writer.WriteLine(CPU.GetState());
                for (int i = 0; i < 30000; i++)
                {
                    CPU.Step();
                    writer.WriteLine(CPU.GetState());
                }
                writer.Close();
                Console.WriteLine("wrote log");
            }
            ImGui.End();
        }

        protected override void Initialize()
        {
            base.Initialize();
            Render.Initialize(GraphicsDevice);
            KInput.Initialize();
            MInput.Initialize();
            RenderTargets.Initialize(GraphicsDevice);

            Renderer = new ImGuiRenderer(this);
            Renderer.RebuildFontAtlas();
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGui.StyleColorsLight();

            ScreenBufferPtr = Renderer.BindTexture(RenderTargets.ScreenBuffer);
            TextureBufferPtr = Renderer.BindTexture(RenderTargets.TextureBuffer);
        }

        protected override void Update(GameTime gameTime)
        {
            MInput.Update();
            KInput.Update();

            if (KInput.CheckPressed(Keys.Space))
            {
                CPUStep = true;
            }

            if (CPUStep)
            {
                CPU.Step();
                CPUStep = false;
            }
            else if (CPURun)
            {
                Stopwatch.Start();
                CPU.Frame();
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

                CPUStep = false;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            Renderer.BeforeLayout(gameTime);

            if (Config.RenderDebugTiles)
            {
                GraphicsDevice.SetRenderTarget(RenderTargets.TextureBuffer);
                Render.Begin();
                CPU.LCD.RenderDebugTiles();
                Render.End();
            }

            ImGuiLayout();

            GraphicsDevice.SetRenderTarget(null);
            Renderer.AfterLayout();
        }
    }
}
