using System.Diagnostics;

namespace HandheldZoom.Core.Magnification;

public static class MagnifierConflictDetector
{
    public static bool IsWindowsMagnifierRunning()
    {
        try
        {
            return Process.GetProcessesByName("Magnify").Length > 0;
        }
        catch
        {
            // Failure to enumerate must not be mistaken for a detected conflict.
            return false;
        }
    }
}
