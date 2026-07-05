# Tiny Timers

A lightweight Windows desktop app for tracking game/app time, with text file
output, countdowns, linked-app auto-detection, and global hotkeys.

## Install

**[⬇ Download the latest installer](../../releases/latest)**

1. Grab `TinyTimersSetup-x.x.x.exe` from the [Releases page](../../releases)
2. Run it
3. Launch **Tiny Timers**

## Uninstall

**Settings > Apps > Installed apps > Tiny Timers > Uninstall**, or
via the Start Menu shortcut **Tiny Timers > Uninstall Tiny Timers**.

You'll be asked whether to also remove your saved data (timers, options, and cached files under
`%LocalAppData%\TinyTimers`):

- **Yes** - deletes your timer text files (wherever you've pointed them, including custom
  folders), settings, and cached data. This cannot be undone.
- **No** - leaves all of that in place, so reinstalling later picks up right where you left off.

## Using Tiny Timers

### Timers

Click **Add Timer** to create a new timer.

- **Regular (count-up)** - counts up from zero once started, like a stopwatch. Can be linked to
  an app (see below).
- **Countdown** - counts down from a duration you set, and plays a sound when it reaches zero.

Timer features:

- **Start / Pause / Resume** - click the timer's button, or use the global hotkey (see below).
- **Edit** - rename it, change its current time (or countdown duration), and change its linked
  app or sound.
- **Reset** - count-up timers reset to zero; countdowns reset to their initial duration.
- **Remove** - deletes the timer and its associated text file. This cannot be undone.
- **Reveal file** - opens the folder containing this timer's live-updating text file.

### Linking a timer to an app

Regular timers can be linked to an app - either one you pick from currently-running processes, or
by browsing to its `.exe` directly.

A linked timer:

- Gets a **blue** border while its app is running, and a **green** border while that app's
  window is actually focused and "foreground-active".
- Becomes the target of the global hotkeys automatically whenever its app is the one in focus.

If several linked timers are running at once, the global hotkeys act on whichever one's
app is currently in the focused and in the foreground. If only one timer has a linked app configured at all, the hotkeys always target that one, from anywhere.

### Countdown sounds

A countdown plays a default system "ding" when it finishes, or you can pick your own `.wav`,
`.mp3`, or `.wma` file to play instead via the edit/options menu.

### Live text files for OBS / streaming

Every timer continuously writes its current display time to its own plain-text `.txt` file, ready
to drop straight into an OBS (or similar) "Read from file" text source. There's also a shared
`active_app.txt` that always reflects whichever *linked* regular timer is currently
foreground-active. Point one text source at that single file and it'll automatically follow
whatever game/app you're currently playing, without needing to swap sources.

By default these files live under `%LocalAppData%\TinyTimers\Timer Text Files`, but you can point
them at any folder you like from **Options > Timer Files**. Switching folders keeps each folder's
timers as its own independent profile. Switching back to a previous folder restores any timers that were there before.

### Global hotkeys

Two systemwide hotkeys work even while a fullscreen app has focus:

- **Play / Pause / Resume** (default `Ctrl+Alt+G`) - toggles whichever timer is currently "active"
  (see linking, above).
- **Reset Active Timer** (default `Ctrl+Alt+R`) - resets that same timer.

Both are rebindable from **Options > Shortcuts**.

### System tray

Closing the window minimizes Tiny Timers to the system tray by default (configurable in Options).
From the tray icon you can:

- **Double-click** to reopen the window.
- **Right-click** for a menu listing every timer (click one to start/pause/reset it directly),
  plus **Open** and **Exit**.

## Options

- **Appearance** - dark, light, or match-system theme.
- **System** - run on startup, minimize to tray on close, keep window always on top.
- **Shortcuts** - rebind any global hotkeys.
- **Timer Files** - change where timer text files are written, or clear out old/orphaned ones.
- **Updates** - check for a new version on demand, and optionally turn on **Automatic updates**.

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
