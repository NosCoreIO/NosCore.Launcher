using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NosCore.Launcher.Views;

/// <summary>
/// Asks DWM to draw a window's native title bar dark. The dialogs keep system
/// chrome — the main window is frameless, but a modal needs the real close
/// button and drag behaviour — and a default-light title bar over a dark dialog
/// body looks like a bug rather than a choice.
/// </summary>
public static class DarkTitleBar
{
    // 20 on Windows 10 20H1 and later; 19 on 1809-1903. Older builds have
    // neither and simply return a failure code, which is fine to ignore.
    private const int UseImmersiveDarkMode = 20;
    private const int UseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Call from a window's <c>SourceInitialized</c>, once its handle exists.</summary>
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, UseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }
    }
}
