# Desktop Guy

A tiny pixel-art companion that sits on your Windows desktop, wanders around
now and then, says the occasional thing, and dozes off if you step away from
the keyboard for a while. Built as a template so more characters can be
dropped in without touching any code.

## Running it

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (get the
   SDK, not just the runtime) - any recent .NET SDK works fine here, since
   .NET SDKs build older-targeted projects too, but the project itself
   targets .NET 10 so that's the simplest match.
2. During install (or after, via **Visual Studio Installer**), make sure the
   **".NET desktop development"** workload is checked - that's what gives you
   WPF. If you already have Visual Studio, you can add it there instead:
   Tools \> Get Tools and Features \> check ".NET desktop development".
3. From a terminal in the repo root:
   ```
   dotnet run --project src/DesktopGuy.App
   ```
   The first build will take a little while (restoring/compiling); later
   ones are fast.

By default it loads the first character it finds under
`src/DesktopGuy.App/Assets/Characters`. To pick a specific one:

```
dotnet run --project src/DesktopGuy.App -- --character Blob
```

Right-click the character for a small menu (say hi / start with Windows /
exit) - there's no taskbar icon or window border, so that's also how you
close it.

### Building a standalone .exe

`dotnet run` is fine for trying it out, but if you want a `DesktopGuy.exe`
you can just double-click (no terminal, no separately-installed .NET
needed on the machine that runs it):

