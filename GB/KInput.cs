using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace GB
{
    /// <summary>
    /// Basic Keyboard input class that handles key presses.
    /// </summary>
    public static class KInput
    {
        public static KeyboardState current;
        public static KeyboardState prev;

        public static void Initialize()
        {
            current = Keyboard.GetState();
        }

        public static void Update()
        {
            prev = current;
            current = Keyboard.GetState();
        }

        public static bool Check(Keys key)
        {
            return current.IsKeyDown(key);
        }

        public static bool CheckPressed(Keys key)
        {
            return Check(key) && !prev.IsKeyDown(key);
        }

        public static KeyState State(Keys key)
        {
            return current[key];
        }
    }
}
