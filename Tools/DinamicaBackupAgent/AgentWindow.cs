using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DinamicaBackup;
using Microsoft.Win32;

namespace DinamicaBackupAgent;

/// <summary>
/// Configuration window: a side navigation with one screen per task, a status bar and one Save that
/// writes the configuration and registers the Windows task when needed.
/// </summary>
public sealed partial class AgentWindow : Window
{
    private readonly BackupJob _job;
    private readonly string _jobPath;
    private readonly ITaskService _tasks;
    private readonly SaveCoordinator _saver;
    private readonly List<Action> _commit = [];
    private readonly List<Func<string>> _configurationValues = [];
    private readonly List<Action> _onEdit = [];
    private readonly Dictionary<Section, FrameworkElement> _pages = [];
    private readonly Dictionary<Section, Action> _onShow = [];
    private readonly Dictionary<Section, (Button Button, Border Marker)> _nav = [];
    private readonly ContentControl _content = new();
    private readonly StackPanel _navPanel = new() { Margin = new Thickness(10, 16, 10, 16) };
    private readonly TextBlock _status = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Muted };
    private readonly ProgressBar _progress = new() { Height = 8, Width = 160, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
    private readonly Button _cancel = Ui.Button("Cancel", "Requests a safe stop. Quick Mail reopens once the operation has released its files.");
    private readonly TextBlock _headerPath = new() { FontSize = 13, Foreground = Ui.Muted, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(30) };
    private Button _saveButton = null!;
    private string? _savedConfiguration;
    private bool _needsInitialSave;
    private RegistrationNeed? _registration;
    private Section _current = Section.Status;
    private CancellationTokenSource? _operation;
    private List<RunResult> _runs = [];
    /// <summary>Replaces the Windows account dialog (tests).</summary>
    internal Func<string, (string User, string Password)?>? IdentityPrompt { get; set; }
    private string _runsNote = "";

    public AgentWindow(BackupJob job, string jobPath) : this(job, jobPath, new WindowsTaskService()) { }

    internal AgentWindow(BackupJob job, string jobPath, ITaskService tasks)
    {
        _job = job; _jobPath = Path.GetFullPath(jobPath); _tasks = tasks;
        _saver = new SaveCoordinator(tasks, Environment.ProcessPath ?? "");
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/DinamicaBackupAgent;component/Assets/DinamicaBackup.ico")); }
        catch (Exception ex) when (ex is IOException or UriFormatException or NotSupportedException) { /* Icon is cosmetic. */ }
        var version = typeof(AgentWindow).Assembly.GetName().Version;
        Title = $"Dinamica Backup Agent v{version?.ToString(3)} — " + job.Name;
        Width = 1200; Height = 800; MinWidth = 1000; MinHeight = 640;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Background = Ui.PageBack; Foreground = Ui.Ink;
        NotificationAddresses.NormalizeLegacySender(job.Notification);
        if (job.Probes.Count == 0) job.Probes = ApplicationDetection.EqmProbes();
        var originalConfiguration = System.Text.Json.JsonSerializer.Serialize(job, JsonFiles.Options);
        // A job that is not EQM is migrated: this configurator only serves Eudora Quick Mail.
        job.ApplicationType = "EQM";

        var root = new DockPanel(); Content = root;
        var header = new Border { Height = 38, Background = Ui.CardBack, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(14, 0, 14, 0) };
        header.Child = Ui.Row(10, new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Ui.Accent },
            new TextBlock { Text = "Dinamica Backup Agent · Eudora Quick Mail", FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center }, _headerPath);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);

        var footer = new Border { Height = 56, Background = Ui.CardBack, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 0, 18, 0) };
        var footerGrid = Ui.Columns(0, -1, -1, -1);
        _saveButton = Ui.Button("Save", "Saves every change in one step. When the schedules need it, it also registers the Windows task, asking for the Windows account first. If anything fails, nothing changes.", Ui.Kind.Primary);
        _saveButton.Margin = new Thickness(10, 0, 0, 0); _saveButton.Click += (_, _) => Guard(() => Save());
        _cancel.Visibility = Visibility.Collapsed;
        _cancel.Click += (_, _) => { _operation?.Cancel(); SetStatus("Cancelling safely. Waiting for the current operation to release Quick Mail's files…"); };
        Ui.Place(footerGrid, _status, 0, 0); Ui.Place(footerGrid, _progress, 0, 1); Ui.Place(footerGrid, _cancel, 0, 2); Ui.Place(footerGrid, _saveButton, 0, 3);
        footer.Child = footerGrid; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        var nav = new Border { Width = 220, Background = Ui.NavBack, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = _navPanel };
        DockPanel.SetDock(nav, Dock.Left); root.Children.Add(nav);
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _content, Padding = new Thickness(26, 20, 26, 20) });

        BuildPages();
        foreach (var section in Enum.GetValues<Section>()) AddNav(section);

        Closing += (_, e) => { if (_operation != null) { e.Cancel = true; SetStatus("Cancel and wait for the operation to finish before closing."); } };
        _refresh.Tick += (_, _) => { if (_current is Section.Status or Section.History && _operation == null) Show(_current); };
        Loaded += (_, _) => _refresh.Start();
        Closed += (_, _) => _refresh.Stop();

        _needsInitialSave = !File.Exists(_jobPath) || originalConfiguration != System.Text.Json.JsonSerializer.Serialize(job, JsonFiles.Options);
        _savedConfiguration = ConfigurationSnapshot();
        _registration = _saver.Need(_job, _jobPath);
        UpdateSaveState();
        Show(Section.Status);
    }

    internal static string SectionName(Section section) => section switch
    {
        Section.Status => "Status",
        Section.Schedule => "Schedule",
        Section.Sources => "Source and destinations",
        Section.RestoreTest => "Restore test",
        Section.Email => "Email reports",
        Section.History => "History",
        Section.Restore => "Restore a backup…",
        _ => "Advanced"
    };

    private void BuildPages()
    {
        _pages[Section.Status] = BuildStatus();
        _pages[Section.Schedule] = BuildSchedule();
        _pages[Section.Sources] = BuildSources();
        _pages[Section.RestoreTest] = BuildRestoreTest();
        _pages[Section.Email] = BuildEmail();
        _pages[Section.History] = BuildHistory();
        _pages[Section.Restore] = BuildRestore();
        _pages[Section.Advanced] = BuildAdvanced();
    }

    private void AddNav(Section section)
    {
        if (section == Section.Restore) _navPanel.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(4, 10, 4, 10) });
        var marker = new Border { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = SectionName(section), VerticalAlignment = VerticalAlignment.Center };
        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(marker, Dock.Right); content.Children.Add(marker); content.Children.Add(label);
        var button = new Button { Template = Ui.FlatTemplate(), Content = content, Padding = new Thickness(12, 9, 12, 9), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Foreground = section == Section.Advanced ? Ui.Muted : Ui.Ink, Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += (_, _) => Show(section);
        _navPanel.Children.Add(button);
        _nav[section] = (button, marker);
    }

    /// <summary>Shows a screen and refreshes what it reads from disk.</summary>
    internal void Show(Section section)
    {
        _current = section;
        foreach (var (key, (button, _)) in _nav)
        {
            button.Background = key == section ? Ui.AccentSoft : Brushes.Transparent;
            button.FontWeight = key == section ? FontWeights.SemiBold : FontWeights.Normal;
        }
        if (section is Section.Status or Section.History or Section.Email) LoadRuns();
        if (_onShow.TryGetValue(section, out var refresh)) Guard(refresh);
        _content.Content = _pages[section];
        UpdateMarkers();
    }

    private void LoadRuns()
    {
        _runsNote = "";
        List<RunResult> receipts;
        var localOnly = string.IsNullOrWhiteSpace(_job.Destination);
        try { receipts = Catalog.ReadRecentReceipts(_job, 1000, !localOnly); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { receipts = Catalog.ReadRecentReceipts(_job, 1000, false); } catch (Exception inner) when (inner is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { receipts = []; }
            _runsNote = "Showing this computer's history only: the backup folder could not be read (" + ex.Message + ").";
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ArgumentException) { receipts = []; _runsNote = "History could not be read: " + ex.Message; }
        RunResult? last = null;
        try
        {
            var path = Path.Combine(_job.ProfileDirectory, "backup-agent-last-run.json");
            if (_job.ProfileDirectory.Length > 0 && File.Exists(path)) last = JsonFiles.Read<RunResult>(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException) { }
        _runs = History.Merge(receipts, last);
    }

    private List<Issue> CurrentIssues() => StatusAdvisor.Issues(_job, _runs, _registration, CredentialExists, DateTime.Now);

    private void UpdateMarkers()
    {
        var issues = CurrentIssues();
        foreach (var (section, (_, marker)) in _nav)
        {
            if (section == Section.Status)
            {
                var errors = issues.Count;
                marker.Visibility = errors > 0 ? Visibility.Visible : Visibility.Collapsed;
                marker.Background = issues.Any(x => x.Severity == Severity.Error) ? Ui.Red : Ui.Amber; marker.CornerRadius = new CornerRadius(9); marker.Padding = new Thickness(7, 1, 7, 1);
                marker.Child = new TextBlock { Text = errors.ToString(), Foreground = Brushes.White, FontSize = 11 };
                continue;
            }
            var mine = issues.Where(x => x.Fix == section).ToArray();
            marker.Visibility = mine.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            marker.Child = null; marker.Width = 8; marker.Height = 8; marker.CornerRadius = new CornerRadius(4);
            marker.Background = mine.Any(x => x.Severity == Severity.Error) ? Ui.Red : Ui.Amber;
        }
        _headerPath.Text = string.IsNullOrWhiteSpace(_job.ApplicationDirectory) ? Path.GetDirectoryName(_job.ApplicationPath) ?? "" : _job.ApplicationDirectory;
    }

    // ---- Fields bound to the job: each one commits on Save and takes part in change tracking.

    private void Track(Action commit, Func<string> value)
    {
        _commit.Add(commit); _configurationValues.Add(value);
    }
    private TextBox Field(Func<string> get, Action<string> set, double width, string tip, bool mono = false, bool multiline = false)
    {
        var box = Ui.Box(width, tip, mono);
        box.Text = get();
        if (multiline) { box.AcceptsReturn = true; box.TextWrapping = TextWrapping.NoWrap; box.MinHeight = 64; box.VerticalContentAlignment = VerticalAlignment.Top; box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; }
        Track(() => set(box.Text.Trim()), () => box.Text.Trim());
        box.TextChanged += (_, _) => UpdateSaveState();
        return box;
    }
    private TextBox Number(Func<int> get, Action<int> set, double width, string tip, string name, int min, int max)
    {
        var box = Field(() => get().ToString(System.Globalization.CultureInfo.InvariantCulture), x =>
        {
            if (!int.TryParse(x, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < min || value > max)
                throw new FormatException(name + " must be a whole number from " + min + " to " + max + ".");
            set(value);
        }, width, tip);
        box.TextAlignment = TextAlignment.Right;
        return box;
    }
    private CheckBox Toggle(string label, Func<bool> get, Action<bool> set, string tip)
    {
        var check = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = get(), VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        Ui.Tip(check, tip);
        Track(() => set(check.IsChecked == true), () => check.IsChecked == true ? "true" : "false");
        check.Checked += (_, _) => UpdateSaveState(); check.Unchecked += (_, _) => UpdateSaveState();
        return check;
    }
    private ComboBox Pick(IEnumerable<string> values, Func<string> get, Action<string> set, double width, string tip)
    {
        var combo = new ComboBox { ItemsSource = values.ToArray(), SelectedItem = get(), Width = width, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 4, 6, 4) };
        Ui.Tip(combo, tip);
        Track(() => set(combo.SelectedItem?.ToString() ?? ""), () => combo.SelectedItem?.ToString() ?? "");
        combo.SelectionChanged += (_, _) => UpdateSaveState();
        return combo;
    }
    /// <summary>Two or more exclusive options drawn as a segmented control.</summary>
    private (StackPanel Panel, Func<int> Selected, Action<int> Select) Segmented(string[] options, string[] tips, Func<int> get, Action<int> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var border = new Border { BorderBrush = Ui.FieldLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = panel, HorizontalAlignment = HorizontalAlignment.Left, ClipToBounds = true };
        var selected = get();
        var buttons = new List<Button>();
        void Paint() { for (var i = 0; i < buttons.Count; i++) { buttons[i].Background = i == selected ? Ui.Accent : Ui.CardBack; buttons[i].Foreground = i == selected ? Brushes.White : Ui.Ink; } }
        void Select(int index) { selected = index; Paint(); UpdateSaveState(); }
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var button = new Button { Template = Ui.FlatTemplate(), Content = options[i], Padding = new Thickness(12, 6, 12, 6), BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0), BorderBrush = Ui.FieldLine, Cursor = System.Windows.Input.Cursors.Hand };
            Ui.Tip(button, tips[i]); button.Click += (_, _) => Select(index);
            buttons.Add(button); panel.Children.Add(button);
        }
        Paint();
        Track(() => set(selected), () => selected.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var outer = new StackPanel(); outer.Children.Add(border);
        return (outer, () => selected, Select);
    }

    private string ConfigurationSnapshot() => System.Text.Json.JsonSerializer.Serialize(
        _configurationValues.Select(get => get()).Concat(new[] { _job.NasShare, _job.NasCredential, _job.AttachmentNasShare, _job.AttachmentNasCredential }));
    private bool HasUnsavedChanges => _needsInitialSave || ConfigurationSnapshot() != _savedConfiguration;

    private void UpdateSaveState()
    {
        if (_savedConfiguration == null) return;
        var changed = HasUnsavedChanges;
        _saveButton.IsEnabled = _operation == null && (changed || _registration != null);
        Ui.Tip(_saveButton, changed || _registration == null
            ? "Saves every change in one step. When the schedules need it, it also registers the Windows task, asking for the Windows account first. If anything fails, nothing changes."
            : "Registers the Windows task so the schedules run. " + _registration.Reason);
        if (_operation == null) SetStatus(changed ? "Unsaved changes" : _registration != null ? "Saved, but the schedules are not running yet: " + _registration.Reason : "No unsaved changes", changed ? Ui.Ink : _registration != null ? Ui.AmberInk : Ui.Muted);
        foreach (var refresh in _onEdit) refresh();
    }
    private void SetStatus(string text, Brush? color = null) { _status.Text = text; _status.Foreground = color ?? Ui.Ink; }

    private void Commit() { foreach (var action in _commit) action(); }

    /// <summary>Atomic save: configuration plus, when needed, the Windows task. Returns false when cancelled.</summary>
    internal bool Save(bool forceRegistration = false)
    {
        Commit();
        bool registered;
        try { registered = _saver.Save(_job, _jobPath, IdentityPrompt ?? AskTaskIdentity, forceRegistration); }
        catch (OperationCanceledException ex) { SetStatus(ex.Message, Ui.AmberInk); return false; }
        _savedConfiguration = ConfigurationSnapshot(); _needsInitialSave = false;
        _registration = _saver.Need(_job, _jobPath);
        UpdateSaveState();
        SetStatus("Saved at " + DateTime.Now.ToString("HH:mm") + (registered ? ". The Windows task is registered and the schedules are active." : "."), Ui.Green);
        Show(_current);
        return true;
    }

    /// <summary>Asks for the Windows account the task runs as. Null means the user cancelled.</summary>
    private (string User, string Password)? AskTaskIdentity(string reason)
    {
        (string, string)? result = null;
        var window = new Window { Title = "Windows account for the scheduled task", Owner = IsLoaded ? this : null, Width = 560, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily };
        var panel = new StackPanel { Margin = new Thickness(18) }; window.Content = panel;
        Ui.Add(panel, Ui.Text(reason + " Saving registers the Windows task that runs the schedules.", Ui.Ink, true));
        Ui.Add(panel, Ui.Note("Current Windows account: " + System.Security.Principal.WindowsIdentity.GetCurrent().Name + ".\nWith a username and password, the task also runs while you are signed out, and Back up now and Test access are available. Leave both blank to run only while you are signed in. Use the account password, not a Windows Hello PIN."), 10);
        Ui.Add(panel, Ui.Label("Username"), 14); var user = Ui.Box(double.NaN, "Windows username. A bare name is resolved on this computer. Leave blank to use the current session."); panel.Children.Add(user);
        Ui.Add(panel, Ui.Label("Password"), 10);
        var password = new PasswordBox { Padding = new Thickness(6, 4, 6, 4), BorderBrush = Ui.FieldLine }; Ui.Tip(password, "Passed to Windows Task Scheduler only; never written to the configuration file."); panel.Children.Add(password);
        var ok = Ui.Button("Save and register", "Saves the configuration and registers the Windows task with this account.", Ui.Kind.Primary);
        var cancel = Ui.Button("Cancel", "Nothing is saved.");
        var buttons = Ui.Row(8, cancel, ok); buttons.HorizontalAlignment = HorizontalAlignment.Right; Ui.Add(panel, buttons, 16);
        ok.Click += (_, _) => { result = (user.Text.Trim(), password.Password); password.Clear(); window.DialogResult = true; };
        cancel.Click += (_, _) => window.DialogResult = false;
        window.ShowDialog();
        return result;
    }

    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            var message = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner.Message : ex.Message;
            SetStatus(message, Ui.RedInk);
            if (IsLoaded) MessageBox.Show(this, message, "Dinamica Backup Agent", MessageBoxButton.OK, MessageBoxImage.Error);
            else throw;
        }
    }

    private void SetBusy(bool busy)
    {
        _content.IsEnabled = !busy; _navPanel.IsEnabled = !busy;
        _cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; _cancel.IsEnabled = busy;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (!busy) { _progress.IsIndeterminate = false; UpdateSaveState(); }
        else _saveButton.IsEnabled = false;
    }

    /// <summary>Runs an operation in this process (external window, restore test, restore). Saves first.</summary>
    private async void Start(string kind, string? archive = null, string? destination = null, RestoreMode mode = RestoreMode.Database, IReadOnlyCollection<string>? selected = null, bool profileFiles = false)
    {
        if (_operation != null) return;
        try
        {
            if (!Save()) return;
            _operation = new CancellationTokenSource(); SetBusy(true);
            var progress = new Progress<ProgressInfo>(p => { SetStatus(p.Stage); _progress.IsIndeterminate = p.Total == 0; _progress.Maximum = Math.Max(1, p.Total); _progress.Value = p.Completed; });
            var runner = Program.CreateRunner(_job, progress);
            var result = await Task.Run(() => runner.RunAsync(kind, _operation.Token, archive, destination, mode, selected, profileFiles));
            var text = result.DisplayKind + " · " + Formatting.Result(result) + ": " + result.Detail
                + (result.Warnings.Count == 0 ? "" : " " + string.Join(" ", result.Warnings.Take(3)))
                + (string.IsNullOrEmpty(result.NotificationError) ? "" : " The email report was not sent: " + result.NotificationError);
            _operation.Dispose(); _operation = null; SetBusy(false);
            SetStatus(text, result.Status == "Success" ? Ui.Green : result.Status == "Failed" ? Ui.RedInk : Ui.AmberInk);
        }
        catch (Exception ex) { _operation?.Dispose(); _operation = null; SetBusy(false); SetStatus("Failed: " + ex.Message, Ui.RedInk); }
        Show(_current);
    }

    private string? BrowseFolder(string current)
    {
        var dialog = new OpenFolderDialog();
        if (Directory.Exists(current)) dialog.InitialDirectory = current;
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private static bool CredentialExists(string name)
    {
        try { _ = Secrets.Read(name); return true; }
        catch (Exception) { return false; }
    }
}
