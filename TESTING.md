# Testplan und aktueller Nachweis

## Automatisierbar

`build.ps1` baut die x64-Solution und startet einen eigenen, paketfreien Test-Runner. Er prüft:

- 100, 125, 150, 175 und 200 %
- Fokus Bildschirmmitte
- linke obere und rechte untere Kante
- Clamping normierter Fokuswerte
- Clamping direkter Offsets
- ungültige Faktoren, NaN und ungültige Auflösungen

Der Test-Runner verändert den Bildschirm nicht.

## API-Smoke-Test unter Windows 11

1. `HandheldZoom.MagnificationTest.exe --status`: `MagInitialize`, `MagGetFullscreenTransform`, `MagUninitialize`; keine Änderung.
2. `HandheldZoom.MagnificationTest.exe --auto`: 150 % Mitte, rechts unten, links oben, 100 %; jeder Schritt wird zurückgelesen.
3. `HandheldZoom.MagnificationTest.exe --run`: Pfeiltasten bis zu allen vier Kanten, dann `R`.
4. Während aktivem Zoom `Strg+C` drücken: gespeicherter Ausgangszustand muss wiederhergestellt sein.
5. Während aktivem Zoom das Fenster normal schließen: Ausgangszustand muss wiederhergestellt sein.
6. Windows-Lupe starten, dann `--run`: Test muss vor jeder Änderung mit Konfliktmeldung abbrechen.
7. Recovery starten: Ergebnis muss 100 %, `(0, 0)` melden.
8. Auflösung ändern, Test neu starten: neue Primärauflösung muss protokolliert und das Clamping daraus berechnet werden.

## Noch manuell auf ASUS ROG Ally X auszuführen

- Sichtprüfung bei 1920×1080 sowie der am Gerät verwendeten alternativen Auflösung
- Desktop, normales Fenster, randloses Vollbild und exklusives Vollbild getrennt prüfen
- Alt-Tab, Auflösungswechsel und Spielneustart während/zwischen Zoomvorgängen
- Controller verbunden/getrennt (erst ab Meilenstein 3)
- Halten/Loslassen und Toggle (erst ab Meilenstein 2)
- CPU, Working Set, Reaktionszeit, FPS/Frametime und Akkulaufzeit mit festgelegtem Messverfahren

Ohne diese Läufe werden keine Aussagen zur Spielkompatibilität oder Performance gemacht.

## Ergebnisprotokoll

Trage Hardwaretests mit Datum, Windows-Build, GPU-Treiber, Spiel/Render-Modus, Auflösung und beobachtetem Resultat ein. API-Logs liegen unter `%LOCALAPPDATA%\HandheldZoom\Logs`.

| Test | Umgebung | Ergebnis |
|---|---|---|
| Reine ZoomMath-Tests | Windows-11-Build-Rechner, 1920×1080 | 11/11 bestanden am 11.10.2026 |
| API-Status | Windows-11-Build-Rechner, 1920×1080, 96 DPI | Bestanden: 100 %, Offset (0,0), 11.10.2026 |
| Automatische 150-%-Sequenz | Windows-11-Build-Rechner, 1920×1080 | Bestanden: Mitte/Kanten/Reset zurückgelesen, 11.10.2026 |
| Eigenständige Recovery | Windows-11-Build-Rechner | Bestanden: 100 %, Offset (0,0), 11.10.2026 |
| ROG Ally X | Echte Hardware | Offen |

## Orientierende Messung auf dem Build-Rechner

Gemessen wurde die selbstenthaltende portable x64-EXE mit `--auto` am 11.10.2026. Das ist ein kurzer Kaltstart-/API-Test, kein repräsentativer Dauerbetrieb und kein Spiele-Benchmark:

- Gesamtlaufzeit: 2.891,3 ms, davon 2.100 ms absichtliche Wartezeit zur Sichtprüfung
- Prozess-CPU-Zeit: ca. 578,1 ms während des kurzen Starts und Laufs
- Peak Working Set: 55,6 MiB
- erstes Aktivieren einschließlich Zurücklesen: 443,23 ms
- weitere Positionswechsel einschließlich Zurücklesen: 0,10 ms und 0,32 ms
- Reset auf 100 % einschließlich Zurücklesen: 0,63 ms

