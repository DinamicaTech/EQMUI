using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DinamicaBackup;

namespace DinamicaBackupAgent;

public sealed partial class AgentWindow
{
    private ScheduleRow _backupRow = null!, _windowRow = null!, _testRow = null!;
    private TextBox _windowMinutes = null!;

    /// <summary>The controls of one task in the Schedule table.</summary>
    private sealed class ScheduleRow
    {
        public CheckBox Enabled = null!;
        public ComboBox Frequency = null!, Weekday = null!;
        public TextBox Time = null!, DayOfMonth = null!;
        public CheckBox CatchUp = null!;
        public TextBlock NoDay = null!;
        public Schedule Preview()
        {
            int.TryParse(DayOfMonth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var day);
            return new Schedule
            {
                Enabled = Enabled.IsChecked == true, Frequency = Frequency.SelectedItem?.ToString() ?? "Daily", Time = Time.Text.Trim(),
                Day = Weekday.SelectedItem is string name && Enum.TryParse<DayOfWeek>(name, out var weekday) ? weekday : DayOfWeek.Sunday,
                DayOfMonth = Math.Clamp(day == 0 ? 1 : day, 1, 31), CatchUp = CatchUp.IsChecked == true
            };
        }
        public void ShowDay()
        {
            var frequency = Frequency.SelectedItem?.ToString();
            Weekday.Visibility = frequency == "Weekly" ? Visibility.Visible : Visibility.Collapsed;
            DayOfMonth.Visibility = frequency == "Monthly" ? Visibility.Visible : Visibility.Collapsed;
            NoDay.Visibility = frequency is "Weekly" or "Monthly" ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private FrameworkElement BuildSchedule()
    {
        var page = new StackPanel();
        page.Children.Add(Ui.Title("Schedule"));
        Ui.Add(page, Ui.Note("When each task runs, in local time. The Windows task checks every 5 minutes, so a new time applies without registering anything again."), 4);

        // Columns: task, frequency, time, day, length, if the computer was off.
        double[] widths = [230, 118, 76, 132, 120, 0];
        var table = new StackPanel();
        var head = Ui.Columns(widths); head.Background = Ui.NavBack;
        string[] titles = ["Task", "Frequency", "Time", "Day", "Length", "If the computer was off"];
        for (var i = 0; i < titles.Length; i++) { var t = Ui.Label(titles[i]); t.Margin = new Thickness(i == 0 ? 16 : 0, 8, 8, 8); Ui.Place(head, t, 0, i); }
        table.Children.Add(head);

        _backupRow = ScheduleLine(table, widths, "Database backup", "Full ZIP backup of the database and profile files, plus the attachment mirror. Quick Mail is closed while it runs.", _job.BackupSchedule, null,
            "Runs a missed backup as soon as the computer is back on.");
        _windowMinutes = Number(() => _job.MaintenanceMinutes, x => _job.MaintenanceMinutes = x, 52, "How long Quick Mail stays closed so an external backup program can copy its files. The agent cannot tell when that program finishes.", "Window length", 1, 1440);
        _windowRow = ScheduleLine(table, widths, "External backup window", "Closes Quick Mail for a while so an external backup program can copy its files. It does not run or check that program.", _job.MaintenanceSchedule, _windowMinutes,
            "Opens a missed window when the computer is back on, if the window has not ended yet. An ended window is never replayed.");
        _testRow = ScheduleLine(table, widths, "Restore test", "Restores a backup into the restore-test folder and checks that it opens and matches. The live data is never touched.", _job.TestSchedule, null,
            "Runs a missed test as soon as the computer is back on.");
        page.Children.Add(WithTop(Ui.Card(table, 0), 16));

        var timeline = new StackPanel();
        timeline.Children.Add(Ui.Heading("A day at a glance"));
        var canvas = new Canvas { Height = 46, ClipToBounds = true };
        var frame = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Background = Ui.NavBack, Child = canvas };
        timeline.Children.Add(frame);
        var hours = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        for (var i = 0; i < 8; i++) hours.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 8; i++) { var label = Ui.Label((i * 3).ToString("00", CultureInfo.InvariantCulture) + ":00"); Grid.SetColumn(label, i); hours.Children.Add(label); }
        timeline.Children.Add(hours);
        var legend = Ui.Row(16, Legend(Ui.Accent, "Database backup"), Legend(Ui.Amber, "Quick Mail closed for the external window"), Legend(Ui.Green, "Restore test"));
        legend.Margin = new Thickness(0, 6, 0, 0); timeline.Children.Add(legend);
        var conflicts = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        timeline.Children.Add(conflicts);
        page.Children.Add(Ui.Card(timeline));

