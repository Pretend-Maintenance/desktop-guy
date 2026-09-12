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
  with a line picked from `phrases` in its character.json.
- **Drag**: click and hold to pick it up - a quick startled "pickUp" hop
  plays, then it follows the cursor with a little wobble. Drop it above the
  ground and it gently falls back down; drop it on the ground and it's ready
  to wander again.
- **Position memory**: it remembers roughly where you left it (per
  character) and starts there next time, instead of always the same corner.
- **Music**: if something is playing that looks like music (Spotify, YouTube
  Music, etc.) it puts on headphones and dances instead of wandering off.
- **Video**: if something is playing that looks like a video (a YouTube tab,
  Netflix, ...) it puts on sunglasses, grabs popcorn, and settles in to watch.
- **Terminal focused, or you're typing**: if a shell or terminal app
  (Command Prompt, PowerShell, Windows Terminal, PuTTY, ...) is the window
  you're actually using right now, or you're actively pressing keys
  anywhere, it pulls up its own little terminal with scrolling matrix-style
  code - both reuse the same animation, since there's no separate art for
  "typing in a terminal" vs. "typing anywhere else". Terminal focus is
  checked against whichever window is actually focused, not just "is a
  terminal running somewhere" - otherwise launching the app from a terminal
  would leave it stuck showing this forever.
- **Discord call**: an incoming Discord call makes it pick up a phone for a
  moment, then goes back to whatever it was doing.
- **Discord message**: a new Discord message makes it open an envelope for a
  moment, then resumes.

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
being open wins over video, which wins over music, which wins over just
wandering/idling. See the priority list at the top of
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
   - `watch` - a video is playing
   - `hacking` - a terminal/shell is open
   - `cold` / `hot` / `sunny` / `rainy` - weather poses, shown only via the
     right-click menu's Preview Weather submenu (not triggered automatically)
   - `answerCall` - an incoming Discord call
   - `openMail` - a new Discord message
   - `pickUp` - the moment you grab it (falls back straight to `drag` if omitted)
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
4. In the `.csproj`, files under `Assets/Characters/**` are already
   configured to copy to the output folder automatically - no project file
   changes needed.
5. Run it with `dotnet run --project src/DesktopGuy.App -- --character
   YourCharacterName`.

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

### Generating real art for Blob

If you want to hand the placeholder off to an AI image generator to make
real pixel art, here's a ready-to-use prompt sized for the current 64x64,
6-column x 15-row layout (adjust the column/row counts if you add/remove
animations first, and check the frame boundaries after generating - AI
image models are inconsistent about exact grid alignment, so you'll likely
need to nudge frames into place, or generate row-by-row/frame-by-frame and
assemble the sheet yourself for the cleanest alignment):

> Pixel art sprite sheet, 6 columns x 15 rows, each cell exactly 64x64
> pixels, transparent background, crisp hard-edged pixel art (no
> anti-aliasing/blur), consistent character size and pixel scale in every
> cell, character centered in each cell.
>
> Character: a small round teal/cyan blob creature, big white oval eyes
> with black pupils, tiny simple mouth, soft pink blush marks on the
> cheeks, no arms or visible limbs except small stubby feet - a cute,
> minimal desktop-pet mascot style (think a cross between a Tamagotchi and
> a slime).
>
> Each row is one animation (frames left to right, unused trailing cells
> in a row left blank/transparent):
> - Row 1 (4 frames): idle - gentle breathing bob, blinks on the last frame
> - Row 2 (4 frames): walking - bounces with a squash-and-stretch step,
>   little feet alternate
> - Row 3 (4 frames): sleeping - eyes closed, breathing, small "Zzz" text
>   appears and fades
> - Row 4 (2 frames): being dragged by the cursor - wide surprised eyes,
>   leans left then right
> - Row 5 (2 frames): waking up - eyes half-open, then fully open
> - Row 6 (4 frames): dancing - wearing headphones, bouncing side to side,
>   a music note appears
> - Row 7 (4 frames): watching a video - sunglasses on, holding a small
>   popcorn box, chewing
> - Row 8 (3 frames): answering a phone call - a phone rises to its ear,
>   surprised then talking
> - Row 9 (3 frames): opening an envelope - the flap opens across the
>   frames, a letter peeks out on the last frame
> - Row 10 (4 frames): "hacking" - sunglasses on, a small terminal/monitor
>   prop with scrolling green code
> - Row 11 (2 frames): just picked up - a quick startled squish, small
>   motion lines above its head
> - Row 12 (4 frames): cold weather - wearing a small jacket, shivering,
>   visible breath/condensation puffs from its mouth
> - Row 13 (4 frames): hot weather - waving a small hand fan, a sweat drop
>   drips down
> - Row 14 (4 frames): sunny weather - sunglasses on, small sun-ray marks
>   in the corners, happy bounce
> - Row 15 (4 frames): rainy weather - holding a small umbrella overhead,
>   raindrops falling around it
>
> Style: flat colors, limited palette (teal blob body, a few accent
> colors for props), clean 1-2px black or dark outlines, no gradients or
> soft shadows, game-ready sprite sheet.

Once you have art back, drop it in as
`Assets/Characters/Blob/spritesheet.png` (or a new character's folder) -
the row/frame layout in `character.json` just needs to match whatever grid
the art actually ended up with.
