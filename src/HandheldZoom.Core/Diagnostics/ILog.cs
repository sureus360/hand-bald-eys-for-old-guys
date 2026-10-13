namespace HandheldZoom.Core.Diagnostics;

public interface ILog
{
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
