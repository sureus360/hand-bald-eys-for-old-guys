using HandheldZoom.Core.Diagnostics;
using HandheldZoom.Core.Magnification;

namespace HandheldZoom.Recovery;

internal static class Program
{
    public static int Main()
    {
        using var log = FileLog.CreateDefault("recovery");
        Console.WriteLine("Handheld Zoom Recovery – setze Vollbild-Zoom auf 100 % …");
        Console.WriteLine($"Protokoll: {log.Path}");

        try
        {
            // Recovery greift absichtlich auch bei einem erkannten Fremdzustand ein.
            using var controller = MagnificationController.Open(log, blockExistingMagnification: false);
            controller.ResetToIdentity();
            controller.KeepCurrentOnDispose();
            Console.WriteLine("Erfolg: Zoomfaktor 100 %, Offset (0, 0) wurde gelesen und bestätigt.");
            return 0;
        }
        catch (Exception exception)
        {
            log.Error("Recovery fehlgeschlagen.", exception);
            Console.Error.WriteLine($"FEHLER: {exception.Message}");
            Console.Error.WriteLine("Alternative Windows-Notfalltaste: Win+Esc schließt die integrierte Windows-Lupe.");
            return 1;
        }
    }
}
