# Fox character - upload instructions

This folder is ready for the fox sprite sheet. To finish setting it up:

1. Upload the sprite sheet image here as **`spritesheet.png`** (replacing
   this note isn't necessary - `UPLOAD.md` can stay, it's not read by the
   app).
2. Ping me once it's in - I'll open it, clean up any tiles that aren't
   fully transparent, verify the actual frame pixel size and row layout
   against `character.json`, and adjust anything that doesn't match.

## What's already here

`character.json` is pre-filled assuming the sheet follows the same
6-column x 15-row layout as the AI-art prompt in the main README (idle,
walk, sleep, drag, wake, dance, watch, answerCall, openMail, hacking,
pickUp, cold, hot, sunny, rainy - in that order), at 64x64px per frame.

**Heads up from looking at the sheet you pasted in chat earlier:** a few
rows looked like they drifted from that spec - e.g. what should be "wake"
looked more like a second startled/drag pose, there seemed to be an extra
row of the fox reading a book (not one of our states), and the hot-weather
row showed a lollipop/ice pop rather than a fan. That's normal for
AI-generated sheets and expected - once the actual file is here I'll go
row by row, match what's really in each one to the closest animation (or
ask you to confirm ambiguous ones), and fix up `character.json` and the
image together so they agree.

## Try it once it's wired up

```
dotnet run --project src/DesktopGuy.App -- --character Fox
```
