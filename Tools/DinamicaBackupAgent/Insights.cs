using System.Globalization;
using System.IO;
using System.Text;
using DinamicaBackup;

namespace DinamicaBackupAgent;

/// <summary>Screens of the configuration window, in navigation order.</summary>
internal enum Section { Status, Schedule, Sources, RestoreTest, Email, History, Restore, Advanced }

internal enum Severity { Error, Warning }

/// <summary>A visible problem: what happened, why, and the action that fixes it.</summary>
internal sealed record Issue(Severity Severity, string Title, string Cause, string FixLabel, Section Fix);

internal static class Formatting
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    public static string When(DateTimeOffset value, DateTime now)
    {
        var local = value.ToLocalTime().DateTime;
        if (local.Date == now.Date) return "Today " + local.ToString("HH:mm", English);
        if (local.Date == now.Date.AddDays(-1)) return "Yesterday " + local.ToString("HH:mm", English);
        if (local.Date == now.Date.AddDays(1)) return "Tomorrow " + local.ToString("HH:mm", English);
        return local.ToString("ddd d MMM, HH:mm", English);
    }
    public static string When(DateTime local, DateTime now) => When(new DateTimeOffset(local), now);

    public static string Duration(TimeSpan? duration)
    {
        if (duration is null) return "–";
        var span = duration.Value < TimeSpan.Zero ? TimeSpan.Zero : duration.Value;
        if (span.TotalSeconds < 60) return (int)span.TotalSeconds + " s";
        if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " min";
        return span.TotalHours.ToString("0.#", English) + " h";
    }

    public static string Size(long bytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        double value = bytes; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? bytes + " bytes" : value.ToString(value < 10 ? "0.0" : "0", English) + " " + units[unit];
    }

    public static string Result(RunResult run) => run.Status switch
    {
        "Success" => "Succeeded",
        "Failed" => "Failed",
        "Warning" => "Warning",
        "Cancelled" => "Cancelled",
        "Missed" => "Missed",
        "Running" => "Running",
        "Recovered" => "Recovered",
        _ => run.Status
    };
}

internal static class ScheduleText
{
    public static string Describe(Schedule schedule)
    {
        if (!schedule.Enabled) return "Off";
        return schedule.Frequency switch
        {
            "Weekly" => "Weekly, " + schedule.Day + "s at " + schedule.Time,
            "Monthly" => "Monthly, day " + schedule.DayOfMonth + " at " + schedule.Time,
            _ => "Daily at " + schedule.Time
        };
    }

    /// <summary>Next occurrence after <paramref name="now"/>, using the same rules as <see cref="Schedule.LatestOccurrence"/>.</summary>
    public static DateTime? Next(Schedule schedule, DateTime now)
    {
        if (!schedule.Enabled || !TimeOnly.TryParseExact(schedule.Time, "HH:mm", out var time)) return null;
        for (var i = 0; i < 63; i++)
        {
            var date = now.Date.AddDays(i);
            if (schedule.Frequency == "Weekly" && date.DayOfWeek != schedule.Day) continue;
            if (schedule.Frequency == "Monthly" && date.Day != Math.Min(schedule.DayOfMonth, DateTime.DaysInMonth(date.Year, date.Month))) continue;
            var candidate = date.Add(time.ToTimeSpan());
            if (candidate > now) return candidate;
        }
        return null;
    }

    public static double Hour(Schedule schedule) => TimeOnly.TryParseExact(schedule.Time, "HH:mm", out var time) ? time.ToTimeSpan().TotalHours : 0;

