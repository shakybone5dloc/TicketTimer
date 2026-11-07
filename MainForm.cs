using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace TicketTimer;

public class MainForm : Form
{
    readonly Store store = Store.Load();
    readonly Stopwatch stopwatch = new();
    readonly System.Windows.Forms.Timer tick = new() { Interval = 250 };
    DateTime? sessionStarted;
    TimeEntry? continuing;            // the logged entry being resumed, if any
    TimeSpan baseTime;                // time already on the clock before the stopwatch (resumed entry or "Set time")

    /// <summary>Time shown on the clock: the starting amount plus the time counted now.</summary>
    TimeSpan Elapsed => baseTime + stopwatch.Elapsed;

    /// <summary>Time that would be lost by discarding: all of it, or for a resumed entry what's beyond its saved time.</summary>
    TimeSpan UnsavedTime
    {
        get
        {
            var unsaved = continuing != null ? Elapsed - continuing.Duration : Elapsed;
            return unsaved < TimeSpan.Zero ? TimeSpan.Zero : unsaved;
        }
    }

    readonly TextBox ticketBox = new() { PlaceholderText = "e.g. JIRA-1234 Fix login bug" };
    readonly TextBox rateBox = new() { TextAlign = HorizontalAlignment.Right };
    readonly OutlinedField ticketField;
    readonly OutlinedField rateField;
    readonly Label timeLabel = new()
    {
        Text = "00:00:00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI Semibold", 36)
    };
    readonly Label earnedLabel = new()
    {
        Text = "$0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 16)
    };
    readonly RoundButton startButton = new() { Text = "Start", Height = 44, Dock = DockStyle.Fill, Kind = ButtonKind.Filled };
    readonly RoundButton saveButton = new() { Text = "Stop && Save", Height = 44, Dock = DockStyle.Fill, Kind = ButtonKind.Tonal, Enabled = false };
    readonly RoundButton discardButton = new() { Text = "Discard", Height = 44, Dock = DockStyle.Fill, Kind = ButtonKind.Outlined, Enabled = false };
    readonly ThemedListView log = new() { Dock = DockStyle.Fill, FillColumn = 1 };
    readonly Label logHeader = new()
    {
        Text = "Logged time  ·  double-click a row to continue its timer, Delete removes it",
        AutoSize = true, Margin = new Padding(3, 14, 3, 6)
    };
    readonly Label totalsLabel = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    readonly RoundButton onTopButton = new() { Text = "Keep on top", AutoSize = true, Kind = ButtonKind.Outlined };
    readonly Dictionary<string, RoundButton> themeButtons = new();
    readonly RoundButton setTimeButton = new() { Text = "Set time…", AutoSize = true, Kind = ButtonKind.Text };
    readonly RoundButton widgetButton =new() { Text = "Mini widget", AutoSize = true, Kind = ButtonKind.Outlined };
    MiniWidget? widget;
    bool exiting;

    readonly NotifyIcon tray = new() { Text = "Ticket Timer" };
    readonly ToolStripMenuItem trayWidgetItem = new("Mini widget");
    readonly ToolStripMenuItem trayStartItem = new("Start");
    readonly ToolStripMenuItem traySaveItem = new("Stop && Save");

    public MainForm()
    {
        Text = "Ticket Timer";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(660, 660);
        MinimumSize = new Size(520, 540);
        StartPosition = FormStartPosition.CenterScreen;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("TicketTimer.app.ico"))
            if (iconStream != null) Icon = new Icon(iconStream);

        Theme.Current = Theme.Resolve(store.ThemeMode);
        ticketField = new OutlinedField("Ticket", ticketBox) { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        rateField = new OutlinedField("Rate ($/hr)", rateBox) { Anchor = AnchorStyles.Left | AnchorStyles.Right };

        BuildLayout();

        ticketBox.Text = store.LastTicket;
        rateBox.Text = store.LastRate.ToString("0.00");

        tick.Tick += (_, _) => RefreshTimer();
        startButton.Click += (_, _) => ToggleRunning();
        saveButton.Click += (_, _) => SaveSession();
        discardButton.Click += (_, _) => DiscardSession();
        rateBox.TextChanged += (_, _) => RefreshTimer();
        rateBox.Leave += (_, _) => { if (TryGetRate(out var r)) rateBox.Text = r.ToString("0.00"); };
        ticketBox.TextChanged += (_, _) => { RefreshTotals(); RefreshTimer(); };
        onTopButton.Click += (_, _) =>
        {
            TopMost = !TopMost;
            onTopButton.Kind = TopMost ? ButtonKind.Tonal : ButtonKind.Outlined;
        };
        log.DoubleClick += (_, _) => ContinueSelected();
        log.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        widgetButton.Click += (_, _) => ToggleWidget();
        setTimeButton.Click += (_, _) => SetTime();
        BuildTray();

        ApplyTheme();
        RefreshLog();
        RefreshTimer();
    }

    void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 14), ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // top bar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // inputs
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // time
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // earned
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // buttons
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // log header
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // log
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // footer

        // Top bar: keep-on-top toggle and the System / Light / Dark switch
        var themeSwitch = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false, Margin = Padding.Empty };
        foreach (var mode in new[] { Theme.System, Theme.Light, Theme.Dark })
        {
            var button = new RoundButton { Text = mode, AutoSize = true, Margin = new Padding(2, 0, 0, 0) };
            button.Click += (_, _) => SetThemeMode(mode);
            themeButtons[mode] = button;
            themeSwitch.Controls.Add(button);
        }
        var topBar = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 8) };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var leftButtons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Left, WrapContents = false, Margin = Padding.Empty };
        onTopButton.Margin = new Padding(0, 0, 6, 0);
        widgetButton.Margin = Padding.Empty;
        leftButtons.Controls.Add(onTopButton);
        leftButtons.Controls.Add(widgetButton);
        topBar.Controls.Add(leftButtons, 0, 0);
        topBar.Controls.Add(themeSwitch, 1, 0);

        var inputs = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        inputs.Controls.Add(ticketField, 0, 0);
        inputs.Controls.Add(rateField, 1, 0);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        for (var i = 0; i < 3; i++) buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        buttons.Controls.Add(startButton, 0, 0);
        buttons.Controls.Add(saveButton, 1, 0);
        buttons.Controls.Add(discardButton, 2, 0);

        log.Columns.Add("Date", 168);
        log.Columns.Add("Ticket", 200);
        log.Columns.Add("Time", 84, HorizontalAlignment.Right);
        log.Columns.Add("Rate", 74, HorizontalAlignment.Right);
        log.Columns.Add("Earned", 86, HorizontalAlignment.Right);
        var logCard = new Card { Dock = DockStyle.Fill };
        logCard.Controls.Add(log);

        var deleteButton = new RoundButton { Text = "Delete", AutoSize = true, Kind = ButtonKind.Text };
        deleteButton.Click += (_, _) => DeleteSelected();
        var exportButton = new RoundButton { Text = "Export CSV…", AutoSize = true, Kind = ButtonKind.Outlined };
        exportButton.Click += (_, _) => ExportCsv();

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 8, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(totalsLabel, 0, 0);
        footer.Controls.Add(deleteButton, 1, 0);
        footer.Controls.Add(exportButton, 2, 0);

        root.Controls.Add(topBar, 0, 0);
        root.Controls.Add(inputs, 0, 1);
        root.Controls.Add(timeLabel, 0, 2);
        // Earned stays centered; "Set time…" sits to its right
        var earnedRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        earnedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        earnedRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        earnedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        earnedLabel.Dock = DockStyle.None;
        earnedLabel.AutoSize = true;
        earnedLabel.Anchor = AnchorStyles.None;
        setTimeButton.Anchor = AnchorStyles.Left;
        setTimeButton.Margin = new Padding(12, 0, 0, 0);
        earnedRow.Controls.Add(earnedLabel, 1, 0);
        earnedRow.Controls.Add(setTimeButton, 2, 0);
        root.Controls.Add(earnedRow, 0, 3);
        root.Controls.Add(buttons, 0, 4);
        root.Controls.Add(logHeader, 0, 5);
        root.Controls.Add(logCard, 0, 6);
        root.Controls.Add(footer, 0, 7);
        Controls.Add(root);
        AcceptButton = startButton;
    }

    // ---------- Theme ----------

    void SetThemeMode(string mode)
    {
        store.ThemeMode = mode;
        ApplyTheme();
        Persist();
    }

    void OnSystemPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (store.ThemeMode == Theme.System && e.Category == UserPreferenceCategory.General)
            BeginInvoke(ApplyTheme);
    }

    void ApplyTheme()
    {
        var p = Theme.Current = Theme.Resolve(store.ThemeMode);
        SuspendLayout();
        BackColor = p.Background;
        ForeColor = p.OnSurface;
        ApplyColors(this, p);
        logHeader.ForeColor = p.Muted;
        earnedLabel.ForeColor = p.Success;
        foreach (var (mode, button) in themeButtons)
            button.Kind = mode == store.ThemeMode ? ButtonKind.Tonal : ButtonKind.Text;
        ResumeLayout();

        Theme.ApplyTitleBar(this, p);
        Theme.ApplyNativeTheme(log, p);
        widget?.ApplyTheme();
        RefreshTimer();
        Invalidate(true);
    }

    static void ApplyColors(Control parent, Palette p)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case ListView list: list.BackColor = p.Surface; list.ForeColor = p.OnSurface; break;
                case TextBox box: box.BackColor = p.Background; box.ForeColor = p.OnSurface; break;
                case Label label: label.ForeColor = p.OnSurface; break;
                case Panel panel: panel.BackColor = p.Background; break;
            }
            ApplyColors(c, p);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this, Theme.Current);
    }

    // ---------- Timer ----------

    bool HasSession => sessionStarted != null;

    bool TryGetRate(out decimal rate) =>
        decimal.TryParse(rateBox.Text.Trim().TrimStart('$'), NumberStyles.Number, CultureInfo.CurrentCulture, out rate)
        && rate >= 0;

    bool CheckInputs()
    {
        if (string.IsNullOrWhiteSpace(ticketBox.Text))
        {
            MessageBox.Show("Give the ticket a name first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            ticketBox.Focus();
            return false;
        }
        if (!TryGetRate(out _))
        {
            MessageBox.Show("The hourly rate needs to be a number, like 45 or 62.50.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            rateBox.Focus();
            return false;
        }
        return true;
    }

    void ToggleRunning()
    {
        if (stopwatch.IsRunning)
        {
            stopwatch.Stop();
            tick.Stop();
        }
        else
        {
            if (!CheckInputs()) return;
            sessionStarted ??= DateTime.Now;
            stopwatch.Start();
            tick.Start();
        }
        RefreshTimer();
    }

    void SaveSession()
    {
        if (!HasSession || !CheckInputs()) return;
        stopwatch.Stop();
        TryGetRate(out var rate);
        if (continuing != null && store.Entries.Contains(continuing))
        {
            // Resumed entry: update it in place with the new total
            continuing.Ticket = ticketBox.Text.Trim();
            continuing.Seconds = Math.Round(Elapsed.TotalSeconds);
            continuing.HourlyRate = rate;
        }
        else
        {
            store.Entries.Add(new TimeEntry
            {
                Ticket = ticketBox.Text.Trim(),
                Started = sessionStarted!.Value,
                Seconds = Math.Round(Elapsed.TotalSeconds),
                HourlyRate = rate
            });
        }
        ResetSession();
        Persist();
        RefreshLog();
    }

    void DiscardSession()
    {
        if (!HasSession) return;
        var wasRunning = stopwatch.IsRunning;
        stopwatch.Stop();
        var question = continuing != null
            ? $"Throw away the {Format(UnsavedTime)} added since you resumed? The entry keeps its {Format(continuing.Duration)}."
            : $"Throw away {Format(Elapsed)} without saving?";
        if (MessageBox.Show(question, Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            ResetSession();
        else if (wasRunning)
            stopwatch.Start();
        RefreshTimer();
    }

    /// <summary>
    /// "Set time…": say when you really started or how long you've worked. With no timer going it
    /// starts one from that point; a running timer keeps running from the new time; a paused one stays paused.
    /// </summary>
    void SetTime()
    {
        if (!HasSession && !CheckInputs()) return;
        using var dialog = new SetTimeDialog(Elapsed, Icon);
        var owner = widget is { Visible: true } && WindowState == FormWindowState.Minimized ? (IWin32Window)widget : this;
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;

        var start = !HasSession || stopwatch.IsRunning;
        stopwatch.Reset();
        baseTime = dialog.Elapsed;
        if (continuing == null) sessionStarted = dialog.Started; // a resumed entry keeps its original date
        if (start) { stopwatch.Start(); tick.Start(); }
        RefreshTimer();
    }

    void ResetSession()
    {
        tick.Stop();
        stopwatch.Reset();
        sessionStarted = null;
        continuing = null;
        baseTime = TimeSpan.Zero;
        RefreshTimer();
    }

    void RefreshTimer()
    {
        var p = Theme.Current;
        var rateOk = TryGetRate(out var rate);
        rateField.Error = !rateOk;

        var elapsed = Elapsed;
        timeLabel.Text = Format(elapsed);
        earnedLabel.Text = Money((decimal)elapsed.TotalHours * (rateOk ? rate : 0));
        timeLabel.ForeColor = stopwatch.IsRunning ? p.Primary : HasSession ? p.Warning : p.Muted;

        startButton.Text = stopwatch.IsRunning ? "Pause" : HasSession ? "Resume" : "Start";
        saveButton.Enabled = discardButton.Enabled = HasSession;
        Text = HasSession ? $"{Format(elapsed)} – {ticketBox.Text.Trim()} – Ticket Timer" : "Ticket Timer";

        var earned = (decimal)elapsed.TotalHours * (rateOk ? rate : 0);
        widget?.ShowState(new WidgetState(ticketBox.Text.Trim(), elapsed, earned, stopwatch.IsRunning, HasSession));
        trayStartItem.Text = startButton.Text;
        traySaveItem.Enabled = HasSession;
        var tip = HasSession ? $"{Format(elapsed)} – {ticketBox.Text.Trim()}" : "Ticket Timer";
        tray.Text = tip.Length > 120 ? tip[..120] : tip;
    }

    // ---------- Mini widget and tray ----------

    void ToggleWidget()
    {
        if (widget != null) { widget.Close(); return; }
        OpenWidget();
        WindowState = FormWindowState.Minimized;
    }

    void OpenWidget()
    {
        if (widget != null) return;
        widget = new MiniWidget(Icon, store.WidgetOnTop);
        widget.ToggleRequested += ToggleRunning;
        widget.SaveRequested += SaveSession;
        widget.OpenMainRequested += ShowMain;
        widget.SetTimeRequested += SetTime;
        widget.OnTopChanged += () => { store.WidgetOnTop = widget?.KeepOnTop ?? true; Persist(); };
        widget.LocationChanged += (_, _) =>
        {
            if (widget is { Visible: true }) { store.WidgetX = widget.Left; store.WidgetY = widget.Top; }
        };
        widget.FormClosed += (_, _) =>
        {
            widget = null;
            widgetButton.Kind = ButtonKind.Outlined;
            trayWidgetItem.Checked = false;
            if (!exiting) { store.WidgetOpen = false; Persist(); }
        };
        widget.PlaceAt(store.WidgetX, store.WidgetY);
        widget.Show();
        store.WidgetOpen = true;
        widgetButton.Kind = ButtonKind.Tonal;
        trayWidgetItem.Checked = true;
        RefreshTimer();
        Persist();
    }

    void ShowMain()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void BuildTray()
    {
        tray.Icon = Icon;
        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open Ticket Timer", null, (_, _) => ShowMain()) { Font = new Font(menu.Font, FontStyle.Bold) };
        trayWidgetItem.Click += (_, _) => { if (widget != null) widget.Close(); else OpenWidget(); };
        trayStartItem.Click += (_, _) => ToggleRunning();
        traySaveItem.Click += (_, _) => SaveSession();
        menu.Items.AddRange(new ToolStripItem[]
        {
            open, trayWidgetItem, new ToolStripSeparator(), trayStartItem, traySaveItem,
            new ToolStripSeparator(), new ToolStripMenuItem("Exit", null, (_, _) => { ShowMain(); Close(); })
        });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowMain();
        tray.Visible = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (store.WidgetOpen) OpenWidget();
    }

    // ---------- Log ----------

    void RefreshLog()
    {
        log.BeginUpdate();
        log.Items.Clear();
        foreach (var e in store.Entries.OrderByDescending(e => e.Started))
        {
            var item = new ListViewItem(e.Started.ToString("ddd MMM d, h:mm tt")) { Tag = e };
            item.SubItems.Add(e.Ticket);
            item.SubItems.Add(Format(e.Duration));
            item.SubItems.Add(Money(e.HourlyRate));
            item.SubItems.Add(Money(e.Earned));
            log.Items.Add(item);
        }
        log.EndUpdate();
        log.FitColumns();
        RefreshTotals();
    }

    void RefreshTotals()
    {
        var name = ticketBox.Text.Trim();
        var forTicket = store.Entries.Where(e => string.Equals(e.Ticket, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var all = $"All: {Format(Sum(store.Entries))} · {Money(store.Entries.Sum(e => e.Earned))}";
        totalsLabel.Text = forTicket.Count == 0 ? all
            : $"This ticket: {Format(Sum(forTicket))} · {Money(forTicket.Sum(e => e.Earned))}    {all}";
    }

    /// <summary>
    /// Double-click on a log row: resume that entry. The clock starts from its saved time,
    /// and Stop &amp; Save updates the same row instead of adding a new one.
    /// </summary>
    void ContinueSelected()
    {
        if (log.SelectedItems.Count == 0 || log.SelectedItems[0].Tag is not TimeEntry e) return;

        if (HasSession)
        {
            if (ReferenceEquals(continuing, e))
            {
                if (!stopwatch.IsRunning) ToggleRunning(); // already resumed and paused → keep going
                return;
            }
            var answer = MessageBox.Show(
                $"You're timing \"{ticketBox.Text.Trim()}\" ({Format(Elapsed)}).\n\n" +
                $"Save that time and continue \"{e.Ticket}\" from {Format(e.Duration)}?",
                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK) return;
            SaveSession();
            if (HasSession) return; // save was blocked (bad ticket name or rate)
        }

        ticketBox.Text = e.Ticket;
        rateBox.Text = e.HourlyRate.ToString("0.00");
        continuing = e;
        baseTime = e.Duration;
        sessionStarted = e.Started;
        ToggleRunning();
        if (!stopwatch.IsRunning) ResetSession(); // couldn't start (bad input) → don't leave it half-resumed
    }

    void DeleteSelected()
    {
        var picked = log.SelectedItems.Cast<ListViewItem>().Select(i => (TimeEntry)i.Tag!).ToList();
        if (picked.Count == 0) return;
        if (continuing != null && picked.Contains(continuing))
        {
            MessageBox.Show("That entry is being timed right now. Save or discard the timer first.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var what = picked.Count == 1 ? "this entry" : $"these {picked.Count} entries";
        if (MessageBox.Show($"Delete {what}?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        foreach (var e in picked) store.Entries.Remove(e);
        Persist();
        RefreshLog();
    }

    void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv", FileName = $"ticket-time-{DateTime.Now:yyyy-MM-dd}.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var csv = new StringBuilder("Started,Ticket,Hours,Rate,Earned\r\n");
        foreach (var e in store.Entries.OrderBy(e => e.Started))
            csv.Append(CultureInfo.InvariantCulture,
                $"{e.Started:yyyy-MM-dd HH:mm},\"{e.Ticket.Replace("\"", "\"\"")}\",{e.Duration.TotalHours:0.00},{e.HourlyRate:0.00},{e.Earned:0.00}\r\n");
        File.WriteAllText(dialog.FileName, csv.ToString());
    }

    void Persist()
    {
        store.LastTicket = ticketBox.Text.Trim();
        if (TryGetRate(out var rate)) store.LastRate = rate;
        try { store.Save(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't save:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (HasSession)
        {
            var answer = MessageBox.Show($"You have {Format(UnsavedTime)} not saved on \"{ticketBox.Text.Trim()}\". Save it before closing?",
                Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
            if (answer == DialogResult.Yes) SaveSession();
            if (HasSession) { e.Cancel = true; return; } // save was blocked (bad ticket name or rate)
        }
        Persist();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        exiting = true;   // keep "widget open" saved so it comes back next launch
        widget?.Close();
        Persist();        // saves the widget's last position
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosed(e);
    }

    static TimeSpan Sum(IEnumerable<TimeEntry> entries) => TimeSpan.FromSeconds(entries.Sum(e => e.Seconds));
    static string Format(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    static string Money(decimal amount) => amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
}
