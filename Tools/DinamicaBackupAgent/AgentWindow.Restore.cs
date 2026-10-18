using System.IO;
using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;
using Microsoft.Win32;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private sealed record BackupItem(string Path, Manifest Manifest, long Size);
    private List<BackupItem> _backups = [];
    private string _backupsError = "";
    private BackupItem? _chosenBackup;
    private bool _showAllBackups;
    private string[] _chosenAttachments = [];

    private FrameworkElement BuildRestore()
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Title("Restore a backup"));
        Ui.Add(page, Ui.Note("Quick Mail is closed while restoring. The files it replaces are kept as a safety copy first, so a restore can be undone."), 4);

        var list = new StackPanel();
        var listCard = Ui.Card(list, 0); WithTop(listCard, 14); page.Children.Add(listCard);

        var options = Ui.Columns(0, 0);
        var what = new StackPanel();
        what.Children.Add(Ui.Heading("2 · What to restore"));
        var database = Check("Mail database", true, "The mail database, " + _job.DatabaseName + ".");
        var profile = Check("Settings, rules and contacts", true, "Accounts, settings, rules, contacts, templates and the other profile files in the backup. Restored together with the database.");
        var attachments = Check("Attachments", false, "Restores attachments from the mirror. The mirror only has the latest version of each file.");
        var all = new RadioButton { Content = "All", IsChecked = true, GroupName = "restore-attachments", VerticalAlignment = VerticalAlignment.Center }; Ui.Tip(all, "Restores every attachment in the mirror.");
        var only = new RadioButton { Content = "Only selected", GroupName = "restore-attachments", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) }; Ui.Tip(only, "Restores only the attachments you pick.");
        var choose = Ui.Link("Choose…", "Opens the list of attachments in this backup to pick which ones to restore."); choose.Margin = new Thickness(14, 0, 0, 0);
        var attachmentChoice = Ui.Row(0, all, only, choose); attachmentChoice.Margin = new Thickness(24, 6, 0, 0);
        foreach (var item in new UIElement[] { database, profile, attachments }) Ui.Add(what, item, 8);
        what.Children.Add(attachmentChoice);
        var whatHint = Ui.Note("", Ui.AmberInk); whatHint.FontSize = 12; Ui.Add(what, whatHint, 8);
        var whatCard = Ui.Card(what); whatCard.Margin = new Thickness(0, 0, 6, 12); Ui.Place(options, whatCard, 0, 0);

        var where = new StackPanel();
        where.Children.Add(Ui.Heading("3 · Where"));
        var replace = new RadioButton { GroupName = "restore-where", IsChecked = true, VerticalContentAlignment = VerticalAlignment.Top };
        var replaceText = Ui.Stack(2, Ui.Text("Replace the current Quick Mail data"), Ui.Note(""));
        replace.Content = replaceText; Ui.Tip(replace, "Overwrites the live data after keeping the files it replaces as a safety copy.");
        var other = new RadioButton { GroupName = "restore-where", Content = "Into another folder to inspect", VerticalAlignment = VerticalAlignment.Center };
        Ui.Tip(other, "Restores into another folder so you can look at it without touching Quick Mail.");
        var otherFolder = Ui.Box(220, "Folder to restore into. Must be outside the backup folders.", true);
        var browse = Ui.Button("Browse…", "Choose the folder to restore into.");
        browse.Click += (_, _) => Guard(() => { if (BrowseFolder(otherFolder.Text) is { } folder) { otherFolder.Text = folder; other.IsChecked = true; } });
        where.Children.Add(replace);
        Ui.Add(where, Ui.Row(8, other, otherFolder, browse), 10);
        var whereCard = Ui.Card(where); whereCard.Margin = new Thickness(6, 0, 0, 12); Ui.Place(options, whereCard, 0, 1);
        page.Children.Add(options);

        var interrupted = Ui.Columns(0, -1);
        var interruptedText = Ui.Note("");
        var recover = Ui.Button("Recover…", "Undoes a restore that was cut off, using its recovery journal. Asks for confirmation first.");
        Ui.Place(interrupted, interruptedText, 0, 0); Ui.Place(interrupted, recover, 0, 1);
        page.Children.Add(Ui.Card(interrupted, 12));

        var summaryBar = Ui.Columns(0, -1);
        var summary = Ui.Text("", Ui.Ink);
        var restore = Ui.Button("Restore…", "Closes Quick Mail, keeps the safety copy, restores the chosen backup and reopens Quick Mail. Asks for confirmation first.", Ui.Kind.Primary);
        Ui.Place(summaryBar, summary, 0, 0); Ui.Place(summaryBar, restore, 0, 1);
        page.Children.Add(Ui.Card(summaryBar, 12));

        string Destination() => replace.IsChecked == true ? _profileBox.Text.Trim() : otherFolder.Text.Trim();
        (RestoreMode? Mode, string Problem) Plan()
        {
            var db = database.IsChecked == true; var att = attachments.IsChecked == true; var selected = only.IsChecked == true;
            if (_chosenBackup is null) return (null, "Choose a backup first.");
            if (!db && profile.IsChecked == true) return (null, "Settings are restored together with the database: tick Mail database too.");
            if (db && att && selected) return (null, "Selected attachments are restored on their own: untick Mail database, or choose All.");
            if (att && !_chosenBackup.Manifest.IncludesAttachments) return (null, "This backup has no attachment list: untick Attachments.");
            if (att && selected && _chosenAttachments.Length == 0) return (null, "Choose the attachments to restore with Choose….");
            if (!db && !att) return (null, "Tick what to restore.");
            if (replace.IsChecked != true && otherFolder.Text.Trim().Length == 0) return (null, "Choose the folder to restore into.");
            return (db ? att ? RestoreMode.DatabaseAndAttachments : RestoreMode.Database : selected ? RestoreMode.SelectedAttachments : RestoreMode.Attachments, "");
        }
        void Refresh()
        {
            attachmentChoice.IsEnabled = attachments.IsChecked == true;
            replaceText.Children.OfType<TextBlock>().Last().Text = "Replaced files kept in " + System.IO.Path.GetFileName(_profileBox.Text.Trim().TrimEnd('\\')) + "\\.restore-previous-[run]";
            var (mode, problem) = Plan();
            whatHint.Text = problem; whatHint.Visibility = problem.Length == 0 || _chosenBackup is null ? Visibility.Collapsed : Visibility.Visible;
            if (only.IsChecked == true && _chosenAttachments.Length > 0) choose.Content = new TextBlock { Text = "Choose… (" + _chosenAttachments.Length + ")", Foreground = Ui.Accent, TextDecorations = TextDecorations.Underline };
            restore.IsEnabled = mode is not null && _operation == null;
            summary.Text = mode is null ? problem : Formatting.When(_chosenBackup!.Manifest.CreatedUtc, DateTime.Now) + " · " + Describe(mode.Value, profile.IsChecked == true)
                + " · " + (replace.IsChecked == true ? "replaces current data · Quick Mail will close" : "into " + otherFolder.Text.Trim());
            var journal = System.IO.Path.Combine(Destination(), ".restore-in-progress.json");
            var found = Destination().Length > 0 && File.Exists(journal);
            interruptedText.Text = found ? "An interrupted restoration was found in " + Destination() + ". Recover it before restoring again." : "Interrupted restoration: none found. If a restore is ever cut off, you can undo it here.";
            interruptedText.Foreground = found ? Ui.RedInk : Ui.Muted;
            recover.IsEnabled = found;
        }
        foreach (var check in new[] { database, profile, attachments }) { check.Checked += (_, _) => Refresh(); check.Unchecked += (_, _) => Refresh(); }
        foreach (var radio in new[] { all, only, replace, other }) radio.Checked += (_, _) => Refresh();
        otherFolder.TextChanged += (_, _) => Refresh();
        choose.Click += (_, _) => Guard(() => { if (_chosenBackup is null) return; ChooseAttachments(_chosenBackup.Manifest); only.IsChecked = true; Refresh(); });
        recover.Click += (_, _) => Guard(() =>
        {
            if (MessageBox.Show(this, "Undo the interrupted restoration in " + Destination() + "? The files it replaced are put back; the partly restored files are kept aside.", "Recover interrupted restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            RestoreEngine.RecoverInterrupted(Destination(), _job.Id); SetStatus("The interrupted restoration was undone.", Ui.Green); Refresh();
        });
        restore.Click += (_, _) => Guard(() =>
        {
            var (mode, problem) = Plan();
            if (mode is null) throw new InvalidOperationException(problem);
            var backup = _chosenBackup!;
            var message = "This replaces the matching files in " + Destination() + ".\n\nBackup: " + backup.Manifest.CreatedUtc.ToLocalTime().ToString("g") + "\nWhat: " + Describe(mode.Value, profile.IsChecked == true)
                + "\n\nThe replaced files are kept in .restore-previous-[run] inside that folder. Attachments come from the mirror, which keeps only their latest version. Restoring the database makes Quick Mail start offline so you can review it.\n\nContinue?";
            if (MessageBox.Show(this, message, "Confirm restoration", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            Start("Restore", backup.Path, Destination(), mode.Value, mode == RestoreMode.SelectedAttachments ? _chosenAttachments : [], profile.IsChecked == true);
        });

        void FillList()
        {
            list.Children.Clear();
            var head = Ui.Heading("1 · Choose a backup"); head.Margin = new Thickness(16, 10, 16, 10); list.Children.Add(head);
            if (_backupsError.Length > 0)
            {
                var open = Ui.Button("Check destinations", "Opens Source and destinations.", Ui.Kind.Primary); open.Click += (_, _) => Show(Section.Sources);
                var file = Ui.Button("Choose a file…", "Choose a backup ZIP from any folder.");
                file.Click += (_, _) => Guard(() =>
                {
                    var dialog = new OpenFileDialog { Filter = "Backup ZIP (*.zip)|*.zip" };
                    if (Directory.Exists(_job.Destination)) dialog.InitialDirectory = _job.Destination;
                    if (dialog.ShowDialog(this) != true) return;
                    var manifest = BackupEngine.ReadManifest(dialog.FileName);
                    if (manifest.JobId != _job.Id) throw new InvalidDataException("This backup belongs to another configuration.");
                    _backups.Insert(0, new BackupItem(dialog.FileName, manifest, new FileInfo(dialog.FileName).Length)); _chosenBackup = _backups[0]; _backupsError = ""; FillList(); Refresh();
                });
                var banner = Ui.Banner(Severity.Error, "The backups could not be listed", _backupsError, null); banner.Margin = new Thickness(16, 0, 16, 10);
                list.Children.Add(banner);
                var row = Ui.Row(8, open, file); row.Margin = new Thickness(16, 0, 16, 14); list.Children.Add(row);
                return;
            }
            double[] widths = [36, 180, 100, 100, 0];
            var header = Ui.Columns(widths); header.Background = Ui.NavBack;
            string[] titles = ["", "Date", "Kept as", "Size", "Restore test"];
            for (var i = 0; i < titles.Length; i++) { var t = Ui.Label(titles[i]); t.Margin = new Thickness(i == 0 ? 16 : 0, 7, 8, 7); Ui.Place(header, t, 0, i); }
            list.Children.Add(header);
            var tiers = Retention.Tiers(_backups.Select(x => (x.Path, x.Manifest.CreatedUtc)), _job.KeepDaily, _job.KeepWeekly, _job.KeepMonthly);
            var tests = _runs.Where(x => x.Kind == "Test").ToArray();
            var now = DateTime.Now;
            var visible = _showAllBackups ? _backups : _backups.Take(4).ToList();
            if (_backups.Count == 0) { var none = Ui.Note("No backups found in " + (_job.Destination.Length == 0 ? "the backup folder (none is set)" : _job.Destination) + "."); none.Margin = new Thickness(16, 10, 16, 14); list.Children.Add(none); }
            foreach (var item in visible)
            {
                var test = tests.Where(x => x.Detail.Contains(System.IO.Path.GetFileName(item.Path), StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.StartedUtc).FirstOrDefault();
                var radio = new RadioButton { GroupName = "restore-backup", IsChecked = item == _chosenBackup, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
                Ui.Tip(radio, "Restores this backup: " + item.Path);
                var grid = Ui.Columns(widths);
                Ui.Place(grid, radio, 0, 0);
                Ui.Place(grid, Ui.Text(Formatting.When(item.Manifest.CreatedUtc, now), Ui.Ink, item == _chosenBackup), 0, 1);
                Ui.Place(grid, Ui.Text(tiers.TryGetValue(item.Path, out var tier) ? tier : "–"), 0, 2);
                Ui.Place(grid, Ui.Text(Formatting.Size(item.Size)), 0, 3);
                Ui.Place(grid, Ui.Text(test is null ? "Not tested" : (test.Status == "Success" ? "Passed on " : Formatting.Result(test) + " on ") + Formatting.When(test.StartedUtc, now),
                    test is null ? Ui.Muted : test.Status == "Success" ? Ui.Green : Ui.RedInk), 0, 4);
                var line = new Border { Child = grid, Padding = new Thickness(0, 8, 0, 8), BorderBrush = Ui.SoftLine, BorderThickness = new Thickness(0, 0, 0, 1), Background = item == _chosenBackup ? Ui.RowSelected : Ui.CardBack, Cursor = System.Windows.Input.Cursors.Hand };
                Ui.Tip(line, item.Path);
                void Pick() { _chosenBackup = item; _chosenAttachments = []; FillList(); Refresh(); }
                line.MouseLeftButtonUp += (_, _) => Pick(); radio.Checked += (_, _) => { if (_chosenBackup != item) Pick(); };
                list.Children.Add(line);
            }
            var more = Ui.Columns(0, -1); more.Margin = new Thickness(16, 8, 16, 8);
            Ui.Place(more, Ui.Note(visible.Count + " of " + _backups.Count + " backups shown"), 0, 0);
            if (_backups.Count > visible.Count) { var show = Ui.Link("Show all", "Shows every backup kept in the backup folder."); show.Click += (_, _) => { _showAllBackups = true; FillList(); }; Ui.Place(more, show, 0, 1); }
            list.Children.Add(more);
        }

        _onShow[Section.Restore] = () =>
        {
            LoadRuns(); LoadBackups();
            if (_chosenBackup is null || !_backups.Contains(_chosenBackup)) _chosenBackup = _backups.FirstOrDefault();
            FillList(); Refresh();
        };
        _onEdit.Add(Refresh);
        return page;
    }

    private CheckBox Check(string label, bool value, string tip)
    {
        var check = new CheckBox { Content = label, IsChecked = value, VerticalAlignment = VerticalAlignment.Center }; Ui.Tip(check, tip); return check;
    }

    private static string Describe(RestoreMode mode, bool profile) => mode switch
    {
        RestoreMode.Database => profile ? "database and settings" : "database only",
        RestoreMode.DatabaseAndAttachments => profile ? "database, settings and all attachments" : "database and all attachments",
        RestoreMode.Attachments => "all attachments",
        _ => "selected attachments"
    };

    private void LoadBackups()
    {
        _backupsError = "";
        var keep = _backups.Where(x => !SafeWithin(_job.Destination, x.Path)).ToList(); // files picked by hand stay listed
        try
        {
            var engine = new BackupEngine(_job);
            _backups = engine.Backups().Select(x => new BackupItem(x.Path, x.Manifest, new FileInfo(x.Path).Length)).OrderByDescending(x => x.Manifest.CreatedUtc).ToList();
            if (_chosenBackup is not null) _chosenBackup = _backups.FirstOrDefault(x => string.Equals(x.Path, _chosenBackup.Path, StringComparison.OrdinalIgnoreCase)) ?? keep.FirstOrDefault(x => x.Path == _chosenBackup.Path);
            _backups.InsertRange(0, keep);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
        {
            _backups = keep;
            _backupsError = "The backup folder " + _job.Destination + " could not be read: " + ex.Message + " Check the folder and its credentials, or choose a backup file by hand.";
        }
    }
    private static bool SafeWithin(string root, string path)
    {
        try { return root.Length > 0 && SafePaths.Within(root, path); } catch (ArgumentException) { return false; }
    }

    private void ChooseAttachments(Manifest manifest)
    {
        var dialog = Dialog("Choose attachments to restore", 640);
        var panel = new StackPanel { Margin = new Thickness(18) }; dialog.Content = panel;
        var filter = Ui.Box(double.NaN, "Type part of a file name to filter the list.");
        Ui.Add(panel, Ui.Label("Filter")); panel.Children.Add(filter);
        var list = new ListBox { SelectionMode = SelectionMode.Extended, Height = 320, Margin = new Thickness(0, 10, 0, 0) };
        var all = manifest.Attachments.Select(x => x.Path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        void Fill() { var text = filter.Text.Trim(); list.ItemsSource = all.Where(x => text.Length == 0 || x.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray(); foreach (var item in _chosenAttachments) if (list.Items.Contains(item)) list.SelectedItems.Add(item); }
        filter.TextChanged += (_, _) => Fill(); Fill();
        panel.Children.Add(list);
        var ok = Ui.Button("Use selected", "Restores only the selected attachments.", Ui.Kind.Primary);
        var cancel = Ui.Button("Cancel", "Keeps the previous choice.");
        var buttons = Ui.Row(8, cancel, ok); buttons.HorizontalAlignment = HorizontalAlignment.Right; Ui.Add(panel, buttons, 14);
        ok.Click += (_, _) => { _chosenAttachments = list.SelectedItems.Cast<string>().ToArray(); dialog.DialogResult = true; };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        dialog.ShowDialog();
    }
}