Der auffällig langsamere erste Aufruf ist reproduzierbar und muss in Meilenstein 2 bei der Aktivierungsreaktion berücksichtigt werden. FPS-, Frametime-, GPU- und Akkuauswirkungen sind damit ausdrücklich **nicht** gemessen.

## Meilenstein 2 – 12.10.2026

16 automatisierte Tests: die bisherigen 11 plus Halten/Wiederholung/Loslassen, Toggle, Reset bei weiterhin gehaltener Taste, fehlgeschlagene Aktivierung und erneuter Reset nach Ausgabefehler. Diese Tests verwenden simulierte Ausgabe und verändern den Bildschirm nicht.

Manuelle Abnahme noch offen:
1. Recovery griffbereit halten, Windows-Lupe schließen. QuickZoom starten.
2. Ctrl+Alt+Z im Desktop und in einem anderen aktiven Fenster halten und loslassen; auch zuerst Ctrl bzw. Alt loslassen.
3. Mit --mode toggle aktivieren, loslassen, erneut drücken: an/aus.
4. Während Halten Ctrl+Alt+R drücken: 100 %, kein Wiederaktivieren bis zum Loslassen.
5. Normal beenden/Strg+C/Fenster schließen während Zoom; Ausgangszustand prüfen.
6. Zweite Instanz und belegte Hotkeys: sauberer Startfehler, keine Zoomänderung.
7. Auflösung zwischen Aktivierungen ändern; Mitte und Kanten prüfen. Windows-Lupe nach Start öffnen: nächste Aktivierung muss abbrechen.
8. Desktop, randloses und exklusives Vollbild am ROG Ally getrennt prüfen. Reaktionszeit und Ressourcenverbrauch gesondert messen.

Verifiziert am 12.10.2026 auf dem aktuellen Rechner: Build 0 Warnungen/0 Fehler, 16/16 Tests bestanden, portable Veröffentlichung aller drei Anwendungen erfolgreich. QuickZoom --check für Standard-Halten und Toggle mit Ctrl+Shift+F8/F9 erfolgreich. Anschließende Statusabfrage: 1920×1080, 96 DPI, 100 %, Offset (0,0). Kein sichtbarer Zoom- oder Spieltest durchgeführt.

## Meilenstein 3 – 12.10.2026

24 automatisierte Tests: zusätzlich Controller-Kombination/Validierung, vollständige Kombination, Toggle-Flanken, Start/Reconnect mit gehaltenen Tasten, Disconnect in Toggle, gemischte Eingabe, globaler Reset beider Quellen und Disconnect bei gehaltener Tastatur. Native Controller-Daten werden im Zustands-Test simuliert; es handelt sich nicht um einen sichtbaren Hardwaretest.

Lesende portable Checks erfolgreich: Standard-Controller 0 erkannt; Controller 3 nicht verbunden; --controller off funktioniert. Status danach 100 %, (0,0), Primärbildschirm 1600×900 bei 144 DPI. Tastaturfunktion vom Nutzer bestätigt. Sichtbare Controller-/Spiele-Abnahme offen:
1. Vorherige QuickZoom-Instanz beenden, neue portable EXE starten. LB+RB halten/loslassen.
2. Toggle starten, Kombination drücken/loslassen/erneut drücken.
3. Während Halten oder Toggle Controller trennen: sofortiger Reset beim nächsten Poll. Wiederverbinden mit gehaltener Kombination darf nicht aktivieren.
4. Tastatur und Controller gleichzeitig halten; beide Quellen und Notfall-Reset prüfen.
5. --buttons Back+Y und gewählten Platz 0–3 prüfen. Eingaben im Spiel separat beobachten.
6. Handheld-Moduswechsel, Suspend/Resume, Desktop und Spiel-Vollbild prüfen. Keine Performance- oder Kompatibilitätswerte abgeleitet.

## Meilenstein 4 – 13.10.2026

