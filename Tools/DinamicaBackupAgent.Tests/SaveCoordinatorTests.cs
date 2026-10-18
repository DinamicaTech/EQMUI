using DinamicaBackup;
using DinamicaBackupAgent;

public sealed class SaveCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "backup-save-coordinator-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly BackupJob _job;
    private readonly FakeTasks _tasks = new();
    private readonly SaveCoordinator _saver;

    public SaveCoordinatorTests()
    {
        Directory.CreateDirectory(_root);
        _job = new BackupJob { ProfileDirectory = Path.Combine(_root, "profile") };
        File.WriteAllText(Path.Combine(_root, ".backup-owner"), _job.Id);
        _path = Path.Combine(_root, "job.json");
        _saver = new SaveCoordinator(_tasks, Environment.ProcessPath ?? "");
    }
    public void Dispose() => SafePaths.DeleteOwnedDirectory(_root, _job.Id);

    [Fact] public void WithoutSchedulesItOnlyWritesTheFile()
    {
        var asked = false;
        Assert.False(_saver.Save(_job, _path, _ => { asked = true; return ("", ""); }));
        Assert.False(asked);
        Assert.Equal(0, _tasks.Registrations);
        Assert.True(File.Exists(_path));
    }

    [Fact] public void ATurnedOnScheduleRegistersTheTaskOnceAndThenItIsCurrent()
    {
        _job.MaintenanceSchedule.Enabled = true;
        string? reason = null;
        Assert.True(_saver.Save(_job, _path, r => { reason = r; return ("", ""); }));
        Assert.Contains("does not exist", reason);
        Assert.Equal(1, _tasks.Registrations);
        Assert.Null(_saver.Need(_job, _path));
        Assert.False(_saver.Save(_job, _path, _ => throw new InvalidOperationException("must not ask")));
        Assert.Equal(1, _tasks.Registrations);
    }

    [Fact] public void ATaskForAnotherConfigurationNeedsRegistering()
    {
        _job.TestSchedule.Enabled = false; _job.BackupSchedule.Enabled = false; _job.MaintenanceSchedule.Enabled = true;
        _tasks.Task = new TaskInfo(true, true, Environment.ProcessPath ?? "", SaveCoordinator.ExpectedArguments(Path.Combine(_root, "other.json")), "user");
        Assert.Contains("another", _saver.Need(_job, _path)!.Reason);
        _tasks.Task = _tasks.Task with { Arguments = SaveCoordinator.ExpectedArguments(_path), Enabled = false };
        Assert.Contains("disabled", _saver.Need(_job, _path)!.Reason);
    }

    [Fact] public void CancellingTheAccountWritesNothing()
    {
        JsonFiles.Write(_path, _job);
        var before = File.ReadAllText(_path);
        _job.MaintenanceSchedule.Enabled = true; _job.Name = "Changed";
        Assert.Throws<OperationCanceledException>(() => _saver.Save(_job, _path, _ => null));
        Assert.Equal(before, File.ReadAllText(_path));
    }

    [Fact] public void AFailedRegistrationRestoresThePreviousFile()
    {
        JsonFiles.Write(_path, _job);
        var before = File.ReadAllText(_path);
        _job.MaintenanceSchedule.Enabled = true; _job.Name = "Changed";
        _tasks.FailRegistration = true;
        Assert.Throws<InvalidOperationException>(() => _saver.Save(_job, _path, _ => ("", "")));
        Assert.Equal(before, File.ReadAllText(_path));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
    }

    [Fact] public void AFailedFirstRegistrationLeavesNoFile()
    {
        _job.MaintenanceSchedule.Enabled = true;
        _tasks.FailRegistration = true;
        Assert.Throws<InvalidOperationException>(() => _saver.Save(_job, _path, _ => ("", "")));
        Assert.False(File.Exists(_path));
    }

    [Fact] public void InvalidSettingsFailBeforeAskingOrWriting()
    {
        _job.MaintenanceSchedule.Enabled = true; _job.MaintenanceSchedule.Time = "25:00";
        Assert.Throws<InvalidDataException>(() => _saver.Save(_job, _path, _ => throw new InvalidOperationException("must not ask")));
        Assert.False(File.Exists(_path));
    }
}
