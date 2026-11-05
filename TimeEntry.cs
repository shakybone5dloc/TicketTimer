using System.Text.Json;

namespace TicketTimer;

/// <summary>One finished stretch of work on a ticket.</summary>
public class TimeEntry
{
    public string Ticket { get; set; } = "";
    public DateTime Started { get; set; }
    public double Seconds { get; set; }
    public decimal HourlyRate { get; set; }

    public TimeSpan Duration => TimeSpan.FromSeconds(Seconds);
    public decimal Earned => Math.Round((decimal)Seconds / 3600m * HourlyRate, 2);
}

/// <summary>Saves entries and the last-used settings as JSON in %AppData%\TicketTimer.</summary>
public class Store
{
    public List<TimeEntry> Entries { get; set; } = new();
    public string LastTicket { get; set; } = "";
    public decimal LastRate { get; set; } = 50m;
    public string ThemeMode { get; set; } = Theme.System;

    static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TicketTimer");
    static readonly string FilePath = Path.Combine(Folder, "data.json");
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Store Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Store>(File.ReadAllText(FilePath)) ?? new Store();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Couldn't read saved data:\n{ex.Message}", "Ticket Timer",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        return new Store();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
