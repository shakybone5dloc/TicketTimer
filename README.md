# Ticket Timer

A small, free Windows app for tracking how long you spend on each ticket and what that time is worth. There are no accounts, no subscription, and no features behind a paywall.

## Features

- **Time a ticket:** enter a ticket name and your hourly rate, then start, pause and save. The clock shows the time and what you've earned as it runs.
- **Log with totals:** every saved session is listed, with totals for the current ticket and for everything. Export it all to CSV.
- **Continue where you left off:** double-click a logged entry to keep timing it from its saved time. Saving updates that entry.
- **Set time:** forgot to start the timer? Enter when you actually started (`9:15 am`) or how long you've already worked (`1h 30m`).
- **Mini widget:** a small floating card with the live timer and play/pause and save buttons. Drag it anywhere and keep it on top.
- **Tray icon:** hover to see the running time, or use its menu to start, pause or save.
- **Light, dark or system theme**, in a Material-style design.

Your data stays on your computer, in `%AppData%\TicketTimer\data.json`.

## Download

Grab `TicketTimer.exe` from the [Releases](https://github.com/shakybone5dloc/TicketTimer/releases) page. It's a single standalone file, so there's nothing to install and .NET doesn't need to be on your PC. Run it from anywhere.

## Build it yourself

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```bash
dotnet publish -c Release -o publish
```

This produces `publish\TicketTimer.exe`. To regenerate the app icon after changing its design, run `pwsh tools/make-icon.ps1`.

## Support

Ticket Timer is free and always will be. If it saves you some time and you'd like to say thanks, you can [buy me a coffee on Ko-fi](https://ko-fi.com/shakybone5dloc) ☕. It's appreciated, never expected.

Found a bug or have an idea? [Open an issue](https://github.com/shakybone5dloc/TicketTimer/issues).
