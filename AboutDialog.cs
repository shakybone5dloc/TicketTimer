using System.Diagnostics;

namespace TicketTimer;

public static class AppInfo
{
    public const string Name = "Ticket Timer";
    public const string SupportUrl = "https://ko-fi.com/shakybone5dloc";
    public const string RepoUrl = "https://github.com/shakybone5dloc/TicketTimer";

    /// <summary>Version from the project file, without any "+commit" suffix.</summary>
    public static string Version => Application.ProductVersion.Split('+')[0];

    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't open {url}\n\n{ex.Message}", Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}

/// <summary>Small About box: what the app is, its version, and an optional tip link.</summary>
public class AboutDialog : Form
{
    public AboutDialog(Icon? icon)
    {
        var p = Theme.Current;
        Text = $"About {AppInfo.Name}";
        Icon = icon;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = p.Background;
        ForeColor = p.OnSurface;

        var picture = new PictureBox
        {
            Image = icon != null ? new Icon(icon, 64, 64).ToBitmap() : null,
            SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64), Margin = new Padding(0, 4, 16, 0)
        };
        var title = new Label { Text = AppInfo.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 16), Margin = new Padding(0) };
        var version = new Label { Text = $"Version {AppInfo.Version}", AutoSize = true, ForeColor = p.Muted, Margin = new Padding(2, 0, 0, 10) };
        var blurb = new Label
        {
            Text = "Track time and pay for the tickets you work on.\n" +
                   "Free to use, with no features behind a paywall.\n\n" +
                   "If it saves you some time and you'd like to say thanks,\n" +
                   "you can buy me a coffee. It's appreciated, never expected.",
            AutoSize = true, Margin = new Padding(2, 0, 0, 14)
        };

        var coffee = new RoundButton { Text = "☕  Buy me a coffee", AutoSize = true, Kind = ButtonKind.Filled, Margin = new Padding(0, 0, 6, 0) };
        var github = new RoundButton { Text = "GitHub", AutoSize = true, Kind = ButtonKind.Outlined, Margin = new Padding(0, 0, 6, 0) };
        var close = new RoundButton { Text = "Close", AutoSize = true, Kind = ButtonKind.Text, Margin = Padding.Empty };
        coffee.Click += (_, _) => AppInfo.Open(AppInfo.SupportUrl);
        github.Click += (_, _) => AppInfo.Open(AppInfo.RepoUrl);
        close.Click += (_, _) => Close();
        CancelButton = close;

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = p.Background };
        buttons.Controls.AddRange(new Control[] { coffee, github, close });

        var text = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty, BackColor = p.Background };
        text.Controls.AddRange(new Control[] { title, version, blurb, buttons });

        var root = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(20, 18, 22, 18), BackColor = p.Background };
        root.Controls.Add(picture);
        root.Controls.Add(text);
        Controls.Add(root);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.ApplyTitleBar(this, Theme.Current);
    }
}
