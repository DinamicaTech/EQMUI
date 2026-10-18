using System.IO;
using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;
using Microsoft.Win32;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private HistoryFilter _historyFilter = HistoryFilter.All;
    private bool _problemsOnly;
    private int _historyDays = 30;
    private string? _selectedRun;

    private FrameworkElement BuildHistory()
    {
        var page = new StackPanel();
        Action fill = null!;
        var head = Ui.Columns(0, -1);
        Ui.Place(head, Ui.Stack(4, Ui.Title("History"), Ui.Note("Every run, newest first, in local time. It updates on its own while this window is open.")), 0, 0);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        var buttons = new Dictionary<HistoryFilter, Button>();
        var segment = new Border { BorderBrush = Ui.FieldLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = new StackPanel { Orientation = Orientation.Horizontal } };
        foreach (var (filter, label, tip) in new[] { (HistoryFilter.All, "All", "Shows every operation."), (HistoryFilter.Backups, "Backups", "Shows only database backups and restores."), (HistoryFilter.Windows, "Windows", "Shows only external backup windows."), (HistoryFilter.Tests, "Tests", "Shows only restore tests.") })
        {
            var button = new Button { Template = Ui.FlatTemplate(), Content = label, Padding = new Thickness(12, 6, 12, 6), BorderBrush = Ui.FieldLine, BorderThickness = new Thickness(buttons.Count == 0 ? 0 : 1, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
            Ui.Tip(button, tip); button.Click += (_, _) => { _historyFilter = filter; fill(); };
            buttons[filter] = button; ((StackPanel)segment.Child).Children.Add(button);
        }
        var problems = new CheckBox { Content = "Problems only", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Ui.Tip(problems, "Hides the operations that succeeded without warnings.");
        problems.Checked += (_, _) => { _problemsOnly = true; fill(); }; problems.Unchecked += (_, _) => { _problemsOnly = false; fill(); };
        filters.Children.Add(segment); filters.Children.Add(problems);
        Ui.Place(head, filters, 0, 1);
        page.Children.Add(head);
        var note = Ui.Note("", Ui.AmberInk); note.Margin = new Thickness(0, 10, 0, 0); page.Children.Add(note);

        var table = new StackPanel();
        page.Children.Add(WithTop(Ui.Card(table, 0), 14));
        var footer = Ui.Columns(0, -1, -1);
        var shown = Ui.Note(""); Ui.Place(footer, shown, 0, 0);
        var older = Ui.Link("Show older", "Loads older operations."); older.Margin = new Thickness(0, 0, 12, 0);
        older.Click += (_, _) => { _historyDays += 60; fill(); };
        Ui.Place(footer, older, 0, 1);
        var export = Ui.Button("Export CSV…", "Saves the visible history as a CSV file.");
        export.Click += (_, _) => Guard(() =>
        {
            var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "backup-history.csv" };
            if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, History.Csv(Visible()), new System.Text.UTF8Encoding(true));
        });
        Ui.Place(footer, export, 0, 2);
        page.Children.Add(footer);

        IEnumerable<RunResult> Visible() => History.Filter(_runs, _historyFilter, _problemsOnly).Where(x => x.StartedUtc >= DateTimeOffset.Now.AddDays(-_historyDays));

        void FillHistory()
        {
            foreach (var (filter, button) in buttons) { button.Background = filter == _historyFilter ? Ui.Accent : Ui.CardBack; button.Foreground = filter == _historyFilter ? System.Windows.Media.Brushes.White : Ui.Ink; }
            note.Text = _runsNote; note.Visibility = _runsNote.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            table.Children.Clear();
            double[] widths = [160, 100, 140, 80, 70, 0];
            var header = Ui.Columns(widths); header.Background = Ui.NavBack;
            string[] titles = ["Operation", "Result", "Started", "Duration", "Files", "Detail"];
            for (var i = 0; i < titles.Length; i++) { var t = Ui.Label(titles[i]); t.Margin = new Thickness(i == 0 ? 16 : 0, 9, 12, 9); Ui.Place(header, t, 0, i); }
            table.Children.Add(header);
            var now = DateTime.Now;
            var runs = Visible().ToList();
            if (runs.Count == 0) table.Children.Add(new Border { Padding = new Thickness(16, 14, 16, 14), Child = Ui.Note(_runs.Count == 0 ? "No operations yet." : "No operations match this filter.") });
            foreach (var run in runs)
            {
                var selected = run.Id == _selectedRun;
                var grid = Ui.Columns(widths);
                var cells = new[] { run.DisplayKind, Formatting.Result(run), Formatting.When(run.StartedUtc, now), Formatting.Duration(run.FinishedUtc - run.StartedUtc), run.Kind == "Maintenance" ? "–" : run.FilesCopied.ToString(), run.Detail + (run.NotificationError.Length > 0 ? " · report not sent" : "") };
                for (var i = 0; i < cells.Length; i++)
                {
                    var text = Ui.Text(cells[i], i == 1 ? ResultColor(run) : Ui.Ink, i == 1 && run.Status != "Success" || selected && i == 0);
                    text.Margin = new Thickness(i == 0 ? 16 : 0, 9, 12, 9);
                    if (i == 5) { text.TextWrapping = TextWrapping.NoWrap; text.TextTrimming = TextTrimming.CharacterEllipsis; Ui.Tip(text, cells[i]); }
                    Ui.Place(grid, text, 0, i);
                }
                var row = new Button { Template = Ui.FlatTemplate(), Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = selected ? Ui.RowSelected : Ui.CardBack, BorderBrush = Ui.SoftLine,
                    BorderThickness = new Thickness(selected ? 3 : 0, 0, 0, 1), Padding = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                if (selected) row.BorderBrush = Ui.Accent;
                Ui.Tip(row, "Shows the details of this run.");
                row.Click += (_, _) => { _selectedRun = selected ? null : run.Id; fill(); };
                table.Children.Add(row);
                if (selected) table.Children.Add(RunDetails(run));
            }
            shown.Text = "Showing the last " + _historyDays + " days · " + runs.Count + " operations";
        }
        fill = FillHistory;
        _onShow[Section.History] = FillHistory;
        return page;
    }

    private Border RunDetails(RunResult run)
    {
        var panel = new StackPanel();
        var grid = Ui.Columns(150, 0);
        var lines = new List<(string, string, System.Windows.Media.Brush)> { ("Cause", run.Detail, Ui.Ink) };
        foreach (var warning in run.Warnings) lines.Add(("Warning", warning, Ui.AmberInk));
        foreach (var check in run.VerificationDetails) lines.Add(("Check", check, Ui.Ink));
        if (run.Kind != "Maintenance")
            lines.Add(("Email report", run.NotificationError.Length > 0 ? "Not sent: " + MailErrors.Advice(run.NotificationError) : _job.Notification.Enabled ? "Sent" : "Email reports are off", run.NotificationError.Length > 0 ? Ui.AmberInk : Ui.Ink));
        if (run.Artifact.Length > 0) lines.Add(("Files", run.Artifact, Ui.Ink));
        lines.Add(("Run id", run.Id, Ui.Ink));
        for (var i = 0; i < lines.Count; i++)
        {
            var label = Ui.Text(lines[i].Item1, Ui.Muted); label.Margin = new Thickness(0, 2, 12, 2); label.VerticalAlignment = VerticalAlignment.Top;
            var value = Ui.Text(lines[i].Item2, lines[i].Item3); value.Margin = new Thickness(0, 2, 0, 2);
            if (lines[i].Item1 is "Run id" or "Files") { value.FontFamily = Ui.Mono; value.FontSize = 12; }
            Ui.Place(grid, label, i, 0); Ui.Place(grid, value, i, 1);
        }
        panel.Children.Add(grid);
        var actions = Ui.Row(8); actions.Margin = new Thickness(0, 10, 0, 0);
        if (History.IsProblem(run) && run.Status != "Success")
        {
            var (label, section) = History.FixFor(run);
            if (section != Section.History) { var fix = Ui.Button(label, "Opens " + SectionName(section) + " to fix it.", Ui.Kind.Primary); fix.Click += (_, _) => Show(section); actions.Children.Add(fix); }
        }
        var copy = Ui.Button("Copy details", "Copies every detail of this run, to paste in a support request.");
        copy.Click += (_, _) => Guard(() => { Clipboard.SetText(History.Details(run)); SetStatus("Details copied."); });
        Ui.Add(actions, copy);
        panel.Children.Add(actions);
        return new Border { Background = Ui.NavBack, BorderBrush = Ui.SoftLine, BorderThickness = new Thickness(3, 0, 0, 1), Padding = new Thickness(16, 12, 16, 14), Child = panel };
    }
}
