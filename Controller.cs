using GB.emu;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB
{
    public class Controller : Game
    {
        public static Controller Instance;

        private GraphicsDeviceManager Graphics;

        public Controller() : base()
        {
            Instance = this;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Graphics = new GraphicsDeviceManager(this);
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

        protected override void Update(GameTime gameTime)
        {
            MInput.Update();
            KInput.Update();

            new CPU();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            Render.Begin();
            Render.Circle(MInput.PositionF, 5, Color.Red, 5);
            Render.End();
            base.Draw(gameTime);
        }
    }
}