```
dotnet publish src/DesktopGuy.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The exe lands in
`src/DesktopGuy.App/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`.
Copy that whole `publish` folder wherever you like (it needs the
`Assets` folder alongside the exe) - or just the exe if you don't mind it
fetching the character files from the original location.

#### About the "Windows protected your PC" (SmartScreen) warning

Running a freshly-built, unsigned `.exe` - especially one that arrived via
a browser download (a ZIP from GitHub, say), which tags it with a "Mark of
the Web" - can trigger Windows SmartScreen. This isn't a bug and can't be
fixed by changing anything in this repo: it's Windows flagging any
executable it doesn't recognize (no publisher signature, no download
reputation built up yet), which is true of essentially any personal/hobby
`.exe` that isn't code-signed. Options, roughly cheapest to most involved:

- **Click through it once.** "More info" → "Run anyway" on the SmartScreen
  dialog. Windows remembers that decision for that *exact* file (by hash) -
  rebuilding produces a new file and resets it, which is why this can
  reappear after a fresh `dotnet publish`.
- **Unblock the file** before running it, if it came from a ZIP download -
  this removes the Mark-of-the-Web that triggers the prompt in the first
  place: `Unblock-File .\DesktopGuy.exe` in PowerShell (or right-click →
  Properties → check "Unblock" at the bottom, if present).
- **Code-sign it** for a real, permanent fix - needs a code-signing
  certificate (a paid one, typically, though free options exist for open-
  source projects like SignPath's program) and a publishing pipeline to
  actually sign the build. Out of scope for a personal project like this
  one unless you're distributing it more widely.

Running `dotnet run` from source (rather than a published `.exe`) mostly
sidesteps this, since there's no separately-downloaded executable file for
Windows to flag in the first place.

#### If Windows blocks it outright (Smart App Control) rather than just warning

A stricter, separate mechanism from SmartScreen above: Windows 11's **Smart
App Control** (Settings → Privacy & security → Windows Security → App &
browser control) can outright refuse to load an unsigned binary rather than
just warning about it - no "run anyway" option, just a hard failure. It
showed up here as:

```
System.IO.FileLoadException: Could not load file or assembly '...DesktopGuy.dll'.
An Application Control policy has blocked this file. (0x800711C7)
```

Unlike SmartScreen, `dotnet run` doesn't sidestep this - it still has to
load the freshly-compiled DLL, and a *fresh* compile means a *new* file
hash Windows has never evaluated before, every single time. Rebuilding via
`dotnet run` on every launch was effectively asking Smart App Control to
re-judge a brand new file on every run.

The fix that worked here, without touching Smart App Control's settings or
needing a code-signing certificate: **publish a stable build once, then
launch that same file repeatedly instead of rebuilding on every launch.**

```powershell
dotnet publish src/DesktopGuy.App -c Release -r win-x64 --self-contained false
```

This produces a stable `DesktopGuy.exe` (plus its `.dll` and the `Assets`
folder alongside it) under
`src\DesktopGuy.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\`.
Run that exe directly (double-click it, or `.\DesktopGuy.exe --character
Dave` from that folder) instead of `dotnet run` from then on - its hash
only changes when you actually rebuild after a real code change, not on
every launch.

If a *first* run of a freshly published build still gets blocked, that's a
genuine Smart App Control call on this specific binary rather than a
build-freshness artifact, and the real fixes are the two mentioned in the
SmartScreen section above (click through / code-sign) - Smart App Control
has no per-app exception list to add this app to, and turning it off
entirely is a one-way decision (it can't be re-enabled without a full
Windows reset).

**One real data point from hitting this in practice**: a build that
included `BluetoothWatcher` (using `Windows.Devices.Bluetooth` /
`Windows.Devices.Enumeration` to watch for paired devices connecting)
got blocked; removing that watcher and rebuilding cleared it on the same
machine. That's not proof Bluetooth enumeration specifically is what
Smart App Control's cloud classifier objects to - it's still an opaque,
hash-based decision, and it could just as easily have been another
"never-seen-before hash" roll of the dice - but it's the one reproducible
before/after result seen so far, so it's worth knowing about before
re-adding Bluetooth support (or any other watcher touching a WinRT device
API this app hasn't used before): rebuild once with it, once without, and
compare, rather than assuming it's fine.

### Always on Top

Right-click the character and toggle **"Always on Top"** to control
whether he stays drawn over other windows (the default) or behaves like a
normal window that other apps can cover. Remembered across restarts, and
applies immediately without needing to relaunch.

### Starting automatically at login

Right-click the character and toggle **"Start with Windows"**. This writes a
normal per-user startup entry (`HKEY_CURRENT_USER\...\Run` - the same
mechanism most tray apps use), pointing at whichever exe you toggled it
from. No admin rights needed, and unchecking it removes the entry cleanly.

Toggle it from wherever you actually plan to run it long-term (the
published `.exe`, not a `dotnet run` build) - it registers the exact path
that's currently running, so a debug build in `bin\Debug\...` would
register that temporary path instead of your real install.

## How it behaves

- **Idle & sleep**: if you don't touch the mouse or keyboard *anywhere on
  the system* for `idleTimeoutSeconds` (see character.json), the character
  curls up and snoozes with a little Zzz. Touch anything and it wakes back up.
- **Wandering**: every so often (a random interval between
  `walkIntervalMinSeconds` and `walkIntervalMaxSeconds`) it walks a short
  distance left or right along the bottom of the screen - right along the
  top edge of the taskbar, since that's where the usable work area ends.
- **Speech bubbles**: at random intervals it pops up a little speech bubble
  with a line picked from `phrases` in its character.json - plus, during a
  few narrow holiday windows (Oct 25-31, Dec 20-26, and Dec 31/Jan 1), extra
  lines from `seasonalPhrases` if the character defines any for that
  occasion, same layering as the morning/evening/late-night pools below.
- **Drag**: click and hold to pick it up - a quick startled "pickUp" hop
  plays, then it follows the cursor with a little wobble. Drop it above the
  ground and it gently falls back down; drop it on the ground and it's ready
  to wander again.
- **Bonk**: double-click for a distinct startled/annoyed reaction (`bonk`),
  separate from a single click's plain pet.
- **Affection**: total time it's spent onscreen is tracked per character,
  cumulative across restarts. At a few thresholds (5 min, 30 min, 1 hour,
  4 hours, 1 day, 1 week, 30 days) it says something special once, plus a
  dedicated `milestone` animation if the character defines one. The line
  itself is picked from a combined pool of `affectionPhrases` (general
  celebratory lines, e.g. "we've been together for a while now") and
  `togetherTimePhrases` (same idea, but with the actual elapsed time
  filled in via a `{0}` placeholder, e.g. "We've been together for {0}
  now!" becomes "We've been together for a day now!") - so it's a mix of
  both flavors rather than always one or the other, and either falls back
  to a generic line/template if the character doesn't define its own.
  Petting it (a click that isn't a drag) is unrelated to this - that
  always just pops up a normal phrase from `phrases`.
- **Waking from sleep** (the PC's, not the idle-timeout kind above): if a
  `systemResume` animation is defined, it plays once the moment Windows
  itself resumes from suspend/hibernation - a startled "oh, we're back"
  beat distinct from the calmer `wake` used for you simply stepping away
  for a while with the PC left on.
- **Position memory**: it remembers roughly where you left it (per
  character) and starts there next time, instead of always the same corner
  - including which monitor, if you have more than one (wandering, the
    ground line, and drag physics all use whichever monitor it's actually
    on rather than always the primary display). This assumes every
    monitor runs the same DPI/display scale; on a mixed-DPI setup (a
    laptop screen at 150% next to an external monitor at 100%, say) the
    position on a non-primary monitor can be slightly off.
- **First-run hint**: the very first time you ever run the app (regardless
  of which character), it mentions the right-click menu once via a speech
  bubble - it never repeats itself after that, even across restarts or
  switching characters.
- **Music**: if something is playing that looks like music (Spotify, YouTube
  Music, etc.) it puts on headphones and dances instead of wandering off -
  and if the track title is available, it announces it once ("▶ Now
  playing: ...") the moment it starts, not on every beat.
- **Video**: if something is playing that looks like a video (a YouTube tab,
  Netflix, VLC, ...) it puts on sunglasses, grabs popcorn, and settles in
  to watch - same one-off "now playing" announcement as music, when a
  title's available. A brief gap with nothing detected (a few seconds of
  silence between tracks, a buffering hiccup) doesn't immediately drop the
  pose or forget the announcement - only a genuine stop lasting longer
  than that grace period does, so the same song resuming after a short
  pause doesn't re-announce itself.
  - Browsers get disambiguated by checking the focused tab's title against
    a list of known video sites, since some don't self-report the right
    type. Standalone player apps that aren't trustworthy about
    self-reporting either (currently just VLC) are instead matched by the
    media session's own app ID, so it counts even while playing in the
    background/minimized - see `VideoPlayerAppIdMarkers` in
    `MediaContextWatcher.cs` to add another one.
- **Terminal or code editor focused, or you're typing**: if a shell or
  terminal app (Command Prompt, PowerShell, Windows Terminal, PuTTY, ...),
  or a code editor/IDE (VS Code, Visual Studio, a JetBrains IDE, Sublime
  Text, ...) is the window you're actually using right now, or you're
  actively pressing keys anywhere, it pulls up its own little terminal
  with scrolling matrix-style code - all of these reuse the same
  animation, since there's no separate art for "typing in a terminal" vs.
  "in an IDE" vs. "typing anywhere else". Focus is checked against
  whichever window is actually focused, not just "is one of these running
  somewhere" - otherwise launching the app from a terminal would leave it
  stuck showing this forever.
- **Video call**: if a video-call app (Zoom, Microsoft Teams, Google Meet,
  Skype, ...) is the window you're actually using right now, it puts on
  sunglasses and settles in to watch, the same pose as a video - reused
  rather than needing its own art, since "staring at a call" and "staring
  at a video" look the same.
- **Discord call**: an incoming Discord call makes it pick up a phone for a
  moment, then goes back to whatever it was doing.
- **Discord message**: a new Discord message makes it open an envelope for a
  moment, then resumes.
- **Battery**: on a laptop, a speech bubble the moment charge drops to 20%
  while unplugged ("Battery's getting low..."), another the moment it's
  plugged into power ("Plugged in! Charging now." - customizable per
  character via `batteryPluggedInPhrases`, same fallback pattern as the
  system-event phrases below), and another the moment it reaches 100%
  while plugged in ("Battery's fully charged!") - each only fires once per
  transition, not repeatedly while it stays low/plugged-in/full. If a
  character also defines a `lowBattery` animation, that pose is mixed into
  the same idle-surprise rotation as `eating`/`playing` while the battery's
  actually low (see "Adding a new character" below) - desktops with no
  battery just never trigger any of this.
- **Network/wifi**: a speech bubble the moment the network connection drops
  ("Uh oh, the wifi just dropped!") and another the moment it comes back
  ("Wifi's back! Phew.") - wording picks "wifi" or "internet" depending on
  what the connection looked like right before it went down. This is a
  best-effort link-level check (the same one Windows itself uses), not a
  true "can I actually reach the internet" test, so a router with no
  upstream still reads as connected - good enough for a nudge, not a
  diagnostic tool.
- **Disk space**: a speech bubble once free space on the app's own drive
  drops below 2 GB, and another once it's back above that - checked every
  few minutes rather than continuously, since disk usage doesn't change
  fast enough to need anything tighter.
- **Session lock/unlock**: a speech bubble when you lock the machine
  (Win+L, the screensaver, an auto-lock policy, ...) and another the moment
  you unlock it - a different signal from the idle-timeout-based
  Sleeping/Waking pair, which only ever knows "no input for a while," not
  a genuine lock.
- **USB/removable drive**: a speech bubble whenever a removable drive
  (USB stick, external HDD/SSD, an SD card via a reader, ...) shows up or
  disappears, naming the drive letter ("Ooh, what's this? New drive at
  D:\!" / "Huh, D:\ is gone now."). Checked via a 2-second poll of the
  drive list rather than a device-change hook - no window handle or native
  struct marshaling needed for something this low-stakes.

None of these have dedicated animations - just phrases, layered on top of
whatever pose it's already in, the same way the battery nudges above work.

None of the above needs administrator rights. Every watcher in the app -
battery, network, disk space, session lock, USB drives, Discord awareness,
media/terminal/typing detection - uses plain standard-user Windows APIs
(WMI-free file-system/network enumeration, public broadcast events like
`SystemEvents.SessionSwitch`, or reading the same "now playing"/session
info any app can read). The app runs fine as a normal user the whole time;
the only unrelated friction is the SmartScreen warning mentioned below,
which is about the executable being unsigned, not about needing elevated
permissions.

There are also four weather poses - `cold`, `hot`, `sunny`, `rainy` - but
they're preview-only, triggered from the right-click menu's **Preview
Weather** submenu rather than automatically. An earlier version tried to
detect the real weather and settle into one of these after being idle for a
while, but in practice that meant it only ever showed up if the actual
forecast happened to match one of the four categories *and* you'd stayed
idle long enough at the same time - rare enough that it read as "broken"
rather than "occasional." Picking one from the menu forces that pose for a
few seconds so you can actually see it.

If more than one of the automatic behaviors applies at once, momentary
things (a Discord call or message) always interrupt and play out fully
before it resumes whatever it was doing. Among the ongoing ones: a terminal
or code editor being focused wins over a video call, which wins over
video/music, which wins over just wandering/idling. See the priority list
at the top of
`Engine/CharacterController.cs` if you want to change any of that ordering.

The music/video awareness uses Windows' own "now playing" system (the same
thing behind the media controls on your lock screen), checking every app
with a registered session (not just whichever one Windows considers
"current") for one that's actually playing, so it works with whatever's
actually playing without knowing about specific apps. Browsers can't
always be trusted to correctly report whether what's playing is music or
video, though - some don't report a type at all, others have been seen
reporting the wrong one - so before trusting that, it first checks whether
the focused window's title names a known video site (YouTube, Netflix,
Twitch, ...) and treats that as authoritative when it matches. This whole
system still depends on the browser choosing to integrate with Windows'
media session API in the first place - Chromium-based browsers (Edge,
Chrome, Brave, ...) do this consistently; Firefox's support is less
consistent release to release, so it may not report anything at all for
some content even though something's genuinely playing (in which case the
title check never gets a chance to run, since nothing looks like it's
"playing" yet). That's a limitation of Firefox's own OS integration rather
than something fixable from here. Terminal
awareness checks which window currently has focus. Typing awareness uses a
low-level keyboard hook (`WH_KEYBOARD_LL`) to notice *that* a key was
pressed and *when*, system-wide - it's the only way to see keystroke
timing outside our own window. It never reads, stores, or logs which keys
are pressed; the only thing it ever keeps is a single timestamp of the
last key-down, overwritten every time. Discord awareness reads Discord's
own notifications via Windows' notification listener - see **Context
awareness setup** below, since that one needs a one-time permission grant.
If that permission was never granted (denied, dismissed, or just never
asked because it's unsupported here), you'll get a one-time speech bubble
the first time the app ever notices - after that it doesn't repeat itself,
so check **View Error Log** below if you want to know why it's not active
on a later run.

## System tray, fullscreen auto-hide, and troubleshooting

- **One instance per character**: launching the same character while it's
  already running (double-clicking the exe again, a manual launch racing
  a "Start with Windows" copy) just quietly exits instead of opening a
  second overlapping window and a second tray icon. Different characters
  are unaffected and can still run side by side if you want more than one
  companion at once - this only guards against the exact same one twice.
  Picking a new size or switching characters from the menu isn't affected
  either, since those already replace the running instance on purpose.
  This relies on a named lock that a security/permissions quirk could
  occasionally prevent from working (e.g. one copy launched "as
  Administrator" while another runs normally) - if that happens it fails
  open (launches anyway, logging the issue to **View Error Log**) rather
  than refusing to start over what's essentially a niceness check.
- **System tray icon**: alongside the character itself, there's a small
  tray icon (a picture of whichever character is currently running) -
  right-click it for the exact same menu as right-clicking the character,
  and left-click it to manually show/hide the character on demand (handy
  if he's ended up somewhere awkward, or you just want him out of the way
  for a bit without exiting the app - or if fullscreen auto-hide below
  ever gets it wrong, since a manual click always wins over it until
  clicked again). The tray icon and its menu keep working even while the
  character itself is hidden, either manually or by the fullscreen
  behavior below.
- **Fullscreen auto-hide**: if whatever you're focused on (a game, a video
  player, a presentation) is genuinely fullscreen - its window covers the
  entire monitor *and* has no title bar - the character hides itself
  automatically rather than floating on top of it, and reappears the
  moment you're back to something windowed. Both conditions matter: an
  ordinary maximized window (a browser, a terminal, ...) still has a title
  bar even when its visible chrome is minimal, so it's never mistaken for
  fullscreen - checking the monitor coverage alone isn't enough, since
  with an auto-hiding taskbar a plain maximized window covers just as much
  screen as a real fullscreen app would. Everything (onscreen-time
  tracking, the tray icon, ambient state changes) keeps running normally
  while hidden; only the window itself stops being drawn.
- **Error log**: several of the context-awareness pieces above (media
  detection, Discord notifications, the keyboard hooks) depend on Windows
  APIs that can fail to start for reasons outside the app's control (an
  older Windows build, a locked-down system, a denied permission) - when
  that happens they just quietly do nothing rather than crash, which is
  usually the right call but can make "why isn't X detecting anything"
  hard to debug from the outside. Right-click → **View Error Log** opens a
  small text file (`%AppData%\DesktopGuy\errors.log`) with the last 10
  such failures, each one timestamped and naming which piece of the app
  hit it.
- **Reset All Settings / uninstalling**: right-click → **Reset All
  Settings...** clears everything the app remembers (position, size,
  which character you last picked, onscreen-time and pet-milestone
  progress, the error log, which one-time hints have shown) and turns off
  "Start with Windows", then restarts fresh - the same state as a brand
  new install. To remove the app entirely: delete the installed folder
  (or uninstall it, if you installed it via a packaged installer rather
  than just running the built `.exe`), and optionally delete
  `%AppData%\DesktopGuy\` too if you don't want its files lingering
  (Reset All Settings already does this for you, so it's only needed if
  you want to skip straight to deleting the app without launching it
  again first). There's no separate registry cleanup needed beyond that -
  "Start with Windows" only ever writes the one `HKCU` Run-key value this
  reset (or unchecking the menu item) already removes.

## Context awareness setup

The first time you run the app, Windows may prompt to let it read your
notifications (this is what lets it react to a Discord call or message).
That's a **standard Windows permission** - the same kind of prompt apps like
Cortana or Phone Link ask for - and it's scoped to notifications only. If you
say no, or your Windows build doesn't support this for a plain .exe (see
caveat below), the character just skips Discord reactions; everything else
(idle/sleep, wandering, music/video awareness) works the same either way.

If it doesn't prompt automatically, check **Settings \> Privacy & security \>
App notifications \> Manage notification access** (or the older **Settings \>
Privacy \> Notifications**) and allow it there.

> **Caveat:** the Windows notification-listener API this relies on
> (`UserNotificationListener`) is mainly documented for packaged (MSIX) apps.
> It's expected to work for a plain unpackaged build like this one on current
> Windows 10/11, but this was built and reviewed without a Windows machine to
> test it on - if Discord reactions don't show up for you, that's the most
> likely culprit, and the rest of the character is unaffected.

## Project layout

```
src/DesktopGuy.App/
  Engine/               <- character-agnostic behavior engine (state machine,
                            idle detection, sprite animation, window plumbing)
  Characters/            <- CharacterDefinition model + loader for character.json
  Context/                <- context awareness: now-playing media (music/video),
                            the focused window/terminal, keyboard activity, and
                            Discord notifications (call/message) - all optional and
                            independent of the core engine
  Assets/Characters/
    Blob/                <- the default (placeholder) character
      character.json
      spritesheet.png
  MainWindow.xaml(.cs)    <- the transparent always-on-top overlay window
