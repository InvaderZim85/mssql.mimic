using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MsSql.Mimic.Ui.Services;

/// <summary>
/// Provides monitor-related helper functionality for determining whether a window should start in fullscreen mode based on the work area, resolution, and aspect ratio of the nearest display.
/// </summary>
internal static class MonitorHelper
{
    /// <summary>
    /// Determines whether the specified window should start in fullscreen mode based on the work area, resolution, and aspect ratio of the nearest monitor.
    /// </summary>
    /// <param name="window">The window used to identify the nearest monitor.</param>
    /// <returns>
    /// <see langword="true" /> if the nearest monitor work area is at least 1920 by 1080 and does not exceed a 16:9 aspect ratio; otherwise, <see langword="false" />.
    /// </returns>
    public static bool ShouldStartFullscreen(Window window)
    {
        const double maxWidthForFullscreen = 1_920;
        const double maxHeightForFullscreen = 1_200;

        var monitorArea = GetMonitorWorkArea(window);

        var width = monitorArea.Right - monitorArea.Left;
        var height = monitorArea.Bottom - monitorArea.Top;

        return width <= maxWidthForFullscreen &&
               height <= maxHeightForFullscreen;
    }

    /// <summary>
    /// Gets the working area of the monitor nearest to the specified window, excluding system-reserved areas such as the taskbar.
    /// </summary>
    /// <param name="window">The window used to identify the nearest monitor.</param>
    /// <returns>
    /// A rectangle describing the monitor working area, or the system working area if monitor information cannot be retrieved.
    /// </returns>
    private static RECT GetMonitorWorkArea(Window window)
    {
        const int monitorDefaultToNearest = 2;

        var helper = new WindowInteropHelper(window);
        var monitor = MonitorFromWindow(helper.Handle, monitorDefaultToNearest);

        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            var workArea = SystemParameters.WorkArea;

            return new RECT
            {
                Left = (int)workArea.Left,
                Top = (int)workArea.Top,
                Right = (int)workArea.Right,
                Bottom = (int)workArea.Bottom
            };
        }

        return monitorInfo.rcWork;
    }

    /// <summary>
    /// Retrieves a handle to the display monitor that contains the specified window or is selected according to the specified fallback behavior.
    /// </summary>
    /// <param name="hwnd">The handle to the window used to locate the display monitor.</param>
    /// <param name="dwFlags">A value that specifies which monitor handle to return when the window does not intersect any display monitor.</param>
    /// <returns>
    /// A handle to the display monitor associated with the specified window, or <see cref="IntPtr.Zero" /> if no monitor is found and the fallback behavior allows no default monitor.
    /// </returns>
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    /// <summary>
    /// Retrieves information about the specified display monitor, including its monitor area, work area, and flags.
    /// </summary>
    /// <param name="hMonitor">A handle to the display monitor.</param>
    /// <param name="lpmi">A monitor information structure that receives details about the specified monitor.</param>
    /// <returns>
    /// <see langword="true" /> if the monitor information was retrieved successfully; otherwise, <see langword="false" />.
    /// </returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Represents native display monitor information, including the monitor bounds, available work area, and monitor status flags returned by the operating system.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    /// <summary>
    /// Represents a Win32-compatible rectangle defined by the coordinates of its left, top, right, and bottom edges.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}