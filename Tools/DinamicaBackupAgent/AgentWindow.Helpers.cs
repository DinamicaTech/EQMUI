using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private Window Dialog(string title, double width)
        => new() { Title = title, Owner = IsLoaded ? this : null, Width = width, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, FontFamily = FontFamily, FontSize = 13 };

    private void EditNasCredentials(bool attachments)
    {
        var existingShare = attachments ? _job.AttachmentNasShare : _job.NasShare;
        var existingName = attachments ? _job.AttachmentNasCredential : _job.NasCredential;
        var destination = (attachments ? _attachmentDestination : _destination).Text.Trim();
        var dialog = Dialog(attachments ? "Attachments folder credentials" : "Database backups folder credentials", 560);
        var panel = new StackPanel { Margin = new Thickness(18) }; dialog.Content = panel;
        panel.Children.Add(Ui.Note("Optional, for a NAS. The password is kept in Windows Credential Manager, never in the configuration file. Windows may refuse two different users for shares on the same server; existing connections are left as they are."));
        TextBox Box(string label, string initial, string help)
        {
            Ui.Add(panel, Ui.Label(label), 12);
            var box = Ui.Box(double.NaN, help); box.Text = initial; panel.Children.Add(box); return box;
        }
        var share = Box("UNC share", string.IsNullOrEmpty(existingShare) ? ApplicationDetection.UncShare(destination) : existingShare, @"Root of the share, for example \\nas\backup.");
        var name = Box("Credential name", string.IsNullOrEmpty(existingName) ? _job.Id + (attachments ? "-attachments" : "-database") : existingName, "Unique name for this saved credential. Clear it to use your Windows account instead.");
        var user = Box("Username", "", "NAS username, optionally DOMAIN\\user. Leave username and password blank to keep the saved credential.");
        Ui.Add(panel, Ui.Label("Password"), 12);
        var password = new PasswordBox { Padding = new Thickness(6, 4, 6, 4), BorderBrush = Ui.FieldLine }; Ui.Tip(password, "Kept for this Windows user in Credential Manager. Never written to the configuration file."); panel.Children.Add(password);
        var apply = Ui.Button("Apply", "Uses these settings for this folder. Save keeps them in the configuration.", Ui.Kind.Primary);
        var cancel = Ui.Button("Cancel", "Leaves the credentials as they were.");
        var buttons = Ui.Row(8, cancel, apply); buttons.HorizontalAlignment = HorizontalAlignment.Right; Ui.Add(panel, buttons, 16);
        cancel.Click += (_, _) => dialog.DialogResult = false;
        apply.Click += (_, _) =>
        {
            try
            {
                var credential = name.Text.Trim(); var unc = share.Text.Trim();
                if (credential.Length > 0)
                {
                    if (ApplicationDetection.UncShare(unc) != unc.TrimEnd('\\')) throw new ArgumentException("Enter the root of a UNC share, for example \\\\nas\\backup.");
                    if (user.Text.Length > 0) Secrets.Save(credential, user.Text.Trim(), password.Password);
                    else { if (password.Password.Length > 0) throw new ArgumentException("Enter the username together with the password."); _ = Secrets.Read(credential); }
                }
                if (attachments) { _job.AttachmentNasShare = unc; _job.AttachmentNasCredential = credential; }
                else { _job.NasShare = unc; _job.NasCredential = credential; }
                password.Clear(); dialog.DialogResult = true;
                UpdateSaveState(); UpdateMarkers();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Folder credentials", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        dialog.ShowDialog();
    }

    private void StoreCredential(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a credential name first.");
        var dialog = Dialog("Mail server sign-in: " + name, 520);
        var panel = new StackPanel { Margin = new Thickness(18) }; dialog.Content = panel;
        panel.Children.Add(Ui.Note("The account and password are kept in Windows Credential Manager for this Windows user, never in the configuration file."));
        Ui.Add(panel, Ui.Label("Username"), 12); var user = Ui.Box(double.NaN, "Mail account username, usually the full address."); panel.Children.Add(user);
        Ui.Add(panel, Ui.Label("Password"), 12);
        var password = new PasswordBox { Padding = new Thickness(6, 4, 6, 4), BorderBrush = Ui.FieldLine }; Ui.Tip(password, "Mail account password or app password."); panel.Children.Add(password);
        var save = Ui.Button("Save", "Saves the sign-in in Windows Credential Manager.", Ui.Kind.Primary);
        var cancel = Ui.Button("Cancel", "Leaves the sign-in as it was.");
        var buttons = Ui.Row(8, cancel, save); buttons.HorizontalAlignment = HorizontalAlignment.Right; Ui.Add(panel, buttons, 16);
        cancel.Click += (_, _) => dialog.DialogResult = false;
        save.Click += (_, _) =>
        {
            try { Secrets.Save(name, user.Text.Trim(), password.Password); password.Clear(); dialog.DialogResult = true; }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Mail server sign-in", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        dialog.ShowDialog();
    }

    private async void TestEmail()
    {
        if (_operation != null) return;
        _try465.Visibility = Visibility.Collapsed;
        try
        {
            Commit();
            var n = _job.Notification;
            if (string.IsNullOrWhiteSpace(n.Host) || string.IsNullOrWhiteSpace(n.From) || string.IsNullOrWhiteSpace(n.To) || n.Port is < 1 or > 65535)
                throw new InvalidOperationException("Enter the mail server, a valid port, the sender and the recipients first.");
            _operation = new CancellationTokenSource(TimeSpan.FromSeconds(60)); SetBusy(true); _progress.IsIndeterminate = true;
            _emailResult.Text = "Sending…"; _emailResult.Foreground = Ui.Muted;
            SetStatus("Sending a test email… No backup is running.");
            await Program.CreateRunner(_job).SendTestEmailAsync(_operation.Token);
            _emailResult.Text = "Sent " + DateTime.Now.ToString("HH:mm") + " · accepted by " + n.Host + " for " + n.To + "."; _emailResult.Foreground = Ui.Green;
        }
        catch (OperationCanceledException)
        {
            _emailResult.Text = "Failed " + DateTime.Now.ToString("HH:mm") + " · no answer from " + _job.Notification.Host + ":" + _job.Notification.Port + " after 60 s."; _emailResult.Foreground = Ui.RedInk;
            _try465.Visibility = _job.Notification.Port == 465 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            _emailResult.Text = "Failed " + DateTime.Now.ToString("HH:mm") + " · " + MailErrors.Advice(ex.Message); _emailResult.Foreground = Ui.RedInk;
            _try465.Visibility = _job.Notification.Port != 465 && MailErrors.Advice(ex.Message).StartsWith("The mail server did not answer", StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _operation?.Dispose(); _operation = null; SetBusy(false); }
    }
}
