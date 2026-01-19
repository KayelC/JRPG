using System;
using System.Collections.Generic;

namespace JRPGPrototype.Services
{
    /// <summary>
    /// Static utility for rendering high-fidelity interactive menus.
    /// Refactored to utilize IGameIO instead of direct System.Console calls.
    /// </summary>
    public static class MenuUI
    {
        public static int RenderMenu(IGameIO io, string header, List<string> options, int initialIndex = 0, List<bool> disabledOptions = null, Action<int> onHighlight = null)
        {
            int selectedIndex = initialIndex;
            if (selectedIndex < 0) selectedIndex = 0;
            if (options.Count > 0 && selectedIndex >= options.Count) selectedIndex = 0;

            // Use the IO abstraction to manage cursor state
            io.SetCursorVisible(false);

            while (true)
            {
                io.Clear();
                io.WriteLine(header);

                for (int i = 0; i < options.Count; i++)
                {
                    bool isDisabled = disabledOptions != null && i < disabledOptions.Count && disabledOptions[i];
                    string prefix = (i == selectedIndex) ? "> " : "  ";

                    if (i == selectedIndex)
                    {
                        // Use abstraction for background/foreground swaps
                        io.SetBackgroundColor(ConsoleColor.Gray);
                        io.SetForegroundColor(ConsoleColor.Black);
                        io.WriteLine($"{prefix}{options[i]}");
                        io.ResetColor();
                    }
                    else
                    {
                        if (isDisabled)
                        {
                            io.WriteLine($"{prefix}{options[i]}", ConsoleColor.DarkGray);
                        }
                        else
                        {
                            io.WriteLine($"{prefix}{options[i]}");
                        }
                    }
                }

                // Handle live-reactive highlights (e.g., stat differentials)
                if (onHighlight != null && options.Count > 0)
                {
                    io.WriteLine("\n------------------------------");
                    onHighlight(selectedIndex);
                }

                ConsoleKeyInfo keyInfo = io.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    selectedIndex--;
                    if (selectedIndex < 0) selectedIndex = options.Count - 1;
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    selectedIndex++;
                    if (selectedIndex >= options.Count) selectedIndex = 0;
                }
                else if (keyInfo.Key == ConsoleKey.Enter)
                {
                    bool isDisabled = disabledOptions != null && selectedIndex < disabledOptions.Count && disabledOptions[selectedIndex];
                    if (!isDisabled)
                    {
                        io.SetCursorVisible(true);
                        return selectedIndex;
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Escape || keyInfo.Key == ConsoleKey.Backspace)
                {
                    io.SetCursorVisible(true);
                    return -1;
                }
            }
        }
    }
}