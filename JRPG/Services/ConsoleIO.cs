using System;
using System.Collections.Generic;
using System.Threading;

namespace JRPGPrototype.Services
{
    public class ConsoleIO : IGameIO
    {
        public void WriteLine(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public void Write(string message, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write(message);
            Console.ResetColor();
        }

        public void Clear() => Console.Clear();

        public void Wait(int milliseconds) => Thread.Sleep(milliseconds);

        public string ReadLine() => Console.ReadLine();

        public ConsoleKeyInfo ReadKey(bool intercept = true) => Console.ReadKey(intercept);

        public int RenderMenu(string header, List<string> options, int initialIndex, List<bool> disabledOptions = null, Action<int> onHighlight = null)
        {
            // Delegates back to the existing static MenuUI logic, 
            // but wrapped here to allow dependency injection in Managers.
            return MenuUI.RenderMenu(header, options, initialIndex, disabledOptions, onHighlight);
        }
    }
}