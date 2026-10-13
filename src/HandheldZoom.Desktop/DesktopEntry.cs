using System;
using System.IO;
using System.Windows.Interop;
using System.Windows.Media;

namespace HandheldZoom.Desktop;

internal static class DesktopEntry
{
    [STAThread]
    public static int Main(string[] args)
    {
        var check = args.Length == 2 && (args[0] == "--ui-check" || args[0] == "--engine-check");
        var path = check ? Path.GetFullPath(args[1]) : null;
        void Stage(string value) { if (path is not null) { Directory.CreateDirectory(path); File.AppendAllText(Path.Combine(path, "startup-stages.txt"), value + Environment.NewLine); } }
        try
        {
            Stage("Entry point");
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            if (check) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            Stage("Creating WPF application");
            var app = new App();
            Stage("Loading application resources");
            app.InitializeComponent();
            Stage("Starting dispatcher");
            return app.Run();
        }
        catch (Exception ex)
        {
            Stage(ex.ToString());
            if (!check) System.Windows.MessageBox.Show(ex.Message, "hand bald eys for old guys");
            return 1;
        }
    }
}
