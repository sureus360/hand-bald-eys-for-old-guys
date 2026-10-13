using System.Runtime.InteropServices;
using System.Diagnostics;
using HandheldZoom.Core.Diagnostics;

namespace HandheldZoom.Core.Magnification;

public sealed class MagnificationController : IDisposable
{
    private readonly object _gate = new();
    private readonly ILog _log;
    private bool _initialized;
    private bool _disposed;
    private bool _changed;

    private MagnificationController(ILog log, ZoomTransform original)
    {
        _log = log;
        Original = original;
        _initialized = true;
    }

    public ZoomTransform Original { get; }

    public static MagnificationController Open(ILog log, bool blockExistingMagnification = true)
    {
        ArgumentNullException.ThrowIfNull(log);
        EnsureSupportedProcess();

        var magnifyProcessDetected = MagnifierConflictDetector.IsWindowsMagnifierRunning();
        if (!NativeMethods.MagInitialize())
        {
            ThrowApiFailure("MagInitialize", log);
        }

        try
        {
            var original = GetCurrentCore(log);
            log.Info($"Magnification API initialisiert. Ausgangszustand: {original}.");

            if (blockExistingMagnification && (magnifyProcessDetected || !original.IsIdentity))
            {
                var reason = magnifyProcessDetected
                    ? "Der Windows-Lupe-Prozess Magnify.exe läuft."
                    : $"Es ist bereits ein nicht neutraler Transform aktiv: {original}.";
                throw new InvalidOperationException($"Konflikt verhindert: {reason} Zuerst die Windows-Lupe schließen bzw. Recovery bewusst ausführen.");
            }

            return new MagnificationController(log, original);
        }
        catch
        {
            if (!NativeMethods.MagUninitialize())
            {
                log.Error(ApiFailureMessage("MagUninitialize"));
            }

            throw;
        }
    }

    public ZoomTransform GetCurrent()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return GetCurrentCore(_log);
        }
    }

    public void Set(ZoomTransform transform)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _changed = true;
            SetCore(transform, "Zoom setzen");
        }
    }

    public void ResetToIdentity()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _changed = true;
            SetCore(new ZoomTransform(1.0f, 0, 0), "Sofort-Reset");
        }
    }

    public void RestoreOriginal()
    {
        lock (_gate)
        {
            if (_disposed || !_initialized || !_changed)
            {
                return;
            }

            SetCore(Original, "Ausgangszustand wiederherstellen");
            _changed = false;
        }
    }

    /// <summary>
    /// Bestätigt den aktuell verifizierten Zustand als gewünschten Endzustand.
    /// Ausschließlich für das eigenständige Recovery-Werkzeug verwenden.
    /// </summary>
    public void KeepCurrentOnDispose()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _changed = false;
            _log.Info("Aktueller Transform wurde als dauerhafter Endzustand übernommen.");
        }
    }

    private void SetCore(ZoomTransform transform, string operation)
    {
        var stopwatch = Stopwatch.StartNew();
        if (!NativeMethods.MagSetFullscreenTransform(transform.Factor, transform.XOffset, transform.YOffset))
        {
            ThrowApiFailure("MagSetFullscreenTransform", _log);
        }

        var actual = GetCurrentCore(_log);
        if (Math.Abs(actual.Factor - transform.Factor) > 0.001f || actual.XOffset != transform.XOffset || actual.YOffset != transform.YOffset)
        {
            var exception = new InvalidOperationException($"API-Verifikation fehlgeschlagen. Erwartet {transform}, gelesen {actual}.");
            _log.Error(operation, exception);
            throw exception;
        }

        stopwatch.Stop();
        _log.Info($"{operation}: {actual}; Setzen und Zurücklesen={stopwatch.Elapsed.TotalMilliseconds:F2} ms.");
    }

    private static ZoomTransform GetCurrentCore(ILog log)
    {
        if (!NativeMethods.MagGetFullscreenTransform(out var factor, out var x, out var y))
        {
            ThrowApiFailure("MagGetFullscreenTransform", log);
        }

        return new ZoomTransform(factor, x, y);
    }

    private static void EnsureSupportedProcess()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            throw new PlatformNotSupportedException("Vollbildvergrößerung benötigt Windows 8 oder neuer.");
        }

        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Die Magnification API wird unter WOW64 nicht unterstützt. Bitte den x64-Build starten.");
        }
    }

    private static void ThrowApiFailure(string apiName, ILog log)
    {
        var error = Marshal.GetLastPInvokeError();
        var exception = new MagnificationApiException(apiName, error);
        log.Error(exception.Message, exception);
        throw exception;
    }

    private static string ApiFailureMessage(string apiName)
    {
        var error = Marshal.GetLastPInvokeError();
        return new MagnificationApiException(apiName, error).Message;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_changed)
                {
                    SetCore(Original, "Dispose-Wiederherstellung");
                    _changed = false;
                }
            }
            finally
            {
                if (_initialized)
                {
                    if (!NativeMethods.MagUninitialize())
                    {
                        _log.Error(ApiFailureMessage("MagUninitialize"));
                    }
                    else
                    {
                        _log.Info("Magnification API freigegeben.");
                    }

                    _initialized = false;
                }

                _disposed = true;
            }
        }
    }
}
