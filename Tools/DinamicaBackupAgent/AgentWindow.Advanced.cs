using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private FrameworkElement BuildAdvanced()
    {
        var job = _job;
        var page = new StackPanel();
        page.Children.Add(Ui.Title("Advanced"));
        Ui.Add(page, Ui.Note("Settings most installations never need to change."), 4);

        var task = new StackPanel();
        task.Children.Add(Ui.Heading("Windows task"));
        var taskState = new StackPanel(); task.Children.Add(taskState);
        var register = Ui.Button("Register again…", "Saves and registers the Windows task again, for example to change the Windows account it runs as.");
        register.Margin = new Thickness(0, 10, 0, 0);
        register.Click += (_, _) => Guard(() => Save(forceRegistration: true));
        task.Children.Add(register);
        page.Children.Add(WithTop(Ui.Card(task), 16));

        var closing = new StackPanel();
        closing.Children.Add(Ui.Heading("Closing Quick Mail"));
        var wait = Number(() => job.ShutdownTimeoutSeconds, x => job.ShutdownTimeoutSeconds = x, 56, "Longest wait for Quick Mail to close safely and finish what it is doing. The agent never forces it to close.", "Wait for Quick Mail to close", 1, 3600);
        closing.Children.Add(Ui.Row(8, Ui.Text("Wait up to"), wait, Ui.Text("seconds for Quick Mail to close safely; it is never forced.")));
        page.Children.Add(Ui.Card(closing));

        var scripts = new StackPanel();
        scripts.Children.Add(Ui.Heading("Scripts (PowerShell)"));
        scripts.Children.Add(Ui.Note("They run as the Windows task's account with DINAMICA_BACKUP_PROFILE and DINAMICA_BACKUP_DESTINATION set. Never write passwords in them."));
        foreach (var (label, get, set, tip) in new (string, Func<string>, Action<string>, string)[]
        {
            ("Before the copy", () => job.BeforeScript, x => job.BeforeScript = x, "Runs once Quick Mail is closed and before copying. If it fails, the backup fails."),
            ("After a successful backup", () => job.AfterScript, x => job.AfterScript = x, "Runs after a successful backup."),
            ("Always at the end", () => job.FinallyScript, x => job.FinallyScript = x, "Runs at the end even after a failure or a cancel, but not if Quick Mail could not be closed."),
        })
        {
            Ui.Add(scripts, Ui.Label(label), 12);
            scripts.Children.Add(Field(get, set, double.NaN, tip, true, true));
        }
        var timeout = Number(() => job.ScriptTimeoutSeconds, x => job.ScriptTimeoutSeconds = x, 56, "Longest time each script may run.", "Script time limit", 1, 86400);
        Ui.Add(scripts, Ui.Row(8, Ui.Text("Stop a script after"), timeout, Ui.Text("seconds.")), 12);
        page.Children.Add(Ui.Card(scripts));

        var files = new StackPanel();
        files.Children.Add(Ui.Heading("Profile files in each backup"));
        var fileList = new WrapPanel(); files.Children.Add(fileList);
        var edit = Ui.Link("Change the list", "Opens the detected settings in Source and destinations."); edit.Margin = new Thickness(0, 8, 0, 0);
        edit.Click += (_, _) => Show(Section.Sources);
        files.Children.Add(edit);
        page.Children.Add(Ui.Card(files));

        var config = new StackPanel();
        config.Children.Add(Ui.Heading("Configuration file"));
        var path = Ui.Text(_jobPath); path.FontFamily = Ui.Mono; path.FontSize = 12; config.Children.Add(path);
        var log = Ui.Note(""); log.Margin = new Thickness(0, 6, 0, 0); config.Children.Add(log);
        var open = Ui.Button("Open folder", "Opens the folder with the configuration and its diagnostic files in Explorer.");
        open.Margin = new Thickness(0, 10, 0, 0);
        open.Click += (_, _) => Guard(() => Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Path.GetDirectoryName(_jobPath) + "\"") { UseShellExecute = true }));
        config.Children.Add(open);
        page.Children.Add(Ui.Card(config));

        void Refresh()
        {
            taskState.Children.Clear();
            TaskInfo? info = null; string? error = null;
            try { info = _tasks.Inspect(_job.Id); } catch (Exception ex) { error = ex.Message; }
            if (error is not null) taskState.Children.Add(Ui.Text("Windows Task Scheduler could not be read: " + error, Ui.RedInk));
            else if (info is null) taskState.Children.Add(Ui.Text(SaveCoordinator.AnyScheduleEnabled(_job) ? "Not registered yet. Save registers it." : "Not registered: every schedule is off.", Ui.AmberInk));
            else
            {
                taskState.Children.Add(Ui.Row(8, info.Enabled ? Ui.Pill("Registered", Ui.GreenBack, Ui.Green) : Ui.Pill("Disabled", Ui.AmberBack, Ui.AmberInk), Ui.Text("Runs as " + info.UserId + ", checking every 5 minutes.")));
                Ui.Add(taskState, Ui.Note(info.PasswordLogon
                    ? "It runs whether you are signed in or not, so Back up now and Test access are available."
                    : "It runs only while you are signed in. Back up now and Test access need it registered with a Windows password: use Register again…"), 6);
                if (_registration is not null) Ui.Add(taskState, Ui.Note(_registration.Reason, Ui.AmberInk), 6);
            }
            fileList.Children.Clear();
            foreach (var name in Lines(_filesBox.Text))
            {
                var exists = _profileBox.Text.Trim().Length > 0 && File.Exists(Path.Combine(_profileBox.Text.Trim(), name));
                var pill = exists ? Ui.Pill(name, Ui.Disabled, Ui.Ink) : Ui.Pill(name + " · missing, skipped", Ui.AmberBack, Ui.AmberInk);
                pill.Margin = new Thickness(0, 0, 6, 6); fileList.Children.Add(pill);
            }
            var registrationLog = Path.Combine(Path.GetDirectoryName(_jobPath)!, "backup-agent-registration-error.log");
            var lastError = Path.Combine(Path.GetDirectoryName(_jobPath)!, "backup-agent-last-error.json");
            var notes = new List<string>();
            if (File.Exists(registrationLog)) notes.Add("A task registration error was logged in backup-agent-registration-error.log.");
            if (File.Exists(lastError)) notes.Add("The last scheduled run left an error in backup-agent-last-error.json.");
            log.Text = string.Join(" ", notes); log.Visibility = notes.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        _onShow[Section.Advanced] = Refresh;
        return page;
    }
}
