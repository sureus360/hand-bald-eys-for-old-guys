namespace HandheldZoom.Core.Magnification;

public readonly record struct DisplayInfo(int Width, int Height, uint Dpi)
{
    public static DisplayInfo Primary()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Handheld Zoom benötigt Windows.");
        }

        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Ungültige Primärbildschirmgröße: {width}x{height}.");
        }

        uint dpi;
        try
        {
            dpi = NativeMethods.GetDpiForSystem();
        }
        catch (EntryPointNotFoundException)
        {
            dpi = 96;
        }

        return new DisplayInfo(width, height, dpi == 0 ? 96u : dpi);
    }
}