        void Redraw()
        {
            canvas.Children.Clear();
            var width = canvas.ActualWidth;
            if (width <= 0) return;
            var backup = _backupRow.Preview(); var window = _windowRow.Preview(); var test = _testRow.Preview();
            int.TryParse(_windowMinutes.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes);
            for (var h = 3; h < 24; h += 3) canvas.Children.Add(new Line { X1 = width * h / 24, X2 = width * h / 24, Y1 = 0, Y2 = 46, Stroke = Ui.SoftLine });
            if (window.Enabled && minutes > 0)
            {
                var start = ScheduleText.Hour(window); var length = minutes / 60.0;
                void Segment(double from, double hoursLong)
                {
                    var rect = new Rectangle { Width = Math.Max(2, width * hoursLong / 24), Height = 22, Fill = Ui.AmberBack, Stroke = Ui.Amber, RadiusX = 3, RadiusY = 3 };
                    Canvas.SetLeft(rect, width * from / 24); Canvas.SetTop(rect, 12); canvas.Children.Add(rect);
                }
                if (start + length <= 24) Segment(start, length);
                else { Segment(start, 24 - start); Segment(0, start + length - 24); }
            }
            void Marker(Schedule s, Brush color, double top)
            {
                if (!s.Enabled) return;
                var x = width * ScheduleText.Hour(s) / 24;
                var mark = new Rectangle { Width = 4, Height = 30, Fill = color, RadiusX = 2, RadiusY = 2 };
                Canvas.SetLeft(mark, Math.Min(width - 4, x)); Canvas.SetTop(mark, top); canvas.Children.Add(mark);
            }
            Marker(backup, Ui.Accent, 8); Marker(test, Ui.Green, 8);
            conflicts.Children.Clear();
            foreach (var text in ScheduleText.Conflicts(backup, window, minutes, test)) conflicts.Children.Add(Ui.Banner(Severity.Warning, "Tasks overlap", text, null));
            if (!backup.Enabled && !window.Enabled && !test.Enabled) conflicts.Children.Add(Ui.Note("Every task is off: nothing will run on its own."));
        }
        canvas.SizeChanged += (_, _) => Redraw();
        _onEdit.Add(Redraw);
        return page;
    }

    private static FrameworkElement WithTop(FrameworkElement element, double top) { element.Margin = new Thickness(element.Margin.Left, top, element.Margin.Right, element.Margin.Bottom); return element; }

    private static StackPanel Legend(Brush color, string text)
    {
        var swatch = new Rectangle { Width = 12, Height = 12, Fill = color, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Center };
        return Ui.Row(6, swatch, Ui.Label(text));
    }

    private ScheduleRow ScheduleLine(StackPanel table, double[] widths, string name, string tip, Schedule schedule, TextBox? length, string catchUpTip)
    {
        var row = new ScheduleRow();
        var grid = Ui.Columns(widths);
        grid.Margin = new Thickness(0);
        var line = new Border { BorderBrush = Ui.SoftLine, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 10), Child = grid };
        row.Enabled = Toggle(name, () => schedule.Enabled, x => schedule.Enabled = x, tip);
        row.Enabled.FontWeight = FontWeights.SemiBold; row.Enabled.Margin = new Thickness(16, 0, 8, 0);
        Ui.Place(grid, row.Enabled, 0, 0);
        row.Frequency = Pick(["Daily", "Weekly", "Monthly"], () => schedule.Frequency, x => schedule.Frequency = x, 104, "Every day, once a week on the chosen weekday, or once a month on the chosen day.");
        Ui.Place(grid, row.Frequency, 0, 1);
        row.Time = Field(() => schedule.Time, x =>
        {
            if (!TimeOnly.TryParseExact(x, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new FormatException(name + ": enter the time as HH:mm in 24-hour format, for example 02:30.");
            schedule.Time = x;
        }, 62, "Local time in 24-hour format, for example 02:30. It may start up to 5 minutes later.");
        row.Time.TextAlignment = TextAlignment.Center;
        Ui.Place(grid, row.Time, 0, 2);
        row.Weekday = Pick(Enum.GetNames<DayOfWeek>(), () => schedule.Day.ToString(), x => schedule.Day = Enum.Parse<DayOfWeek>(x), 118, "Weekday it runs on.");
        row.DayOfMonth = Number(() => schedule.DayOfMonth, x => schedule.DayOfMonth = x, 44, "Day of the month, 1 to 31. Shorter months use their last day.", name + " day of month", 1, 31);
        row.DayOfMonth.HorizontalAlignment = HorizontalAlignment.Left;
        row.NoDay = Ui.Text("Every day", Ui.Muted);
        var day = new Grid(); day.Children.Add(row.Weekday); day.Children.Add(row.DayOfMonth); day.Children.Add(row.NoDay);
        Ui.Place(grid, day, 0, 3);
        if (length is not null) Ui.Place(grid, Ui.Row(6, length, Ui.Text("min", Ui.Muted)), 0, 4);
        else Ui.Place(grid, Ui.Text("–", Ui.Faint), 0, 4);
        row.CatchUp = Toggle("Run it when the computer is back on", () => schedule.CatchUp, x => schedule.CatchUp = x, catchUpTip);
        row.CatchUp.Margin = new Thickness(0, 0, 16, 0);
        Ui.Place(grid, row.CatchUp, 0, 5);
        row.Frequency.SelectionChanged += (_, _) => row.ShowDay();
        row.ShowDay();
        table.Children.Add(line);
        return row;
    }
}
