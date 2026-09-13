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
- **Affection**: total time it's spent onscreen is tracked per character,
  cumulative across restarts. At a few thresholds (5 min, 30 min, 1 hour,
  4 hours, 1 day, 1 week, 30 days) it says something special once, from
  `affectionPhrases` if the character defines any, or a generic line
  otherwise. Petting it (a click that isn't a drag) is unrelated to this -
  that always just pops up a normal phrase from `phrases`.
- **Position memory**: it remembers roughly where you left it (per
  character) and starts there next time, instead of always the same corner.
- **Music**: if something is playing that looks like music (Spotify, YouTube
  Music, etc.) it puts on headphones and dances instead of wandering off -
  and if the track title is available, it announces it once ("~ Now
  playing: ...") the moment it starts, not on every beat.
- **Video**: if something is playing that looks like a video (a YouTube tab,
  Netflix, ...) it puts on sunglasses, grabs popcorn, and settles in to
  watch - same one-off "now playing" announcement as music, when a title's
  available.
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
  while unplugged ("Battery's getting low..."), and another the moment it
  reaches 100% while plugged in ("Battery's fully charged!") - each only
  fires once per transition, not repeatedly while it stays low/full. If a
  character also defines a `lowBattery` animation, that pose is mixed into
  the same idle-surprise rotation as `eating`/`playing` while the battery's
  actually low (see "Adding a new character" below) - desktops with no
  battery just never trigger any of this.

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
  for a bit without exiting the app). The tray icon and its menu keep
  working even while the character itself is hidden, either manually or by
  the fullscreen behavior below.
- **Fullscreen auto-hide**: if whatever you're focused on (a game, a video
  player, a presentation) is genuinely fullscreen - its window covers the
  entire monitor - the character hides itself automatically rather than
  floating on top of it, and reappears the moment you're back to something
  windowed. This is a heuristic (the same rough "focused window exactly
  fills the monitor" check several taskbar-autohide-style tools use), not
  a true "is this app in exclusive fullscreen mode" API call, so a
  borderless-windowed app that happens to size itself to the full screen
  will also trigger it - which in practice is the same case you'd want it
  to hide for anyway. Everything (onscreen-time tracking, the tray icon,
  ambient state changes) keeps running normally while hidden; only the
  window itself stops being drawn.
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
   - `morningPhrases` / `eveningPhrases` / `lateNightPhrases`: optional
     extra lines mixed in alongside `phrases` at the matching time of day
   - `seasonalPhrases`: optional, keyed by `"halloween"` / `"christmas"` /
     `"newYear"` - extra lines mixed in only during those windows
   - `affectionPhrases`: optional - special lines it might say once a
     cumulative-onscreen-time milestone is reached (see "How it behaves" above)
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
   grid is assumed evenly spaced, e.g. 6 columns x 19 rows for a sheet with
   every optional animation.
4. Click Import.

The app chroma-keys the green background out itself, despills any faint
green edge-tint the same way the hand-processed characters get cleaned up,
re-grids everything onto the standard 84x84 frame size, and writes out a
ready-to-use `character.json` (rows are assigned to animation names in the
same fixed order used throughout this doc - `idle`, `walk`, `sleep`,
`drag`, `wake`, `dance`, `watch`, `answerCall`, `openMail`, `hacking`,
`pickUp`, `hot`, `sunny`, `rainy`, `cold`, `eating`, `playing`,
`lowBattery`, `snapshot` - so a sheet with fewer rows just gets fewer
animations, same as leaving them out of a hand-written `character.json`).
It starts with a small set of generic phrases and no time-of-day-specific
ones; edit `character.json` afterwards to personalize those; there's no
in-app editor for phrases.

This only handles the common case cheaply - it expects an evenly-spaced
grid and a plain, roughly-flat green background. A messier upload (a
non-green-adjacent background color, hand-drawn grid lines, a background
with a strong vignette or gradient, a palette that's itself green-adjacent
like Mongo's turquoise skin) will come out with visible cleanup artifacts
or outright fail the size check, and needs the manual, AI-assisted process
described above instead.

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

Here's a reusable template for handing a new character off to an AI image
generator, refined from actually doing this for Fox (see
`Assets/Characters/Fox/NOTES.md` for the specific problems this template's
wording is designed to head off). Four big lessons baked in:

- **Ask for a solid green background, not transparency.** Most image
  generators (this was tested with Gemini) don't produce real alpha - they
  either bake in a fake checkerboard "this is transparent" pattern as
  actual opaque pixels, or leave faint colored fringing at edges. A solid,
  unambiguous green background is trivial to chroma-key out reliably in a
  cleanup pass afterward (any decent image library - Pillow, ImageMagick -
  can do this; ask an AI coding assistant for a script if you don't want to
  write one), and it's the one color guaranteed to never appear in the
  character's own design, so removal is unambiguous.
- **Ask for solid black outlines specifically, not just "an outline."**
  Without that, the character's edge often comes back anti-aliased into
  the green background - a thin ring of green-tinted pixels rather than a
  clean line - which then needs a spill-suppression pass (clamp the green
  channel down wherever it's the dominant one on a kept pixel) to fix up
  afterward. Asking for the outline color explicitly reduces how much of
  that shows up in the first place, though the cleanup script should still
  expect and handle some.
- **Spell out every row's content explicitly and repeat the "only this
  creature" instruction.** Without that, generators can and do drift -
  wrong colors on one frame, or entirely unrelated content on a row (an
  actual result on this project's second Fox sheet: a row that was
  supposed to be a startled reaction came back as an unrelated bear, for
  no apparent reason). Explicit per-row descriptions and a repeated
  constraint make this less likely, but always look over what comes back
  row by row rather than assuming it matched the brief.
- **Never ask for a green prop.** A real mistake made writing an earlier
  version of this template: it suggested a dinosaur character fan itself
  with "a small leaf" for the hot-weather row. A green leaf on a green
  background gets chroma-keyed out along with the background, since
  there's nothing to tell them apart - the prop was just gone in the
  result, leaving faint artifact patches where it used to be. Keep any
  held/worn prop a color clearly different from the background green
  (the umbrella, phone, and laptop in this template's own row list are
  all fine - none of them are green). Also worth knowing: a character
  whose own skin/fur color is itself green-adjacent (teal, lime, olive)
  needs a more careful cleanup pass than one whose colors sit far from
  green (like Fox's orange or Cat's beige/black) - see the "turquoise
  skin" section of `Assets/Characters/Dinosaur/NOTES.md` for what that
  actually took to get right.

Also expect imperfect grid alignment (uneven row/column spacing, a few
stray pixels of a neighboring cell bleeding into another) - a boundary
detection pass based on where the actual content is (rather than assuming
even spacing) handles this more reliably than trusting the nominal grid;
ask an AI coding assistant to write that if you're not comfortable with
image processing directly. This template describes a 6-column x 15-row
layout, one animation per row - adjust the column/row counts if you want
more/fewer frames per animation or to skip some optional animations:

> Sprite sheet, 6 columns x 15 rows, one grid cell per pose, each cell
> approximately [FRAME SIZE]x[FRAME SIZE] pixels with consistent, even
> spacing between all rows and columns. Solid flat green background
> (like a green-screen), the same exact green in every cell - no gradients,
> shadows, or texture in the background. Character centered in each cell,
> consistent size, proportions, color palette, and art style in every
> single cell across the whole sheet - the character must look like the
> same individual throughout, never a different creature, animal, or
> object in any cell.
>
> Character: [DESCRIBE THE CHARACTER - species/shape, color palette,
> face/eyes, distinguishing features, size proportions, no more than a
> couple of sentences]. Style: [e.g. "flat cartoon shading" or "crisp
> hard-edged pixel art, no anti-aliasing"] with solid black outlines
> around the character - never colored or blended into the green
> background - pick one style and keep it identical across every cell.
>
> Each row is one animation, frames left to right (leave any unused
> trailing cells in a row blank, still green background):
> - Row 1 (X frames): idle - [describe a subtle idle loop, e.g. breathing/blinking]
> - Row 2 (X frames): walking - [a walk cycle, legs/body alternating]
> - Row 3 (X frames): sleeping - [eyes closed, a "Zzz" or similar sleep cue]
> - Row 4 (X frames): being dragged by the cursor - [startled/wide-eyed, swaying]
> - Row 5 (X frames): waking up - [eyes opening, groggy to alert]
> - Row 6 (X frames): dancing to music - [headphones or a musical cue, bouncing]
> - Row 7 (X frames): watching a video - [sunglasses/popcorn or similar, facing forward]
> - Row 8 (X frames): answering a phone call - [a phone prop rising to an ear]
> - Row 9 (X frames): opening an envelope/message - [the envelope opening across frames]
> - Row 10 (X frames): typing/at a computer - [a small screen/keyboard prop]
> - Row 11 (X frames): just picked up - [a quick startled squish/flinch]
> - Row 12 (X frames): it's hot outside - [fanning itself with a
>   non-green prop, a sweat drop]
> - Row 13 (X frames): it's sunny outside - [sunglasses, relaxed/happy]
> - Row 14 (X frames): it's raining - [holding/using an umbrella]
> - Row 15 (X frames): it's cold outside - [bundled up, visible breath]
>
> Remember: every cell shows the exact same character described above,
> just in a different pose - no unrelated creatures, objects, or scenery.

Fill in `[FRAME SIZE]`, the character description, style, and per-row
frame counts (`X`) before using it, and drop any rows you don't want (an
animation left out of `character.json` is simply never used). Once you
have art back: check it row by row against the brief, chroma-key out the
green, verify/fix row and column boundaries, then drop the result in as
`Assets/Characters/<Name>/spritesheet.png` - the `row`/`frameCount` values
in `character.json` just need to match whatever grid the art actually
ended up with (see the row-remapping note in Fox's `NOTES.md` for a real
example of this not matching the brief on the first try, and how it was
fixed without regenerating).

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
