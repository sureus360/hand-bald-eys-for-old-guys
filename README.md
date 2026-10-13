# Handheld Zoom

Native, ressourcenschonende Bildschirmvergrößerung für Windows-11-Gaming-Handhelds. Der aktuelle Stand umfasst **Meilensteine 1 bis 5**: API-Test und Quick Zoom mit globalen Tastenkombinationen und XInput-Controllersteuerung. Tastatur- und Controllerbetrieb sowie Zoom und beide Sticks im Gamepad-Modus wurden vom Nutzer bestätigt. Spielprofile sind ergänzt; automatische Erkennung und Verhalten im Spiel müssen noch geprüft werden. Die WPF-Anwendung mit Spieleliste, Profileditor und Tray ist vorhanden.

## Anwendung starten

**hand bald eys for old guys**: `artifacts\app\hand bald eys for old guys.exe` doppelklicken. Die portable Anwendung benötigt keine installierte .NET-Laufzeit.

1. Eine bisher laufende QuickZoom-Konsolenanwendung beenden, bevor du in der Oberfläche Zoom startest.
2. Links Crimson Desert oder ein anderes Profil wählen. Mit **+ Spiel hinzufügen** eine EXE auswählen; alternativ **Laufendes Spiel wählen** für eine bereits gestartete Anwendung.
3. Spielname, EXE-Name, Vergrößerung, Fokus und Halten/Toggle einstellen; **Profil speichern**.
4. **Spielprofil automatisch erkennen** eingeschaltet lassen oder ausschalten, um das links gewählte Profil fest zu nutzen.
5. **Zoom starten**, zurück ins Spiel wechseln und LB+RB bzw. Ctrl+Alt+Z verwenden. **Zoom stoppen** stellt den Ausgangszustand wieder her.

Das Fenster passt sich an die verfügbare Bildschirmfläche an; rechts können die Einstellungen gescrollt werden. Minimieren oder **In den Tray** blendet das Fenster aus, während die Zoom-Steuerung weiterläuft. Doppelklick auf das Tray-Symbol öffnet es wieder; das Tray-Menü enthält Reset, Stoppen und Beenden. Das Fenster schließen beendet die Anwendung und den Zoom.

Profilwerte werden dauerhaft in `%LOCALAPPDATA%\HandheldZoom\profiles.json` gespeichert. Die vorherige Datei bleibt als `profiles.json.bak` erhalten; externe Änderungen führen zum Konflikthinweis statt Überschreiben. Mit ↻ über der Spieleliste lässt sich die Datei neu laden. Entfernen löscht ausschließlich ein Profil. Änderungen und Profilwahl stoppen die laufende Steuerung; danach erneut **Zoom starten**. Aktivierungstasten und Stick-Steuerung werden pro Spielprofil gespeichert. Tastatur-Hotkeys und Controller-Platz gelten für die aktuelle Sitzung.

**100 % Reset** funktioniert auch bei gestoppter Steuerung. Die unabhängige Recovery liegt direkt neben der Anwendung: `HandheldZoom.Recovery.exe`. Die Anwendung startet weder Spiele noch verändert sie deren Dateien. Crimson-Desert-Prozesszuordnung ist vorbereitet; Spielkompatibilität bleibt gesondert zu prüfen.
## Voraussetzungen

- Windows 11 x64
- Zum Bauen: Visual Studio 2022 (aktuell) oder .NET 10 SDK
- Die integrierte Windows-Lupe muss für den schreibenden Test geschlossen sein.

Die ausführbaren Projekte sind absichtlich x64. Microsoft dokumentiert, dass die Magnification API unter WOW64 (32-Bit-Prozess auf 64-Bit-Windows) nicht unterstützt wird.

Falls im Arbeitsordner ein lokales SDK unter `.dotnet` liegt, verwendet `build.ps1` dieses automatisch; andernfalls das systemweit installierte SDK.

