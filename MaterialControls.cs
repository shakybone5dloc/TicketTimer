using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace TicketTimer;

public enum ButtonKind { Filled, Tonal, Outlined, Text }

/// <summary>A pill-shaped Material 3 button (filled, tonal, outlined or text).</summary>
public class RoundButton : Button
{
    bool hover, pressed;
    ButtonKind kind = ButtonKind.Filled;

    [DefaultValue(ButtonKind.Filled)]
    public ButtonKind Kind { get => kind; set { kind = value; Invalidate(); } }

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(text.Width + 32, Math.Max(36, text.Height + 16));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        var behind = Parent?.BackColor ?? p.Background;
        g.Clear(behind);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (fill, fore, border) = kind switch
        {
            ButtonKind.Filled => (p.Primary, p.OnPrimary, Color.Empty),
            ButtonKind.Tonal => (p.SecondaryContainer, p.OnSecondaryContainer, Color.Empty),
            ButtonKind.Outlined => (behind, p.Primary, p.Outline),
            _ => (behind, p.Primary, Color.Empty)
        };

        if (!Enabled)
        {
            fore = Theme.Blend(p.OnSurface, behind, 0.38);
            if (kind is ButtonKind.Filled or ButtonKind.Tonal) fill = Theme.Blend(p.OnSurface, behind, 0.12);
            if (kind == ButtonKind.Outlined) border = Theme.Blend(p.OnSurface, behind, 0.12);
        }
        else if (pressed) fill = Theme.Blend(fore, fill, 0.14);
        else if (hover) fill = Theme.Blend(fore, fill, 0.08);

        var rect = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
        using (var path = Theme.RoundedRect(rect, rect.Height / 2))
        {
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            if (border != Color.Empty)
                using (var pen = new Pen(border)) g.DrawPath(pen, path);
            if (Focused && ShowFocusCues)
                using (var pen = new Pen(p.Primary, 2)) g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fore, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>A Material outlined text field: rounded border with the label sitting in a notch on top.</summary>
public class OutlinedField : Panel
{
    readonly Control inner;
    readonly string label;
    Font labelFont;
    int labelHeight;
    bool error;

    [DefaultValue(false)]
    public bool Error { get => error; set { if (error != value) { error = value; Invalidate(); } } }

    public OutlinedField(string label, Control inner)
    {
        this.label = label;
        this.inner = inner;
        DoubleBuffered = true;
        ResizeRedraw = true;
        if (inner is TextBox box) box.BorderStyle = BorderStyle.None;
        inner.Dock = DockStyle.Fill;
        inner.GotFocus += (_, _) => Invalidate();
        inner.LostFocus += (_, _) => Invalidate();
        Controls.Add(inner);
        Click += (_, _) => inner.Focus();
        labelFont = new Font(Font.FontFamily, Font.Size * 0.8f);
        UpdateMetrics();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        labelFont.Dispose();
        labelFont = new Font(Font.FontFamily, Font.Size * 0.8f);
        UpdateMetrics();
    }

    void UpdateMetrics()
    {
        labelHeight = TextRenderer.MeasureText("Ag", labelFont).Height;
        Padding = new Padding(14, labelHeight / 2 + 10, 14, 10);
        Height = Padding.Vertical + inner.PreferredSize.Height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var focused = inner.ContainsFocus;
        var color = error ? p.Error : focused ? p.Primary : p.Outline;
        var width = focused || error ? 2f : 1f;
        var top = labelHeight / 2f;
        var rect = new RectangleF(width / 2, top, Width - width - 0.5f, Height - top - width / 2 - 0.5f);
        using (var path = Theme.RoundedRect(rect, 6))
        using (var pen = new Pen(color, width))
            g.DrawPath(pen, path);

        var text = TextRenderer.MeasureText(label, labelFont);
        var notch = new Rectangle(10, 0, text.Width + 4, labelHeight);
        using (var bg = new SolidBrush(BackColor)) g.FillRectangle(bg, notch);
        TextRenderer.DrawText(g, label, labelFont, notch, focused || error ? color : p.Muted, BackColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) labelFont.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A rounded "surface" card that frames its child.</summary>
public class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Padding = new Padding(6);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 12);
        using (var brush = new SolidBrush(p.Surface)) g.FillPath(brush, path);
        using (var pen = new Pen(p.OutlineVariant)) g.DrawPath(pen, path);
    }
}

/// <summary>A details ListView drawn with theme colors, taller rows and a fill-width column.</summary>
public class ThemedListView : ListView
{
    Font? headerFont;

