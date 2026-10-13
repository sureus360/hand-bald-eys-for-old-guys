namespace HandheldZoom.Core.Magnification;

public static class ZoomMath
{
    public const float MinimumFactor = 1.0f;
    public const float MaximumApiFactor = 4096.0f;

    public static ZoomTransform FromNormalizedFocus(
        int screenWidth,
        int screenHeight,
        float factor,
        double normalizedX,
        double normalizedY)
    {
        ValidateDimensions(screenWidth, screenHeight);
        ValidateFactor(factor);

        normalizedX = Math.Clamp(normalizedX, 0d, 1d);
        normalizedY = Math.Clamp(normalizedY, 0d, 1d);

        var visibleWidth = screenWidth / (double)factor;
        var visibleHeight = screenHeight / (double)factor;
        var maxX = Math.Max(0d, screenWidth - visibleWidth);
        var maxY = Math.Max(0d, screenHeight - visibleHeight);

        var desiredX = (normalizedX * screenWidth) - (visibleWidth / 2d);
        var desiredY = (normalizedY * screenHeight) - (visibleHeight / 2d);

        var x = RoundAndClamp(desiredX, maxX);
        var y = RoundAndClamp(desiredY, maxY);
        return factor == 1.0f ? new ZoomTransform(1.0f, 0, 0) : new ZoomTransform(factor, x, y);
    }

    public static ZoomTransform ClampOffsets(
        int screenWidth,
        int screenHeight,
        float factor,
        int requestedX,
        int requestedY)
    {
        ValidateDimensions(screenWidth, screenHeight);
        ValidateFactor(factor);

        if (factor == 1.0f)
        {
            return new ZoomTransform(1.0f, 0, 0);
        }

        var maxX = (int)Math.Floor(screenWidth * (1d - (1d / factor)));
        var maxY = (int)Math.Floor(screenHeight * (1d - (1d / factor)));
        return new ZoomTransform(factor, Math.Clamp(requestedX, 0, maxX), Math.Clamp(requestedY, 0, maxY));
    }

    private static int RoundAndClamp(double requested, double maximum)
    {
        var rounded = (int)Math.Round(requested, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 0, (int)Math.Floor(maximum));
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
    }

    private static void ValidateFactor(float factor)
    {
        if (!float.IsFinite(factor) || factor < MinimumFactor || factor > MaximumApiFactor)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "Der Zoomfaktor muss zwischen 1,0 und 4096,0 liegen.");
        }
    }
}
