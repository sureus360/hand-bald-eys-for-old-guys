namespace HandheldZoom.Core.Magnification;

public readonly record struct ZoomTransform(float Factor, int XOffset, int YOffset)
{
    public bool IsIdentity => Math.Abs(Factor - 1.0f) < 0.0001f && XOffset == 0 && YOffset == 0;

    public override string ToString() => $"{Factor:P0}, Offset=({XOffset}, {YOffset})";
}
