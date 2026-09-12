# Dave character notes

`spritesheet.png` is the cleaned-up sheet actually used by the app.
`spritesheet_raw.png` and `spritesheet_expansion_raw.png` are the
original uploads, kept as backups - not used by the app, safe to delete
if you don't want them around.

## Expansion sheet (rows 15-18)

Added `eating`, `playing`, `lowBattery`, and `snapshot` as a separate
6x4 sheet, appended onto the bottom of the existing 15-row sheet. No
grid lines this time (content-gap detection instead), and no cleanup
issues at all - came out clean on the first pass, no patches needed
(unlike Fox/Cat's versions of this same expansion, which both needed a
battery-icon fix in `lowBattery`).

A black pug with a cute underbite, generated from the AI-art prompt
template in the main README, same green-background/grid-line approach as
Cat and Dinosaur.

## Solid black fur needed two adjustments from the standard cleanup

- **`strip_gridlines()` was skipped entirely.** That step blanks out any
  row/column within a cell that's mostly dark and opaque, on the
  assumption it's a leftover grid line - a reasonable assumption for
  Fox/Cat/Dinosaur, but dangerous here: a solid black dog can itself hit
  that fraction at some rows of its own body, so keeping that step
  would've risked eating chunks out of Dave rather than a grid line.
  Skipped in favor of just trusting the precisely-detected grid line
  positions plus a margin - safe since those are found directly on this
  sheet (the model drew its own grid, same as Cat/Dinosaur), not guessed.
- **The background-removal brightness floor was lowered (150 -> 80).**
  This sheet's green background turned out not to be perfectly flat -
  it has a subtle vignette, darker in some cells than others - and the
  usual G>150 floor was failing on the darker patches, leaving them
  opaque with an oddly dark, speckled look (very visible in the mostly-
  empty `wake`/`pickUp` trailing cells, where it was obviously wrong
  rather than just an edge artifact). Lowering the floor is safe for this
  character specifically: nothing in a black pug's actual palette (black
  fur, pink nose/tongue, white eye highlights) comes anywhere close to
  being green-dominant, so the hue-ratio checks alone (green clearly
  ahead of both red and blue) are enough on their own without also
  requiring a minimum brightness.

Both of these are sheet-specific judgment calls, not new defaults -
they're safe here because of what Dave's own colors are, not something to
copy automatically onto the next character without checking whether the
same reasoning actually applies.

## No frame patches needed this time

Unlike Cat and Dinosaur, no looping animation had its defining prop
missing on frame 1 (dance/hacking/hot all stay consistent frame-to-frame
here) - nothing to patch.

## Frame counts

Every animation came back with a full 6 frames except `wake` and
`pickUp` (2 each, per the prompt) - no partial rows like Cat/Dinosaur's
5-frame `idle` this time.

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Dave
```
