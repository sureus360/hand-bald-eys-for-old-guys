using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using HandheldZoom.Core.Diagnostics;
using HandheldZoom.Core.Magnification;
using HandheldZoom.Core.QuickZoom;
using HandheldZoom.Core.Input;
using HandheldZoom.Core.Profiles;

namespace HandheldZoom.QuickZoom;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--help"))
        {
            Console.WriteLine("QuickZoom [--mode hold|toggle] [--hotkey Ctrl+Alt+Z] [--reset Ctrl+Alt+R] [--factor 1.5] [--controller 0|1|2|3|off] [--buttons LB+RB] [--profiles-path DATEI] [--profile NAME] [--list-profiles] [--check]");
            return 0;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var log = FileLog.CreateDefault("quick-zoom");
        try
        {
            ZoomMode? mode = null;
            var zoom = Hotkey.Parse("Ctrl+Alt+Z");
            var reset = Hotkey.Parse("Ctrl+Alt+R");
            float? factor = null;
            string? profilesPath = null, fixedProfile = null;
            bool listProfiles = false;
            bool check = false;
            int? controllerIndex = 0;
            var buttons = GamepadChord.Parse("LB+RB");
            for (var i = 0; i < args.Length; i++)
            {
                var option = args[i];
                if (option == "--list-profiles") { listProfiles = true; continue; }
                if (option == "--check") { check = true; continue; }
                if (++i >= args.Length) throw new ArgumentException($"Wert fehlt für {option}.");
                switch (option)
                {
                    case "--profiles-path": profilesPath = Path.GetFullPath(args[i]); break;
                    case "--profile": fixedProfile = args[i]; break;
                    case "--mode":
                        mode = args[i].ToLowerInvariant() switch { "hold" => ZoomMode.Hold, "toggle" => ZoomMode.Toggle, _ => throw new ArgumentException("Modus: hold oder toggle.") }; break;
                    case "--hotkey": zoom = Hotkey.Parse(args[i]); break;
                    case "--reset": reset = Hotkey.Parse(args[i]); break;
                    case "--controller": controllerIndex = args[i].Equals("off", StringComparison.OrdinalIgnoreCase) ? null : int.Parse(args[i], CultureInfo.InvariantCulture); break;
                    case "--buttons": buttons = GamepadChord.Parse(args[i]); break;
                    case "--factor": factor = float.Parse(args[i], CultureInfo.InvariantCulture); break;
                    default: throw new ArgumentException($"Unbekannte Option: {option}");
                }
            }
            if (zoom == reset) throw new ArgumentException("Zoom und Reset benötigen unterschiedliche Tastenkombinationen.");
            if (factor.HasValue && (!float.IsFinite(factor.Value) || factor <= 1 || factor > 5)) throw new ArgumentException("Faktor muss größer als 1 und höchstens 5 sein.");
            var path = profilesPath ?? ProfileStore.DefaultPath;
            var catalog = profilesPath is null ? ProfileStore.LoadOrCreate(path) : ProfileStore.Load(path);
            var profiles = new ProfileSession(catalog, fixedProfile);
            Console.WriteLine($"Profildatei: {path}");
            if (listProfiles)
            {
                foreach (var profile in new[] { catalog.DefaultProfile }.Concat(catalog.Profiles))
                    Console.WriteLine($"{profile.Name}: {profile.Factor * 100:F0} %, Fokus ({profile.FocusX}, {profile.FocusY}), {profile.Mode}, Prozesse: {string.Join(", ", profile.ProcessNames)}");
                return 0;
            }
            using var runtime = new ZoomRuntime(catalog, new(zoom.ToString(), reset.ToString(), controllerIndex, buttons, fixedProfile, factor, mode), log, Console.WriteLine);
            if (check) { Console.WriteLine("Profile, API, Hotkeys und Controller geprüft; Bildschirm unverändert."); return 0; }
            Console.WriteLine("Bereit. Strg+C beendet.");
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; runtime.RequestClose(); };
            EventHandler exit = (_, _) => runtime.RestoreOriginal();
            Console.CancelKeyPress += cancel;
            AppDomain.CurrentDomain.ProcessExit += exit;
            try { Application.Run(); }
            finally
            {
                Console.CancelKeyPress -= cancel;
                AppDomain.CurrentDomain.ProcessExit -= exit;
            }
            return runtime.Failed ? 1 : 0;
        }        catch (Exception ex)
        {
            log.Error("QuickZoom fehlgeschlagen", ex);
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
