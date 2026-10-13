using System.Text.Json;
using System.Text.Json.Serialization;
using HandheldZoom.Core.Magnification;
using HandheldZoom.Core.QuickZoom;
using HandheldZoom.Core.Input;

namespace HandheldZoom.Core.Profiles;

public sealed record ZoomProfile
{
    public string Name { get; init; } = "Standard";
    public string[] ProcessNames { get; init; } = [];
    public float Factor { get; init; } = 1.5f;
    public double FocusX { get; init; } = .5;
    public double FocusY { get; init; } = .5;
    public ZoomMode Mode { get; init; } = ZoomMode.Hold;
    public string ControllerButtons { get; init; } = "LB+RB";
    public bool StickControl { get; init; } = true;
    public ZoomTransform Transform(int width, int height) => ZoomMath.FromNormalizedFocus(width, height, Factor, FocusX, FocusY);
    public void Validate()
    {
        GamepadChord.Parse(ControllerButtons);
        if (string.IsNullOrWhiteSpace(Name) || Name != Name.Trim()) throw new ArgumentException("Profilname fehlt oder enthält äußere Leerzeichen.");
        if (!float.IsFinite(Factor) || Factor <= 1 || Factor > 5) throw new ArgumentException($"Profil {Name}: Faktor muss größer als 1 und höchstens 5 sein.");
        if (!double.IsFinite(FocusX) || !double.IsFinite(FocusY) || FocusX is < 0 or > 1 || FocusY is < 0 or > 1)
            throw new ArgumentException($"Profil {Name}: Fokuswerte müssen zwischen 0 und 1 liegen.");
        if (!Enum.IsDefined(Mode)) throw new ArgumentException($"Profil {Name}: Ungültiger Modus.");
        if (ProcessNames is null) throw new ArgumentException($"Profil {Name}: Prozessliste darf nicht null sein.");
        foreach (var process in ProcessNames) NormalizeProcess(process);
    }
    public static string NormalizeProcess(string process)
    {
        if (string.IsNullOrWhiteSpace(process) || process != process.Trim() || process.IndexOfAny(['/', '\\', ':', '*', '?']) >= 0)
            throw new ArgumentException("Prozessname muss ein einfacher Name ohne Pfad oder Platzhalter sein.");
        var result = process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process[..^4] : process;
        if (string.IsNullOrWhiteSpace(result)) throw new ArgumentException("Leerer Prozessname.");
        return result;
    }
}

public sealed record ProfileCatalog
{
    public int SchemaVersion { get; init; } = 1;
    public ZoomProfile DefaultProfile { get; init; } = new();
    public ZoomProfile[] Profiles { get; init; } = [];
    public static ProfileCatalog Starter() => new()
    {
        Profiles = [new() { Name = "Crimson Desert", ProcessNames = ["CrimsonDesert.exe"] }]
    };
    public void Validate()
    {
        if (SchemaVersion != 1) throw new ArgumentException("Nicht unterstützte Profil-Dateiversion.");
        if (DefaultProfile is null || Profiles is null) throw new ArgumentException("Standardprofil oder Profilliste fehlt.");
        DefaultProfile.Validate();
        if (DefaultProfile.ProcessNames.Length != 0) throw new ArgumentException("Das Standardprofil darf keine Prozesszuordnung haben.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultProfile.Name };
        var processes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles)
        {
            if (profile is null) throw new ArgumentException("Leeres Profil.");
            profile.Validate();
            if (!names.Add(profile.Name)) throw new ArgumentException($"Doppelter Profilname: {profile.Name}.");
            foreach (var process in profile.ProcessNames)
                if (!processes.Add(ZoomProfile.NormalizeProcess(process))) throw new ArgumentException($"Doppelte Prozesszuordnung: {process}.");
        }
    }
    public ZoomProfile Resolve(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return DefaultProfile;
        var normalized = ZoomProfile.NormalizeProcess(processName);
        return Profiles.FirstOrDefault(p => p.ProcessNames.Any(n => string.Equals(ZoomProfile.NormalizeProcess(n), normalized, StringComparison.OrdinalIgnoreCase))) ?? DefaultProfile;
    }
    public ZoomProfile ByName(string name) =>
        new[] { DefaultProfile }.Concat(Profiles).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Profil nicht gefunden: {name}.");
}

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter<ZoomMode>(allowIntegerValues: false) }
    };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HandheldZoom", "profiles.json");
    public static ProfileCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<ProfileCatalog>(json, Options) ?? throw new ArgumentException("Die Profildatei ist leer.");
        catalog.Validate(); return catalog;
    }
    public static string Serialize(ProfileCatalog catalog) { catalog.Validate(); return JsonSerializer.Serialize(catalog, Options); }
    public static void Save(string path, ProfileCatalog catalog, string expectedContents)
    {
        var json = Serialize(catalog);
        var fullPath = Path.GetFullPath(path);
        if (File.ReadAllText(fullPath) != expectedContents)
            throw new IOException("Die Profildatei wurde außerhalb der Anwendung geändert. Bitte neu laden, bevor du speicherst.");
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json); writer.Flush(); stream.Flush(true);
            }
            if (File.ReadAllText(fullPath) != expectedContents)
                throw new IOException("Die Profildatei wurde während des Speicherns geändert. Bitte neu laden.");
            File.Replace(temporary, fullPath, fullPath + ".bak");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static ProfileCatalog Load(string path) => Parse(File.ReadAllText(path));
    public static ProfileCatalog LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(Serialize(ProfileCatalog.Starter()));
            }
            catch (IOException) when (File.Exists(path)) { /* Another instance created it; read its contents. */ }
        }
        return Load(path);
    }
}
