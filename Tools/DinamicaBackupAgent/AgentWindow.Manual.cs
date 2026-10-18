using System.Diagnostics;
using System.IO;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private async void StartManual(string kind)
    {
        if (_operation != null) return;
        ManualCommands? store = null;
        ManualCommand? request = null;
        try
        {
            if (!Save()) return;
            if (kind != "Backup")
            {
                var destination = kind == "TestAttachmentAccess" ? _job.AttachmentDestination : _job.Destination;
                if (ApplicationDetection.UncShare(destination).Length == 0)
                    throw new ArgumentException("Test access checks a NAS folder: enter a UNC folder such as \\\\nas\\backup\\folder first.");
            }
            // No duplicate manual operation while a scheduled/local run owns the profile.
            using (ProfileLease.LockAgent(_job.ProfileDirectory)) { }
            store = new ManualCommands(_jobPath, _job.Id);
            store.RecoverAbandoned();
            _operation = new CancellationTokenSource();
            SetBusy(true); _progress.IsIndeterminate = true;
            var queue = store;
            await Task.Run(() => WindowsScheduler.RunManual(_jobPath, _job.Id,
                () => request = queue.Submit(kind, ManualCommands.Hash(_jobPath))));
            SetStatus("Request sent to Windows Task Scheduler. Waiting for it to start…");
            var cancelSent = false;
            while (true)
            {
                await Task.Delay(300); // cancellation requests stop the worker, not result/cleanup monitoring
                try
                {
                    if (_operation.IsCancellationRequested && !cancelSent)
                    { store.Cancel(request!.Id); cancelSent = true; }
                    store.RecoverAbandoned();
                    var status = store.ReadStatus(request!.Id);
                    if (status?.Id != request.Id) throw new InvalidOperationException("Manual request status is unavailable.");
                    SetStatus((cancelSent && !status.Finished ? "Cancelling safely… " : "") + status.Detail);
                    _progress.IsIndeterminate = !status.Finished && status.Total == 0;
                    _progress.Maximum = Math.Max(1, status.Total); _progress.Value = status.Completed;
                    if (!status.Finished) continue;
                    if (status.ReopenApplication)
                    {
                        try
                        {
                            var start = new ProcessStartInfo(_job.ApplicationPath) { UseShellExecute = true,
                                WorkingDirectory = Path.GetDirectoryName(_job.ApplicationPath)! };
                            foreach (var argument in _job.ApplicationArguments) start.ArgumentList.Add(argument);
                            Process.Start(start);
                        }
                        catch (Exception ex) { SetStatus(_status.Text + " Quick Mail could not be reopened: " + ex.Message, Ui.AmberInk); }
                    }
                    break;
                }
                catch (IOException) { /* writer briefly holds the status lock; retry */ }
            }
        }
        catch (Exception ex)
        {
            if (request != null && store != null)
            {
                try { store.Cancel(request.Id); } catch (IOException) { }
            }
            SetStatus("Request failed: " + ex.Message, Ui.RedInk);
        }
        finally
        {
            var final = _status.Text;
            _operation?.Dispose(); _operation = null; SetBusy(false);
            SetStatus(final, _status.Foreground);
        }
    }
}