```

The `Engine` and `Characters` code never mentions "Blob" or any specific
character - everything character-specific lives in `character.json` and the
sprite sheet, which is how new characters get added.

## Adding a new character

1. Make a new folder under `Assets/Characters/<YourCharacterName>/`.
2. Add a sprite sheet PNG. Frames are laid out in a grid: one **row per
   animation**, frames left-to-right within a row, all frames the same
   pixel size. Five animations are required: `idle`, `walk`, `sleep`, `drag`,
   `wake`. The rest are optional and light up individual context-aware
   reactions - leave any of them out of `character.json` and the character
   simply never enters that state (e.g. skips dancing but still handles
   video, or vice versa):
   - `dance` - music is playing
   - `watch` - a video is playing, or a video-call app (Zoom, Teams, Google
     Meet, ...) is focused
   - `hacking` - a terminal/shell or code editor/IDE is focused, or you're
     typing anywhere
   - `cold` / `hot` / `sunny` / `rainy` - weather poses, shown only via the
     right-click menu's Preview Weather submenu (not triggered automatically)
   - `answerCall` - an incoming Discord call
   - `openMail` - a new Discord message
   - `pickUp` - the moment you grab it (falls back straight to `drag` if omitted)
   - `snapshot` - a startled reaction to the PrintScreen key being pressed
   - `eating` / `playing` - spontaneous one-off animations while otherwise
     just standing around idle (see `idleSurpriseIntervalMinSeconds` /
     `idleSurpriseIntervalMaxSeconds` in `behavior` to tune how often)
   - `lowBattery` - mixed into the same idle-surprise rotation as `eating`/
     `playing`, but only while a laptop's battery is actually low and
     unplugged
   - `bonk` - a double-click (as opposed to a plain click, which is a pet)
   - `systemResume` - the whole PC waking from sleep/hibernation -
     different from `wake`, which is about *you* being away for a while
     with the PC still on
   - `milestone` - a cumulative-onscreen-time milestone being reached (see
     `affectionPhrases` below) - optional on top of the phrase that
     already plays either way
3. Add a `character.json` next to it (copy `Blob/character.json` as a
   starting point) describing:
   - `frameSize`: pixel width/height of a single frame in the sheet
   - `scale`: how many times to scale the sprite up on screen (pixel art
     usually wants 3-6x so it isn't tiny) - a whole number is still
     recommended on general principle (fractional scales are a plausible
     source of edge-bleed with nearest-neighbor scaling), though in
     practice the edge-bleed bugs found while building this template
     traced back to artifacts baked into the source art, not the scale
     itself - see `Assets/Characters/Fox/NOTES.md` for the full story if
     you hit something similar. If a character needs a specific on-screen
     size that isn't a clean multiple of its native resolution, resizing
     the sprite sheet itself is the safer route either way.
   - `smoothTransitions`: `true` to cross-fade between animation frames
     instead of hard-cutting - makes a handful of poses read as smoother
     motion without needing more art. Suits smooth-shaded/cartoon art
     (like Fox); usually looks wrong for blocky pixel art (like Blob,
     which leaves this `false`, the default).
   - `animations`: for each animation name, which sprite-sheet `row` it's
     on, how many frames (`frameCount`), how fast to play them (`fps`),
     and whether it `loop`s (`wake`, `answerCall`, `openMail` and `pickUp`
     are non-looping - they play once and then move on; everything else loops)
   - `behavior`: idle timeout, wander timing, speech timing
   - `phrases`: the lines it can say
   - `morningPhrases` / `afternoonPhrases` / `eveningPhrases` /
     `lateNightPhrases`: optional extra lines mixed in alongside `phrases`
     at the matching time of day (roughly 5am-noon / noon-6pm / 6pm-11pm /
     11pm-5am)
   - `seasonalPhrases`: optional, keyed by `"halloween"`, `"christmas"`,
     `"newYear"`, `"valentinesDay"`, `"aprilFools"`, `"bonfireNight"`, or
     `"easter"` - extra lines mixed in only during those windows (leaning
     UK - Bonfire Night is November 5th, and Easter's window is computed
     properly per year, Good Friday through Easter Monday, rather than
     hardcoded to one date). See `CharacterController.GetSeasonalKey` for
     the exact date ranges.
   - `affectionPhrases`: optional - special lines it might say once a
     cumulative-onscreen-time milestone is reached (see "How it behaves" above)
   - `togetherTimePhrases`: optional - same milestone moment as
     `affectionPhrases` above, mixed into the same pool, but with the
     actual elapsed time filled in. Use `{0}` as a placeholder - it's
     filled in with a friendly label ("5 minutes", "30 minutes", "an
     hour", "4 hours", "a day", "a week", "30 days") matching whichever
     threshold was just crossed, e.g. `"Together for {0}!"` becomes
     "Together for a day!".
   - `networkDownPhrases` / `networkUpPhrases`: optional - override the
     generic wifi/internet drop-and-reconnect lines with the character's own
     voice. Use `{0}` as a placeholder - it's filled in with `"wifi"` or
     `"internet"` depending on what the connection looked like right before
     it dropped, e.g. `"WHERE'S THE {0}?!"` becomes "WHERE'S THE WIFI?!".
   - `diskSpaceLowPhrases` / `diskSpaceRecoveredPhrases`: optional - same
     idea for the low-disk-space nudge, no placeholder needed.
   - `sessionLockedPhrases` / `sessionUnlockedPhrases`: optional - same idea
     for the OS session lock/unlock nudge, no placeholder needed.
   - `usbConnectedPhrases` / `usbDisconnectedPhrases`: optional - same idea
     for the removable-drive nudge. Use `{0}` as a placeholder for the drive
     letter, e.g. `"ooh, {0}!"` becomes "ooh, D:\!".
   - `batteryPluggedInPhrases`: optional - same idea for the moment the
     laptop's plugged into power, no placeholder needed.
   - Any of the nine above left out entirely (or set to an empty array)
     just falls back to a generic line - only worth adding once a
     character has enough personality established to be worth writing
     event-specific lines for.
4. In the `.csproj`, files under `Assets/Characters/**` are already
   configured to copy to the output folder automatically - no project file
   changes needed.
5. Run it with `dotnet run --project src/DesktopGuy.App -- --character
   YourCharacterName`.

### Importing a character in-app, without editing any files

For the common case - a clean green-screen grid sheet, like the ones the
AI-art prompt template above produces - you don't need to do any of the
above by hand. Right-click the character and choose **New Character...**:

1. Give it a name (this becomes both the display name and the folder name
   under `Assets/Characters/`).
2. Browse to the sprite sheet PNG.
3. Enter how many columns (frames per row) and rows the sheet has - the
   grid is assumed evenly spaced, e.g. 6 columns x 22 rows for a sheet with
   every optional animation.
4. Click Import.

The app chroma-keys the green background out itself, despills any faint
green edge-tint the same way the hand-processed characters get cleaned up,
re-grids everything onto the standard 84x84 frame size, and writes out a
ready-to-use `character.json` (rows are assigned to animation names in the
same fixed order used throughout this doc - `idle`, `walk`, `sleep`,
`drag`, `wake`, `dance`, `watch`, `answerCall`, `openMail`, `hacking`,
`pickUp`, `hot`, `sunny`, `rainy`, `cold`, `eating`, `playing`,
`lowBattery`, `snapshot`, `bonk`, `systemResume`, `milestone` - so a
sheet with fewer rows just gets fewer animations, same as leaving them
out of a hand-written `character.json`).
It starts with a small set of generic phrases and no time-of-day-specific
ones; edit `character.json` afterwards to personalize those; there's no
in-app editor for phrases.

This only handles the common case cheaply - it expects an evenly-spaced
grid (the **same number of columns in every row**) and a plain,
roughly-flat green background. A messier upload (a non-green-adjacent
background color, hand-drawn grid lines, a background with a strong
vignette or gradient, a palette that's itself green-adjacent like Mongo's
turquoise skin) will come out with visible cleanup artifacts or outright
fail the size check, and needs the manual, AI-assisted process described
above instead - as does the per-row-generated workflow in that section,
since it deliberately varies frame counts row to row (8 for `walk`, 4 for
a one-shot, ...) for smoother motion, which this importer's fixed-column
grid can't represent. Compositing rows of different frame counts into one
`character.json` needs the manual route: pad each row's unused trailing
cells with transparent pixels up to the widest row's frame count when
building the final sheet, and set each animation's own (correct, smaller)
`frameCount` in `character.json` - the engine just ignores anything past
that count.

## About the placeholder art

`Blob`'s sprite sheet is simple generated pixel-art blocks (64x64px per
frame, displayed at 3x scale - 192x192px on screen), good enough to see the
whole system working end-to-end (idle blink, walk bounce, sleep Zzz,
startled pickUp, drag wobble, wake-up blink, headphone dance,
sunglasses-and-popcorn watch, matrix-code hacking, jacket-and-breath-clouds
cold, fan-and-sweat hot, sunglasses-and-rays sunny, umbrella-and-raindrops
rainy, phone-call pickup, envelope-opening) but it's meant to be swapped
out - drop in a real pixel-art sprite sheet for `Blob` (see the AI-art
prompt below) or any new character and nothing else needs to change.

### Generating real art for a new character

The engine places zero limit on frames per row - `frameCount` in
`character.json` is just a number, checked nowhere else - so "more frames
for smoother motion" is purely an art/prompting question, not a code
change. The guidance below reflects that: it recommends frame counts based
on actual 2D animation convention (a walk cycle reads far better at 8
frames than 6 - see "Frame counts, and why" below) rather than the
"whatever's cheap to generate" 6-frames-flat that every built-in character
so far has used.

**Every documented defect across every built-in character's `NOTES.md` so
far traces back to the same root cause: asking one generation call for an
entire multi-row grid at once, and the model drifting partway through it**
- a cursor icon baked into two frames of Mongo's `drag` row, his eye color
changing between frames, a whole unrelated bear appearing on one of Fox's
rows, a missing prop leaving artifact patches, a last frame that abandons
the pose entirely. A single-shot "generate this whole 6x15 grid" prompt
asks a lot of any current image model's consistency - the workflow below
generates *one row at a time*, feeding the model a reference image of the
character back into each request (Gemini's image model supports exactly
this: attaching an image and asking for a new one that matches it, within
the same conversation) instead of describing the whole cast of poses in
one text prompt and hoping the model holds the character steady across
all of them.

#### Step 1: generate one reference image first

Establish the character's design once, cleanly, before generating any
poses - this becomes the anchor every later row is generated against.

> A single character reference image, front-facing, standing in a simple
> neutral pose (arms/limbs relaxed at its sides, facing the camera), on a
> solid flat green background (like a green-screen) - the same exact
> green everywhere, no gradients, shadows, or texture. Solid black
> outlines around the entire character - never colored or blended into
> the green background.
>
> Character: [DESCRIBE THE CHARACTER - species/shape, color palette,
> face/eyes, distinguishing features, size proportions, no more than a
> couple of sentences]. Style: [e.g. "flat cartoon shading" or "crisp
> hard-edged pixel art, no anti-aliasing"] - pick one style; every later
> image needs to match this exact character design and style.

Look this over carefully before moving on - this is the one image every
later generation will be measured against, so it's worth regenerating a
few times to get a design you're happy with before building anything on
top of it.

#### Step 2: generate one row at a time, attaching the reference image

In the *same* Gemini conversation (so the reference image stays attached
as context - a fresh chat won't have it), ask for one animation row per
message, referencing the image you just approved:

> Using the exact same character shown in the attached reference image -
> identical colors, proportions, outline style, and art style, no
> deviation - generate a new image: a horizontal strip of [N] frames,
> left to right, each frame [FRAME SIZE]x[FRAME SIZE] pixels, consistent
> even spacing between frames, on the same solid flat green background as
> the reference (same exact green, no gradients/shadows/texture).
>
> Animation: [ROW NAME] - [DETAILED POSE DESCRIPTION - see the per-row
> list below]. The character's feet/base must stay at the same height in
> every single frame (no vertical bobbing/drift frame to frame unless the
> pose specifically calls for it). If the character holds or wears
> anything in this pose, it must appear in every frame consistently - not
> only some of them - and must not be colored green (a green prop
> chroma-keys out invisibly against the background). Do not add any UI
> elements, cursors, icons, text, or watermarks anywhere in the image -
> only the character and the green background.

Do this for every row you want, one message at a time - it costs more
generation calls than one giant sheet, but each call is a much easier
consistency problem for the model, and you can regenerate just the one
row that came back wrong instead of the whole sheet.

#### Step 3: composite the rows into one sheet

Once every row looks right individually, stitch the separate horizontal
strips into the final grid (stacked top to bottom, in the row order your
`character.json` will use) - a short Pillow script does this in a few
lines; ask an AI coding assistant to write one if you're not comfortable
with image processing directly, or adapt the cleanup approach documented
in any existing character's `NOTES.md`. Expect to still need a
chroma-key + edge-despill cleanup pass afterward (see the lessons below)
even with this more consistent workflow.

#### Lessons learned (read before writing the character/pose descriptions)

- **Ask for a solid green background, not transparency**, for the
  reasons in Step 1/2 above - a fake checkerboard "transparent" pattern
  or faint colored edge fringing is what you get instead if you don't.
- **Ask for solid black outlines specifically, not just "an outline."**
  Without that, the character's edge often comes back anti-aliased into
  the green background - a thin ring of green-tinted pixels rather than a
  clean line - which then needs a spill-suppression pass (clamp the green
  channel down wherever it's the dominant one on a kept pixel) to fix up
  afterward.
- **Never ask for a green prop.** A real mistake made writing an earlier
  version of this template: it suggested a dinosaur character fan itself
  with "a small leaf." A green leaf on a green background chroma-keys out
  along with the background - the prop was just gone in the result,
  leaving faint artifact patches where it used to be. Keep any held/worn
  prop a color clearly different from the background green. Also worth
  knowing: a character whose own skin/fur color is itself green-adjacent
  (teal, lime, olive) needs a more careful cleanup pass than one whose
  colors sit far from green - see the "turquoise skin" section of
  `Assets/Characters/Dinosaur/NOTES.md`.
- **Don't let a held prop hide the character's limbs.** Mongo's `hacking`-
  equivalent laptop pose and Dave's actual `hacking` row both ended up
  with paws/legs completely hidden behind the prop in every frame - not a
  cropping bug, just the prop drawn too large/low. Explicitly ask for the
  prop sized and positioned so all limbs stay visible around or beside
  it, not behind it.
- **A prop that appears must appear in every frame of that row,
  consistently** - Mongo's `watch` row had sunglasses vanish and popcorn
  appear/disappear inconsistently across its 6 frames, which looked
  jarring on loop. Say explicitly (as in the Step 2 template) that
  anything held or worn must be present in every frame, not phased in
  partway through.
- **No UI elements baked into the art.** A real defect found this
  session: Mongo's `drag` row came back with an actual mouse-cursor icon
  drawn into two of six frames, as if illustrating "being dragged by the
  cursor" a little too literally. Say explicitly that only the character
  and background may appear - no cursors, icons, text, or watermarks.
- **Keep eye color/style identical across every frame of a row** (and
  ideally across rows) - another real defect: one frame of a row came
  back with a different eye color than the other five, for no apparent
  reason. Nothing prevents this proactively as reliably as the reference-
  image + per-row workflow above, but it's still worth explicitly
  checking on a fresh sheet.
- **Spell out every row's content explicitly and repeat the "only this
  creature" instruction anyway**, even with the reference-image workflow -
  drift is reduced, not eliminated. An earlier version of this project
  had an entirely unrelated animal appear on one row for no apparent
  reason. Always look over what comes back row by row rather than
  assuming it matched the brief.
- **The first and last frames of a loop are where drift shows up most**,
  for two different reasons. Cat (`dance`, `hacking`, `hot`) and Dinosaur
  (`dance`) both had their *first* frame missing a prop the character
  should already be holding - the model apparently treating "put it on"
  as part of the sequence rather than a constant across every frame,
  which pops jarringly every loop since a loop cuts straight from its
  last frame back to its first. Mongo's `watch` row (this session) had
  its *last* frame abandon the pose entirely instead - a different
  failure, but the same lesson: check both ends of a loop specifically,
  not just skim the row as a whole. A copy of a nearby good frame fixes a
  missing-prop first frame; a lower `frameCount` (dropping a broken last
  frame) is usually easier than trying to regenerate just one frame in
  isolation.
- Expect imperfect grid/strip alignment even within a single row (uneven
  frame spacing, a few stray pixels of a neighboring frame bleeding in) -
  a boundary-detection pass based on where the actual content is (rather
  than assuming even spacing) handles this more reliably than trusting
  the nominal frame width.

#### Frame counts, and why

More frames per row is just smoother motion at the same fps - there's no
code-side reason to stick to 6. Rough guidance based on standard 2D
animation practice, not just "whatever's cheap":

- **`walk`**: 8 is the classic convention for a readable walk cycle
  (contact - down - passing - up, twice) - noticeably smoother than 6,
  which tends to look like a shuffle.
- **`dance`**: 8, for the same reason - a rhythmic loop benefits from
  enough frames to read as "on the beat" rather than a slow wobble.
- **`idle`**: 5-8, a subtle breathing/blink loop - more than 8 is wasted
  detail for how understated this pose usually is.
- **`sleep`, `hacking`, weather loops (`hot`/`sunny`/`rainy`/`cold`)**:
  4-6, slow ambient loops where extra frames add little.
- **`watch`**: 5-6 - keep it on the lower end per the "last frame drifts"
  lesson above, and double-check the last one specifically before
  committing to a higher count.
- **One-shot (non-looping) rows** - `wake`, `answerCall`, `openMail`,
  `pickUp`, `eating`, `playing`, `lowBattery`, `snapshot`, `bonk`,
  `systemResume`, `milestone` - 3-6 frames
  covering a clear beginning-to-end arc (e.g. `wake`: eyes-closed →
  eyes-opening → alert) reads better than 2 abrupt extremes, but don't
  overdo it - these play once and move on, so smoothness matters less
  than for a loop you'll see for seconds at a time.

The row list to work through, with suggested frame counts baked in
(adjust freely - drop any row you don't want, an animation simply left
out of `character.json` is never used):

- `idle` (6 frames): a subtle breathing/blink loop
- `walk` (8 frames): a walk cycle, legs/body alternating
- `sleep` (5 frames): eyes closed, a "Zzz" or similar sleep cue
- `drag` (5 frames): being held/carried - startled/wide-eyed, swaying
  (no cursor icon - see the lessons above)
- `wake` (4 frames, one-shot): eyes opening, groggy to alert
- `dance` (8 frames): headphones or a musical cue, bouncing to a beat
- `watch` (5 frames): sunglasses/popcorn or similar, facing forward -
  the prop must appear in every frame, not partway through
- `answerCall` (4 frames, one-shot): a phone prop rising to an ear
- `openMail` (4 frames, one-shot): an envelope opening across frames
- `hacking` (6 frames): at a small screen/keyboard prop, sized so all
  limbs stay visible around it
- `pickUp` (3 frames, one-shot): a quick startled squish/flinch
- `hot` (5 frames): fanning itself with a non-green prop, a sweat drop
- `sunny` (5 frames): sunglasses, relaxed/happy
- `rainy` (5 frames): holding/using an umbrella
- `cold` (5 frames): bundled up, visible breath
- `eating` (4 frames, one-shot): a small snack/food prop
- `playing` (4 frames, one-shot): tossing/chasing something, a moment of fun
- `lowBattery` (4 frames, one-shot): a droopy, low-energy moment
- `snapshot` (3 frames, one-shot): a startled camera-flash reaction
- `bonk` (3 frames, one-shot): a startled/annoyed double-click reaction -
  a quick flinch or grumpy look, distinct from the plain idle pose a
  single click/pet reuses
- `systemResume` (4 frames, one-shot): the PC itself waking from sleep -
  eyes snapping open disoriented, a beat more startled than the calmer
  `wake` above
- `milestone` (4 frames, one-shot): a small celebration - a sparkle, a
  proud pose, a little cheer - for reaching an onscreen-time milestone

Once you have art back: check every row against the brief (frame by
frame, not just at a glance - see the lessons above), chroma-key out the
green, verify/fix frame boundaries, then drop the result in as
`Assets/Characters/<Name>/spritesheet.png` - the `row`/`frameCount`
values in `character.json` just need to match whatever grid the art
actually ended up with (see the row-remapping note in Fox's `NOTES.md`
for a real example of this not matching the brief on the first try, and
how it was fixed without regenerating).

### Adding new rows to an existing character (an "expansion sheet")

To add a new optional animation to a character that already has art
(`eating`, `playing`, `snapshot`, `lowBattery`, or any future one), there's
no need to regenerate the whole sheet - generate a small standalone sheet
with just the new poses (same character description and style as before,
for consistency), then paste its rows onto the bottom of the existing
`spritesheet.png` and point `character.json` at the new row numbers
(15 onward, continuing past the existing rows). Same green-background/
solid-black-outline/one-creature-only rules apply, just a shorter grid -
e.g. 6 columns x however many new rows you're adding this round.
