using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private TextBox _appFolder = null!, _profileBox = null!, _databaseBox = null!, _executableBox = null!, _argumentsBox = null!, _filesBox = null!, _attachmentsBox = null!;
    private TextBox _destination = null!, _attachmentDestination = null!;
    private CheckBox _copyAttachments = null!, _legacyAttachments = null!;
    private string _lastDetectedFolder = "";

    private FrameworkElement BuildSources()
    {
        var job = _job;
        var page = new StackPanel();
        page.Children.Add(Ui.Title("Source and destinations"));
        Ui.Add(page, Ui.Note("What is copied and where it goes. Every folder must be separate from the others."), 4);

        // Source: the Quick Mail folder, from which profile, database and executable are detected.
        var source = new StackPanel();
        source.Children.Add(Ui.Heading("Eudora Quick Mail folder"));
        _appFolder = Field(() => string.IsNullOrWhiteSpace(job.ApplicationDirectory) ? System.IO.Path.GetDirectoryName(job.ApplicationPath) ?? "" : job.ApplicationDirectory, x => job.ApplicationDirectory = x, double.NaN,
            "Folder that contains EudoraQM.exe. The profile is found from Quick Mail's own settings or QuickMailDataPath.txt; it does not have to be inside this folder.", mono: true);
        var browse = Ui.Button("Browse…", "Choose the Eudora Quick Mail folder. Nothing is copied.");
        var detect = Ui.Button("Detect again", "Reads the profile, database and executable from this folder without opening the database.");
        var folderRow = Ui.Columns(0, -1, -1);
        Ui.Place(folderRow, _appFolder, 0, 0); browse.Margin = new Thickness(8, 0, 0, 0); Ui.Place(folderRow, browse, 0, 1); detect.Margin = new Thickness(8, 0, 0, 0); Ui.Place(folderRow, detect, 0, 2);
        source.Children.Add(folderRow);
        var detected = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        detected.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        detected.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        source.Children.Add(detected);

        var details = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        _profileBox = Field(() => job.ProfileDirectory, x => job.ProfileDirectory = x, double.NaN, "Folder with the live database and profile settings. Never the backup folder.", mono: true);
        _databaseBox = Field(() => job.DatabaseName, x => job.DatabaseName = x, 160, "Database path inside the profile. Quick Mail uses mail.db.", mono: true);
        _executableBox = Field(() => job.ApplicationPath, x => job.ApplicationPath = x, double.NaN, "Program reopened after a backup. It must belong to this installation.", mono: true);
        _argumentsBox = Field(() => string.Join(Environment.NewLine, job.ApplicationArguments), x => job.ApplicationArguments = Lines(x), double.NaN, "One argument per line. For Quick Mail: --profileDir on one line and the profile path on the next, without quotes.", true, true);
        _filesBox = Field(() => string.Join(Environment.NewLine, job.ProfileFiles), x => job.ProfileFiles = Lines(x), double.NaN, "Settings files saved in each backup, relative to the profile. Missing files are skipped.", true, true);
        _attachmentsBox = Field(() => string.Join(Environment.NewLine, job.AttachmentDirectories), x => job.AttachmentDirectories = Lines(x), double.NaN, "Attachment folders relative to the profile. Quick Mail's Embedded folder is always included.", true, true);
        foreach (var (label, box) in new (string, FrameworkElement)[] { ("Profile folder", _profileBox), ("Database file", _databaseBox), ("Program", _executableBox), ("Program arguments", _argumentsBox), ("Profile files", _filesBox), ("Attachment folders", _attachmentsBox) })
        { Ui.Add(details, Ui.Label(label), 10); details.Children.Add(box); }
        var expander = new Expander { Header = "Detected settings (change only for a custom installation)", Content = details, Margin = new Thickness(0, 10, 0, 0), Foreground = Ui.Muted };
        Ui.Tip(expander, "The values detected from the Quick Mail folder. Change them only if Quick Mail runs with a custom profile.");
        source.Children.Add(expander);
        page.Children.Add(WithTop(Ui.Card(source), 16));

        void ShowDetected()
        {
            detected.Children.Clear(); detected.RowDefinitions.Clear();
            var database = _profileBox.Text.Trim().Length == 0 ? "" : System.IO.Path.Combine(_profileBox.Text.Trim(), _databaseBox.Text.Trim());
            var databaseText = database.Length == 0 ? "not found" : database + (File.Exists(database) ? " · " + Formatting.Size(new FileInfo(database).Length) : " · missing");
            var exe = _executableBox.Text.Trim();
            var rows = new[] { ("Profile", _profileBox.Text.Trim().Length == 0 ? "not found" : _profileBox.Text.Trim(), Directory.Exists(_profileBox.Text.Trim())),
                ("Database", databaseText, database.Length > 0 && File.Exists(database)), ("Program", exe.Length == 0 ? "not found" : exe, exe.Length > 0 && File.Exists(exe)) };
            for (var i = 0; i < rows.Length; i++)
            {
                Ui.Place(detected, Ui.Text(rows[i].Item1, Ui.Muted), i, 0);
                var value = Ui.Text(rows[i].Item2, rows[i].Item3 ? Ui.Ink : Ui.RedInk); value.FontFamily = Ui.Mono; value.FontSize = 12; value.Margin = new Thickness(0, 2, 0, 2);
                Ui.Place(detected, value, i, 1);
            }
        }
        browse.Click += (_, _) => Guard(() => { if (BrowseFolder(_appFolder.Text) is { } folder) { _appFolder.Text = folder; DetectApplication(folder); ShowDetected(); } });
        detect.Click += (_, _) => Guard(() => { DetectApplication(_appFolder.Text.Trim()); ShowDetected(); });
        _appFolder.LostKeyboardFocus += (_, _) => Guard(() =>
        {
            if (Directory.Exists(_appFolder.Text.Trim()) && !string.Equals(_appFolder.Text.Trim(), _lastDetectedFolder, StringComparison.OrdinalIgnoreCase)) { DetectApplication(_appFolder.Text.Trim()); ShowDetected(); }
        });
        _onEdit.Add(ShowDetected);

        // Destinations.
        var grid = Ui.Columns(0, 0);
        var backups = new StackPanel();
        backups.Children.Add(Ui.Heading("Database backups"));
        _destination = DestinationRow(backups, () => job.Destination, x => job.Destination = x, false);
        Ui.Add(backups, CredentialLine(false), 8);
        var databaseCard = Ui.Card(backups); databaseCard.Margin = new Thickness(0, 0, 6, 12);
        Ui.Place(grid, databaseCard, 0, 0);

        var attachments = new StackPanel();
        attachments.Children.Add(Ui.Heading("Attachments"));
        _copyAttachments = Toggle("Copy attachments", () => job.CopyAttachments, x => job.CopyAttachments = x,
            "Copies new or changed attachments (and Quick Mail's embedded images) to a mirror folder and checks their hashes. Unchanged files are skipped.");
        attachments.Children.Add(_copyAttachments);
        var attachmentBody = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        _attachmentDestination = DestinationRow(attachmentBody, () => job.AttachmentDestination, x => job.AttachmentDestination = x, true);
        Ui.Add(attachmentBody, CredentialLine(true), 8);
        _legacyAttachments = Toggle("Also copy linked Eudora attachments", () => job.IncludeLegacyEudoraAttachments, x => job.IncludeLegacyEudoraAttachments = x, "");
        Ui.Add(attachmentBody, _legacyAttachments, 10);
        var deletion = Number(() => (int)Math.Round(job.MaximumDeletionPercent), x => job.MaximumDeletionPercent = x, 40, "Safety limit from 0 to 100. If more than this share of the mirror would be deleted at once, deletions are paused and reported.", "Deletion limit", 0, 100);
        var deleteMissing = Toggle("Delete files removed from Quick Mail, unless more than", () => job.DeleteMissingAttachments, x => job.DeleteMissingAttachments = x,
            "Removes from the mirror the attachments that no longer exist in Quick Mail. Older backups may then point to files that are gone.");
        var deleteRow = Ui.Row(6, deleteMissing, deletion, Ui.Text("% would go at once", Ui.Ink)); Ui.Add(attachmentBody, deleteRow, 10);
        Ui.Add(attachmentBody, Ui.Note("The mirror keeps only the latest version of each attachment.", Ui.AmberInk), 8);
        attachments.Children.Add(attachmentBody);
        var attachmentCard = Ui.Card(attachments); attachmentCard.Margin = new Thickness(6, 0, 0, 12);
        Ui.Place(grid, attachmentCard, 0, 1);
        page.Children.Add(grid);

        // Retention.
        var retention = new StackPanel();
        retention.Children.Add(Ui.Heading("Backups kept"));
        var daily = Number(() => job.KeepDaily, x => job.KeepDaily = x, 44, "Most recent days that keep their last backup. At least 1.", "Daily backups to keep", 1, 999);
        var weekly = Number(() => job.KeepWeekly, x => job.KeepWeekly = x, 44, "Weeks that keep one backup, besides the daily ones. 0 turns this off.", "Weekly backups to keep", 0, 999);
        var monthly = Number(() => job.KeepMonthly, x => job.KeepMonthly = x, 44, "Months that keep one backup, besides the others. 0 turns this off.", "Monthly backups to keep", 0, 999);
        retention.Children.Add(Ui.Row(6, Ui.Text("Keep"), daily, Ui.Text("daily,"), weekly, Ui.Text("weekly and"), monthly, Ui.Text("monthly backups.")));
        var summary = Ui.Note(""); summary.Margin = new Thickness(0, 8, 0, 0); retention.Children.Add(summary);
        var bar = new Grid { Height = 10, Margin = new Thickness(0, 8, 0, 0) }; retention.Children.Add(bar);
        page.Children.Add(Ui.Card(retention));

        int Value(TextBox box) => int.TryParse(box.Text.Trim(), out var v) ? Math.Max(0, v) : 0;
        void Refresh()
        {
            attachmentBody.IsEnabled = _copyAttachments.IsChecked == true;
            UpdateLegacyState();
            summary.Text = Retention.Summary(Value(daily), Value(weekly), Value(monthly));
            bar.ColumnDefinitions.Clear(); bar.Children.Clear();
            var parts = new[] { (Value(daily), Ui.Accent), (Value(weekly) * 7, Ui.AccentSoft), (Value(monthly) * 30, Ui.Line) };
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Item1 <= 0) continue;
                bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(parts[i].Item1, GridUnitType.Star) });
                var rect = new Rectangle { Fill = parts[i].Item2, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 0, 2, 0) };
                Grid.SetColumn(rect, bar.ColumnDefinitions.Count - 1); bar.Children.Add(rect);
            }
        }
        _onEdit.Add(Refresh);
        _onShow[Section.Sources] = () => { ShowDetected(); Refresh(); };
        return page;
    }

    private static List<string> Lines(string text) => text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private TextBox DestinationRow(StackPanel panel, Func<string> get, Action<string> set, bool attachments)
    {
        var box = Field(get, set, double.NaN, attachments
            ? "Folder for the attachment mirror: a local folder or a UNC share such as \\\\nas\\backup\\attachments. It must be empty the first time."
            : "Folder for the ZIP backups and their history: a local folder or a UNC share such as \\\\nas\\backup\\mail. Outside the Quick Mail profile.", mono: true);
        var browse = Ui.Button("Browse…", "Choose a folder. Nothing is copied.");
        var credentials = Ui.Button("Credentials…", "Optional user and password for this NAS folder, kept in Windows Credential Manager. Each destination can use its own.");
        var test = Ui.Button("Test access", "Checks that the Windows task can list, write, read and delete a small file in this UNC folder. It does not close Quick Mail or back up anything. Needs the task registered with a Windows password.");
        browse.Click += (_, _) => Guard(() => { if (BrowseFolder(box.Text) is { } folder) box.Text = folder; });
        credentials.Click += (_, _) => Guard(() => EditNasCredentials(attachments));
        test.Click += (_, _) => Guard(() => StartManual(attachments ? "TestAttachmentAccess" : "TestDatabaseAccess"));
        panel.Children.Add(box);
        var buttons = Ui.Row(8, browse, credentials, test); buttons.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(buttons);
        return box;
    }

    private FrameworkElement CredentialLine(bool attachments)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal };
        void Refresh()
        {
            line.Children.Clear();
            var name = attachments ? _job.AttachmentNasCredential : _job.NasCredential;
            if (string.IsNullOrWhiteSpace(name)) { line.Children.Add(Ui.Note("Uses your Windows account to reach this folder.")); return; }
            var saved = CredentialExists(name);
            line.Children.Add(Ui.Text(name + " ", Ui.Ink));
            line.Children.Add(saved ? Ui.Pill("Password saved", Ui.GreenBack, Ui.Green) : Ui.Pill("Password missing", Ui.RedBack, Ui.RedInk));
        }
        Refresh(); _onEdit.Add(Refresh);
        return line;
    }

    private void UpdateLegacyState()
    {
        LegacyAttachmentSource? source;
        try { source = LegacyAttachmentSource.Detect(_profileBox.Text.Trim()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { source = null; }
        _legacyAttachments.IsEnabled = _copyAttachments.IsChecked == true && source is not null;
        Ui.Tip(_legacyAttachments, source is null
            ? "No linked Eudora Attach or AttachYYYY folder was found in this profile's import settings."
            : "Copies " + source.Directories.Count + " Eudora attachment folders from " + source.Root + " into LegacyEudora in the mirror. Restoring does not rewrite their original paths.");
    }

    private void DetectApplication(string folder)
    {
        var result = ApplicationDetection.DetectEqm(folder, _job);
        _job.ApplicationDirectory = result.Directory;
        _profileBox.Text = result.Profile;
        _databaseBox.Text = "mail.db";
        _executableBox.Text = result.Executable;
        _argumentsBox.Text = "--profileDir" + Environment.NewLine + result.Profile;
        _filesBox.Text = string.Join(Environment.NewLine, ApplicationDetection.EqmProfileFiles);
        _attachmentsBox.Text = "Attachments";
        _lastDetectedFolder = folder.Trim();
        UpdateSaveState();
    }
}
