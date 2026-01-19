using System;
using System.Collections.Generic;
using System.Threading;

namespace JRPGPrototype.Services
{
    /// <summary>
    /// Concrete implementation of IGameIO using the standard System.Console.
    /// Handles terminal state management and color-safe text output.
    /// </summary>
    public class ConsoleIO : IGameIO
    {
        #region Text Output

        /// <summary>
        /// Writes a line of text with the specified color.
        /// Logic: Saves existing foreground color, sets new color, writes, then restores previous state.
        /// </summary>
        public void WriteLine(string message, ConsoleColor color = ConsoleColor.White)
        {
            ConsoleColor previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        /// <summary>
        /// Writes text with the specified color without a trailing newline.
        /// </summary>
        public void Write(string message, ConsoleColor color = ConsoleColor.White)
        {
            ConsoleColor previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ForegroundColor = previousColor;
        }

        #endregion

        #region Navigation and Timing

        public void Clear() => Console.Clear();

        public void Wait(int milliseconds) => Thread.Sleep(milliseconds);

        #endregion

        #region User Input

        public string ReadLine() => Console.ReadLine();

        public ConsoleKeyInfo ReadKey(bool intercept = true) => Console.ReadKey(intercept);

        #endregion

        #region Terminal State Management

        public void SetForegroundColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }

        public void SetBackgroundColor(ConsoleColor color)
        {
            Console.BackgroundColor = color;
        }

        public void ResetColor()
        {
            Console.ResetColor();
        }

        public void SetCursorVisible(bool visible)
        {
            Console.CursorVisible = visible;
        }

        #endregion

        #region Menu Rendering

        /// <summary>
        /// Renders a dynamic menu via the MenuUI utility.
        /// Fix: Now passes 'this' as the IGameIO provider to MenuUI.
        /// </summary>
        public int RenderMenu(string header, List<string> options, int initialIndex, List<bool> disabledOptions = null, Action<int> onHighlight = null)
        {
            return MenuUI.RenderMenu(this, header, options, initialIndex, disabledOptions, onHighlight);
        }

        #endregion
    }
}