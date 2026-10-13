using HandheldZoom.Core.QuickZoom;

namespace HandheldZoom.Core.Input;

/// <summary>Combines input sources and blocks reactivation after reset until both are released.</summary>
public sealed class ZoomInputRouter(QuickZoomService service, Func<GamepadButtons> getChord)
{
    public ZoomInputRouter(QuickZoomService service, GamepadButtons chord) : this(service, () => chord) { }
    private bool _keyboard;
    private bool _gamepad;
    private bool _connected;
    private bool _armed;
    private bool _blocked;
    private bool _combined;

    public void Keyboard(bool down) { _keyboard = down; Update(); }

    public void Controller(GamepadSnapshot snapshot)
    {
        var chord = getChord();
        var down = snapshot.Connected && (snapshot.Buttons & chord) == chord;
        if (!snapshot.Connected)
        {
            _gamepad = false;
            _armed = false;
            if (_connected) Reset();
            _connected = false;
        }
        else
        {
            if (!_connected) _armed = !down;
            if (!down) _armed = true;
            _connected = true;
            _gamepad = _armed && down;
        }
        Update();
    }

    public void Reset()
    {
        service.Reset();
        service.Release();
        _combined = false;
        _blocked = _keyboard || _gamepad;
    }

    private void Update()
    {
        var down = _keyboard || _gamepad;
        if (_blocked) { if (!down) _blocked = false; return; }
        if (down == _combined) return;
        if (down) service.Press(); else service.Release();
        _combined = down;
    }
}
