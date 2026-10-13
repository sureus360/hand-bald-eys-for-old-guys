using HandheldZoom.Core.Diagnostics;
using HandheldZoom.Core.Input;
using HandheldZoom.Core.Magnification;
using HandheldZoom.Core.Profiles;
using HandheldZoom.Core.QuickZoom;

namespace HandheldZoom.QuickZoom;

public sealed record RuntimeOptions(
    string Hotkey = "Ctrl+Alt+Z", string ResetHotkey = "Ctrl+Alt+R", int? ControllerIndex = 0,
    GamepadButtons Buttons = GamepadButtons.LB | GamepadButtons.RB, string? FixedProfile = null,
    float? Factor = null, ZoomMode? Mode = null, bool UseProfileControls = false);

/// <summary>UI-thread owner of the shared keyboard/controller zoom engine.</summary>
public sealed class ZoomRuntime : IDisposable
{
    private Mutex? _instance;
    private MagnificationController? _controller;
    private QuickZoomService? _service;
    private ZoomInputRouter? _inputs;
    private HotkeyWindow? _window;
    private ProfileSession? _profiles;
    private bool _ownsMutex;
    public event Action<Exception>? FailedEvent;
    public bool Failed => _window?.Failed ?? false;
    public bool IsZoomActive => _service?.IsActive ?? false;
    public bool? ControllerConnected { get; private set; }
    public string CurrentProfileName => _profiles?.Current.Name ?? "—";

    public ZoomRuntime(ProfileCatalog catalog, RuntimeOptions options, ILog log, Action<string>? report = null)
    {
        try
        {
            var zoom = Hotkey.Parse(options.Hotkey);
            var reset = Hotkey.Parse(options.ResetHotkey);
            if (zoom == reset) throw new ArgumentException("Zoom und Reset benötigen unterschiedliche Tastenkombinationen.");
            if (options.Factor.HasValue && (!float.IsFinite(options.Factor.Value) || options.Factor <= 1 || options.Factor > 5))
                throw new ArgumentException("Faktor muss größer als 1 und höchstens 5 sein.");
            _profiles = new ProfileSession(catalog, options.FixedProfile);
            var reader = options.ControllerIndex.HasValue ? new XInputReader(options.ControllerIndex.Value) : null;
            _instance = new Mutex(false, "Local\\HandheldZoom.QuickZoom");
            try { _ownsMutex = _instance.WaitOne(0); } catch (AbandonedMutexException) { _ownsMutex = true; }
            if (!_ownsMutex) throw new InvalidOperationException("QuickZoom läuft bereits. Die bisherige Instanz zuerst beenden.");
            _controller = MagnificationController.Open(log);
            ZoomTransform Target()
            {
                if (MagnifierConflictDetector.IsWindowsMagnifierRunning()) throw new InvalidOperationException("Windows-Lupe wurde gestartet. Zoom wird beendet.");
                if (!_controller.GetCurrent().IsIdentity) throw new InvalidOperationException("Fremder Zoom erkannt. Zoom wird beendet.");
                var display = DisplayInfo.Primary();
                var selected = _profiles.Current;
                return (options.Factor.HasValue ? selected with { Factor = options.Factor.Value } : selected).Transform(display.Width, display.Height);
            }
            _service = new QuickZoomService(_controller.Set, Target, () => options.Mode ?? _profiles.Current.Mode);
            GamepadButtons Chord() => options.UseProfileControls ? GamepadChord.Parse(_profiles.Current.ControllerButtons) : options.Buttons;
            _inputs = new ZoomInputRouter(_service, Chord);
            var navigation = new StickNavigation();
            var lastPoll = Environment.TickCount64;
            void Navigate(GamepadSnapshot snapshot)
            {
                var now = Environment.TickCount64;
                var elapsed = (now - lastPoll) / 1000d; lastPoll = now;
                if (!_service.IsActive || !snapshot.Connected || !_profiles.Current.StickControl) { navigation.Reset(); return; }
                if (MagnifierConflictDetector.IsWindowsMagnifierRunning()) throw new InvalidOperationException("Windows-Lupe wurde gestartet. Zoom wird beendet.");
                if (_controller.GetCurrent() != _service.Current) throw new InvalidOperationException("Fremder Zoom erkannt. Zoom wird beendet.");
                var display = DisplayInfo.Primary();
                _service.Adjust(navigation.Step(_service.Current, snapshot, display.Width, display.Height, elapsed));
            }
            var foreground = new ForegroundReader();
            void Refresh()
            {
                var application = foreground.Read();
                if (_profiles.Update(application, () => { if (_service.IsActive) _inputs.Reset(); }))
                {
                    var selected = _profiles.Current;
                    var message = $"Profil: {selected.Name}; {(options.Factor ?? selected.Factor) * 100:F0} %, {options.Mode ?? selected.Mode}. Vordergrund: {application.ProcessName ?? "unbekannt"}.";
                    log.Info(message); report?.Invoke(message);
                }
            }
            _window = new HotkeyWindow(zoom, reset, _inputs, reader, options.Buttons, Refresh, log,
                connected => ControllerConnected = connected, Navigate);
            _window.FailedEvent += ex => { if (FailedEvent is not null) FailedEvent.Invoke(ex); else Application.ExitThread(); };
        }
        catch { Dispose(); throw; }
    }
    public void Reset() => _inputs?.Reset();
    public void RequestClose() { if (_window is not null) Native.PostMessage(_window.Handle, 0x0010, 0, 0); }
    public void RestoreOriginal() => _controller?.RestoreOriginal();
    public void Dispose()
    {
        try { _window?.Dispose(); }
        finally
        {
            _window = null;
            try { _controller?.Dispose(); }
            finally
            {
            _controller = null; _service = null; _inputs = null;
            if (_ownsMutex) { _instance!.ReleaseMutex(); _ownsMutex = false; }
            _instance?.Dispose(); _instance = null;
            }
        }
    }
}