    /// <summary>Clashes between tasks that both need Quick Mail closed or its data quiet.</summary>
    public static List<string> Conflicts(Schedule backup, Schedule window, int windowMinutes, Schedule test)
    {
        var conflicts = new List<string>();
        if (window.Enabled && TimeOnly.TryParseExact(window.Time, "HH:mm", out var start))
        {
            var from = start.ToTimeSpan().TotalMinutes; var to = from + windowMinutes;
            bool Inside(Schedule s)
            {
                if (!s.Enabled || !TimeOnly.TryParseExact(s.Time, "HH:mm", out var t) || !SameDays(s, window)) return false;
                var minute = t.ToTimeSpan().TotalMinutes;
                return (minute >= from && minute < to) || (minute + 1440 >= from && minute + 1440 < to);
            }
            var end = start.AddMinutes(windowMinutes).ToString("HH:mm", CultureInfo.InvariantCulture);
            if (Inside(backup)) conflicts.Add("The database backup at " + backup.Time + " falls inside the external backup window (" + window.Time + "–" + end + "). It will wait until the window ends; move one of them.");
            if (Inside(test)) conflicts.Add("The restore test at " + test.Time + " falls inside the external backup window (" + window.Time + "–" + end + "). The external program may be reading the same backups; move one of them.");
        }
        return conflicts;
    }

    // Weekly and monthly schedules only clash on the days they share; daily ones always can.
    private static bool SameDays(Schedule a, Schedule b)
    {
        if (a.Frequency == "Daily" || b.Frequency == "Daily") return true;
        if (a.Frequency == "Weekly" && b.Frequency == "Weekly") return a.Day == b.Day;
        if (a.Frequency == "Monthly" && b.Frequency == "Monthly") return a.DayOfMonth == b.DayOfMonth;
        return true;
    }
}

internal static class Retention
{
    public static string Summary(int daily, int weekly, int monthly)
    {
        var count = Math.Max(1, daily) + Math.Max(0, weekly) + Math.Max(0, monthly);
        string oldest = monthly > 0 ? Plural(monthly, "month")
            : weekly > 0 ? Plural(weekly, "week")
            : Plural(Math.Max(1, daily), "day");
        return "Keeps up to " + count + " backups; the oldest is about " + oldest + " old.";
    }
    private static string Plural(int n, string unit) => n + " " + unit + (n == 1 ? "" : "s");

    /// <summary>Label of the tier that keeps each backup, using the same grouping as <see cref="BackupEngine.ApplyRetention"/>.</summary>
    public static Dictionary<string, string> Tiers(IEnumerable<(string Path, DateTimeOffset Created)> backups, int daily, int weekly, int monthly)
    {
        var all = backups.OrderByDescending(x => x.Created).ToArray();
        var tiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in all.GroupBy(x => x.Created.LocalDateTime.ToString("yyyy-MM")).Take(monthly).Select(x => x.First())) tiers[item.Path] = "Monthly";
        foreach (var item in all.GroupBy(x => { var d = x.Created.LocalDateTime.Date; return d.AddDays(-((int)d.DayOfWeek + 6) % 7); }).Take(weekly).Select(x => x.First())) tiers[item.Path] = "Weekly";
        foreach (var item in all.GroupBy(x => x.Created.LocalDateTime.Date).Take(daily).Select(x => x.First())) tiers[item.Path] = "Daily";
        if (all.Length > 0) tiers.TryAdd(all[0].Path, "Latest");
        return tiers;
    }
}

internal enum HistoryFilter { All, Backups, Windows, Tests }

internal static class History
{
    /// <summary>Receipts plus the last-run file: a run that failed before it could write a receipt is still shown.</summary>
    public static List<RunResult> Merge(IEnumerable<RunResult> receipts, RunResult? lastRun)
    {
        var runs = receipts.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        if (lastRun is not null) runs[lastRun.Id] = lastRun;
        return runs.Values.OrderByDescending(x => x.StartedUtc).ToList();
    }

    public static bool IsProblem(RunResult run) => run.Status is not ("Success" or "Running") || run.Warnings.Count > 0 || run.NotificationError.Length > 0;

    public static IEnumerable<RunResult> Filter(IEnumerable<RunResult> runs, HistoryFilter filter, bool problemsOnly) => runs.Where(run =>
        (filter switch
        {
            HistoryFilter.Backups => run.Kind is "Backup" or "Restore",
            HistoryFilter.Windows => run.Kind == "Maintenance",
            HistoryFilter.Tests => run.Kind == "Test",
            _ => true
        }) && (!problemsOnly || IsProblem(run)));

