using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Loupedeck.LogitumAdaptiveRing.Helpers
{
    /// <summary>
    /// Win32 API declarations for process and window management
    /// </summary>
    public static class Win32Api
    {
        /// <summary>
        /// Retrieves a handle to the foreground window (the window with which the user is currently working)
        /// </summary>
        /// <returns>Handle to the foreground window</returns>
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Retrieves the identifier of the thread that created the specified window
        /// and optionally the identifier of the process that created the window
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="processId">Process identifier</param>
        /// <returns>Thread identifier</returns>
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /// <summary>
        /// Copies the text of the specified window's title bar into a buffer
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <param name="text">Buffer to receive the text</param>
        /// <param name="count">Maximum number of characters to copy</param>
        /// <returns>Length of the copied string</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        /// <summary>
        /// Retrieves the length of the specified window's title bar text
        /// </summary>
        /// <param name="hWnd">Handle to the window</param>
        /// <returns>Length of the text in characters</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
    }
}
