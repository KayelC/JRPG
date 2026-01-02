using System;
using System.Collections.Generic;

namespace JRPGPrototype.Services
{
    // The contract that decouples Logic from the Console.
    // In a MonoGame port, this would implement graphical text rendering.
    public interface IGameIO
    {
        void WriteLine(string message, ConsoleColor color = ConsoleColor.White);
        void Write(string message, ConsoleColor color = ConsoleColor.White);
        void Clear();
        void Wait(int milliseconds);
        string ReadLine();
        ConsoleKeyInfo ReadKey(bool intercept = true);

        // Abstracting the Menu System
        int RenderMenu(string header, List<string> options, int initialIndex, List<bool> disabledOptions = null, Action<int> onHighlight = null);
    }
}