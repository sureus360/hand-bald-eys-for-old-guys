using HandheldZoom.Core.Magnification;

namespace HandheldZoom.Core.QuickZoom;

public enum ZoomMode { Hold, Toggle }

/// <summary>Single-threaded state machine; settings are sampled once for each activation.</summary>
public sealed class QuickZoomService
{
    private readonly Action<ZoomTransform> _apply;
    private readonly Func<ZoomTransform> _target;
    private readonly Func<ZoomMode> _mode;
    private ZoomMode _activeMode;
    private bool _pressed;
    public QuickZoomService(Action<ZoomTransform> apply, Func<ZoomTransform> target, ZoomMode mode)
        : this(apply, target, () => mode) { }
    public QuickZoomService(Action<ZoomTransform> apply, Func<ZoomTransform> target, Func<ZoomMode> mode)
    {
        _apply = apply; _target = target; _mode = mode;
    }
    public bool IsActive { get; private set; }
    public ZoomTransform Current { get; private set; } = new(1, 0, 0);
    public void Adjust(ZoomTransform transform)
    {
        if (!IsActive || transform == Current) return;
        _apply(transform); Current = transform;
    }
    public void Press()
    {
        if (_pressed) return;
        _pressed = true;
        _activeMode = _mode();
        SetActive(_activeMode == ZoomMode.Hold || !IsActive);
    }
    public void Release()
    {
        _pressed = false;
        if (_activeMode == ZoomMode.Hold && IsActive) SetActive(false);
    }
    public void Reset()
    {
        _apply(new ZoomTransform(1, 0, 0));
        Current = new(1, 0, 0);
        IsActive = false;
    }
    private void SetActive(bool active)
    {
        var transform = active ? _target() : new ZoomTransform(1, 0, 0);
        _apply(transform); Current = transform;
        IsActive = active;
    }
}
