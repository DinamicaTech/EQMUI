using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;
using DinamicaBackupAgent;

public sealed class ConfigurationTests
{
    [Fact] public Task SaveTracksEditsRevertsFailuresAndSuccessfulWrites() => InSta(() =>
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "backup-save-test-" + Guid.NewGuid().ToString("N"));
        var job = new BackupJob { ProfileDirectory = System.IO.Path.Combine(root, "profile"), Probes = ApplicationDetection.EqmProbes() };
        System.IO.Directory.CreateDirectory(root);
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, ".backup-owner"), job.Id);
        var path = System.IO.Path.Combine(root, "job.json");
        JsonFiles.Write(path, job);
        var window = new AgentWindow(job, path, new FakeTasks());
        try
        {
            var save = Field<Button>(window, "_saveButton");
            var profile = Field<TextBox>(window, "_profileBox");
            Assert.False(save.IsEnabled);
            profile.Text += "-changed"; Assert.True(save.IsEnabled);
            profile.Text = job.ProfileDirectory; Assert.False(save.IsEnabled);
            profile.Text = ""; Assert.True(save.IsEnabled);
            Assert.ThrowsAny<Exception>(() => window.Save());
            Assert.True(save.IsEnabled);
            profile.Text = System.IO.Path.Combine(root, "new-profile");
            Assert.True(window.Save());
            Assert.False(save.IsEnabled);
            Assert.Equal(profile.Text, JsonFiles.Read<BackupJob>(path).ProfileDirectory);
            job.AttachmentNasCredential = "new-destination-credential";
            typeof(AgentWindow).GetMethod("UpdateSaveState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert.True(save.IsEnabled);
            window.Save();
            Assert.False(save.IsEnabled);
        }
        finally { window.Close(); SafePaths.DeleteOwnedDirectory(root, job.Id); }
    });

    [Fact] public Task NewConfigurationNeedsInitialSave() => InSta(() =>
    {
        var window = new AgentWindow(new BackupJob(), System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"), new FakeTasks());
        try { Assert.True(Field<Button>(window, "_saveButton").IsEnabled); }
        finally { window.Close(); }
    });

    [Fact] public Task NavigationShowsTheEightScreensInOrder() => InSta(() =>
    {
        var window = new AgentWindow(new BackupJob(), UnusedPath(), new FakeTasks());
        try
        {
            var nav = Field<StackPanel>(window, "_navPanel").Children.OfType<Button>().ToArray();
            Assert.Equal(new[] { "Status", "Schedule", "Source and destinations", "Restore test", "Email reports", "History", "Restore a backup…", "Advanced" },
                nav.Select(b => ((DockPanel)b.Content).Children.OfType<TextBlock>().Last().Text));
            var content = Field<ContentControl>(window, "_content");
            var pages = Field<Dictionary<Section, FrameworkElement>>(window, "_pages");
            Assert.Same(pages[Section.Status], content.Content);
            foreach (var section in Enum.GetValues<Section>())
            {
                window.Show(section);
                Assert.Same(pages[section], content.Content);
            }
        }
        finally { window.Close(); }
    });

    [Fact] public Task EveryControlHasATooltip() => InSta(() =>
    {
        var window = new AgentWindow(new BackupJob(), UnusedPath(), new FakeTasks());
        try
        {
            var missing = new List<string>();
            foreach (var (section, page) in Field<Dictionary<Section, FrameworkElement>>(window, "_pages"))
            {
                window.Show(section);
                foreach (var control in Descendants(page).OfType<Control>().Where(x => x is System.Windows.Controls.Primitives.ButtonBase or TextBox or ComboBox))
                    if (control.ToolTip is null && control.Visibility == Visibility.Visible)
                        missing.Add(section + ": " + control.GetType().Name + " " + Describe(control));
            }
            Assert.Empty(missing);
        }
        finally { window.Close(); }
    });

    [Fact] public Task NoApplicationSelectorAndQuickMailFolderInSources() => InSta(() =>
    {
        var job = new BackupJob { ApplicationType = "ProConta" };
        var window = new AgentWindow(job, UnusedPath(), new FakeTasks());
        try
        {
            var pages = Field<Dictionary<Section, FrameworkElement>>(window, "_pages");
            Assert.DoesNotContain(pages.Values.SelectMany(Descendants).OfType<ComboBox>(), combo => combo.Items.OfType<object>().Any(x => x.ToString()!.Contains("ProConta")));
            Assert.Contains(Descendants(pages[Section.Sources]).OfType<TextBlock>(), x => x.Text == "Eudora Quick Mail folder");
            Assert.Equal("EQM", job.ApplicationType);
        }
        finally { window.Close(); }
    });

    [Fact] public Task PreserveCustomQueriesAndDefaultQueries() => InSta(() =>
    {
        var custom = new AgentWindow(new BackupJob { Probes = [new("Custom", "SELECT 1")] }, UnusedPath(), new FakeTasks());
        var fresh = new AgentWindow(new BackupJob(), UnusedPath(), new FakeTasks());
        try
        {
            var rows = Field<List<(TextBox Name, TextBox Sql)>>(custom, "_probeRows");
            Assert.Equal(("Custom", "SELECT 1"), (rows.Single().Name.Text, rows.Single().Sql.Text));
            Assert.Contains(Field<List<(TextBox Name, TextBox Sql)>>(fresh, "_probeRows"), x => x.Sql.Text == "SELECT MAX(date_ticks) FROM MessageSummary");
        }
        finally { custom.Close(); fresh.Close(); }
    });

    [Fact] public Task DestinationsHaveBrowseCredentialsAndTestAccess() => InSta(() =>
    {
        var window = new AgentWindow(new BackupJob(), UnusedPath(), new FakeTasks());
        try
        {
            var sources = Field<Dictionary<Section, FrameworkElement>>(window, "_pages")[Section.Sources];
            var labels = Descendants(sources).OfType<Button>().Select(x => x.Content as string).Where(x => x is "Browse…" or "Credentials…" or "Test access").ToArray();
            Assert.Equal(2, labels.Count(x => x == "Credentials…"));
            Assert.Equal(2, labels.Count(x => x == "Test access"));
            Assert.True(labels.Count(x => x == "Browse…") >= 3); // Quick Mail folder plus both destinations
        }
        finally { window.Close(); }
    });

    [Fact] public Task SaveRegistersTheTaskWhenAScheduleIsTurnedOnAndRollsBackOnFailure() => InSta(() =>
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "backup-atomic-test-" + Guid.NewGuid().ToString("N"));
        var job = new BackupJob { ProfileDirectory = System.IO.Path.Combine(root, "profile") };
        System.IO.Directory.CreateDirectory(root);
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, ".backup-owner"), job.Id);
        var path = System.IO.Path.Combine(root, "job.json");
        JsonFiles.Write(path, job);
        var tasks = new FakeTasks { FailRegistration = true };
        var window = new AgentWindow(job, path, tasks) { IdentityPrompt = _ => ("", "") };
        try
        {
            var windowRow = Field<object>(window, "_windowRow");
            var enabled = (CheckBox)windowRow.GetType().GetField("Enabled")!.GetValue(windowRow)!;
            enabled.IsChecked = true;
            var before = System.IO.File.ReadAllText(path);
            Assert.ThrowsAny<Exception>(() => window.Save());
            Assert.Equal(before, System.IO.File.ReadAllText(path));
            Assert.True(Field<Button>(window, "_saveButton").IsEnabled);
            tasks.FailRegistration = false;
            Assert.True(window.Save());
            Assert.Equal(1, tasks.Registrations);
            Assert.True(JsonFiles.Read<BackupJob>(path).MaintenanceSchedule.Enabled);
            Assert.False(Field<Button>(window, "_saveButton").IsEnabled);
        }
        finally { window.Close(); SafePaths.DeleteOwnedDirectory(root, job.Id); }
    });

    [Fact] public Task StatusShowsTheMissingRestoreTestFolderWithItsFix() => InSta(() =>
    {
        var job = new BackupJob { TestSchedule = { Enabled = true } };
        var window = new AgentWindow(job, UnusedPath(), new FakeTasks());
        try
        {
            var status = Field<Dictionary<Section, FrameworkElement>>(window, "_pages")[Section.Status];
            Assert.Contains(Descendants(status).OfType<TextBlock>(), x => x.Text == "Restore test can't run");
            Assert.Contains(Descendants(status).OfType<Button>(), x => x.Content as string == "Choose folder…");
        }
        finally { window.Close(); }
    });

    // ---- helpers

    private static string Describe(Control control) => control switch
    {
        ContentControl { Content: string text } => "\"" + text + "\"",
        ContentControl { Content: TextBlock block } => "\"" + block.Text + "\"",
        TextBox box => "\"" + box.Text + "\"",
        _ => ""
    };
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static string UnusedPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unused-backup-ui-test.json");
    private static T Field<T>(AgentWindow window, string name) => (T)typeof(AgentWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static Task InSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}

internal sealed class FakeTasks : ITaskService
{
    public TaskInfo? Task { get; set; }
    public bool FailRegistration { get; set; }
    public int Registrations { get; private set; }
    public TaskInfo? Inspect(string jobId) => Task;
    public void Register(string jobPath, string jobId, string? user, string? password)
    {
        if (FailRegistration) throw new InvalidOperationException("Windows task registration could not be completed.");
        Registrations++;
        Task = new TaskInfo(true, !string.IsNullOrEmpty(user), Environment.ProcessPath ?? "", SaveCoordinator.ExpectedArguments(jobPath), user ?? "");
    }
}
