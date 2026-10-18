using DinamicaBackup;
using DinamicaBackupAgent;

public sealed class InsightsTests
{
    [Fact] public void NextOccurrenceFollowsTheEngineRules()
    {
        var now = new DateTime(2026, 10, 18, 10, 0, 0); // Sunday
        Assert.Equal(new DateTime(2026, 10, 19, 0, 1, 0), ScheduleText.Next(new Schedule { Enabled = true, Time = "00:01" }, now));
        Assert.Equal(new DateTime(2026, 10, 18, 23, 0, 0), ScheduleText.Next(new Schedule { Enabled = true, Time = "23:00" }, now));
        Assert.Equal(new DateTime(2026, 10, 25, 4, 0, 0), ScheduleText.Next(new Schedule { Enabled = true, Frequency = "Weekly", Day = DayOfWeek.Sunday, Time = "04:00" }, now));
        Assert.Equal(new DateTime(2026, 11, 30, 2, 0, 0), ScheduleText.Next(new Schedule { Enabled = true, Frequency = "Monthly", DayOfMonth = 31, Time = "02:00" }, new DateTime(2026, 11, 1)));
        Assert.Null(ScheduleText.Next(new Schedule { Enabled = false }, now));
        Assert.Null(ScheduleText.Next(new Schedule { Enabled = true, Time = "25:00" }, now));
    }

    [Fact] public void ConflictsOnlyWhenABackupFallsInsideTheWindow()
    {
        var window = new Schedule { Enabled = true, Time = "23:00" };
        Assert.Single(ScheduleText.Conflicts(new Schedule { Enabled = true, Time = "00:30" }, window, 120, new Schedule()));
        Assert.Empty(ScheduleText.Conflicts(new Schedule { Enabled = true, Time = "01:30" }, window, 120, new Schedule()));
        var weekly = new Schedule { Enabled = true, Frequency = "Weekly", Day = DayOfWeek.Monday, Time = "23:30" };
        var sundayWindow = new Schedule { Enabled = true, Frequency = "Weekly", Day = DayOfWeek.Sunday, Time = "23:00" };
        Assert.Empty(ScheduleText.Conflicts(weekly, sundayWindow, 120, new Schedule()));
        Assert.Single(ScheduleText.Conflicts(new Schedule(), window, 120, new Schedule { Enabled = true, Time = "23:15" }));
    }

    [Fact] public void RetentionSummaryAndTiers()
    {
        Assert.Equal("Keeps up to 23 backups; the oldest is about 12 months old.", Retention.Summary(7, 4, 12));
        Assert.Equal("Keeps up to 7 backups; the oldest is about 7 days old.", Retention.Summary(7, 0, 0));
        var day = new DateTimeOffset(2026, 10, 18, 0, 1, 0, TimeSpan.Zero);
        var tiers = Retention.Tiers([("a", day), ("b", day.AddDays(-1)), ("c", day.AddDays(-40))], 1, 0, 2);
        Assert.Equal("Daily", tiers["a"]);
        Assert.False(tiers.ContainsKey("b"));
        Assert.Equal("Monthly", tiers["c"]);
    }

    [Fact] public void HistoryIncludesTheLastRunEvenWithoutAReceipt()
    {
        var receipt = new RunResult { Kind = "Backup", Status = "Success", StartedUtc = DateTimeOffset.UtcNow.AddHours(-5) };
        var last = new RunResult { Kind = "Test", Status = "Failed", Detail = "Choose a restore-test directory first.", StartedUtc = DateTimeOffset.UtcNow };
        var runs = History.Merge([receipt], last);
        Assert.Equal(new[] { last.Id, receipt.Id }, runs.Select(x => x.Id));
        Assert.Equal(new[] { last.Id }, History.Filter(runs, HistoryFilter.All, true).Select(x => x.Id));
        Assert.Equal(new[] { receipt.Id }, History.Filter(runs, HistoryFilter.Backups, false).Select(x => x.Id));
        Assert.Equal(("Choose folder…", Section.RestoreTest), History.FixFor(last));
        Assert.Contains("\"Restore test\"", History.Csv(runs));
    }

    [Fact] public void StatusExplainsEachProblemOnceWithItsFix()
    {
        var job = new BackupJob { TestSchedule = { Enabled = true }, Destination = @"\\nas\backup", Notification = { Enabled = true, Host = "smtp.example.com", From = "backup@example.com", To = "user@example.com" } };
        var failedTest = new RunResult { Kind = "Test", Status = "Failed", Detail = "Choose a restore-test directory first.", FinishedUtc = DateTimeOffset.UtcNow, NotificationError = "The operation has timed out." };
        var issues = StatusAdvisor.Issues(job, [failedTest], null, _ => true, DateTime.Now);
        Assert.Single(issues, x => x.Fix == Section.RestoreTest);
        Assert.Contains(issues, x => x.Fix == Section.Email && x.Cause.Contains("timed out"));
        Assert.All(issues, x => { Assert.NotEmpty(x.Cause); Assert.NotEmpty(x.FixLabel); });
        Assert.Contains(StatusAdvisor.Issues(job, [], new RegistrationNeed("The Windows task that runs the schedules does not exist yet."), _ => true, DateTime.Now), x => x.FixLabel == "Save now");
        job.NasCredential = "nas";
        Assert.Contains(StatusAdvisor.Issues(job, [], null, _ => false, DateTime.Now), x => x.Title == "Destination password missing");
    }

    [Fact] public void EmailAdviceNamesTheCause()
    {
        Assert.StartsWith("The mail server did not answer", MailErrors.Advice("The operation has timed out."));
        Assert.StartsWith("The mail server rejected the sign-in", MailErrors.Advice("535: 5.7.8 Authentication failed"));
    }
}
