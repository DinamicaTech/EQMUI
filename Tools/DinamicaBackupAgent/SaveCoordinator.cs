using System.IO;
using DinamicaBackup;

namespace DinamicaBackupAgent;

/// <summary>State of the Windows task that runs the schedules, as read from Task Scheduler.</summary>
internal sealed record TaskInfo(bool Enabled, bool PasswordLogon, string ActionPath, string Arguments, string UserId);

internal interface ITaskService
{
    /// <summary>Returns null when the task does not exist.</summary>
    TaskInfo? Inspect(string jobId);
    void Register(string jobPath, string jobId, string? user, string? password);
}

internal sealed class WindowsTaskService : ITaskService
{
    public TaskInfo? Inspect(string jobId) => WindowsScheduler.Inspect(jobId);
    public void Register(string jobPath, string jobId, string? user, string? password) => WindowsScheduler.Register(jobPath, jobId, user, password);
}

/// <summary>Why the Windows task must be (re)registered before the schedules can run; null when it is current.</summary>
internal sealed record RegistrationNeed(string Reason);

/// <summary>
/// Save is one step: it writes the configuration and, only when needed, registers or updates the Windows task.
/// If any part fails, the configuration file and the task are left as they were.
/// </summary>
internal sealed class SaveCoordinator(ITaskService tasks, string agentPath)
{
    public static bool AnyScheduleEnabled(BackupJob job) => job.BackupSchedule.Enabled || job.MaintenanceSchedule.Enabled || job.TestSchedule.Enabled;
    public static string ExpectedArguments(string jobPath) => "/scheduled /job \"" + Path.GetFullPath(jobPath).Replace("\"", "") + "\"";

    public RegistrationNeed? Need(BackupJob job, string jobPath)
    {
        if (!AnyScheduleEnabled(job)) return null;
        TaskInfo? task;
        try { task = tasks.Inspect(job.Id); }
        catch (Exception ex) { return new("Windows Task Scheduler could not be read (" + ex.Message + ")."); }
        if (task is null) return new("The Windows task that runs the schedules does not exist yet.");
        if (!string.Equals(task.ActionPath, agentPath, StringComparison.OrdinalIgnoreCase) || !string.Equals(task.Arguments, ExpectedArguments(jobPath), StringComparison.OrdinalIgnoreCase))
            return new("The Windows task runs another copy of the agent or another configuration file.");
        if (!task.Enabled) return new("The Windows task is disabled.");
        return null;
    }

    /// <summary>
    /// Validates and writes the job. When the task needs registering, asks for the Windows identity first;
    /// returning null from <paramref name="askIdentity"/> cancels and nothing is written.
    /// </summary>
    public bool Save(BackupJob job, string jobPath, Func<string, (string User, string Password)?> askIdentity, bool forceRegistration = false)
    {
        SafePaths.Validate(job, job.BackupSchedule.Enabled || job.TestSchedule.Enabled);
        var need = forceRegistration ? new RegistrationNeed("You asked to register the Windows task again.") : Need(job, jobPath);
        (string User, string Password)? identity = null;
        if (need is not null)
        {
            identity = askIdentity(need.Reason);
            if (identity is null) throw new OperationCanceledException("Nothing was saved: the Windows account for the task was not confirmed.");
        }
        var previous = File.Exists(jobPath) ? File.ReadAllBytes(jobPath) : null;
        JsonFiles.Write(jobPath, job);
        if (need is null) return false;
        try { tasks.Register(jobPath, job.Id, identity!.Value.User, identity.Value.Password); }
        catch
        {
            Restore(jobPath, previous);
            throw;
        }
        return true;
    }

    private static void Restore(string jobPath, byte[]? previous)
    {
        if (previous is null) { File.Delete(jobPath); return; }
        var temporary = jobPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, previous);
        File.Move(temporary, jobPath, true);
    }
}