    public static string Details(RunResult run)
    {
        var text = new StringBuilder();
        text.AppendLine(run.DisplayKind + " · " + Formatting.Result(run));
        text.AppendLine("Started: " + run.StartedUtc.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("en-GB")));
        text.AppendLine("Duration: " + Formatting.Duration(run.FinishedUtc - run.StartedUtc));
        text.AppendLine("Files: " + run.FilesCopied);
        text.AppendLine("Detail: " + run.Detail);
        foreach (var warning in run.Warnings) text.AppendLine("Warning: " + warning);
        foreach (var check in run.VerificationDetails) text.AppendLine("Check: " + check);
        if (run.NotificationError.Length > 0) text.AppendLine("Email report not sent: " + run.NotificationError);
        if (run.Artifact.Length > 0) text.AppendLine("Artifact: " + run.Artifact);
        text.AppendLine("Run id: " + run.Id);
        return text.ToString();
    }

    public static string Csv(IEnumerable<RunResult> runs)
    {
        static string Cell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Operation,Result,Started,Finished,Files,Detail,Email report,Run id\r\n");
        foreach (var run in runs)
            text.Append(string.Join(",", Cell(run.DisplayKind), Cell(Formatting.Result(run)), Cell(run.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Cell(run.FinishedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? ""), run.FilesCopied.ToString(CultureInfo.InvariantCulture),
                Cell(run.Detail), Cell(run.NotificationError), Cell(run.Id))).Append("\r\n");
        return text.ToString();
    }

    /// <summary>The screen that fixes a failed run, and the button text that leads there.</summary>
    public static (string Label, Section Section) FixFor(RunResult run)
    {
        var detail = run.Detail;
        bool Has(string text) => detail.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (Has("restore-test")) return ("Choose folder…", Section.RestoreTest);
        if (Has("credential") || Has("NAS") || Has("UNC") || Has("access") || Has("destination") || Has("free space")) return ("Check destinations", Section.Sources);
        if (Has("script")) return ("Check scripts", Section.Advanced);
        if (Has("mail.db") || Has("profile") || Has("executable")) return ("Check Quick Mail folder", Section.Sources);
        if (Has("schedule")) return ("Check schedule", Section.Schedule);
        return ("See details", Section.History);
    }
}

