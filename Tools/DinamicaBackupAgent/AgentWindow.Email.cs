using System.Windows;
using System.Windows.Controls;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private TextBox _smtpHost = null!, _smtpPort = null!, _smtpCredential = null!, _from = null!, _recipients = null!;
    private Action<int> _selectSecurity = null!;
    private TextBlock _emailResult = null!;
    private Button _try465 = null!;

    private FrameworkElement BuildEmail()
    {
        var n = _job.Notification;
        var page = new StackPanel();
        var head = Ui.Columns(0, -1);
        Ui.Place(head, Ui.Stack(4, Ui.Title("Email reports"), Ui.Note("After each backup and restore test, a report with its result is emailed.")), 0, 0);
        var enabled = Toggle("Send email reports", () => n.Enabled, x => n.Enabled = x, "Turns email reports on or off. A report that cannot be sent never changes the result of the backup.");
        Ui.Place(head, enabled, 0, 1);
        page.Children.Add(head);
        var banner = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        page.Children.Add(banner);

        var columns = Ui.Columns(0, 0);
        var server = new StackPanel();
        server.Children.Add(Ui.Heading("Mail server"));
        _smtpHost = Field(() => n.Host, x => n.Host = x, 230, "Name of the outgoing mail server, without https:// or a port.");
        _smtpPort = Number(() => n.Port, x => n.Port = x, 52, "Server port: usually 587 with STARTTLS or 465 with SSL/TLS.", "SMTP port", 1, 65535);
        var hostRow = Ui.Row(10, Ui.Stack(0, Ui.Label("SMTP server"), _smtpHost), Ui.Stack(0, Ui.Label("Port"), _smtpPort));
        server.Children.Add(hostRow);
        var security = Segmented(["STARTTLS", "SSL/TLS"], ["Starts unencrypted and switches to TLS, which is required. Usual port 587.", "Encrypted from the start. Usual port 465."],
            () => n.ImplicitTls ? 1 : 0, x => n.ImplicitTls = x == 1);
        _selectSecurity = security.Select;
        Ui.Add(server, Ui.Stack(0, Ui.Label("Security"), security.Panel), 12);
        Ui.Add(server, Ui.Label("Sign-in"), 12);
        _smtpCredential = Field(() => n.Credential, x => n.Credential = x, 200, "Name of the Windows Credential Manager entry with the mail account. Leave it blank only if the server accepts mail without signing in.");
        var signIn = new StackPanel { Orientation = Orientation.Horizontal };
        var change = Ui.Link("Change…", "Saves the account and password used to sign in to the mail server, in Windows Credential Manager.");
        change.Click += (_, _) => Guard(() => { if (_smtpCredential.Text.Trim().Length == 0) _smtpCredential.Text = _job.Id + "-smtp"; StoreCredential(_smtpCredential.Text.Trim()); UpdateSaveState(); });
        server.Children.Add(Ui.Row(8, _smtpCredential, signIn, change));
        var serverCard = Ui.Card(server); serverCard.Margin = new Thickness(0, 0, 6, 12); Ui.Place(columns, serverCard, 0, 0);

        var people = new StackPanel();
        people.Children.Add(Ui.Heading("Sender and recipients"));
        _from = Field(() => n.From, x => n.From = x, 210, "Address the reports are sent from, for example user@example.com. The mail account must be allowed to send as it.");
        var display = Field(() => n.SenderDisplayName, x => n.SenderDisplayName = x, 190, "Name shown as the sender. Leave blank to show only the address.");
        people.Children.Add(Ui.Row(10, Ui.Stack(0, Ui.Label("From"), _from), Ui.Stack(0, Ui.Label("Display name (optional)"), display)));
        _recipients = Field(() => n.To, x => n.To = x, double.NaN, "One or more addresses separated by commas.");
        Ui.Add(people, Ui.Stack(0, Ui.Label("Send reports to"), _recipients), 12);
        var peopleCard = Ui.Card(people); peopleCard.Margin = new Thickness(6, 0, 0, 12); Ui.Place(columns, peopleCard, 0, 1);
        page.Children.Add(columns);

        var testPanel = new DockPanel { LastChildFill = true };
        var test = Ui.Button("Send test email", "Sends a test email to the recipients with the settings on this screen, without saving them. No backup runs.", Ui.Kind.Primary);
        test.Click += (_, _) => TestEmail();
        _try465 = Ui.Link("Try 465 with SSL/TLS", "Switches to port 465 with SSL/TLS and sends the test again. Save to keep it.");
        _try465.Visibility = Visibility.Collapsed; _try465.Margin = new Thickness(12, 0, 0, 0);
        _try465.Click += (_, _) => { _smtpPort.Text = "465"; _selectSecurity(1); TestEmail(); };
        _emailResult = Ui.Text("", Ui.Muted); _emailResult.Margin = new Thickness(12, 0, 0, 0);
        DockPanel.SetDock(test, Dock.Left); testPanel.Children.Add(test);
        DockPanel.SetDock(_try465, Dock.Right); testPanel.Children.Add(_try465);
        testPanel.Children.Add(_emailResult);
        page.Children.Add(Ui.Card(testPanel, 14));

        void Refresh()
        {
            signIn.Children.Clear();
            var name = _smtpCredential.Text.Trim();
            signIn.Children.Add(name.Length == 0 ? Ui.Pill("No sign-in", Ui.Disabled, Ui.Muted) : CredentialExists(name) ? Ui.Pill("Password saved", Ui.GreenBack, Ui.Green) : Ui.Pill("Password missing", Ui.RedBack, Ui.RedInk));
        }
        _onEdit.Add(Refresh);
        _onShow[Section.Email] = () =>
        {
            Refresh();
            banner.Children.Clear();
            var last = _runs.Where(x => x.Kind != "Maintenance" && x.FinishedUtc.HasValue).OrderByDescending(x => x.StartedUtc).FirstOrDefault();
            if (n.Enabled && last is { NotificationError.Length: > 0 })
                banner.Children.Add(Ui.Banner(Severity.Warning, "Last report not sent · " + Formatting.When(last.StartedUtc, DateTime.Now), MailErrors.Advice(last.NotificationError), null));
        };
        return page;
    }
}
