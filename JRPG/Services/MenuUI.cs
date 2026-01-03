using System;
using System.Collections.Generic;

namespace JRPGPrototype.Services
{
    public static class MenuUI
    {
        public static int RenderMenu(string header, List<string> options, int initialIndex = 0, List<bool> disabledOptions = null, Action<int> onHighlight = null)
        {
            int selectedIndex = initialIndex;
            if (selectedIndex < 0) selectedIndex = 0;
            if (options.Count > 0 && selectedIndex >= options.Count) selectedIndex = 0;

            Console.CursorVisible = false;

            while (true)
            {
                Console.Clear();
                Console.WriteLine(header);

                for (int i = 0; i < options.Count; i++)
                {
                    bool isDisabled = disabledOptions != null && i < disabledOptions.Count && disabledOptions[i];
                    string prefix = (i == selectedIndex) ? "> " : " ";

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

                if (onHighlight != null && options.Count > 0)
                {
                    Console.WriteLine("\n------------------------------");
                    onHighlight(selectedIndex);
                }

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
                    bool isDisabled = disabledOptions != null && selectedIndex < disabledOptions.Count && disabledOptions[selectedIndex];
                    if (!isDisabled)
                    {
                        Console.CursorVisible = true;
                        return selectedIndex;
                    }
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