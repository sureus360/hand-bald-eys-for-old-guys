using System.Text.Json;
using HandheldZoom.Core.Profiles;
using HandheldZoom.Core.Input;
using HandheldZoom.Core.QuickZoom;

namespace HandheldZoom.Core.Tests;

internal static class ProfileTests
{
    public static void Register(Action<string, Action> run)
    {
        run("Profilzuordnung ist exakt und ignoriert Großschreibung/EXE", () =>
        {
            var catalog = ProfileCatalog.Starter(); catalog.Validate();
            Equal("Crimson Desert", catalog.Resolve("CRIMSONDESERT").Name);
            Equal("Crimson Desert", catalog.Resolve("crimsondesert.EXE").Name);
            Equal("Standard", catalog.Resolve("CrimsonDesertLauncher").Name);
            Equal("Standard", catalog.Resolve(null).Name);
        });
        run("Profilfokus wird auf aktuelle Auflösung abgebildet", () =>
        {
            var profile = new ZoomProfile { Factor = 2, FocusX = 1, FocusY = 0 };
            Equal(new Magnification.ZoomTransform(2, 960, 0), profile.Transform(1920, 1080));
            Equal(new Magnification.ZoomTransform(2, 800, 0), profile.Transform(1600, 900));
        });
        run("Profil-JSON speichert benannte Modi und Fokus", () =>
        {
            var catalog = new ProfileCatalog { DefaultProfile = new() { Factor = 2, FocusX = .9, FocusY = .1, Mode = ZoomMode.Toggle } };
            var loaded = ProfileStore.Parse(ProfileStore.Serialize(catalog));
            Equal(catalog.DefaultProfile, loaded.DefaultProfile with { ProcessNames = catalog.DefaultProfile.ProcessNames });
            Equal(ZoomMode.Toggle, loaded.DefaultProfile.Mode);
        });
        run("Ungültige Faktor- und Fokuswerte werden abgewiesen", () =>
        {
            foreach (var factor in new[] { 1f, 0f, 5.1f, float.NaN, float.PositiveInfinity })
                Throws<ArgumentException>(() => new ZoomProfile { Factor = factor }.Validate());
            foreach (var focus in new[] { -.1, 1.1, double.NaN, double.PositiveInfinity })
            {
                Throws<ArgumentException>(() => new ZoomProfile { FocusX = focus }.Validate());
                Throws<ArgumentException>(() => new ZoomProfile { FocusY = focus }.Validate());
            }
        });
        run("Doppelte Profile und Prozesszuordnungen werden abgewiesen", () =>
        {
            Throws<ArgumentException>(() => new ProfileCatalog { Profiles = [new() { Name = "standard" }] }.Validate());
            Throws<ArgumentException>(() => new ProfileCatalog { Profiles = [new() { Name = "A", ProcessNames = ["Game.exe"] }, new() { Name = "B", ProcessNames = ["game"] }] }.Validate());
            Throws<ArgumentException>(() => new ZoomProfile { ProcessNames = ["C:\\Game.exe"] }.Validate());
            Throws<ArgumentException>(() => new ZoomProfile { ProcessNames = ["*"] }.Validate());
        });
        run("Fehlerhaftes und unbekanntes JSON wird abgewiesen", () =>
        {
            Throws<JsonException>(() => ProfileStore.Parse("{"));
            Throws<JsonException>(() => ProfileStore.Parse("{\"defaultProfile\":{\"facotr\":2}}"));
            Throws<JsonException>(() => ProfileStore.Parse("{\"defaultProfile\":{\"mode\":\"invalid\"}}"));
            Throws<JsonException>(() => ProfileStore.Parse("{\"defaultProfile\":{\"mode\":0}}"));
            Throws<ArgumentException>(() => ProfileStore.Parse("{\"schemaVersion\":2}"));
            Throws<ArgumentException>(() => ProfileStore.Parse("{\"defaultProfile\":null}"));
            Throws<ArgumentException>(() => ProfileStore.Parse("{\"profiles\":[null]}"));
        });
        run("Profilwechsel setzt vor Übernahme neuer Einstellungen zurück", () =>
        {
            var session = new ProfileSession(ProfileCatalog.Starter());
            var resets = 0;
            session.Update(new(1, "explorer"), () => resets++); Equal(0, resets);
            session.Update(new(2, "CrimsonDesert"), () => { Equal("Standard", session.Current.Name); resets++; });
            Equal("Crimson Desert", session.Current.Name); Equal(1, resets);
            session.Update(new(2, "CrimsonDesert"), () => resets++); Equal(1, resets);
            session.Update(new(3, "explorer"), () => resets++); Equal("Standard", session.Current.Name); Equal(2, resets);
        });
        run("Alt-Tab setzt auch bei identischem Standardprofil zurück", () =>
        {
            var session = new ProfileSession(ProfileCatalog.Starter()); var resets = 0;
            session.Update(new(1, "explorer"), () => resets++);
            session.Update(new(2, "notepad"), () => resets++); Equal(1, resets);
            session.Update(default, () => resets++); Equal(2, resets);
        });
        run("Festes Profil bleibt bei Vordergrundwechsel gewählt", () =>
        {
            var session = new ProfileSession(ProfileCatalog.Starter(), "crimson desert"); var resets = 0;
            session.Update(new(1, "explorer"), () => resets++);
            session.Update(new(2, "notepad"), () => resets++);
            Equal("Crimson Desert", session.Current.Name); Equal(1, resets);
            Throws<ArgumentException>(() => new ProfileSession(ProfileCatalog.Starter(), "Unbekannt"));
        });
        run("Fehlgeschlagener Reset verhindert Profilwechsel", () =>
        {
            var session = new ProfileSession(ProfileCatalog.Starter());
            session.Update(new(1, "explorer"), () => { });
            Throws<InvalidOperationException>(() => session.Update(new(2, "CrimsonDesert"), () => throw new InvalidOperationException()));
            Equal("Standard", session.Current.Name);
            session.Update(new(2, "CrimsonDesert"), () => { }); Equal("Crimson Desert", session.Current.Name);
        });
        run("Profilwechsel bei gehaltener Eingabe verlangt Loslassen", () =>
        {
            var session = new ProfileSession(ProfileCatalog.Starter());
            var service = new QuickZoomService(_ => { }, () => session.Current.Transform(1920, 1080), () => session.Current.Mode);
            var inputs = new ZoomInputRouter(service, GamepadButtons.LB | GamepadButtons.RB);
            session.Update(new(1, "explorer"), inputs.Reset);
            inputs.Keyboard(true); Equal(true, service.IsActive);
            session.Update(new(2, "CrimsonDesert"), inputs.Reset); Equal(false, service.IsActive);
            inputs.Keyboard(true); Equal(false, service.IsActive);
            inputs.Keyboard(false); inputs.Keyboard(true); Equal(true, service.IsActive);
        });
        run("Benutzerprofile bleiben bei erneutem Laden erhalten", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "HandheldZoom-ProfileTest-" + Guid.NewGuid());
            var path = Path.Combine(directory, "profiles.json");
            try
            {
                Equal(1, ProfileStore.LoadOrCreate(path).Profiles.Length);
                var custom = new ProfileCatalog { DefaultProfile = new() { Factor = 2 } };
                File.WriteAllText(path, ProfileStore.Serialize(custom));
                Equal(2f, ProfileStore.LoadOrCreate(path).DefaultProfile.Factor);
                Equal(0, ProfileStore.LoadOrCreate(path).Profiles.Length);
                File.WriteAllText(path, "broken");
                Throws<JsonException>(() => ProfileStore.LoadOrCreate(path));
                Equal("broken", File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }
        });
        run("Atomisches Speichern erhält vorherige Datei als Sicherung", () => WithFile(path =>
        {
            var previous = File.ReadAllText(path);
            ProfileStore.Save(path, new() { DefaultProfile = new() { Factor = 2 } }, previous);
            Equal(2f, ProfileStore.Load(path).DefaultProfile.Factor);
            Equal(previous, File.ReadAllText(path + ".bak"));
            Equal(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length);
        }));
        run("Externe Änderungen werden beim Speichern nicht überschrieben", () => WithFile(path =>
        {
            var previous = File.ReadAllText(path);
            var external = ProfileStore.Serialize(new() { DefaultProfile = new() { Factor = 3 } });
            File.WriteAllText(path, external);
            Throws<IOException>(() => ProfileStore.Save(path, ProfileCatalog.Starter(), previous));
            Equal(external, File.ReadAllText(path));
            Equal(false, File.Exists(path + ".bak"));
        }));
        run("Ungültiges Profil lässt gespeicherte Datei unverändert", () => WithFile(path =>
        {
            var previous = File.ReadAllText(path);
            Throws<ArgumentException>(() => ProfileStore.Save(path, new() { DefaultProfile = new() { Factor = 0 } }, previous));
            Equal(previous, File.ReadAllText(path));
        }));
        run("Halten verwendet Modus der Aktivierung bis Loslassen", () =>
        {
            var mode = ZoomMode.Hold;
            var service = new QuickZoomService(_ => { }, () => new(1.5f, 0, 0), () => mode);
            service.Press(); mode = ZoomMode.Toggle; service.Release(); Equal(false, service.IsActive);
            service.Press(); service.Release(); Equal(true, service.IsActive);
        });
    }
    private static void WithFile(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "HandheldZoom-SaveTest-" + Guid.NewGuid());
        var path = Path.Combine(directory, "profiles.json");
        try { ProfileStore.LoadOrCreate(path); test(path); }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Erwartet {expected}, tatsächlich {actual}.");
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Erwartete Ausnahme: {typeof(T).Name}.");
    }
}
