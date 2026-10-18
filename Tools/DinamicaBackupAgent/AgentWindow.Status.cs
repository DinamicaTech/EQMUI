using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private FrameworkElement BuildStatus()
    {
        var page = new StackPanel();
        _onShow[Section.Status] = () => FillStatus(page);
        return page;
    }

    private void FillStatus(StackPanel page)
    {
        page.Children.Clear();
        var now = DateTime.Now;
        var issues = CurrentIssues();
        page.Children.Add(Ui.Title("Status"));
        Ui.Add(page, Ui.Note(issues.Count == 0 ? "Everything is working. Backups, external windows and restore tests run on their own." : issues.Count == 1 ? "1 problem needs your attention." : issues.Count + " problems need your attention."), 4);
        var issueList = new StackPanel { Margin = new Thickness(0, 14, 0, 4) };
        foreach (var issue in issues) issueList.Children.Add(Ui.Banner(issue.Severity, issue.Title, issue.Cause, FixButton(issue.FixLabel, issue.Fix)));
        page.Children.Add(issueList);

        var cards = new UniformGrid { Columns = 3, Margin = new Thickness(0, 4, 0, 0) };
        cards.Children.Add(TaskCard("Database backup", _job.BackupSchedule, "Backup", now, "Back up now",
            "Runs a backup now through the Windows task (it needs the task registered with a Windows password). Progress and Cancel stay here.", () => StartManual("Backup")));
        cards.Children.Add(TaskCard("External backup window", _job.MaintenanceSchedule, "Maintenance", now, "Open window now",
            "Closes Quick Mail now for the window's length so an external backup program can copy its files. It does not run that program.", () => Start("Maintenance")));
        cards.Children.Add(TaskCard("Restore test", _job.TestSchedule, "Test", now, "Test now",
            "Restores a backup into the restore-test folder and checks it. It never touches the live Quick Mail data.", () => Start("Test")));
        page.Children.Add(cards);

        var recent = new StackPanel();
        recent.Children.Add(Ui.Heading("Latest operations"));
        if (_runsNote.Length > 0) Ui.Add(recent, Ui.Note(_runsNote, Ui.AmberInk), 0);
        if (_runs.Count == 0) Ui.Add(recent, Ui.Note("No operations yet."), 0);
        foreach (var run in _runs.Take(5))
        {
            var row = Ui.Columns(180, 110, 140, 0);
            row.Margin = new Thickness(0, 6, 0, 0);
            Ui.Place(row, Ui.Text(run.DisplayKind), 0, 0);
            Ui.Place(row, Ui.Text(Formatting.Result(run), ResultColor(run), true), 0, 1);
            Ui.Place(row, Ui.Text(Formatting.When(run.StartedUtc, now)), 0, 2);
            var detail = Ui.Text(run.Detail, Ui.Muted); detail.TextTrimming = TextTrimming.CharacterEllipsis; detail.TextWrapping = TextWrapping.NoWrap; Ui.Tip(detail, run.Detail);
            Ui.Place(row, detail, 0, 3);
            recent.Children.Add(row);
        }
        var all = Ui.Link("Open history", "Shows every operation with its details.");
        all.Click += (_, _) => Show(Section.History); all.Margin = new Thickness(0, 10, 0, 0);
        recent.Children.Add(all);
        var card = Ui.Card(recent); card.Margin = new Thickness(0, 12, 0, 0);
        page.Children.Add(card);
    }

    private Button FixButton(string label, Section section)
    {
        var button = Ui.Button(label, section == Section.Status ? "Saves the configuration and registers the Windows task." : "Opens " + SectionName(section) + " to fix it.", Ui.Kind.Secondary);
        button.Click += (_, _) => Guard(() => { if (section == Section.Status) Save(); else Show(section); });
        return button;
    }

    private static System.Windows.Media.Brush ResultColor(RunResult run) => run.Status switch
    {
        "Success" => Ui.Green,
        "Failed" => Ui.RedInk,
        "Running" => Ui.Accent,
        _ => Ui.AmberInk
    };

    private Border TaskCard(string title, Schedule schedule, string kind, DateTime now, string action, string tip, Action run)
    {
        var panel = new StackPanel();
        var head = new DockPanel();
        var state = schedule.Enabled ? Ui.Pill("On", Ui.GreenBack, Ui.Green) : Ui.Pill("Off", Ui.Disabled, Ui.Muted);
        DockPanel.SetDock(state, Dock.Right); head.Children.Add(state);
        head.Children.Add(Ui.Text(title, Ui.Ink, true));
        panel.Children.Add(head);
        Ui.Add(panel, Ui.Note(ScheduleText.Describe(schedule) + (kind == "Maintenance" && schedule.Enabled ? " for " + _job.MaintenanceMinutes + " min" : "")), 4);
        var last = _runs.FirstOrDefault(x => x.Kind == kind);
        Ui.Add(panel, Ui.Text(last is null ? "Last: never" : "Last: " + Formatting.Result(last) + " · " + Formatting.When(last.StartedUtc, now), last is null ? Ui.Muted : ResultColor(last)), 10);
        var next = ScheduleText.Next(schedule, now);
        Ui.Add(panel, Ui.Text("Next: " + (next is null ? "not scheduled" : Formatting.When(next.Value, now))), 2);
        var button = Ui.Button(action, tip); button.Click += (_, _) => Guard(run);
        Ui.Add(panel, button, 12);
        var card = Ui.Card(panel); card.Margin = new Thickness(0, 0, kind == "Test" ? 0 : 12, 0);
        return card;
    }
}
