using System.IO;
using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private TextBox _restoreTestFolder = null!;
    private readonly List<(TextBox Name, TextBox Sql)> _probeRows = [];

    private FrameworkElement BuildRestoreTest()
    {
        var job = _job;
        var page = new StackPanel();
        var head = Ui.Columns(0, -1);
        var scheduleLine = Ui.Note("");
        var title = Ui.Stack(4, Ui.Title("Restore test"), scheduleLine);
        Ui.Place(head, title, 0, 0);
        var testNow = Ui.Button("Test now", "Saves and runs the test now with the settings on this screen. The live Quick Mail data is never touched.");
        testNow.Click += (_, _) => Guard(() => Start("Test"));
        Ui.Place(head, testNow, 0, 1);
        page.Children.Add(head);
        var change = Ui.Link("change", "Opens Schedule."); change.Click += (_, _) => Show(Section.Schedule);
        var scheduleText = new System.Windows.Documents.Run();
        scheduleLine.Inlines.Add(scheduleText);
        scheduleLine.Inlines.Add(new System.Windows.Documents.InlineUIContainer(change) { BaselineAlignment = BaselineAlignment.Center });

        var folder = new StackPanel();
        folder.Children.Add(Ui.Heading("Restore-test folder"));
        _restoreTestFolder = Field(() => job.RestoreTestDirectory, x => job.RestoreTestDirectory = x, double.NaN,
            "An empty folder used only for tests, outside the Quick Mail profile and the backup folders. The test backup is restored here.", mono: true);
        var browse = Ui.Button("Browse…", "Choose an empty folder for the tests.", Ui.Kind.Primary);
        var suggestion = Ui.Button("", "Creates this folder and uses it for the tests.");
        browse.Click += (_, _) => Guard(() => { if (BrowseFolder(_restoreTestFolder.Text) is { } chosen) _restoreTestFolder.Text = chosen; });
        suggestion.Click += (_, _) => Guard(() => { var path = SuggestedTestFolder(); Directory.CreateDirectory(path); _restoreTestFolder.Text = path; });
        var row = Ui.Columns(0, -1, -1);
        Ui.Place(row, _restoreTestFolder, 0, 0); browse.Margin = new Thickness(8, 0, 0, 0); Ui.Place(row, browse, 0, 1); suggestion.Margin = new Thickness(8, 0, 0, 0); Ui.Place(row, suggestion, 0, 2);
        folder.Children.Add(row);
        var problem = Ui.Note("", Ui.RedInk); problem.Margin = new Thickness(0, 8, 0, 0); folder.Children.Add(problem);
        page.Children.Add(WithTop(Ui.Card(folder), 16));

        var options = Ui.Columns(0, 0);
        var tested = new StackPanel();
        tested.Children.Add(Ui.Heading("What is tested"));
        tested.Children.Add(Toggle("Yesterday's latest successful backup", () => job.TestPreviousDay, x => job.TestPreviousDay = x,
            "Tests the last successful backup of the previous day instead of the newest one. If there is none, the test says so instead of picking another day."));
        var sample = Number(() => job.AttachmentSampleSize, x => job.AttachmentSampleSize = x, 52, "How many attachments, picked at random, are read and hashed. 0 skips this check.", "Attachments to verify", 0, 1_000_000);
        Ui.Add(tested, Ui.Row(8, sample, Ui.Text("random attachments, comparing their hash")), 10);
        Ui.Add(tested, Toggle("Full database integrity check (slower)", () => job.FullIntegrityCheck, x => job.FullIntegrityCheck = x,
            "Runs SQLite's full integrity check instead of the quick one. A large database can take much longer."), 10);
        var testedCard = Ui.Card(tested); testedCard.Margin = new Thickness(0, 0, 6, 12); Ui.Place(options, testedCard, 0, 0);
        var after = new StackPanel();
        after.Children.Add(Ui.Heading("After the test"));
        after.Children.Add(Toggle("Keep the files when it succeeds", () => job.KeepSuccessfulTest, x => job.KeepSuccessfulTest = x, "Leaves the restored files in the folder after a successful test instead of deleting them."));
        Ui.Add(after, Ui.Note("When it fails, the files are always kept for review."), 10);
        var afterCard = Ui.Card(after); afterCard.Margin = new Thickness(6, 0, 0, 12); Ui.Place(options, afterCard, 0, 1);
        page.Children.Add(options);

        var checks = new StackPanel();
        var checksHead = Ui.Columns(0, -1);
        Ui.Place(checksHead, Ui.Heading("Data checks"), 0, 0);
        var hint = Ui.Label("Each query returns one value; the restored value is compared with the one captured at backup time"); Ui.Place(checksHead, hint, 0, 1);
        checks.Children.Add(checksHead);
        var rows = new StackPanel();
        checks.Children.Add(rows);
        var add = Ui.Button("Add check", "Adds another query to compare.", Ui.Kind.Dashed); add.Margin = new Thickness(0, 10, 0, 0);
        checks.Children.Add(add);
        page.Children.Add(Ui.Card(checks));

        void AddProbe(string name, string sql)
        {
            var line = Ui.Columns(260, 0, -1); line.Margin = new Thickness(0, 6, 0, 0);
            var nameBox = Ui.Box(double.NaN, "Name shown in the report."); nameBox.Text = name; nameBox.Margin = new Thickness(0, 0, 8, 0);
            var sqlBox = Ui.Box(double.NaN, "Read-only SELECT that returns a single value.", true); sqlBox.Text = sql;
            var remove = Ui.Button("Remove", "Removes this check."); remove.Margin = new Thickness(8, 0, 0, 0);
            Ui.Place(line, nameBox, 0, 0); Ui.Place(line, sqlBox, 0, 1); Ui.Place(line, remove, 0, 2);
            var entry = (nameBox, sqlBox);
            _probeRows.Add(entry); rows.Children.Add(line);
            nameBox.TextChanged += (_, _) => UpdateSaveState(); sqlBox.TextChanged += (_, _) => UpdateSaveState();
            remove.Click += (_, _) => { _probeRows.Remove(entry); rows.Children.Remove(line); UpdateSaveState(); };
        }
        foreach (var probe in job.Probes) AddProbe(probe.Name, probe.Sql);
        add.Click += (_, _) => { AddProbe("", "SELECT "); UpdateSaveState(); };
        Track(() =>
        {
            var probes = new List<SqlProbe>();
            foreach (var (name, sql) in _probeRows)
            {
                if (name.Text.Trim().Length == 0 && sql.Text.Trim() is "" or "SELECT") continue;
                if (name.Text.Trim().Length == 0 || !sql.Text.Trim().StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException("Each data check needs a name and a query that starts with SELECT.");
                probes.Add(new SqlProbe(name.Text.Trim(), sql.Text.Trim()));
            }
            job.Probes = probes;
        }, () => string.Join("\n", _probeRows.Select(x => x.Name.Text.Trim() + "|" + x.Sql.Text.Trim())));

        void Refresh()
        {
            scheduleText.Text = "Restores a backup into a separate folder and checks that it opens and that its data matches the original. " + ScheduleText.Describe(_testRow.Preview()) + " · ";
            var path = SuggestedTestFolder();
            suggestion.Content = "Use " + path; suggestion.Visibility = path.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            var text = _restoreTestFolder.Text.Trim();
            var failed = _runs.FirstOrDefault(x => x.Kind == "Test") is { Status: "Failed" } last && last.Detail.Contains("restore-test", StringComparison.OrdinalIgnoreCase);
            var space = DatabaseSize() is { } size ? " It needs free space for a full copy of the database (about " + Formatting.Size(size) + ")." : "";
            problem.Text = text.Length == 0 ? (failed ? "No folder is set, which is why the last test failed." : "No folder is set: the test cannot run.") + space
                : !Directory.Exists(text) ? "This folder does not exist yet; it will be created when the test runs." + space : "";
            problem.Visibility = problem.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            _restoreTestFolder.BorderBrush = text.Length == 0 ? Ui.Red : Ui.FieldLine;
            _restoreTestFolder.BorderThickness = new Thickness(text.Length == 0 ? 2 : 1);
        }
        _onEdit.Add(Refresh);
        _onShow[Section.RestoreTest] = Refresh;
        return page;
    }

    private string SuggestedTestFolder()
    {
        var root = _appFolder.Text.Trim();
        return root.Length == 0 ? "" : Path.Combine(root, "RestoreTest");
    }

    private long? DatabaseSize()
    {
        try
        {
            var path = Path.Combine(_profileBox.Text.Trim(), _databaseBox.Text.Trim());
            return File.Exists(path) ? new FileInfo(path).Length : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { return null; }
    }
}
