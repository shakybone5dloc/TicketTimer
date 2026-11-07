using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TicketTimer;

/// <summary>Material 3 color roles for one theme.</summary>
public record Palette(
    bool IsDark,
    Color Background, Color Surface, Color SurfaceVariant,
    Color OnSurface, Color Muted, Color Outline, Color OutlineVariant,
    Color Primary, Color OnPrimary, Color PrimaryContainer, Color OnPrimaryContainer,
    Color SecondaryContainer, Color OnSecondaryContainer,
    Color Success, Color Warning, Color Error);

public static class Theme
{
    public const string System = "System", Light = "Light", Dark = "Dark";

    static Color Hex(int rgb) => Color.FromArgb(255, Color.FromArgb(rgb));

    public static readonly Palette LightPalette = new(false,
        Background: Hex(0xFEF7FF), Surface: Hex(0xFFFFFF), SurfaceVariant: Hex(0xF3EDF7),
        OnSurface: Hex(0x1D1B20), Muted: Hex(0x49454F), Outline: Hex(0x79747E), OutlineVariant: Hex(0xCAC4D0),
        Primary: Hex(0x6750A4), OnPrimary: Hex(0xFFFFFF), PrimaryContainer: Hex(0xEADDFF), OnPrimaryContainer: Hex(0x21005D),
        SecondaryContainer: Hex(0xE8DEF8), OnSecondaryContainer: Hex(0x1D192B),
        Success: Hex(0x2E7D32), Warning: Hex(0xB25E00), Error: Hex(0xB3261E));

    public static readonly Palette DarkPalette = new(true,
        Background: Hex(0x141218), Surface: Hex(0x1D1B20), SurfaceVariant: Hex(0x2B2930),
        OnSurface: Hex(0xE6E0E9), Muted: Hex(0xCAC4D0), Outline: Hex(0x938F99), OutlineVariant: Hex(0x49454F),
        Primary: Hex(0xD0BCFF), OnPrimary: Hex(0x381E72), PrimaryContainer: Hex(0x4F378B), OnPrimaryContainer: Hex(0xEADDFF),
        SecondaryContainer: Hex(0x4A4458), OnSecondaryContainer: Hex(0xE8DEF8),
        Success: Hex(0x9CD67D), Warning: Hex(0xFFB870), Error: Hex(0xF2B8B5));

    public static Palette Current { get; set; } = LightPalette;

    public static Palette Resolve(string mode) => mode switch
    {
        Dark => DarkPalette,
        Light => LightPalette,
        _ => SystemPrefersDark() ? DarkPalette : LightPalette
    };

    public static bool SystemPrefersDark()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1) is int v && v == 0;
        }
        catch { return false; }
    }

    /// <summary>Mixes <paramref name="top"/> over <paramref name="bottom"/> at the given opacity.</summary>
    public static Color Blend(Color top, Color bottom, double amount) => Color.FromArgb(
        (int)(top.R * amount + bottom.R * (1 - amount)),
        (int)(top.G * amount + bottom.G * (1 - amount)),
        (int)(top.B * amount + bottom.B * (1 - amount)));

    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        if (d <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---- Native bits: title bar and Windows-drawn parts (scrollbars, list header) ----

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    public static void ApplyTitleBar(Form form, Palette p)
    {
        if (!form.IsHandleCreated) return;
        var dark = p.IsDark ? 1 : 0;
        var caption = ColorRef(p.Background);
        var text = ColorRef(p.OnSurface);
        DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));    // immersive dark mode
        DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int)); // caption color (Windows 11)
        DwmSetWindowAttribute(form.Handle, 36, ref text, sizeof(int));    // caption text color (Windows 11)
    }

    /// <summary>Rounded corners and a theme-colored border for a borderless window (Windows 11).</summary>
    public static void ApplyPopupChrome(Form form, Palette p)
    {
        if (!form.IsHandleCreated) return;
        var round = 2; // DWMWCP_ROUND
        var border = ColorRef(p.OutlineVariant);
        DwmSetWindowAttribute(form.Handle, 33, ref round, sizeof(int));  // corner preference
        DwmSetWindowAttribute(form.Handle, 34, ref border, sizeof(int)); // border color
    }

    public static void ApplyNativeTheme(ListView list, Palette p)
    {
        if (!list.IsHandleCreated) return;
        SetWindowTheme(list.Handle, p.IsDark ? "DarkMode_ItemsView" : "ItemsView", null);
        var header = SendMessage(list.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero)
            SetWindowTheme(header, p.IsDark ? "DarkMode_ItemsView" : "ItemsView", null);
    }
}
