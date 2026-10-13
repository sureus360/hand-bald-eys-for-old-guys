using System;

using System.ComponentModel;

using System.Diagnostics;

using System.IO;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Threading;

using HandheldZoom.Core.Diagnostics;

using HandheldZoom.Core.Input;

using HandheldZoom.Core.Profiles;

using HandheldZoom.Core.QuickZoom;

using HandheldZoom.Core.Magnification;

using HandheldZoom.QuickZoom;

using Forms = System.Windows.Forms;



namespace HandheldZoom.Desktop;



public partial class MainWindow : Window

{

    private readonly string _path;

    private readonly FileLog _log = FileLog.CreateDefault("desktop");

    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    private Forms.NotifyIcon? _tray;

    private ProfileCatalog _catalog = ProfileCatalog.Starter();

    private string _contents = "";

    private int _selected;

    private bool _ready, _loading, _dirty;

    private ZoomRuntime? _runtime;



    public MainWindow(string? path = null, bool preview = false)

    {

        _path = path ?? ProfileStore.DefaultPath;

        InitializeComponent();

        try

        {

            ProfileStore.LoadOrCreate(_path);

            _contents = File.ReadAllText(_path); _catalog = ProfileStore.Parse(_contents);

        }

        catch { _log.Dispose(); throw; }

        _ready = true;

        FillList(0);

        _statusTimer.Tick += (_, _) => UpdateStatus();

        if (!preview)

        {

            _statusTimer.Start();

            var menu = new Forms.ContextMenuStrip();

            menu.Items.Add("Öffnen", null, (_, _) => ShowWindow());

            menu.Items.Add("100 % Reset", null, (_, _) => Guard(ResetCore));

            menu.Items.Add("Zoom stoppen", null, (_, _) => Guard(StopEngine));

            menu.Items.Add("Beenden", null, (_, _) => Close());

            _tray = new Forms.NotifyIcon { Text = "hand bald eys for old guys", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };

            _tray.DoubleClick += (_, _) => ShowWindow();

            StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };

        }

