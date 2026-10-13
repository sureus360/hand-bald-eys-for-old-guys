# Architektur – Meilenstein 1

## Komponenten

- `HandheldZoom.Core`: P/Invoke-Grenze, Zoommathematik, Primärbildschirmdaten, Konflikterkennung und transaktionaler API-Lebenszyklus.
- `HandheldZoom.MagnificationTest`: absichtlich kleine Konsolenanwendung für Status, 150-%-Test, Verschieben und Reset.
- `HandheldZoom.Recovery`: unabhängiger, minimaler 100-%-Reset.
- `HandheldZoom.Core.Tests`: abhängigkeitenfreier Test-Runner für Offsetberechnung und Clamping.

## Sicherheitsmodell

`MagnificationController.Open` ruft `MagInitialize` auf und liest sofort den bestehenden Zustand mit `MagGetFullscreenTransform`. Der Test verweigert schreibende Änderungen, wenn `Magnify.exe` läuft oder bereits ein nicht neutraler Transform aktiv ist. Jede Schreiboperation wird über `MagGetFullscreenTransform` zurückgelesen und verifiziert.

Der Controller speichert den Ausgangszustand und stellt ihn in `Dispose` wieder her. Die Test-App ergänzt dies um `Console.CancelKeyPress` und `ProcessExit`. Weil kein Prozess einen harten Absturz zuverlässig behandeln kann, existiert eine separate Recovery-App, die Konfliktprüfungen bewusst übergeht und `(1.0, 0, 0)` setzt.

## Koordinatenmodell

Die Windows-API erwartet Offsets in nicht vergrößerten, physischen Pixeln relativ zur linken oberen Ecke des Primärmonitors. Die Manifestdateien aktivieren Per-Monitor-V2-DPI-Awareness; die API-Offets selbst werden laut Microsoft nicht von DPI-Skalierung beeinflusst.

Für Faktor `f`, Bildschirmbreite `W` und Höhe `H` ist der sichtbare Quellbereich `W/f × H/f`. Zulässige Offsets für den Primärmonitor sind:

```text
x = 0 .. floor(W - W/f)
y = 0 .. floor(H - H/f)
```

Ein normierter Fokuspunkt `(nx, ny)` wird auf die Mitte dieses sichtbaren Quellbereichs abgebildet und danach geclamped. Dadurch bleiben gespeicherte Punkte auflösungsunabhängig.

## Bewusste Grenzen

- Multi-Monitor-Geometrie ist noch nicht Teil von Meilenstein 1.
- Eingabetransformation für Touch/Stift (`MagSetInputTransform`) ist nicht implementiert. Sie benötigt UIAccess und ist für Quick-Zoom-Controllersteuerung zunächst nicht erforderlich.
- Die Prozessprüfung auf `Magnify.exe` ist eine konservative Heuristik; der zusätzlich gelesene Transform fängt bereits aktive Zustände ab.
- Der Fullscreen-Transform ist systemweit. Parallele Lupenprogramme dürfen daher nicht gleichzeitig schreiben.

## Geplante Erweiterung

Meilenstein 2 ergänzt einen globalen Tastatur-Hotkey und Halten/Toggle. Meilenstein 3 ergänzt XInput-Polling; Stickeingaben bleiben als experimentell gekennzeichnet, weil XInput-Polling das Spiel nicht am Empfang derselben Eingaben hindert.

## Erweiterung vom 12.10.2026 – Meilenstein 2

`HandheldZoom.QuickZoom` ist eine Konsolenanwendung mit unsichtbarem WinForms-Nachrichtenfenster. RegisterHotKey mit MOD_NOREPEAT liefert Aktivierungen und einen getrennten Reset. Ein Timer prüft ausschließlich die konfigurierte Kombination mit dem hohen Bit von GetAsyncKeyState auf Loslassen. QuickZoomService kapselt Halten/Toggle und Reset ohne native Abhängigkeiten; alle normalen Eingabe- und Zoomoperationen laufen auf demselben Thread. Konfiguration erfolgt über Startparameter. Keine Hooks oder Injektion.

Die Wiederherstellungsmarkierung im MagnificationController wird vor dem Schreibversuch gesetzt, damit auch eine fehlgeschlagene Verifikation einen Wiederherstellungsversuch auslöst. build.ps1 bricht nun nach fehlgeschlagenen Restore-, Build-, Test- und Publish-Aufrufen ab.

## Meilenstein 3 – XInput

