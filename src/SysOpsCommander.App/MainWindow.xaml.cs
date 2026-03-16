using System.Runtime.InteropServices;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace SysOpsCommander.App;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;
        ApplyStartupSize();
    }

    /// <summary>
    /// Sets window size to 80% of the working area on the monitor containing the mouse cursor.
    /// </summary>
    private void ApplyStartupSize()
    {
        _ = NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPos);
        nint hMonitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);

        NativeMethods.MONITORINFO monitorInfo = new() { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            return;
        }

        NativeMethods.RECT workArea = monitorInfo.rcWork;

        // Convert physical pixels to WPF device-independent pixels
        System.Windows.DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double availableWidth = (workArea.Right - workArea.Left) / scaleX;
        double availableHeight = (workArea.Bottom - workArea.Top) / scaleY;

        Width = Math.Max(MinWidth, availableWidth * 0.8);
        Height = Math.Max(MinHeight, availableHeight * 0.8);

        Left = (workArea.Left / scaleX) + ((availableWidth - Width) / 2);
        Top = (workArea.Top / scaleY) + ((availableHeight - Height) / 2);
    }

    private static partial class NativeMethods
    {
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport("user32.dll")]
        public static partial nint MonitorFromPoint(POINT pt, int dwFlags);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);
    }
}