## Bauen

PowerShell im Projektordner öffnen:

```powershell
.\build.ps1
```

Portable, selbstenthaltende x64-Dateien erzeugen:

```powershell
.\build.ps1 -Publish
```

Die Ausgaben liegen danach unter `artifacts\magnification-test`, `artifacts\quick-zoom` und `artifacts\recovery`. Auf dem Zielgerät ist dafür keine .NET-Laufzeit nötig.

## Sicher testen

1. Zuerst nur lesen; dies verändert den Bildschirm nicht:

   ```powershell
   .\src\HandheldZoom.MagnificationTest\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\HandheldZoom.MagnificationTest.exe --status
   ```

2. Die integrierte Windows-Lupe schließen.
3. Interaktiven Test starten:

   ```powershell
   .\src\HandheldZoom.MagnificationTest\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\HandheldZoom.MagnificationTest.exe --run
   ```

4. Pfeiltasten verschieben den Ausschnitt, `M` zentriert, `R`, `Esc` oder `Enter` setzt sofort auf 100 % zurück und beendet. `Strg+C` stellt ebenfalls den gespeicherten Ausgangszustand wieder her.

Alternativ führt `--auto` eine kurze Sequenz Mitte → rechts unten → links oben → Reset aus.

## Notfall-Reset

`HandheldZoom.Recovery.exe` ist unabhängig von der Test-App und setzt den systemweiten Vollbildtransform auf exakt 100 %, Offset `(0, 0)`. Die Recovery blockiert bewusst nicht, wenn `Magnify.exe` läuft, weil ihr einziger Zweck die Wiederherstellung ist.

```powershell
.\src\HandheldZoom.Recovery\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\HandheldZoom.Recovery.exe
```

Bei der veröffentlichten Variante genügt `artifacts\recovery\HandheldZoom.Recovery.exe`. Die Windows-Kombination `Win+Esc` beendet zusätzlich die integrierte Windows-Lupe.

Ein normaler Programmabbruch, `Strg+C` und `ProcessExit` werden abgefangen. Ein harter Abbruch (Stromverlust, Kernel-/Treiberfehler oder sofortiges Prozess-Kill) kann keinen verwalteten Aufräumcode garantieren; dafür muss die separate Recovery-Datei erreichbar bleiben.

## Protokolle

Jeder Lauf schreibt unter `%LOCALAPPDATA%\HandheldZoom\Logs`. API-Name, Win32-Fehlercode, gelesener Ausgangszustand und jeder bestätigte Transform werden protokolliert.

## Wichtige Grenzen dieses Meilensteins

- Nur der Primärbildschirm wird für Berechnung und Clamping verwendet.
- Normale Fenster, Desktop, randloses und exklusives Vollbild wurden noch nicht auf echter Hardware verifiziert.
- Die integrierte Windows-Lupe wird über `Magnify.exe` und einen bereits nicht neutralen Fullscreen-Transform erkannt. Für den Test werden dann keine Änderungen vorgenommen.
- Es findet keine Bildschirmaufnahme, DLL-Injektion, Treiberinstallation oder Manipulation eines Spiels statt.
- Es werden noch keine FPS-, CPU-, RAM- oder Akkulaufzeitwerte behauptet; solche Messungen gehören zu einem späteren Hardwaretest.

Siehe [ARCHITECTURE.md](ARCHITECTURE.md) und [TESTING.md](TESTING.md).

## Quick Zoom – Meilenstein 2

Start: `artifacts\quick-zoom\HandheldZoom.QuickZoom.exe`

