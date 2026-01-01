using System;
using System.Collections.Generic;

namespace JRPGPrototype
{
    public static class MenuUI
    {
        /// <summary>
        /// Renders a menu and returns the index of the selected option.
        /// </summary>
        /// <param name="header">Text displayed at the top (e.g., Battle HUD).</param>
        /// <param name="options">List of menu items.</param>
        /// <param name="initialIndex">Starting cursor position (for persistence).</param>
        /// <param name="disabledOptions">List of bools matching options count. True = Greyed out/Unselectable.</param>
        /// <param name="onHighlight">Callback to run when selection changes (for descriptions/previews).</param>
        /// <returns>Index of selection, or -1 if cancelled (Esc/Backspace).</returns>
        public static int RenderMenu(string header, List<string> options, int initialIndex = 0, List<bool> disabledOptions = null, Action<int> onHighlight = null)
        {
            int selectedIndex = initialIndex;

            // Safety clamp
            if (selectedIndex < 0) selectedIndex = 0;
            if (options.Count > 0 && selectedIndex >= options.Count) selectedIndex = 0;

            Console.CursorVisible = false;

            while (true)
            {
                Console.Clear();
                Console.WriteLine(header);

                // Draw Options
                for (int i = 0; i < options.Count; i++)
                {
                    bool isDisabled = disabledOptions != null && i < disabledOptions.Count && disabledOptions[i];
                    string prefix = (i == selectedIndex) ? "> " : "  ";

                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.WriteLine($"{prefix}{options[i]}");
                        Console.ResetColor();
                    }
                    else
                    {
                        if (isDisabled)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.WriteLine($"{prefix}{options[i]}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine($"{prefix}{options[i]}");
                        }
                    }
                }

                // Trigger Highlight Callback (Footer Info)
                if (onHighlight != null && options.Count > 0)
                {
                    Console.WriteLine("\n------------------------------");
                    onHighlight(selectedIndex);
                }

                // Input Handling
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

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
                    // Check if the selected option is disabled
                    bool isDisabled = disabledOptions != null && selectedIndex < disabledOptions.Count && disabledOptions[selectedIndex];

                    if (!isDisabled)
                    {
                        Console.CursorVisible = true;
                        return selectedIndex;
                    }
                    // Else: Do nothing (or beep)
                }
                else if (keyInfo.Key == ConsoleKey.Escape || keyInfo.Key == ConsoleKey.Backspace)
                {
                    Console.CursorVisible = true;
                    return -1;
                }
            }
        }
    }
}