        Closing += OnClosing;

    }



    private ZoomProfile[] AllProfiles => new[] { _catalog.DefaultProfile }.Concat(_catalog.Profiles).ToArray();

    private void FillList(int index)

    {

        _loading = true;

        GameList.ItemsSource = AllProfiles;

        _selected = Math.Clamp(index, 0, AllProfiles.Length - 1);

        GameList.SelectedIndex = _selected;

        GameList.ScrollIntoView(GameList.SelectedItem);

        LoadEditor();

        _loading = false;

    }

    private void LoadEditor()

    {

        var profile = AllProfiles[_selected];

        NameBox.Text = profile.Name;

        ProcessBox.Text = string.Join("; ", profile.ProcessNames);

        FactorSlider.Value = profile.Factor * 100;

        FocusXSlider.Value = profile.FocusX * 100; FocusYSlider.Value = profile.FocusY * 100;

        ModeBox.SelectedIndex = profile.Mode == ZoomMode.Hold ? 0 : 1;

        ButtonsBox.Text = profile.ControllerButtons; StickBox.IsChecked = profile.StickControl;

        ProcessBox.IsEnabled = BrowseButton.IsEnabled = RemoveButton.IsEnabled = _selected != 0;

        EditorTitle.Text = _selected == 0 ? "Standardprofil" : "Spielprofil";

        _dirty = false; UpdateLabels();

    }

    private void UpdateLabels()

    {

        FactorLabel.Text = $"{FactorSlider.Value:F0} %";

        FocusXLabel.Text = $"Horizontal · {FocusXSlider.Value:F0} %";

        FocusYLabel.Text = $"Vertikal · {FocusYSlider.Value:F0} %";

    }

    private void SelectProfile(object sender, SelectionChangedEventArgs e)

    {

        if (!_ready || _loading || GameList.SelectedIndex < 0) return;

        var next = GameList.SelectedIndex;

        if (!ConfirmEdits()) { _loading = true; GameList.SelectedIndex = _selected; _loading = false; return; }

        Guard(() => { StopEngine(); FillList(next); DetailText.Text = "Profil gewählt. Zum Verwenden Zoom starten."; });

    }

    private void MarkDirty()

    {

        if (!_ready || _loading) return;

        Guard(() => { StopEngine(); _dirty = true; DetailText.Text = "Ungespeicherte Änderungen · Profil speichern."; });

    }

    private void Edited(object sender, TextChangedEventArgs e) => MarkDirty();

    private void ModeEdited(object sender, SelectionChangedEventArgs e) => MarkDirty();

    private void SliderEdited(object sender, RoutedPropertyChangedEventArgs<double> e)

    {

        if (!_ready) return; UpdateLabels(); MarkDirty();

    }

    private void ControlEdited(object sender, RoutedEventArgs e) => MarkDirty();

    private void ChooseButtons(object sender, RoutedEventArgs e)

    {

        Guard(() =>

        {

            var selected = GamepadChord.Parse(ButtonsBox.Text.Trim());

            var panel = new StackPanel { Margin = new Thickness(20) };

            var choices = Enum.GetValues<GamepadButtons>().Select(button => new CheckBox

            { Content = button.ToString(), Tag = button, IsChecked = (selected & button) != 0 }).ToArray();

            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };

            foreach (var choice in choices) grid.Children.Add(choice);

            panel.Children.Add(new TextBlock { Text = "Eine oder mehrere Tasten wählen", Margin = new Thickness(0,0,0,12) });

            panel.Children.Add(grid);

            var apply = new Button { Content = "Übernehmen", Margin = new Thickness(0,16,0,0) };

            panel.Children.Add(apply);

            var window = new Window { Title = "Zoom-Tasten für dieses Spiel", Width = 480, Height = 320,

                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,

                Background = Background, Content = panel };

            apply.Click += (_, _) =>

            {

                var names = choices.Where(c => c.IsChecked == true).Select(c => c.Content.ToString());

                var value = string.Join("+", names);

                try { GamepadChord.Parse(value); ButtonsBox.Text = value; window.DialogResult = true; }

                catch (ArgumentException) { MessageBox.Show(window, "Bitte mindestens eine Taste wählen."); }

            };

            window.ShowDialog();

        });

    }

    private void LaunchOptionEdited(object sender, RoutedEventArgs e)

    {

        if (!_ready || _loading) return;

        Guard(() => { StopEngine(); DetailText.Text = "Steuerung geändert. Zoom erneut starten."; });

    }

    private void SetFocus(object sender, RoutedEventArgs e)

    {

        var values = ((string)((Button)sender).Tag).Split(',');

        FocusXSlider.Value = int.Parse(values[0]); FocusYSlider.Value = int.Parse(values[1]);

    }

    private ZoomProfile Draft() => new()

    {

        Name = NameBox.Text.Trim(),

        ProcessNames = _selected == 0 ? [] : ProcessBox.Text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),

        Factor = (float)(FactorSlider.Value / 100), FocusX = FocusXSlider.Value / 100,

        FocusY = FocusYSlider.Value / 100, Mode = ModeBox.SelectedIndex == 0 ? ZoomMode.Hold : ZoomMode.Toggle,

        ControllerButtons = ButtonsBox.Text.Trim(), StickControl = StickBox.IsChecked == true

    };

    private void SaveCurrent()

    {

        var draft = Draft(); draft.Validate();

        var games = _catalog.Profiles.ToArray();

        if (_selected != 0) games[_selected - 1] = draft;

        var next = _catalog with { DefaultProfile = _selected == 0 ? draft : _catalog.DefaultProfile, Profiles = games };

        ProfileStore.Save(_path, next, _contents);

        _contents = File.ReadAllText(_path); _catalog = next;

        FillList(_selected);

        DetailText.Text = "Profil gespeichert · Sicherung der vorherigen Datei vorhanden.";

    }

    private void SaveProfile(object sender, RoutedEventArgs e) => Guard(SaveCurrent);

    private bool ConfirmEdits()

    {

        if (!_dirty) return true;

        var result = MessageBox.Show(this, "Möchtest du die Änderungen am Profil speichern?", "Ungespeicherte Änderungen", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel) return false;

        if (result == MessageBoxResult.Yes)

        {

            try { SaveCurrent(); } catch (Exception ex) { ShowError(ex); return false; }

        }

        else { _catalog = ProfileStore.Parse(_contents); _dirty = false; FillList(Math.Min(_selected, AllProfiles.Length - 1)); }

        return true;

    }

    private void AddGame(object sender, RoutedEventArgs e)

    {

        if (!ConfirmEdits()) return;

        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Spielanwendung (*.exe)|*.exe", Title = "Spiel-EXE auswählen" };

        if (dialog.ShowDialog(this) == true) Guard(() => AddProcess(Path.GetFileName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName)));

    }

    private void BrowseExe(object sender, RoutedEventArgs e)

    {

        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Spielanwendung (*.exe)|*.exe", Title = "Spiel-EXE zuordnen" };

        if (dialog.ShowDialog(this) == true) ProcessBox.Text = Path.GetFileName(dialog.FileName);

    }

    private void AddProcess(string processName, string displayName)

    {

        StopEngine();

        var existing = _catalog.Resolve(processName);

        if (existing != _catalog.DefaultProfile)

        {

            FillList(Array.FindIndex(AllProfiles, p => p.Name == existing.Name));

            DetailText.Text = "Dieses Spiel hat bereits ein Profil."; return;

        }

        var name = displayName; var suffix = 2;

        while (AllProfiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{displayName} ({suffix++})";

        _catalog = _catalog with { Profiles = _catalog.Profiles.Append(new ZoomProfile { Name = name, ProcessNames = [processName] }).ToArray() };

        FillList(AllProfiles.Length - 1); _dirty = true;

        DetailText.Text = "Spiel hinzugefügt. Einstellungen wählen und Profil speichern.";

    }

    private void AddRunning(object sender, RoutedEventArgs e)

    {

        if (!ConfirmEdits()) return;

        Guard(() =>

        {

            var list = new ListBox { Margin = new Thickness(16), MinHeight = 180, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 33, 45)), Foreground = System.Windows.Media.Brushes.White };

            foreach (var process in Process.GetProcesses())

            {

                using (process)

                {

                    try

                    {

                        if (process.Id != Environment.ProcessId && process.MainWindowHandle != IntPtr.Zero)

                            list.Items.Add(new RunningGame(process.ProcessName + ".exe", process.MainWindowTitle));

                    }

                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }

                }

            }

            list.Items.SortDescriptions.Add(new SortDescription(nameof(RunningGame.ProcessName), ListSortDirection.Ascending));

            var pick = new Button { Content = "Als Spielprofil hinzufügen", Margin = new Thickness(16), IsEnabled = false };

            var window = new Window { Title = "Laufendes Spiel wählen", Width = 670, Height = 430, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 44, 58)) };

            var panel = new DockPanel(); DockPanel.SetDock(pick, Dock.Bottom); panel.Children.Add(pick); panel.Children.Add(list); window.Content = panel;

            list.SelectionChanged += (_, _) => pick.IsEnabled = list.SelectedItem is RunningGame;

            pick.Click += (_, _) => window.DialogResult = true;

            if (window.ShowDialog() == true && list.SelectedItem is RunningGame game) AddProcess(game.ProcessName, Path.GetFileNameWithoutExtension(game.ProcessName));

        });

    }

    private sealed record RunningGame(string ProcessName, string Title)

    {

        public override string ToString() => $"{ProcessName} · {Title}";

    }

    private void RemoveGame(object sender, RoutedEventArgs e)

    {

        if (_selected == 0) return;

        if (!ConfirmEdits()) return;

        if (_selected >= AllProfiles.Length) { FillList(0); return; }

        var profile = AllProfiles[_selected];

        if (MessageBox.Show(this, $"Profil „{profile.Name}“ entfernen? Das Spiel bleibt installiert.", "Profil entfernen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        Guard(() =>

        {

            StopEngine();

            var next = _catalog with { Profiles = _catalog.Profiles.Where(p => p.Name != profile.Name).ToArray() };

            ProfileStore.Save(_path, next, _contents); _contents = File.ReadAllText(_path); _catalog = next;

            FillList(0); DetailText.Text = "Spielprofil entfernt.";

        });

    }

    private void Reload(object sender, RoutedEventArgs e)

    {

        if (!ConfirmEdits()) return;

        Guard(() =>

        {

            StopEngine(); var contents = File.ReadAllText(_path); var next = ProfileStore.Parse(contents);

            _contents = contents; _catalog = next; FillList(0); DetailText.Text = "Profile neu geladen.";

        });

    }

    private void ToggleEngine(object sender, RoutedEventArgs e)

    {

        Guard(() =>

        {

            if (_runtime is not null) { StopEngine(); return; }

            if (_dirty) SaveCurrent();

            var buttons = GamepadChord.Parse(ButtonsBox.Text.Trim());

            var options = new RuntimeOptions(HotkeyBox.Text.Trim(), ResetBox.Text.Trim(), ControllerBox.SelectedIndex == 4 ? null : ControllerBox.SelectedIndex,

                buttons, AutomaticBox.IsChecked == true ? null : AllProfiles[_selected].Name, UseProfileControls: true);

            _runtime = new ZoomRuntime(_catalog, options, _log, message => DetailText.Text = message);

            _runtime.FailedEvent += ex => Dispatcher.BeginInvoke(new Action(() => { Guard(StopEngine); ShowError(ex); }));

            StartButton.Content = "Zoom stoppen";

            DetailText.Text = "Bereit im Hintergrund · mit Tastatur oder Controller zoomen.";

            UpdateStatus();

        });

    }

    private void StopEngine()

    {

        if (_runtime is null) return;

        var runtime = _runtime; _runtime = null;

        try { runtime.Dispose(); }

        finally { StartButton.Content = "Zoom starten"; UpdateStatus(); }

    }

    private void UpdateStatus()

    {

        if (_runtime is null) { StatusText.Text = "Bereit · Zoom ist gestoppt"; return; }

        var controller = _runtime.ControllerConnected switch { true => "Controller verbunden", false => "Controller getrennt", null => "Nur Tastatur" };

        StatusText.Text = $"{(_runtime.IsZoomActive ? "Zoom aktiv" : "Zoom bereit")} · {_runtime.CurrentProfileName} · {controller}";

    }

    private void ResetCore()

    {

        if (_runtime is not null) _runtime.Reset();

        else

        {

            using var recovery = MagnificationController.Open(_log, blockExistingMagnification: false);

            recovery.ResetToIdentity(); recovery.KeepCurrentOnDispose();

        }

        DetailText.Text = "Auf 100 % zurückgesetzt."; UpdateStatus();

    }

    private void ResetZoom(object sender, RoutedEventArgs e) => Guard(ResetCore);

    private void HideToTray(object sender, RoutedEventArgs e) { Hide(); }

    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }

    private void Guard(Action action) { try { action(); } catch (Exception ex) { ShowError(ex); } }

    private void ShowError(Exception ex) { _log.Error("Desktop-Aktion fehlgeschlagen", ex); DetailText.Text = ex.Message; }

    private void OnClosing(object? sender, CancelEventArgs e)

    {

        if (!ConfirmEdits()) { e.Cancel = true; return; }

        _statusTimer.Stop(); Guard(StopEngine);

        _tray?.Dispose(); _tray = null; _log.Dispose();

    }



    internal void CheckEngine()

    {

        for (var attempt = 0; attempt < 2; attempt++)

        {

            _runtime = new ZoomRuntime(_catalog, new RuntimeOptions(ControllerIndex: null), _log);

            if (_runtime.IsZoomActive) throw new InvalidOperationException("Unerwarteter Zoom im Start/Stopp-Test.");

            StopEngine();

            if (_runtime is not null || !Equals(StartButton.Content, "Zoom starten"))

                throw new InvalidOperationException("UI-Start/Stopp-Prüfung fehlgeschlagen.");

        }

    }



    // Exercises the actual editor and persistence in an isolated preview profile file.

    internal void CheckEditor()

    {

        AddProcess("UITestGame.exe", "UI Testspiel");

        FactorSlider.Value = 200; FocusXSlider.Value = 80; ModeBox.SelectedIndex = 1;
        ButtonsBox.Text = "LT+Y"; StickBox.IsChecked = false;

        SaveCurrent();

        var saved = ProfileStore.Load(_path).ByName("UI Testspiel");

        if (saved.Factor != 2 || saved.FocusX != .8 || saved.Mode != ZoomMode.Toggle || saved.ProcessNames[0] != "UITestGame.exe" || saved.ControllerButtons != "LT+Y" || saved.StickControl)

            throw new InvalidOperationException("UI-Speicherprüfung fehlgeschlagen.");

        var clean = _catalog with { Profiles = _catalog.Profiles.Where(p => p.Name != "UI Testspiel").ToArray() };

        ProfileStore.Save(_path, clean, _contents); _contents = File.ReadAllText(_path); _catalog = clean;

        FillList(1); DetailText.Text = "Crimson Desert vorbereitet · Profil speichern und Zoom starten.";

    }

}
