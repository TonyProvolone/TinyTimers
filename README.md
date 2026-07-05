# Tiny Timers

A lightweight Windows desktop app for tracking game/app time, with OBS-friendly text file
output, countdowns, linked-app auto-detection, and global hotkeys.

## Download

**[⬇ Download the latest installer](../../releases/latest)**

Grab `TinyTimersSetup-x.x.x.exe` from the [Releases page](../../releases), run it, and launch
Tiny Timers from the Start Menu.

## Features

- Regular (count-up) and countdown timers, each with its own live-updating `.txt` file for
  OBS/streaming overlays
- Link a timer to an app - it highlights and becomes the hotkey target automatically when that
  app is focused
- A shared `active_app.txt` that always reflects whichever linked timer is currently active, so
  a single OBS text source can follow whatever you're playing
- Global hotkeys (start/pause/resume and reset) that work even while a fullscreen game has focus
- Dark/light/system theme, run-on-startup, minimize-to-tray, and always-on-top options

## Building from source

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build TinyTimers/TinyTimers.csproj
```

To build the installer locally you'll also need [Inno Setup](https://jrsoftware.org/isinfo.php):

```powershell
dotnet publish TinyTimers/TinyTimers.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o TinyTimers/publish

& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\TinyTimers.iss
```

## Releasing

Releases are built and published automatically by GitHub Actions. Push a version tag and CI
builds the installer and attaches it to a new GitHub Release:

```powershell
git tag v1.0.0
git push origin v1.0.0
```
