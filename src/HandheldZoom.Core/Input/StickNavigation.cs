using HandheldZoom.Core.Magnification;

namespace HandheldZoom.Core.Input;

public sealed class StickNavigation
{
    private ZoomTransform? _last;
    private double _x, _y, _factor;
    public static double Axis(short value, int deadzone)
    {
        var magnitude = Math.Abs((int)value);
        return magnitude <= deadzone ? 0 : Math.Sign(value) * Math.Min(1, (magnitude - deadzone) / (32767d - deadzone));
    }
    public void Reset() => _last = null;
    public ZoomTransform Step(ZoomTransform current, GamepadSnapshot input, int width, int height, double seconds)
    {
        if (width <= 0 || height <= 0 || !double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        seconds = Math.Min(seconds, .05);
        if (_last != current) { _x = current.XOffset; _y = current.YOffset; _factor = current.Factor; }
        if (!input.Connected) { Reset(); return current; }
        var centerX = _x + width / (2 * _factor);
        var centerY = _y + height / (2 * _factor);
        _factor = Math.Clamp(_factor + Axis(input.LeftY, 7849) * 1.5 * seconds, 1, 5);
        _x = centerX - width / (2 * _factor) + Axis(input.RightX, 8689) * width / _factor * seconds;
        _y = centerY - height / (2 * _factor) - Axis(input.RightY, 8689) * height / _factor * seconds;
        _x = Math.Clamp(_x, 0, width * (1 - 1 / _factor));
        _y = Math.Clamp(_y, 0, height * (1 - 1 / _factor));
        var result = ZoomMath.ClampOffsets(width, height, (float)_factor, (int)Math.Round(_x), (int)Math.Round(_y));
        _last = result;
        return result;
    }
}
