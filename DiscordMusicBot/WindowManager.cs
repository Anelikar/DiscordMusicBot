using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using HWND = System.IntPtr;

namespace DiscordMusicBot
{
    class WindowManager
    {
        public class Window
        {
            public readonly string PartialTitle;
            public enum TruncationDirection{
                start,
                end,
                none
            }
            public readonly TruncationDirection Truncation;
            public readonly int SymbolsToTruncate;
            public string Title { get; private set; }

            IntPtr hwnd;

            public Window(string partialTitle, TruncationDirection truncation = TruncationDirection.none, int symbolsToTruncate = 0) {
                PartialTitle = partialTitle;
                Truncation = truncation;
                SymbolsToTruncate = symbolsToTruncate;

                hwnd = GetHWNDByPartialAsync(partialTitle).Result;
                Title = GetWindowTitleByHWNDAsync(hwnd).Result;
            }

            public async Task<bool> UpdateTitleAsync() {
                string newTitle = await GetWindowTitleByHWNDAsync(hwnd);
                if (newTitle == null) {
                    hwnd = await GetHWNDByPartialAsync(PartialTitle);
                    newTitle = await GetWindowTitleByHWNDAsync(hwnd);
                    if (newTitle == null) {
                        Title = null;
                        return false;
                    }
                }

                if (Title != newTitle) {
                    Title = newTitle;
                    return true;
                }
                return false;
            }

            public async Task<string> GetTruncatedTitleAsync() {
                switch (Truncation) {
                    case TruncationDirection.start:
                        return await Task.FromResult(Title.Substring(SymbolsToTruncate - 1, Title.Length - SymbolsToTruncate));
                    case TruncationDirection.end:
                        return await Task.FromResult(Title.Substring(0, Title.Length - SymbolsToTruncate));
                    case TruncationDirection.none:
                        return Title;
                    default:
                        return Title;
                }
            }
        }

        /// <summary>
        /// Returns a list that contains the title of all the open windows.
        /// </summary>
        /// <returns>
        /// A list that contains the title of all the open windows.
        /// </returns>
        public static Task<List<string>> GetOpenWindowsAsync() {
            List<string> titles = new List<string>();
            foreach (KeyValuePair<IntPtr, string> window in getOpenWindows()) {
                titles.Add(window.Value);
            }

            return Task.FromResult(titles);
        }

        /// <summary>
        /// Returns window title that contains partial name or null if window wasn't found.
        /// </summary>
        /// <param name="partial">
        /// Partial title of the window
        /// </param>
        /// <returns>
        /// String containing window title or null if window wasn't found.
        /// </returns>
        public static Task<string> GetTitleByPartialAsync(string partial) {
            foreach (KeyValuePair<IntPtr, string> window in getOpenWindows()) {
                if (window.Value.Contains(partial)) {
                    return Task.FromResult(window.Value);
                }
            }
            return Task.FromResult<string>(null);
        }

        /// <summary>
        /// Returns HWND of window that contains partial name or IntPtr.Zero if window wasn't found.
        /// </summary>
        /// <param name="partial">
        /// Partial title of the window
        /// </param>
        /// <returns>
        /// HWND of window or IntPtr.Zero if window wasn't found.
        /// </returns>
        public static Task<IntPtr> GetHWNDByPartialAsync(string partial) {
            foreach (KeyValuePair<IntPtr, string> window in getOpenWindows()) {
                if (window.Value.Contains(partial)) {
                    return Task.FromResult(window.Key);
                }
            }
            return Task.FromResult<IntPtr>(IntPtr.Zero);
        }

        /// <summary>
        /// Returns a string containing title of the window with spceified HWND or null if window wasn't found.
        /// </summary>
        /// <param name="hwnd">
        /// HWND of the window
        /// </param>
        /// <returns>
        /// String containing title of the window or null if window wasn't found.
        /// </returns>
        public static Task<string> GetWindowTitleByHWNDAsync(IntPtr hwnd) {
            int length = GetWindowTextLength(hwnd);
            if (length == 0)
                return Task.FromResult<string>(null);

            StringBuilder builder = new StringBuilder(length);
            GetWindowText(hwnd, builder, length + 1);

            return Task.FromResult(builder.ToString());
        }

        //Code below courtesy of https://stackoverflow.com/questions/7268302/get-the-titles-of-all-open-windows
        /// <summary>
        /// Returns a dictionary that contains the handle and title of all the open windows.
        /// </summary>
        /// <returns>
        /// A dictionary that contains the handle and title of all the open windows.
        /// </returns>
        static IDictionary<IntPtr, string> getOpenWindows() {
            HWND shellWindow = GetShellWindow();
            Dictionary<HWND, string> windows = new Dictionary<HWND, string>();

            EnumWindows(delegate (HWND hWnd, int lParam)
            {
                if (hWnd == shellWindow)
                    return true;
                if (!IsWindowVisible(hWnd))
                    return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                windows[hWnd] = builder.ToString();
                return true;

            }, 0);

            return windows;
        }

        private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

        [DllImport("USER32.DLL")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowTextLength(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsWindowVisible(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetShellWindow();
    }
}