- `Ctrl+Alt+Z` halten: 150 % um die Bildschirmmitte; Loslassen einer Taste setzt auf 100 % zurück.
- `Ctrl+Alt+R`: globaler Sofort-Reset. Die Zoomkombination erst vollständig loslassen, bevor erneut aktiviert wird.
- `Strg+C` im Konsolenfenster: Reset und Ende.
- Toggle: `HandheldZoom.QuickZoom.exe --mode toggle`
- Konfiguration beim Start: `--hotkey Ctrl+Shift+Z --reset Ctrl+Shift+R --factor 2.0`
- Unterstützt: Ctrl, Alt, Shift mit A–Z, 0–9 oder F1–F24 (außer reserviertem F12). Faktor >1 bis 5, Dezimalpunkt verwenden.
- `--check`: Optionen, Konfliktprüfung, API-Initialisierung und beide Hotkey-Registrierungen prüfen, ohne den Zoom zu verändern.

Belegte Hotkeys führen zu einem verständlichen Startfehler; schon registrierte Hotkeys werden wieder freigegeben. Nur eine QuickZoom-Instanz pro Windows-Sitzung ist zulässig. Halten nutzt eine 16-ms-Abfrage der konfigurierten Kombination auf dem Nachrichtenthread. Das ist keine garantierte Reaktionszeit. Beim Wechsel auf einen Desktop ohne lesbaren Tastenzustand wird Halten beendet. Die Auflösung wird bei jeder Aktivierung neu gelesen. Tray/Oberfläche sind noch offen; XInput und Profile siehe unten.

Windows-Hotkey-Verhalten: [Microsoft RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey). Ein reservierter Hotkey kann auch die Eingabe im Spiel beeinflussen; Spielkompatibilität muss am Gerät geprüft werden.



## Controller – Meilenstein 3

Die aktualisierte QuickZoom-EXE verwendet standardmäßig **Controller 0 und LB + RB**. Beide Schultertasten halten aktiviert 150 %, Loslassen einer Schultertaste beendet den Zoom. Die Tastatur bleibt verfügbar.

```powershell
# Halten mit Standardkombination
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe
# Toggle: gemeinsam drücken, loslassen, erneut gemeinsam drücken
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --mode toggle
# Eigene Kombination und anderer Controller-Platz
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --controller 1 --buttons Back+Y
# Nur Tastatur
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --controller off
```

Unterstützte Tasten: LB, RB, A, B, X, Y, Start, Back, LS, RS, Up, Down, Left, Right. Kombinationen benötigen mindestens zwei unterschiedliche Tasten. Trigger und Stickbewegungen werden noch nicht verwendet. Zusätzliche gedrückte Tasten stören die Kombination nicht.

Bei Start oder Wiederverbinden mit gehaltener Kombination muss diese zunächst losgelassen werden. Trennen des ausgewählten Controllers setzt auch Toggle auf 100 % zurück. Ctrl+Alt+R bleibt der globale Notfall-Reset; anschließend beide Eingabequellen loslassen. Bei gleichzeitigem Halten von Tastatur und Controller endet der Zoom erst, wenn beide losgelassen wurden. Im Toggle-Modus zählt die gemeinsame Eingabe als ein Druck.

Der ausgewählte XInput-Platz wird ungefähr alle 16 ms abgefragt; getrennte Geräte werden nur einmal pro Sekunde erneut geprüft. Controller-Plätze sind 0–3; andere Plätze bleiben unbeachtet. `--check` führt auch eine lesende XInput-Abfrage durch und meldet verbunden/getrennt, ohne sichtbaren Zoom.

**Die Controller-Eingaben erreichen weiterhin das Spiel.** Wähle eine Kombination, die im jeweiligen Spiel möglichst wenig auslöst. Keine Eingabeunterdrückung, DLL-Injektion oder Treiberinstallation. API-Grundlage: [Microsoft XInputGetState](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputgetstate).


## Spielprofile – Meilenstein 4

Beim ersten Start wird `%LOCALAPPDATA%\HandheldZoom\profiles.json` angelegt. Sie enthält ein Standardprofil und **Crimson Desert** mit 150 %, Fokus Mitte und Halten. Vorhandene Dateien werden nicht überschrieben. Die Datei wird beim Start geladen; nach Änderungen QuickZoom neu starten.

