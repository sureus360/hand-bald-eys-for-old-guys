using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HandheldZoom.Core.Profiles;

namespace HandheldZoom.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            if (e.Args.Length == 2 && e.Args[0] == "--engine-check")
            {
                var output = Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(output);
                var path = Path.Combine(output, "engine-check-profiles.json");
                File.WriteAllText(path, ProfileStore.Serialize(ProfileCatalog.Starter()));
                var enginePreview = new MainWindow(path, preview: true);
                enginePreview.CheckEngine();
                File.WriteAllText(Path.Combine(output, "engine-check-result.txt"), "PASS: WPF-ZoomRuntime zweimal gestartet und gestoppt, Hotkeys freigegeben, Mutex erneut nutzbar. Keine Transform-Schreiboperation.");
                enginePreview.Close(); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && e.Args[0] == "--ui-check")
            {
                var output = Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(output);
                var profilePath = Path.Combine(output, "ui-check-profiles.json");
                File.WriteAllText(profilePath, ProfileStore.Serialize(ProfileCatalog.Starter()));
                var preview = new MainWindow(profilePath, preview: true);
                preview.CheckEditor();
                RenderPreview(preview, 1100, 800, Path.Combine(output, "desktop-preview.png"));
                var previewArea = SystemParameters.WorkArea;
                RenderPreview(preview, Math.Max(820, Math.Min(1100, previewArea.Width - 32)), Math.Max(480, Math.Min(800, previewArea.Height - 32)), Path.Combine(output, "desktop-preview-handheld.png"));
                File.WriteAllText(Path.Combine(output, "ui-check-result.txt"), "PASS: Spiel hinzufügen, Faktor/Fokus/Modus bearbeiten, atomar speichern und neu laden. Kein sichtbares Fenster, keine Zoom-API.");
                preview.Close(); Shutdown(0); return;
            }
            MainWindow = new MainWindow();
            var area = SystemParameters.WorkArea;
            MainWindow.Width = Math.Max(MainWindow.MinWidth, Math.Min(1100, area.Width - 32));
            MainWindow.Height = Math.Max(MainWindow.MinHeight, Math.Min(800, area.Height - 32));
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            if (e.Args.Length == 2 && (e.Args[0] == "--ui-check" || e.Args[0] == "--engine-check"))
                File.WriteAllText(Path.Combine(Path.GetFullPath(e.Args[1]), e.Args[0] == "--engine-check" ? "engine-check-result.txt" : "ui-check-result.txt"), ex.ToString());
            else MessageBox.Show(ex.Message, "hand bald eys for old guys", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    private static void RenderPreview(MainWindow window, double width, double height, string path)
    {
        window.Width = width; window.Height = height;
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 27, 38)), null, new Rect(0, 0, width, height));
        bitmap.Render(background); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
