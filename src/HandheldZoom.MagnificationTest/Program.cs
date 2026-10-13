using System.Text;
using HandheldZoom.Core.Diagnostics;
using HandheldZoom.Core.Magnification;

namespace HandheldZoom.MagnificationTest;

internal static class Program
{
    private const float TestFactor = 1.5f;
    private static readonly object ShutdownGate = new();
    private static MagnificationController? _controller;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        using var log = FileLog.CreateDefault("magnification-test");
        Console.WriteLine("Handheld Zoom – technischer Magnification-Test (Meilenstein 1)");
        Console.WriteLine($"Protokoll: {log.Path}");

        if (!args.Contains("--run", StringComparer.OrdinalIgnoreCase) &&
            !args.Contains("--auto", StringComparer.OrdinalIgnoreCase) &&
            !args.Contains("--status", StringComparer.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        using var mutex = new Mutex(true, "Local\\HandheldZoom.MagnificationTest", out var ownsMutex);
        if (!ownsMutex)
        {
            Console.Error.WriteLine("Der Magnification-Test läuft bereits.");
            return 2;
        }

        try
        {
            var display = DisplayInfo.Primary();
            Console.WriteLine($"Primärbildschirm: {display.Width}x{display.Height}, System-DPI: {display.Dpi} ({display.Dpi / 96d:P0})");
            log.Info($"Prozess: {(Environment.Is64BitProcess ? "x64" : "x86")}; Bildschirm: {display.Width}x{display.Height}; DPI={display.Dpi}.");

            var statusOnly = args.Contains("--status", StringComparer.OrdinalIgnoreCase);
            _controller = MagnificationController.Open(log, blockExistingMagnification: !statusOnly);
            RegisterShutdownHandlers(log);
            Console.WriteLine($"Aktueller Zustand: {_controller.GetCurrent()}");

            if (statusOnly)
            {
                return 0;
            }

            var center = ZoomMath.FromNormalizedFocus(display.Width, display.Height, TestFactor, 0.5, 0.5);
            Console.WriteLine($"Aktiviere 150 % in der Bildschirmmitte: {center}");
            _controller.Set(center);

            if (args.Contains("--auto", StringComparer.OrdinalIgnoreCase))
            {
                RunAutomaticSequence(display);
            }
            else
            {
                RunInteractive(display);
            }

            _controller.ResetToIdentity();
            Console.WriteLine("Zoom steht wieder auf 100 %.");
            return 0;
        }
        catch (Exception exception)
        {
            log.Error("Technischer Test fehlgeschlagen.", exception);
            Console.Error.WriteLine($"FEHLER: {exception.Message}");
            Console.Error.WriteLine("Falls der Bildschirm vergrößert blieb: HandheldZoom.Recovery.exe ausführen.");
            return 1;
        }
        finally
        {
            SafeShutdown(log);
        }
    }

    private static void RunAutomaticSequence(DisplayInfo display)
    {
        Console.WriteLine("Automatik: Mitte → rechts unten → links oben → 100 %. Abbruch mit Strg+C.");
        Thread.Sleep(700);
        _controller!.Set(ZoomMath.FromNormalizedFocus(display.Width, display.Height, TestFactor, 1.0, 1.0));
        Thread.Sleep(700);
        _controller.Set(ZoomMath.FromNormalizedFocus(display.Width, display.Height, TestFactor, 0.0, 0.0));
        Thread.Sleep(700);
    }

    private static void RunInteractive(DisplayInfo display)
    {
        var focusX = 0.5;
        var focusY = 0.5;
        Console.WriteLine("Pfeiltasten: Ausschnitt verschieben | M: Mitte | R/Esc/Enter: sofort auf 100 % und beenden");

        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    focusX -= 0.05;
                    break;
                case ConsoleKey.RightArrow:
                    focusX += 0.05;
                    break;
                case ConsoleKey.UpArrow:
                    focusY -= 0.05;
                    break;
                case ConsoleKey.DownArrow:
                    focusY += 0.05;
                    break;
                case ConsoleKey.M:
                    focusX = 0.5;
                    focusY = 0.5;
                    break;
                case ConsoleKey.R:
                case ConsoleKey.Escape:
                case ConsoleKey.Enter:
                    _controller!.ResetToIdentity();
                    return;
                default:
                    continue;
            }

            focusX = Math.Clamp(focusX, 0d, 1d);
            focusY = Math.Clamp(focusY, 0d, 1d);
            var transform = ZoomMath.FromNormalizedFocus(display.Width, display.Height, TestFactor, focusX, focusY);
            _controller!.Set(transform);
            Console.Write($"\rFokus=({focusX:F2}, {focusY:F2}), {transform}        ");
        }
    }

    private static void RegisterShutdownHandlers(ILog log)
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = false;
            SafeShutdown(log);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => SafeShutdown(log);
    }

    private static void SafeShutdown(ILog log)
    {
        lock (ShutdownGate)
        {
            if (_controller is null)
            {
                return;
            }

            try
            {
                _controller.Dispose();
            }
            catch (Exception exception)
            {
                log.Error("Wiederherstellung beim Beenden ist fehlgeschlagen.", exception);
                Console.Error.WriteLine($"RESET-FEHLER: {exception.Message}");
            }
            finally
            {
                _controller = null;
            }
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Dieser Start verändert den Bildschirm noch nicht.");
        Console.WriteLine("  --status   API initialisieren und Zustand nur lesen");
        Console.WriteLine("  --run      150 % aktivieren und interaktiv verschieben");
        Console.WriteLine("  --auto     kurze, automatisch zurückgesetzte Testsequenz");
        Console.WriteLine();
        Console.WriteLine("Vor --run/--auto die integrierte Windows-Lupe schließen.");
    }
}
