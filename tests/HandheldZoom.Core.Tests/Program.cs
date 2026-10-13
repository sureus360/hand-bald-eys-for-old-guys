using HandheldZoom.Core.Profiles;
using HandheldZoom.Core.Magnification;

using HandheldZoom.Core.QuickZoom;

using HandheldZoom.Core.Input;



namespace HandheldZoom.Core.Tests;



internal static class Program

{

    private static int _failures;



    public static int Main()

    {

        Run("100 % ist immer Identität", IdentityAtOneHundredPercent);

        Run("Mitte 125 %", () => AssertEqual(new ZoomTransform(1.25f, 192, 108), Transform(1.25f, 0.5, 0.5)));

        Run("Mitte 150 %", () => AssertEqual(new ZoomTransform(1.5f, 320, 180), Transform(1.5f, 0.5, 0.5)));

        Run("Mitte 175 %", () => AssertEqual(new ZoomTransform(1.75f, 411, 231), Transform(1.75f, 0.5, 0.5)));

        Run("Mitte 200 %", () => AssertEqual(new ZoomTransform(2.0f, 480, 270), Transform(2.0f, 0.5, 0.5)));

        Run("Linker oberer Rand", () => AssertEqual(new ZoomTransform(2.0f, 0, 0), Transform(2.0f, 0.0, 0.0)));

        Run("Rechter unterer Rand", () => AssertEqual(new ZoomTransform(2.0f, 960, 540), Transform(2.0f, 1.0, 1.0)));

        Run("Normierter Fokus wird begrenzt", NormalizedFocusIsClamped);

        Run("Direkte Offsets werden begrenzt", RequestedOffsetsAreClamped);

        Run("Ungültiger Faktor wird abgewiesen", InvalidFactorThrows);

        Run("Ungültige Auflösung wird abgewiesen", InvalidResolutionThrows);



        Run("Halten, Wiederholung und Loslassen", () =>

        {

            var output = new List<ZoomTransform>();

            var service = new QuickZoomService(output.Add, () => Transform(1.5f, .5, .5), ZoomMode.Hold);

            service.Press(); service.Press(); service.Release(); service.Release();

            AssertEqual(2, output.Count);

            AssertEqual(Transform(1.5f, .5, .5), output[0]);

            AssertEqual(new ZoomTransform(1, 0, 0), output[1]);

            AssertEqual(false, service.IsActive);

        });

        Run("Toggle bleibt nach Loslassen aktiv", () =>

        {

            var service = new QuickZoomService(_ => { }, () => Transform(2, .5, .5), ZoomMode.Toggle);

            service.Press(); service.Release(); AssertEqual(true, service.IsActive);

            service.Press(); service.Release(); AssertEqual(false, service.IsActive);

        });

        Run("Reset sperrt gehaltene Taste bis Loslassen", () =>

        {

            foreach (var mode in new[] { ZoomMode.Hold, ZoomMode.Toggle })

            {

                var service = new QuickZoomService(_ => { }, () => Transform(2, .5, .5), mode);

                service.Press(); service.Reset(); service.Press(); AssertEqual(false, service.IsActive);

                service.Release(); service.Press(); AssertEqual(true, service.IsActive);

            }

        });

        Run("Fehlgeschlagene Aktivierung bleibt inaktiv", () =>

        {

            var service = new QuickZoomService(_ => throw new InvalidOperationException(), () => Transform(2, .5, .5), ZoomMode.Hold);

            AssertThrows<InvalidOperationException>(service.Press);

            AssertEqual(false, service.IsActive);

        });

        Run("Fehlgeschlagener Reset kann erneut versucht werden", () =>

        {

            var fail = false;

            var service = new QuickZoomService(_ => { if (fail) throw new InvalidOperationException(); }, () => Transform(2, .5, .5), ZoomMode.Hold);

            service.Press(); fail = true;

            AssertThrows<InvalidOperationException>(service.Reset); AssertEqual(true, service.IsActive);

            fail = false; service.Reset(); AssertEqual(false, service.IsActive);

        });

        Run("Controller-Kombination und ungültige Eingaben", () =>

        {

            AssertEqual(GamepadButtons.LB | GamepadButtons.RB, GamepadChord.Parse("lb+RB"));

            AssertEqual(GamepadButtons.Back | GamepadButtons.Y, GamepadChord.Parse("Back+Y"));

            AssertEqual(GamepadButtons.LB, GamepadChord.Parse("LB"));

            AssertThrows<ArgumentException>(() => GamepadChord.Parse("LB+LB"));

            AssertEqual(GamepadButtons.LT | GamepadButtons.RB, GamepadChord.Parse("LT+RB"));

            AssertThrows<ArgumentException>(() => GamepadChord.Parse("256+512"));

            AssertThrows<ArgumentOutOfRangeException>(() => new XInputReader(4));

            AssertThrows<ArgumentOutOfRangeException>(() => new XInputReader(-1));

        });

        Run("Controller-Halten benötigt gesamte Kombination", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Hold);

            inputs.Controller(new(true, 0));

            inputs.Controller(new(true, GamepadButtons.LB)); AssertEqual(false, service.IsActive);

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB | GamepadButtons.A)); AssertEqual(true, service.IsActive);

            inputs.Controller(new(true, GamepadButtons.RB)); AssertEqual(false, service.IsActive);

        });

        Run("Controller-Toggle reagiert einmal pro Druck", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Toggle);

            inputs.Controller(new(true, 0));

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB));

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(true, service.IsActive);

            inputs.Controller(new(true, 0)); AssertEqual(true, service.IsActive);

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(false, service.IsActive);

        });

        Run("Start und Reconnect mit gehaltener Kombination bleiben neutral", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Hold);

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(false, service.IsActive);

            inputs.Controller(new(true, 0));

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(true, service.IsActive);

            inputs.Controller(default); AssertEqual(false, service.IsActive);

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(false, service.IsActive);

            inputs.Controller(new(true, 0));

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); AssertEqual(true, service.IsActive);

        });

        Run("Disconnect setzt auch Toggle zurück", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Toggle);

            inputs.Controller(new(true, 0));

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB));

            inputs.Controller(new(true, 0)); AssertEqual(true, service.IsActive);

            inputs.Controller(default); AssertEqual(false, service.IsActive);

        });

        Run("Gemischtes Halten endet erst nach beiden Eingaben", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Hold);

            inputs.Controller(new(true, 0)); inputs.Keyboard(true);

            inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB));

            inputs.Keyboard(false); AssertEqual(true, service.IsActive);

            inputs.Controller(new(true, 0)); AssertEqual(false, service.IsActive);

        });

        Run("Globaler Reset blockiert beide gehaltenen Quellen", () =>

        {

            foreach (var mode in new[] { ZoomMode.Hold, ZoomMode.Toggle })

            {

                var (service, inputs) = Inputs(mode);

                inputs.Controller(new(true, 0)); inputs.Keyboard(true);

                inputs.Controller(new(true, GamepadButtons.LB | GamepadButtons.RB)); inputs.Reset();

                inputs.Keyboard(false); inputs.Keyboard(true); AssertEqual(false, service.IsActive);

                inputs.Controller(new(true, 0)); AssertEqual(false, service.IsActive);

                inputs.Keyboard(false); inputs.Keyboard(true); AssertEqual(true, service.IsActive);

            }

        });

        Run("Disconnect bei gehaltener Tastatur verhindert Wiederaktivierung", () =>

        {

            var (service, inputs) = Inputs(ZoomMode.Hold);

            inputs.Controller(new(true, 0)); inputs.Keyboard(true);

            inputs.Controller(default); inputs.Keyboard(true); AssertEqual(false, service.IsActive);

            inputs.Keyboard(false); inputs.Keyboard(true); AssertEqual(true, service.IsActive);

        });

        Run("Stick-Totzone verhindert Drift", () =>

        {

            var navigation = new StickNavigation();

            var initial = new ZoomTransform(2, 480, 270);

            AssertEqual(initial, navigation.Step(initial, new(true, 0, 0, 7000, 8000, -8000), 1920, 1080, .016));

        });

        Run("Sticks bewegen und begrenzen Ausschnitt und Faktor", () =>

        {

            var navigation = new StickNavigation();

            var current = new ZoomTransform(2, 480, 270);

            var moved = navigation.Step(current, new(true, 0, 0, 32767, 32767, 32767), 1920, 1080, .016);

            AssertEqual(true, moved.Factor > 2 && moved.XOffset > 480 && moved.YOffset < 270);

            for (var i = 0; i < 1000; i++) moved = navigation.Step(moved, new(true, 0, 0, -32768, -32768, -32768), 1920, 1080, .016);

            AssertEqual(new ZoomTransform(1, 0, 0), moved);

            for (var i = 0; i < 1000; i++) moved = navigation.Step(moved, new(true, 0, 0, 32767, 32767, -32768), 1920, 1080, .016);

            AssertEqual(new ZoomTransform(5, 1536, 864), moved);

        });

        Run("Inaktiver Zoom ignoriert Stick-Anpassung und Reset setzt zurück", () =>

        {

            var applied = new ZoomTransform(1, 0, 0);

            var service = new QuickZoomService(t => applied = t, () => new(2, 480, 270), ZoomMode.Toggle);

            service.Adjust(new(3, 100, 100)); AssertEqual(true, applied.IsIdentity);

            service.Press(); service.Release(); service.Adjust(new(3, 100, 100));

            AssertEqual(new ZoomTransform(3, 100, 100), applied);

            service.Reset(); AssertEqual(true, service.Current.IsIdentity);

        });

        Run("Gespeicherte Spielsteuerung bleibt erhalten und alte Profile laden", () =>
        {
            var old = ProfileStore.Parse("{\"schemaVersion\":1,\"defaultProfile\":{},\"profiles\":[]}");
            AssertEqual("LB+RB", old.DefaultProfile.ControllerButtons);
            AssertEqual(true, old.DefaultProfile.StickControl);
            var next = old with { DefaultProfile = old.DefaultProfile with { ControllerButtons = "RT+Back", StickControl = false } };
            var loaded = ProfileStore.Parse(ProfileStore.Serialize(next));
            AssertEqual("RT+Back", loaded.DefaultProfile.ControllerButtons); AssertEqual(false, loaded.DefaultProfile.StickControl);
        });
        Run("Profilwechsel nutzt neue Controller-Tasten erst nach Loslassen", () =>
        {
            var service = new QuickZoomService(_ => { }, () => new(2, 480, 270), ZoomMode.Hold);
            var chord = GamepadButtons.LB;
            var router = new ZoomInputRouter(service, () => chord);
            router.Controller(new(true, 0)); router.Controller(new(true, GamepadButtons.LB));
            router.Reset(); chord = GamepadButtons.RT;
            router.Controller(new(true, GamepadButtons.RT)); AssertEqual(false, service.IsActive);
            router.Controller(new(true, 0)); router.Controller(new(true, GamepadButtons.RT)); AssertEqual(true, service.IsActive);
        });
        ProfileTests.Register(Run);

        Console.WriteLine();

        Console.WriteLine(_failures == 0 ? "Alle Berechnungstests bestanden." : $"{_failures} Test(s) fehlgeschlagen.");

        return _failures == 0 ? 0 : 1;

    }



    private static (QuickZoomService Service, ZoomInputRouter Inputs) Inputs(ZoomMode mode)

    {

        var service = new QuickZoomService(_ => { }, () => Transform(1.5f, .5, .5), mode);

        return (service, new ZoomInputRouter(service, GamepadButtons.LB | GamepadButtons.RB));

    }



    private static ZoomTransform Transform(float factor, double x, double y) =>

        ZoomMath.FromNormalizedFocus(1920, 1080, factor, x, y);



    private static void IdentityAtOneHundredPercent()

    {

        AssertEqual(new ZoomTransform(1.0f, 0, 0), Transform(1.0f, 1.0, 1.0));

    }



    private static void NormalizedFocusIsClamped()

    {

        AssertEqual(new ZoomTransform(1.5f, 0, 360), Transform(1.5f, -4.0, 8.0));

    }



    private static void RequestedOffsetsAreClamped()

    {

        AssertEqual(

            new ZoomTransform(1.5f, 640, 0),

            ZoomMath.ClampOffsets(1920, 1080, 1.5f, 50_000, -50_000));

    }



    private static void InvalidFactorThrows()

    {

        AssertThrows<ArgumentOutOfRangeException>(() => Transform(0.99f, 0.5, 0.5));

        AssertThrows<ArgumentOutOfRangeException>(() => Transform(float.NaN, 0.5, 0.5));

    }



    private static void InvalidResolutionThrows()

    {

        AssertThrows<ArgumentOutOfRangeException>(() => ZoomMath.FromNormalizedFocus(0, 1080, 1.5f, 0.5, 0.5));

    }



    private static void Run(string name, Action test)

    {

        try

        {

            test();

            Console.WriteLine($"BESTANDEN  {name}");

        }

        catch (Exception exception)

        {

            _failures++;

            Console.Error.WriteLine($"FEHLER     {name}: {exception.Message}");

        }

    }



    private static void AssertEqual<T>(T expected, T actual) where T : notnull

    {

        if (!EqualityComparer<T>.Default.Equals(expected, actual))

        {

            throw new InvalidOperationException($"Erwartet: {expected}; tatsächlich: {actual}");

        }

    }



    private static void AssertThrows<TException>(Action action) where TException : Exception

    {

        try

        {

            action();

        }

        catch (TException)

        {

            return;

        }



        throw new InvalidOperationException($"Erwartete Ausnahme {typeof(TException).Name} wurde nicht ausgelöst.");

    }

}
