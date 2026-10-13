using System.ComponentModel;
using System.Runtime.InteropServices;
using HandheldZoom.Core.Diagnostics;
using HandheldZoom.Core.Input;
namespace HandheldZoom.QuickZoom;
internal readonly record struct Hotkey(uint Modifiers, int Key)
{
    public static Hotkey Parse(string value)
    {
        uint modifiers = 0;
        var parts = value.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) throw new ArgumentException("Hotkey benötigt mindestens einen Modifikator, z.B. Ctrl+Alt+Z.");
        foreach (var part in parts[..^1])
        {
            uint bit = part.ToLowerInvariant() switch { "ctrl" => 2, "alt" => 1, "shift" => 4, _ => throw new ArgumentException($"Unbekannter Modifikator: {part}") };
            if ((modifiers & bit) != 0) throw new ArgumentException("Doppelter Modifikator.");
            modifiers |= bit;
        }
        var name = parts[^1].ToUpperInvariant();
        int key;
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0])) key = name[0];
        else if (name.StartsWith('F') && int.TryParse(name[1..], out var f) && f is >= 1 and <= 24) key = 0x70 + f - 1;
        else throw new ArgumentException("Taste muss A–Z, 0–9 oder F1–F24 sein.");
        if (key == 0x7B) throw new ArgumentException("F12 ist für den Debugger reserviert.");
        return new(modifiers, key);
    }
    public bool IsDown => Down(Key) && ((Modifiers & 1) == 0 || Down(0x12)) && ((Modifiers & 2) == 0 || Down(0x11)) && ((Modifiers & 4) == 0 || Down(0x10));
    private static bool Down(int key) => (Native.GetAsyncKeyState(key) & 0x8000) != 0;
    public override string ToString() => $"{((Modifiers & 2) != 0 ? "Ctrl+" : "")}{((Modifiers & 1) != 0 ? "Alt+" : "")}{((Modifiers & 4) != 0 ? "Shift+" : "")}{(Keys)Key}";
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Hotkey _zoom;
    private readonly ZoomInputRouter _inputs;
    private readonly XInputReader? _reader;
    private bool? _connected;
    private readonly GamepadButtons _buttons;
    private readonly Action _refreshProfiles;
    private long _nextProfilePoll;
    private readonly ILog _log;
    private readonly Action<bool>? _connectionChanged;
    private readonly Action<GamepadSnapshot>? _navigate;
    public bool Failed { get; private set; }
    public event Action<Exception>? FailedEvent;
    public HotkeyWindow(Hotkey zoom, Hotkey reset, ZoomInputRouter inputs, XInputReader? reader, GamepadButtons buttons, Action refreshProfiles, ILog log, Action<bool>? connectionChanged = null, Action<GamepadSnapshot>? navigate = null)
    {
        _zoom = zoom; _inputs = inputs; _reader = reader; _buttons = buttons; _refreshProfiles = refreshProfiles; _log = log; _connectionChanged = connectionChanged; _navigate = navigate;
        CreateHandle(new CreateParams { Caption = "HandheldZoom Hotkeys", Parent = new IntPtr(-3) });
        try
        {
            Register(1, zoom); Register(2, reset);
            _timer.Tick += (_, _) => Guard(() =>
            {
                if (Environment.TickCount64 >= _nextProfilePoll)
                { _refreshProfiles(); _nextProfilePoll = Environment.TickCount64 + 250; }
                if (!_zoom.IsDown) _inputs.Keyboard(false);
                if (_reader is not null) PollController();
            });
            _refreshProfiles();
            if (_reader is not null) PollController();
            _timer.Start();
        }
        catch { Dispose(); throw; }
    }
    private void PollController()
    {
        var snapshot = _reader!.Read();
        if (_connected != snapshot.Connected)
        {
            _connected = snapshot.Connected;
            _connectionChanged?.Invoke(snapshot.Connected);
            var message = snapshot.Connected ? "Controller verbunden." : "Controller nicht verbunden; Tastatur bleibt verfügbar.";
            _log.Info(message); Console.WriteLine(message);
        }
        if (snapshot.Connected && snapshot.Buttons != 0) _refreshProfiles();
        _inputs.Controller(snapshot);
        _navigate?.Invoke(snapshot);
    }
    private void Register(int id, Hotkey key)
    {
        if (!Native.RegisterHotKey(Handle, id, key.Modifiers | 0x4000, (uint)key.Key))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Hotkey {key} konnte nicht registriert werden.");
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) { var id = m.WParam; Guard(() => { if (id == 2) _inputs.Reset(); else if (_zoom.IsDown) { _refreshProfiles(); _inputs.Keyboard(true); } }); }
        else if (m.Msg == 0x0010) { Guard(_inputs.Reset); Application.ExitThread(); }
        base.WndProc(ref m);
    }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Failed = true; _timer.Stop();
            _log.Error("Eingabeverarbeitung fehlgeschlagen", ex);
            Console.Error.WriteLine(ex.Message);
            if (FailedEvent is not null) FailedEvent.Invoke(ex); else Application.ExitThread();
        }
    }
    public void Dispose()
    {
        _timer.Dispose();
        if (Handle != IntPtr.Zero) { Native.UnregisterHotKey(Handle, 1); Native.UnregisterHotKey(Handle, 2); DestroyHandle(); }
    }
}

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr window, uint message, nuint wParam, nint lParam);
}
