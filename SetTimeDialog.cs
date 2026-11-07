using System.Globalization;
using System.Text.RegularExpressions;

namespace TicketTimer;

/// <summary>
/// Lets you say when you really started ("9:15 am") or how long you've already worked ("1h 30m").
/// On OK, <see cref="Elapsed"/> is the time the clock should show and <see cref="Started"/> when that began.
/// </summary>
public class SetTimeDialog : Form
{
    readonly RoundButton startedTab = new() { Text = "Started at", AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
    readonly RoundButton workedTab = new() { Text = "Already worked", AutoSize = true, Margin = Padding.Empty };
    readonly TextBox input = new();
    readonly OutlinedField field;
    readonly Label hint = new() { AutoSize = true, Margin = new Padding(3, 4, 3, 0) };
    readonly Label preview = new() { AutoSize = true, Margin = new Padding(3, 10, 3, 4), Font = new Font("Segoe UI Semibold", 11) };
    readonly RoundButton okButton = new() { Text = "Set time", AutoSize = true, Kind = ButtonKind.Filled, Margin = new Padding(6, 0, 0, 0) };
    readonly RoundButton cancelButton = new() { Text = "Cancel", AutoSize = true, Kind = ButtonKind.Text, Margin = Padding.Empty };
    bool startedMode = true;
    readonly TimeSpan current;

    public TimeSpan Elapsed { get; private set; }
    public DateTime Started { get; private set; }

    public SetTimeDialog(TimeSpan currentElapsed, Icon? icon)
    {
        current = currentElapsed;
        Text = "Set time";
        Icon = icon;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        field = new OutlinedField("Time", input) { Width = 430, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 14, 0, 0) };

        var tabs = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        tabs.Controls.Add(startedTab);
        tabs.Controls.Add(workedTab);

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        var root = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(18, 16, 18, 14), Dock = DockStyle.Fill };
        root.Controls.Add(tabs);
        root.Controls.Add(field);
        root.Controls.Add(hint);
        root.Controls.Add(preview);
        root.Controls.Add(buttons);
        Controls.Add(root);

        startedTab.Click += (_, _) => SetMode(true);
        workedTab.Click += (_, _) => SetMode(false);
        input.TextChanged += (_, _) => UpdatePreview();
        okButton.Click += (_, _) => { if (UpdatePreview()) { DialogResult = DialogResult.OK; Close(); } };
        cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        AcceptButton = okButton;
        CancelButton = cancelButton;

        ApplyTheme();
        SetMode(true);
    }

    void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Background;
        ForeColor = p.OnSurface;
        foreach (var c in Descendants(this))
        {
            if (c is Panel) c.BackColor = p.Background;
            if (c is TextBox t) { t.BackColor = p.Background; t.ForeColor = p.OnSurface; }
        }
        hint.ForeColor = p.Muted;
    }

    static IEnumerable<Control> Descendants(Control c) =>
        c.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this, Theme.Current);
    }

    void SetMode(bool started)
    {
        startedMode = started;
        startedTab.Kind = started ? ButtonKind.Tonal : ButtonKind.Text;
        workedTab.Kind = started ? ButtonKind.Text : ButtonKind.Tonal;
        if (started)
        {
            input.Text = (DateTime.Now - current).ToString("h:mm tt");
            hint.Text = "When you began, e.g. 9:15, 9:15 am, 14:30";
        }
        else
        {
            input.Text = $"{(int)current.TotalHours}:{current.Minutes:00}";
            hint.Text = "How long so far, e.g. 1:30, 90m, 1.5h, 1h 30m, or 45 (minutes)";
        }
        input.Focus();
        input.SelectAll();
        UpdatePreview();
    }

    /// <summary>Validates the input, shows what the clock will read, and returns whether it's usable.</summary>
    bool UpdatePreview()
    {
        var p = Theme.Current;
        var now = DateTime.Now;
        string? error = null;

        if (startedMode)
        {
            if (TryParseStart(input.Text, now, out var start))
            {
                Started = start;
                Elapsed = now - start;
                if (Elapsed > TimeSpan.FromDays(7)) error = "That's more than a week ago.";
            }
            else error = "Couldn't read that time.";
        }
        else
        {
            if (TryParseDuration(input.Text, out var worked))
            {
                Elapsed = worked;
                Started = now - worked;
                if (worked > TimeSpan.FromDays(7)) error = "That's more than a week.";
            }
            else error = "Couldn't read that length of time.";
        }

        field.Error = error != null;
        okButton.Enabled = error == null;
        preview.ForeColor = error != null ? p.Error : p.Primary;
        preview.Text = error ?? $"Clock will show {(int)Elapsed.TotalHours:00}:{Elapsed.Minutes:00}:{Elapsed.Seconds:00}" +
                                $"   (since {Started:ddd h:mm tt})";
        return error == null;
    }

    /// <summary>A clock time today, or yesterday if that time hasn't happened yet today.</summary>
    public static bool TryParseStart(string text, DateTime now, out DateTime start)
    {
        start = default;
        text = text.Trim();
        if (text.Length == 0) return false;
        // "9" or "915" style inputs aren't times; require a colon or am/pm
        if (!text.Contains(':') && !Regex.IsMatch(text, @"[ap]\.?m?\.?$", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(text, @"^\d{1,2}\s*[ap]\.?m?\.?$", RegexOptions.IgnoreCase))  // "9am"
            text = Regex.Replace(text, @"^(\d{1,2})\s*", "$1:00 ");
        if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var parsed))
            return false;
        start = now.Date + parsed.TimeOfDay;
        if (parsed.Date != DateTime.MinValue.Date) start = parsed; // a full date was typed
        else if (start > now) start = start.AddDays(-1);         // e.g. 11 pm typed just after midnight
        return start <= now;
    }

    /// <summary>"1:30", "1:30:15", "90m", "1.5h", "1h 30m", "2h", "45" (minutes).</summary>
    public static bool TryParseDuration(string text, out TimeSpan duration)
    {
        duration = default;
        text = text.Trim().ToLowerInvariant();
        if (text.Length == 0) return false;

        var clock = Regex.Match(text, @"^(\d+):([0-5]?\d)(?::([0-5]?\d))?$");
        if (clock.Success)
        {
            duration = new TimeSpan(int.Parse(clock.Groups[1].Value), int.Parse(clock.Groups[2].Value),
                clock.Groups[3].Success ? int.Parse(clock.Groups[3].Value) : 0);
            return true;
        }
        if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var minutes) && minutes >= 0)
        {
            duration = TimeSpan.FromMinutes(minutes);
            return true;
        }
        var parts = Regex.Matches(text, @"(\d+(?:\.\d+)?)\s*(h|hr|hrs|hours?|m|min|mins|minutes?|s|sec|secs|seconds?)\b");
        if (parts.Count == 0 || Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*[a-z]+|\s|,|and", "").Length > 0) return false;
        double total = 0;
        foreach (Match m in parts)
        {
            var n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            total += m.Groups[2].Value[0] switch { 'h' => n * 3600, 'm' => n * 60, _ => n };
        }
        duration = TimeSpan.FromSeconds(Math.Round(total));
        return true;
    }
}