/// <summary>Everything the Status screen warns about, each with its cause and the screen that fixes it.</summary>
internal static class StatusAdvisor
{
    public static List<Issue> Issues(BackupJob job, IReadOnlyList<RunResult> runs, RegistrationNeed? registration, Func<string, bool> credentialExists, DateTime now)
    {
        var issues = new List<Issue>();
        var database = string.IsNullOrWhiteSpace(job.ProfileDirectory) ? "" : Path.Combine(job.ProfileDirectory, job.DatabaseName);
        if (database.Length == 0 || !File.Exists(database))
            issues.Add(new(Severity.Error, "Quick Mail data not found", database.Length == 0 ? "No Eudora Quick Mail folder is set." : "The database " + database + " does not exist.", "Choose Quick Mail folder…", Section.Sources));
        if ((job.BackupSchedule.Enabled || job.TestSchedule.Enabled) && string.IsNullOrWhiteSpace(job.Destination))
            issues.Add(new(Severity.Error, "No backup folder", "Backups and restore tests are on, but no folder for the backups is set.", "Choose backup folder…", Section.Sources));
        if (job.BackupSchedule.Enabled && job.CopyAttachments && string.IsNullOrWhiteSpace(job.AttachmentDestination))
            issues.Add(new(Severity.Error, "No attachments folder", "Attachments are copied, but no folder for them is set.", "Choose attachments folder…", Section.Sources));
        if (job.TestSchedule.Enabled && string.IsNullOrWhiteSpace(job.RestoreTestDirectory))
            issues.Add(new(Severity.Error, "Restore test can't run", "No restore-test folder is set, so the scheduled test fails before it starts.", "Choose folder…", Section.RestoreTest));
        foreach (var (name, kind) in new[] { (job.NasCredential, "database backups"), (job.AttachmentNasCredential, "attachments") })
            if (!string.IsNullOrWhiteSpace(name) && !credentialExists(name))
                issues.Add(new(Severity.Error, "Destination password missing", "The saved credential \"" + name + "\" for the " + kind + " folder is not in Windows Credential Manager.", "Set credentials…", Section.Sources));
        if (registration is not null)
            issues.Add(new(Severity.Error, "Schedules are not running", registration.Reason + " Saving registers it.", "Save now", Section.Status));

        foreach (var kind in new[] { "Backup", "Maintenance", "Test" })
        {
            var last = runs.Where(x => x.Kind == kind && x.Status is not "Running").OrderByDescending(x => x.StartedUtc).FirstOrDefault();
            if (last is null || last.Status is not ("Failed" or "Missed")) continue;
            // A cause already reported above (for example the missing restore-test folder) is not repeated.
            var (label, section) = History.FixFor(last);
            if (issues.Any(x => x.Fix == section && section != Section.History && x.Severity == Severity.Error)) continue;
            issues.Add(new(last.Status == "Failed" ? Severity.Error : Severity.Warning,
                last.DisplayKind + (last.Status == "Failed" ? " failed · " : " missed · ") + Formatting.When(last.StartedUtc, now), last.Detail, label, section));
        }
        var lastReport = runs.Where(x => x.Kind != "Maintenance" && x.FinishedUtc.HasValue).OrderByDescending(x => x.StartedUtc).FirstOrDefault();
        if (job.Notification.Enabled && lastReport is { NotificationError.Length: > 0 })
            issues.Add(new(Severity.Warning, "Last report not sent · " + Formatting.When(lastReport.StartedUtc, now), MailErrors.Advice(lastReport.NotificationError), "Open email reports", Section.Email));
        if (job.Notification.Enabled && (string.IsNullOrWhiteSpace(job.Notification.Host) || string.IsNullOrWhiteSpace(job.Notification.From) || string.IsNullOrWhiteSpace(job.Notification.To)))
            issues.Add(new(Severity.Warning, "Email reports incomplete", "Reports are on, but the mail server, sender or recipients are missing.", "Open email reports", Section.Email));

        if (job.BackupSchedule.Enabled)
        {
            var success = runs.Where(x => x.Kind == "Backup" && x.Status is "Success" or "Warning").Select(x => (DateTimeOffset?)x.StartedUtc).Max();
            var period = job.BackupSchedule.Frequency switch { "Weekly" => TimeSpan.FromDays(7), "Monthly" => TimeSpan.FromDays(31), _ => TimeSpan.FromDays(1) };
            if (success is { } at && now - at.ToLocalTime().DateTime > period * 2)
                issues.Add(new(Severity.Warning, "No recent backup", "The last successful database backup was " + Formatting.When(at, now) + ".", "See history", Section.History));
        }
        return issues.OrderBy(x => x.Severity).ToList();
    }
}

internal static class MailErrors
{
    /// <summary>Turns a mail error into its cause and what to check.</summary>
    public static string Advice(string error)
    {
        bool Has(string text) => error.Contains(text, StringComparison.OrdinalIgnoreCase);
        if (Has("timed out") || Has("timeout") || Has("did not respond") || Has("not answer"))
            return "The mail server did not answer (" + error + "). Check the server name and port, or whether a firewall blocks this computer from reaching it.";
        if (Has("authentication") || Has("535") || Has("credential")) return "The mail server rejected the sign-in (" + error + "). Check the account and password with Change….";
        if (Has("certificate") || Has("ssl") || Has("tls")) return "The secure connection failed (" + error + "). Check the Security choice: STARTTLS on 587, SSL/TLS on 465.";
        if (Has("host") || Has("resolve") || Has("no such")) return "The mail server name could not be found (" + error + "). Check the SMTP server name.";
        return error;
    }
}