Normaler Start wählt anhand des Prozesses der aktiven Anwendung ein Profil. Vorbereitet ist die Zuordnung `CrimsonDesert.exe`; der Name muss bei laufendem Spiel im Konsolenprotokoll geprüft werden. Launcher oder Steam-Fenster zählen nicht als Spiel. Groß-/Kleinschreibung und das Suffix `.exe` werden ignoriert; nur exakte Namen ohne Pfad oder Platzhalter sind erlaubt. Unbekannte oder nicht lesbare Prozesse verwenden das Standardprofil.

Beim Wechsel in einen anderen Vordergrundprozess wird aktiver Zoom zurückgesetzt, auch im Toggle-Modus. Eine gehaltene Kombination muss vor erneuter Aktivierung losgelassen werden. Das gilt auch bei fest ausgewähltem Profil. Die Prüfung erfolgt normalerweise alle 250 ms und direkt bei Aktivierung; Fenster desselben Prozesses lösen keinen Reset aus.

```powershell
# Verfügbare Profile anzeigen, ohne Magnification API zu öffnen
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --list-profiles
# Crimson Desert fest auswählen, unabhängig von der Prozesserkennung
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --profile "Crimson Desert"
# Eine andere Profildatei verwenden (muss vorhanden sein)
.\artifacts\quick-zoom\HandheldZoom.QuickZoom.exe --profiles-path "C:\MeinOrdner\profiles.json"
```

Alternativ `artifacts\quick-zoom\Crimson-Desert.cmd` doppelklicken, um das Profil fest auszuwählen. Der Starthelfer startet nur QuickZoom, nicht das Spiel. `profiles.example.json` liegt daneben als Kopiervorlage.

Je Profil werden `name`, `processNames`, `factor`, `focusX`, `focusY` und `mode` gespeichert. Fokuswerte 0–1 beziehen sich auf den physischen Primärbildschirm: `(0.5,0.5)` Mitte, `(1,0)` rechts oben. Faktoren >1 bis 5; Modus `Hold` oder `Toggle`. Tastatur- und Controller-Kombinationen bleiben global. `--factor` und `--mode` überschreiben die jeweiligen Profilwerte für diesen Lauf und verändern die Datei nicht.

Beispiel für das Spielprofil:

```json
{
  "name": "Crimson Desert",
  "processNames": ["CrimsonDesert.exe"],
  "factor": 2.0,
  "focusX": 0.5,
  "focusY": 0.5,
  "mode": "Hold"
}
```

Dieses Objekt gehört in die `profiles`-Liste der Datei. Unbekannte Eigenschaften, ungültige Werte, doppelte Namen und doppelte Prozesszuordnungen führen vor dem Öffnen der Zoom-API zu einem Startfehler. Fehlerhafte Dateien bleiben erhalten.

Die Vordergrundzuordnung nutzt nur [GetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow) und [GetWindowThreadProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid) sowie den Prozessnamen. Es werden keine Spieldateien oder Spieleinstellungen geändert.



### Stick-Steuerung und Spieltasten

Bei aktivem Zoom verschiebt der rechte Stick den Ausschnitt. Der linke Stick vergrößert nach oben und verkleinert nach unten (100–500 %). Die Stick-Totzone verhindert Drift; Ausschnitt und Faktor werden begrenzt. Jede Aktivierung beginnt mit dem Profilfaktor und Fokus.

Im Desktop-Editor wählt „Tasten auswählen“ einzelne Controller-Tasten oder Kombinationen inklusive LT/RT. Aktivierungstasten und ein Schalter für die Stick-Steuerung werden pro Profil gespeichert und bei automatischem Profilwechsel übernommen. Bestehende Profile verwenden LB+RB und eingeschaltete Stick-Steuerung. Controller-Eingaben erreichen auch das Spiel.
