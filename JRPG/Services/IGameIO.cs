using System;
using System.Collections.Generic;

namespace JRPGPrototype.Services
{
    /// <summary>
    /// The contract that decouples Logic from the Console.
    /// Acts as the primary I/O gatekeeper for the engine.
    /// </summary>
    public interface IGameIO
    {
        // Text Output
        void WriteLine(string message, ConsoleColor color = ConsoleColor.White);
        void Write(string message, ConsoleColor color = ConsoleColor.White);

        // Navigation and Control
        void Clear();
        void Wait(int milliseconds);

        // User Input
        string ReadLine();
        ConsoleKeyInfo ReadKey(bool intercept = true);

        // State Management (New requirements to prevent direct Console calls)
        void SetForegroundColor(ConsoleColor color);
        void SetBackgroundColor(ConsoleColor color);
        void ResetColor();
        void SetCursorVisible(bool visible);

        // Abstracting the Menu System
        int RenderMenu(string header, List<string> options, int initialIndex, List<bool> disabledOptions = null, Action<int> onHighlight = null);
    }
}