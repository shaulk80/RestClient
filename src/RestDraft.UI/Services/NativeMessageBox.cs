using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace RestDraft.UI.Services;

// A message box that does not depend on Avalonia, so it works when the UI could not start or has stopped.
// Windows only. Other platforms show nothing, and the error stays on stderr (Linux stage, plan section 8.1).
public static partial class NativeMessageBox
{
    private const uint IconError = 0x10;

    public static void ShowError(string title, string text)
    {
        if (OperatingSystem.IsWindows())
        {
            ShowOnWindows(title, text);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ShowOnWindows(string title, string text)
    {
        MessageBoxW(IntPtr.Zero, text, title, IconError);
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [SupportedOSPlatform("windows")]
    private static partial int MessageBoxW(IntPtr windowHandle, string text, string caption, uint type);
}
