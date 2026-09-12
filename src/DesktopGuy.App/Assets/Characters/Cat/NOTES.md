# Cat character notes

`spritesheet.png` is the cleaned-up sheet actually used by the app.
`spritesheet_raw.png` is the original upload, kept as a backup - not used
by the app, safe to delete if you don't want it around.

The character is an original mascot built from tortoiseshell-Persian
breed traits (fluffy flat face, large yellow eyes, black/beige/cream
coat) - not modeled on any specific named character.

## Generation and cleanup

This sheet came out unusually clean on the first try - generated directly
on a solid green background per the AI-art prompt template in the main
README, with the model additionally drawing its own black grid lines
between cells (not asked for, but a nice bonus: made boundary detection
more reliable than guessing from content gaps, since the grid lines are
found directly rather than inferred).

The cleanup script (`clean_cat.py`, run outside the repo - not checked
in) works differently from Fox's because of that grid:

1. Detect the grid line positions directly (rows/columns that are >90%
   near-black pixels), rather than detecting low-content gaps like Fox's
   sheets needed. A handful of false positives came up where a dark prop
   (sunglasses, a laptop) happened to span most of a row by coincidence -
   filtered out by keeping only detections close to where an evenly-spaced
   15-row/6-column grid would put them.
2. Crop each cell a few pixels in from its detected grid lines (to fully
   exclude the line itself), then chroma-key out the green background -
   safe to do globally since green never appears in the cat's palette.
3. Apply the same green-spill suppression used on Fox's sheet (clamp the
   green channel down wherever it's the dominant channel on a kept pixel)
   to clean up faint tinting on the black outlines.
4. Resize each cleaned cell onto a uniform 84x84 grid.

## Frame counts that don't match a full 6

Per the AI-art prompt's per-row instructions, `idle` came back with 5
frames (not 6) and `wake`/`pickUp` with 2, matching what was asked -
`character.json` reflects that (the unused trailing cells in those rows
are just blank/transparent, harmless to leave in the sheet).

## Frames patched for consistency

Three looping animations had their defining prop missing on the first
frame (a leftover of the model treating "put it on" as part of the
sequence rather than drawing every frame mid-loop) - since a loop cuts
straight from its last frame back to its first, this reads as the
prop visibly popping in and out every cycle rather than something an
initial glance across the whole sheet catches:

- `dance` (row 6): frame 1 had no headphones - replaced with a copy of
  frame 2 (which has them).
- `hacking` (row 10): frame 1 had no laptop - replaced with a copy of
  frame 2.
- `hot` (row 12): frames 1-2 had no fan - replaced with copies of frame 3
  (fan visible, no sweat drop yet, so it still reads as an early frame).

Everything else that varies frame-to-frame (popcorn box in `watch`,
sun-ray marks in `sunny`, breath puffs in `cold`) is a decorative
flourish that comes and goes on top of a constant core pose (sunglasses,
scarf, etc.), not the sole identifying prop for that state - left as-is,
reads as natural motion rather than a glitch.

`answerCall` doesn't have this problem since it's non-looping (frame 1
showing surprise before the phone rises in frame 2 is a nice beat, not a
loop artifact).

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Cat
```
