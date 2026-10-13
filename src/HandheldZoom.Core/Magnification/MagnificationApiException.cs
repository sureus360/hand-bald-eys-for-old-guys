using System.ComponentModel;

namespace HandheldZoom.Core.Magnification;

public sealed class MagnificationApiException : Exception
{
    public MagnificationApiException(string apiName, int win32Error)
        : base(CreateMessage(apiName, win32Error))
    {
        ApiName = apiName;
        Win32Error = win32Error;
    }

    public string ApiName { get; }
    public int Win32Error { get; }

    private static string CreateMessage(string apiName, int error)
    {
        var detail = error == 0 ? "Die API lieferte keinen erweiterten Win32-Fehler." : new Win32Exception(error).Message;
        return $"{apiName} ist fehlgeschlagen (Win32={error}: {detail})";
    }
}
