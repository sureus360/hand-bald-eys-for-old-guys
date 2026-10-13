using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HandheldZoom.Core.Profiles;

public readonly record struct ForegroundApplication(uint ProcessId, string? ProcessName);

public sealed class ForegroundReader
{
    private ForegroundApplication _cached;
    private long _retry;
    public ForegroundApplication Read()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || GetWindowThreadProcessId(window, out var id) == 0 || id == 0) { _cached = default; return default; }
        if (_cached.ProcessId == id && (_cached.ProcessName is not null || Environment.TickCount64 < _retry)) return _cached;
        try
        {
            using var process = Process.GetProcessById(checked((int)id));
            return _cached = new(id, process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or OverflowException)
        {
            // Exited/inaccessible applications use the default profile.
            _retry = Environment.TickCount64 + 1000;
            return _cached = new(id, null);
        }
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

/// <summary>Resets on foreground context changes before committing new profile settings.</summary>
public sealed class ProfileSession
{
    private readonly ProfileCatalog _catalog;
    private readonly ZoomProfile? _fixed;
    private ForegroundApplication _application;
    private bool _initialized;
    public ProfileSession(ProfileCatalog catalog, string? fixedProfile = null)
    {
        catalog.Validate(); _catalog = catalog;
        _fixed = fixedProfile is null ? null : catalog.ByName(fixedProfile);
        Current = _fixed ?? catalog.DefaultProfile;
    }
    public ZoomProfile Current { get; private set; }
    public bool Update(ForegroundApplication application, Action beforeChange)
    {
        if (_initialized && _application == application) return false;
        var next = _fixed ?? _catalog.Resolve(application.ProcessName);
        if (_initialized) beforeChange();
        var changed = Current != next || !_initialized;
        Current = next; _application = application; _initialized = true;
        return changed;
    }
}