    /// <summary>Index of the column that stretches to fill the remaining width.</summary>
    [DefaultValue(-1)]
    public int FillColumn { get; set; } = -1;

    public ThemedListView()
    {
        OwnerDraw = true;
        DoubleBuffered = true;
        View = View.Details;
        FullRowSelect = true;
        HideSelection = false;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        UpdateRowHeight();
    }

    void UpdateRowHeight() =>
        SmallImageList = new ImageList { ImageSize = new Size(1, Font.Height + 14) };

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        headerFont?.Dispose();
        headerFont = null;
        UpdateRowHeight();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyNativeTheme(this, Theme.Current);
        FitColumns();
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        FitColumns();
    }

    public void FitColumns()
    {
        if (FillColumn < 0 || FillColumn >= Columns.Count) return;
        var others = Columns.Cast<ColumnHeader>().Where(c => c.Index != FillColumn).Sum(c => c.Width);
        Columns[FillColumn].Width = Math.Max(80, ClientSize.Width - others);
    }

    static TextFormatFlags Align(HorizontalAlignment a) => a switch
    {
        HorizontalAlignment.Right => TextFormatFlags.Right,
        HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
        _ => TextFormatFlags.Left
    } | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

    static Rectangle Inset(Rectangle r) => new(r.X + 8, r.Y, Math.Max(0, r.Width - 16), r.Height);

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        var p = Theme.Current;
        headerFont ??= new Font(Font, FontStyle.Bold);
        using (var brush = new SolidBrush(p.SurfaceVariant)) e.Graphics.FillRectangle(brush, e.Bounds);
        if (e.Header != null)
            TextRenderer.DrawText(e.Graphics, e.Header.Text, headerFont, Inset(e.Bounds), p.Muted, p.SurfaceVariant,
                Align(e.Header.TextAlign));
    }

    // Draw the whole row here; Windows sometimes repaints a row with only this event (e.g. on hover).
    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        var p = Theme.Current;
        var back = e.Item.Selected ? p.PrimaryContainer : p.Surface;
        var fore = e.Item.Selected ? p.OnPrimaryContainer : p.OnSurface;
        var row = new Rectangle(0, e.Bounds.Y, ClientSize.Width, e.Bounds.Height);
        using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, row);

        for (var i = 0; i < e.Item.SubItems.Count && i < Columns.Count; i++)
        {
            var bounds = e.Item.SubItems[i].Bounds;
            if (i == 0) bounds.Width = Columns[0].Width; // sub-item 0 reports the whole row
            TextRenderer.DrawText(e.Graphics, e.Item.SubItems[i].Text, Font, Inset(bounds), fore, back,
                Align(Columns[i].TextAlign));
        }

        using var pen = new Pen(p.OutlineVariant);
        e.Graphics.DrawLine(pen, row.Left, row.Bottom - 1, row.Right, row.Bottom - 1);
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e) => e.DrawDefault = false;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    struct RECT { public int Left, Top, Right, Bottom; }

    // Windows draws faint column lines below the last row; cover them with the surface color.
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg != 0x000F /* WM_PAINT */ || View != View.Details) return;

        int top;
        if (Items.Count > 0)
            top = Items[^1].Bounds.Bottom;
        else
        {
            var header = SendMessage(Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
            top = header != IntPtr.Zero && GetWindowRect(header, out var r) ? r.Bottom - r.Top : 0;
        }
        if (top >= ClientSize.Height) return;
        using var g = CreateGraphics();
        using var brush = new SolidBrush(Theme.Current.Surface);
        g.FillRectangle(brush, 0, Math.Max(0, top), ClientSize.Width, ClientSize.Height - Math.Max(0, top));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) headerFont?.Dispose();
        base.Dispose(disposing);
    }
}
