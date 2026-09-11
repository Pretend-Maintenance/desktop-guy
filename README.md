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
- **Terminal focused**: if a shell or terminal app (Command Prompt,
  PowerShell, Windows Terminal, PuTTY, ...) is the window you're actually
  using right now, it pulls up its own little terminal with scrolling
  matrix-style code. Checked against whichever window is focused, not just
  "is a terminal running somewhere" - otherwise launching the app from a
  terminal would leave it stuck showing this forever.
- **Weather**: dresses for the weather where you are, checked every 20
  minutes - a jacket and little breath clouds when it's cold, a hand fan
  when it's hot, sunglasses when it's clear and sunny, an umbrella when
  it's raining. See **Weather setup** below for how it figures out where
  "where you are" is.
- **Discord call**: an incoming Discord call makes it pick up a phone for a
  moment, then goes back to whatever it was doing.
- **Discord message**: a new Discord message makes it open an envelope for a
  moment, then resumes.

If more than one of these applies at once, momentary things (a Discord call
or message) always interrupt and play out fully before it resumes whatever
it was doing. Among the ongoing ones: a terminal being open wins over
video, which wins over music, which wins over weather, which wins over
just wandering/idling. Weather is the one exception to "suppresses sleep" -
it doesn't keep the character up, it just changes what idling looks like
while it applies. See the priority list at the top of
`Engine/CharacterController.cs` if you want to change any of that ordering.

The music/video awareness uses Windows' own "now playing" system (the same
thing behind the media controls on your lock screen), so it works with
whatever's actually playing without knowing about specific apps. Terminal
awareness checks which window currently has focus. Discord
awareness reads Discord's own notifications via Windows' notification
listener - see **Context awareness setup** below, since that one needs a
one-time permission grant. Weather awareness makes plain HTTPS calls to two
free services - see **Weather setup** below.

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

## Weather setup

Nothing to configure - it works out of the box, no API keys or accounts.
Under the hood it makes two plain HTTPS calls, no signup for either:

1. **[ipapi.co](https://ipapi.co)** - once, at startup, to turn your public
   IP address into a city-level location (latitude/longitude). This is
   *city-level*, not your exact address - the same accuracy any website
   gets just from you visiting it, nothing more precise.
2. **[Open-Meteo](https://open-meteo.com)** - every 20 minutes after that,
   to get the actual forecast for that location.

If you'd rather it not do the IP lookup at all (e.g. no internet access, a
firewall, or you just don't want it), it fails silently - weather reactions
just never trigger and everything else about the character is unaffected.
There's currently no config file to hand-enter a location instead; if you'd
rather have that than the automatic IP lookup, that's a small follow-up
change (swap `WeatherWatcher`'s IP-lookup step for a fixed latitude/longitude
read from character.json or a settings file).

"Cold"/"hot" are temperature thresholds you can tune per character in
`character.json` under `behavior.coldThresholdCelsius` /
`behavior.hotThresholdCelsius` (Blob defaults to 5°C / 25°C). "Rainy" is
based on the forecast's weather code (drizzle, rain, showers, or storms all
count). "Sunny" is clear skies during daytime that isn't already cold or hot.

## Project layout

```
src/DesktopGuy.App/
  Engine/               <- character-agnostic behavior engine (state machine,
                            idle detection, sprite animation, window plumbing)
  Characters/            <- CharacterDefinition model + loader for character.json
  Context/                <- context awareness: now-playing media (music/video),
                            running terminal processes, weather (via
                            IP geolocation + Open-Meteo), and Discord
                            notifications (call/message) - all optional and
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
   - `cold` / `hot` / `sunny` / `rainy` - current weather
   - `answerCall` - an incoming Discord call
   - `openMail` - a new Discord message
   - `pickUp` - the moment you grab it (falls back straight to `drag` if omitted)
3. Add a `character.json` next to it (copy `Blob/character.json` as a
   starting point) describing:
   - `frameSize`: pixel width/height of a single frame in the sheet
   - `scale`: how many times to scale the sprite up on screen (pixel art
     usually wants 3-6x so it isn't tiny)
   - `animations`: for each animation name, which sprite-sheet `row` it's
     on, how many frames (`frameCount`), how fast to play them (`fps`),
     and whether it `loop`s (`wake`, `answerCall`, `openMail` and `pickUp`
     are non-looping - they play once and then move on; everything else loops)
   - `behavior`: idle timeout, wander timing, speech timing, and the
     `coldThresholdCelsius` / `hotThresholdCelsius` weather cutoffs
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