Core/Input enthält die native XInput-1.4-Grenze (XINPUT_STATE mit sequenziellem Layout), benannte Button-Kombinationen und ZoomInputRouter. Ein ausgewählter Platz 0–3 wird auf dem vorhandenen Nachrichtenthread abgefragt, getrennte Geräte mit 1-s-Wiederholungsintervall. Fehler außer ERROR_DEVICE_NOT_CONNECTED beenden die App über Guard und den vorhandenen Wiederherstellungspfad. Verbindungswechsel werden protokolliert.

Der Router führt Tastatur und Gamepad als gemeinsame Eingabe zusammen und verhindert doppelte Toggle-Ereignisse während Halten. Reset sperrt Wiederaktivierung bis zum Loslassen beider Quellen. Disconnect setzt den Zoom auch bei Toggle zurück; Reconnect mit gedrückter Kombination bleibt gesperrt bis zum Loslassen. Der XInput-Index wird absichtlich nicht automatisch gewechselt. Stick-Panning und Trigger sind nicht Teil dieser Umsetzung.

## Meilenstein 4 – Spielprofile (13.10.2026)

Core/Profiles kapselt Profilmodell, validierten JSON-Speicher, Zuordnung und Vordergrundabfrage. Die Benutzerdatei liegt außerhalb der portablen Ausgabe unter LocalAppData, wird nur bei Abwesenheit erstellt und bei jedem Start validiert. Eigene Dateipfade werden ausschließlich gelesen. Profile speichern Faktor, normierten Fokus und Hold/Toggle; globale Eingabekombinationen bleiben Startparameter. Schema-Version 1, strenge Eigenschaften-/Enum-Prüfung, eindeutige Profilnamen und Prozesszuordnungen.

ProfileSession setzt bei Prozesskontextwechsel über einen Callback zuerst aktiven Zoom zurück und übernimmt erst danach neue Werte. Der Router sperrt dabei gehaltene Eingaben. QuickZoomService liest den ausgewählten Modus beim Aktivieren und hält diesen bis zum Loslassen fest. Vordergrundabfrage alle 250 ms sowie bei Aktivierungsversuchen; Prozessnamen werden pro zuletzt gesehenem PID gecacht. Unlesbare Prozessnamen werden nach einer Sekunde erneut versucht. Unbekannte Anwendungen fallen auf das Standardprofil zurück. Ein festes Profil überschreibt die Zuordnung, behält aber Reset bei Kontextwechsel.

Die vorbereitete Crimson-Desert-Zuordnung ist konfigurierbar und noch nicht am gestarteten Spiel nachgewiesen. Im Konsolenlog stehen Profil und gelesener Vordergrundprozess. Beispiel-JSON und Starthelfer werden mitveröffentlicht. Kein automatisches Neuladen, keine per-Profil-Hotkeys, keine Spieleinstellungen geändert.

## Meilenstein 5 – Desktop-Oberfläche (13.10.2026)

`HandheldZoom.Desktop` ist eine WPF-WinExe mit dem vom Nutzer vorgegebenen Namen „hand bald eys for old guys“. Sie referenziert die QuickZoom-Assembly und verwendet denselben ZoomRuntime wie die Konsole: Magnification-Lebenszyklus, ProfileSession, Hotkeys und XInput laufen auf dem jeweiligen UI-/Nachrichtenthread. NativeWindow und WinForms-Timer empfangen Nachrichten über den WPF-Dispatcher. Der benannte Mutex ist jetzt explizit erwerbbar/freigebbar, damit Start/Stop/Start in einem Prozess funktioniert. Aufräumen versucht die Controller-Wiederherstellung auch bei Fehlern des Eingabefensters.

Oberfläche: Spieleliste, EXE-Auswahl, Liste laufender Anwendungen, Name/Prozess/Faktor/Fokus/Modus, automatische oder feste Profilwahl, globale Eingabekonfiguration, Start/Stop und Reset. Änderungen stoppen die Steuerung; Profile werden mit ausdrücklichem Speichern übernommen. Speichern validiert den gesamten Katalog, prüft den geladenen Dateiinhalt auf externe Änderungen und ersetzt die Datei über eine synchron geschriebene temporäre Datei und File.Replace mit .bak. Ungespeicherte Änderungen werden vor Wechsel/Neuladen/Beenden abgefragt.

NotifyIcon bietet Wiederöffnen, Reset, Stop und Beenden. Minimieren blendet das Fenster in den Tray aus; normales Schließen beendet und stellt wieder her. Per-Monitor-V2-Awareness wird vor WPF-Initialisierung gesetzt, die Fenstergröße auf den Arbeitsbereich begrenzt. Die Portables unter artifacts/app enthalten die selbstenthaltende WPF-EXE und unabhängige Recovery. artifacts/desktop ist die Build-Zwischenausgabe. Keine Installation, Autostart-Registrierung oder Spieleingriffe.