Build: 0 Fehler/0 Warnungen; 37/37 Tests bestanden; alle portablen Anwendungen erfolgreich veröffentlicht. 13 neue Tests prüfen exakte/case-insensitive Prozesszuordnung mit EXE-Normalisierung, Auflösungswechsel und Fokus, JSON-Roundtrip, ungültige Werte/JSON, doppelte Zuordnungen, Wechselreihenfolge, Kontextwechsel innerhalb des Standardprofils, festes Profil, Resetfehler, Sperre gehaltener Eingabe, Beibehalten eigener/defekter Dateien und Modus pro Aktivierung.

Lesende portable Prüfungen: Standardprofil bei Vordergrundprozess ChatGPT, festes Crimson-Desert-Profil mit CLI-Überschreibungen (200 %, Toggle, Controller off), separate Beispieldatei und Abweisung eines unbekannten Profilnamens. Profildatei im Benutzerverzeichnis erfolgreich erstellt. Status danach 100 %, (0,0), 1600×900/144 DPI. Controller auf Platz 0 erkannt. Tastatur-/Controllerfunktion zuvor vom Nutzer bestätigt; keine daraus abgeleitete Crimson-Desert-Spielkompatibilität.

Manuelle Profilabnahme:
1. Alte Instanz schließen, neue QuickZoom starten, Crimson Desert fokussieren: Konsole/Log muss Profil Crimson Desert melden. Falls Standard bleibt, gelesenen Prozessnamen in profiles.json zuordnen; alternativ Crimson-Desert.cmd nutzen.
2. Faktor und Fokus für das Spiel ändern, App neu starten; LB+RB drücken und gewünschten Ausschnitt prüfen.
3. Bei aktivem Toggle Alt-Tab: 100 %. Bei gehaltenen Tasten wechseln: erst nach Loslassen erneut aktivierbar.
4. Spiel beenden/neustarten, Controller trennen und Auflösung wechseln; Reset und Auswahl getrennt prüfen.
5. JSON-Fehler beheben und erneut starten; beschädigte Dateien dürfen nicht überschrieben werden.

## Meilenstein 5 – 13.10.2026

- Build erfolgreich: 0 Fehler/0 Warnungen; 40/40 Core-Tests bestanden. Drei neue Persistenztests prüfen .bak, Ablehnung externer Änderungen und unveränderte Dateien bei ungültigen Profilen.
- Echter WPF-Editor-Test in isolierter Profildatei: Spiel hinzufügen, Faktor/Fokus/Modus ändern, speichern und neu laden; bestanden. RenderTargetBitmap-Vorschauen für 1100×800 und aktuellen skalierten Arbeitsbereich visuell geprüft. Keine sichtbaren Testfenster geöffnet.
- Selbstenthaltende, umbenannte EXE in artifacts/app: --ui-check mit ExitCode 0. Kein Bedarf an systemweitem .NET; kein sichtbares Fenster und keine Zoom-API in diesem Test.
- WPF/ZoomRuntime Start/Stop zweimal im selben Prozess: --engine-check mit ExitCode 0, sowohl lokal als auch portable. API lesend initialisiert, Hotkeys registriert/freigegeben, Mutex wieder erwerbbar. Keine Transform-Schreiboperation; Controller im Smoke-Test deaktiviert.
- Letzter lesender API-Status: 100 %, (0,0), 1600×900/144 DPI. Keine Spieldateien geändert, kein Spiel gestartet/geschlossen.

Reproduzierbare UI-Prüfungen:

```powershell
& '.\artifacts\app\hand bald eys for old guys.exe' --ui-check '.\artifacts\portable-ui-validation'
& '.\artifacts\app\hand bald eys for old guys.exe' --engine-check '.\artifacts\portable-engine-validation'
```

Engine-Prüfung bei gestoppter anderer QuickZoom-Instanz und neutralem Zoom starten. Die Ergebnisse und PNGs liegen in den genannten Ordnern; Tests verwenden eigene Profildateien, nicht Benutzerprofile.

Noch manuell ausführen: EXE-Dateiauswahldialog und laufendes Spiel auswählen, neue Einträge/Entfernen sowie Speichern/Verwerfen/Abbrechen bedienen; Fenster minimieren, Tray-Menü und Wiederöffnen; echte Tastatur-/Controller-Eingaben mit WPF-Nachrichtenloop; aktiven Zoom stoppen/beenden; Crimson Desert und Alt-Tab getrennt prüfen. Keine neue Behauptung zur Spielekompatibilität oder Performance.
