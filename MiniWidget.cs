using System.Runtime.InteropServices;

namespace TicketTimer;

/// <summary>What the widget shows; the main window owns the timer and pushes this in.</summary>
public record WidgetState(string Ticket, TimeSpan Elapsed, decimal Earned, bool Running, bool HasSession);

/// <summary>
/// A small borderless, draggable card with the live timer and play/pause and save buttons.
/// It holds no timer state of its own: it raises events and the main window does the work.
/// </summary>
public class MiniWidget : Form
{
    public event Action? ToggleRequested;
    public event Action? SaveRequested;
    public event Action? OpenMainRequested;
    public event Action? OnTopChanged;
    public event Action? SetTimeRequested;

    readonly Label ticketLabel = new()
    {
        Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 9), Margin = new Padding(0)
    };
    readonly Label timeLabel = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = false,
        Font = new Font("Segoe UI Semibold", 22), Margin = new Padding(0), Height = 44
    };
    readonly Label earnedLabel = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft,
        Font = new Font("Segoe UI", 10), Margin = new Padding(2, 0, 0, 0)
    };
    readonly RoundButton playButton;
    readonly RoundButton saveButton;
    readonly ToolStripMenuItem onTopItem = new("Keep on top") { CheckOnClick = true };

    static readonly string IconFont =
        new System.Drawing.Text.InstalledFontCollection().Families.Any(f => f.Name == "Segoe Fluent Icons")
            ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    const string PlayGlyph = "", PauseGlyph = "", SaveGlyph = "", OpenGlyph = "", CloseGlyph = "";

    public MiniWidget(Icon? icon, bool onTop)
    {
        Text = "Ticket Timer";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(320, 112);
        TopMost = onTop;
        Font = new Font("Segoe UI", 10);

        playButton = IconButton(PlayGlyph, 40, ButtonKind.Filled, "Start / pause");
        saveButton = IconButton(SaveGlyph, 40, ButtonKind.Tonal, "Stop and save");
        var openButton = IconButton(OpenGlyph, 28, ButtonKind.Text, "Open Ticket Timer");
        var closeButton = IconButton(CloseGlyph, 28, ButtonKind.Text, "Close widget");
        playButton.Click += (_, _) => ToggleRequested?.Invoke();
        saveButton.Click += (_, _) => SaveRequested?.Invoke();
        openButton.Click += (_, _) => OpenMainRequested?.Invoke();
        closeButton.Click += (_, _) => Close();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(14, 8, 8, 10) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var windowButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Right | AnchorStyles.Top };
        windowButtons.Controls.Add(openButton);
        windowButtons.Controls.Add(closeButton);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 6, 0), Anchor = AnchorStyles.Right };
        playButton.Margin = new Padding(0, 0, 8, 0);
        saveButton.Margin = new Padding(0);
        actions.Controls.Add(playButton);
        actions.Controls.Add(saveButton);

        root.Controls.Add(ticketLabel, 0, 0);
        root.Controls.Add(windowButtons, 1, 0);
        root.Controls.Add(timeLabel, 0, 1);
        root.Controls.Add(actions, 1, 1);
        root.SetRowSpan(actions, 2);
        root.Controls.Add(earnedLabel, 0, 2);
        Controls.Add(root);

        // Right-click menu
        onTopItem.Checked = onTop;
        onTopItem.CheckedChanged += (_, _) => { TopMost = onTopItem.Checked; OnTopChanged?.Invoke(); };
        ContextMenuStrip = new ContextMenuStrip();
        ContextMenuStrip.Items.Add("Open Ticket Timer", null, (_, _) => OpenMainRequested?.Invoke());
        ContextMenuStrip.Items.Add("Set time…", null, (_, _) => SetTimeRequested?.Invoke());
        ContextMenuStrip.Items.Add(onTopItem);
        ContextMenuStrip.Items.Add(new ToolStripSeparator());
        ContextMenuStrip.Items.Add("Close widget", null, (_, _) => Close());

        EnableDragging(this);
        ApplyTheme();
    }

    public bool KeepOnTop => onTopItem.Checked;

    RoundButton IconButton(string glyph, int size, ButtonKind kind, string tip)
    {
        var button = new RoundButton
        {
            Text = glyph, Kind = kind, Size = new Size(size, size), AutoSize = false,
            Font = new Font(IconFont, size >= 40 ? 13 : 9), AccessibleName = tip, Margin = new Padding(0)
        };
        new ToolTip().SetToolTip(button, tip);
        return button;
    }

    // Shown without taking focus away from whatever you're working in
    protected override bool ShowWithoutActivation => true;

    public void ShowState(WidgetState s)
    {
        var p = Theme.Current;
        ticketLabel.Text = string.IsNullOrWhiteSpace(s.Ticket) ? "No ticket" : s.Ticket;
        timeLabel.Text = $"{(int)s.Elapsed.TotalHours:00}:{s.Elapsed.Minutes:00}:{s.Elapsed.Seconds:00}";
        timeLabel.ForeColor = s.Running ? p.Primary : s.HasSession ? p.Warning : p.Muted;
        earnedLabel.Text = s.Earned.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        playButton.Text = s.Running ? PauseGlyph : PlayGlyph;
        playButton.AccessibleName = s.Running ? "Pause" : s.HasSession ? "Resume" : "Start";
        saveButton.Enabled = s.HasSession;
    }

    public void ApplyTheme()
    {
        var p = Theme.Current;
        BackColor = p.Surface;
        foreach (var panel in Controls.OfType<Control>().SelectMany(Descendants).Prepend(this))
            if (panel is Panel) panel.BackColor = p.Surface;
        ticketLabel.ForeColor = p.Muted;
        earnedLabel.ForeColor = p.Success;
        Theme.ApplyPopupChrome(this, p);
        Invalidate(true);
    }

    static IEnumerable<Control> Descendants(Control c) =>
        new[] { c }.Concat(c.Controls.Cast<Control>().SelectMany(Descendants));

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyPopupChrome(this, Theme.Current);
    }

    /// <summary>Puts the widget at a saved spot if it's still on a screen, else bottom-right of the main screen.</summary>
    public void PlaceAt(int? x, int? y)
    {
        if (x is int px && y is int py)
        {
            var rect = new Rectangle(px, py, Width, Height);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect) &&
                                           s.WorkingArea.Contains(new Point(px + Width / 2, py + 10))))
            {
                Location = new Point(px, py);
                return;
            }
        }
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 24, area.Bottom - Height - 24);
    }

    // ---- Drag from anywhere that isn't a button ----

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    void EnableDragging(Control c)
    {
        if (c is Button) return;
        c.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
        };
        foreach (Control child in c.Controls) EnableDragging(child);
        c.ControlAdded += (_, e) => { if (e.Control != null) EnableDragging(e.Control); };
    }
}
