# Handheld Zoom – weiterarbeiten

Stand: 13. Oktober 2026.

## Aktueller Stand

Meilensteine 1–5 sind implementiert. Die Anwendung heißt auf Nutzerwunsch **hand bald eys for old guys** und liegt als portable WPF-Anwendung in `artifacts/app/hand bald eys for old guys.exe`. Daneben liegt `HandheldZoom.Recovery.exe`. `artifacts/desktop` ist die Publish-Zwischenausgabe.

GUI: Spiele hinzufügen über EXE oder laufende Anwendung, Profile wählen/bearbeiten/speichern/entfernen, Faktor/Fokus/Halten/Toggle, automatische oder feste Profilwahl, globale Eingaben, Start/Stop/Reset und Tray. Standard: Ctrl+Alt+Z / Ctrl+Alt+R, Controller 0 mit LB+RB. Profile dauerhaft unter `%LOCALAPPDATA%/HandheldZoom/profiles.json`; Sicherung `.bak`; Aktivierungstasten (einschließlich LT/RT) und Stick-Steuerung werden pro Profil gespeichert; Tastatur-Hotkeys und Controller-Platz gelten für die Sitzung.

Konsolen- und GUI-App teilen sich ZoomRuntime und den Instanz-Mutex. Alte Konsole vor Start der GUI-Steuerung beenden. Änderungen im Editor stoppen die Steuerung; nach Speichern wieder starten. Normales Schließen beendet, Minimieren/In den Tray lässt weiterlaufen.

## Nachweise

Build 0 Fehler/0 Warnungen, 45/45 automatisierte Core-Tests. WPF-Editor und Persistenz headless getestet und visuell gerendert (große und aktuelle Handheld-Dimensionen). Portable EXE --ui-check und --engine-check erfolgreich; zwei Start/Stop-Zyklen mit freigegebenen Hotkeys und Mutex, ohne Transform-Schreiboperationen. API-Status 100 %, (0,0), 1600×900/144 DPI. Tastatur- und Controllerbetrieb der bisherigen Konsole wurden vom Nutzer bestätigt.

Crimson-Desert-Profil vorbereitet: 150 %, Mitte, Hold, CrimsonDesert.exe. Nutzer startet Spiel für manuelle Abnahme. Keine neue Aussage zu Spielekompatibilität/Performance. GUI-Tray, Dialoge und echte Controller-/Tastatur-Zoomzyklen in WPF noch manuell prüfen, siehe TESTING.md.

## Nächster Schritt

GUI-Abnahme am Gerät und im Spiel. Danach bei Bedarf Steuerungsfelder dauerhaft speichern, Profile live neu laden, Prozess-/Profilstatus verbessern oder gewünschte Bedienungsdetails ergänzen. Kein Autostart oder Installer angelegt.

## Einstieg und Grenzen

README.md: Bedienung. ARCHITECTURE.md: Komponenten. TESTING.md: Tests. `./build.ps1 -Publish` baut alle Projekte, führt den Test-Runner aus und erzeugt alle Portables mit lokalem .dotnet-SDK oder .NET 10.

- Primärbildschirm; kein Multi-Monitor-/Touch-Transform. Rechter Stick verschiebt den Ausschnitt, linker Stick regelt 100 bis 500 Prozent; LT/RT als Aktivierungstasten verfügbar.
- XInput unterdrückt Eingaben nicht im Spiel. Keine DLL-Injektion, Kernel-Treiber oder Anti-Cheat-Umgehung.
- Recovery erreichbar halten. Keine Änderungen an Armoury Crate, Windows-Sicherheit oder Spielen ohne ausdrückliche Freigabe.
- Bestehende/fehlerhafte Profile nicht ungefragt ersetzen; atomischer Save mit Inhaltsvergleich und .bak.
- Nur gemessene Hardwarewerte behaupten. Hard-Kill/Stromausfall kann keinen Aufräumcode garantieren.


## Manuelle Abnahme 13. Oktober 2026

Nutzer bestätigt: Im Armoury-Crate-Gamepad-Modus funktionieren Zoom und beide Sticks. XInput-Verbindung, API und Hotkeys wurden mit --check bestätigt. Startmenü-Verknüpfung angelegt. Armoury-Crate-Bibliothekseintrag noch nicht angelegt, da die Oberfläche für Computer Use nicht erreichbar war.
