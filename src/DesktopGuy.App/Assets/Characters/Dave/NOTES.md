# Dave character notes

`spritesheet.png` is the cleaned-up sheet actually used by the app.
`spritesheet_raw.png`, `spritesheet_expansion_raw.png`, `spritesheet_raw2.png`,
and `spritesheet_raw3.png` are the original uploads, kept as backups - not
used by the app, safe to delete if you don't want them around.

## v3: a cleaner single-shot regeneration, used for the base 15 rows

After v2 (below) read as too somber and had a stray phone prop drifting
into `watch`, this replaced the base 15 rows (`idle` through `cold`)
again with a single-shot 6x15 grid generation - closer to the *original*
Dave prompt (a proper uniform grid) than v2's per-row-strips-composited-
loosely approach, and it came out both more complete and more
consistent: `answerCall` and `sunny` (missing entirely from v2) are back
with a full 6 frames each, `rainy` has all 6 frames with the umbrella
held throughout (v2 only had 1 static frame), and `watch`'s popcorn stays
present from frame 2 onward instead of flickering. The upload already
had real alpha transparency (not a flat green background) - only needed
an edge despill pass (any pixel where green was still the dominant
channel got clamped down to the stronger of red/blue) rather than a full
chroma-key removal.

`eating`/`playing`/`lowBattery`/`snapshot` (the expansion rows) and
`bonk`/`systemResume`/`milestone` weren't part of this regeneration -
they're not in the older 15-row template this used. Reused as-is from
before: the expansion rows from the last commit prior to any of this
session's Dave changes (`6a6a9c0`), and bonk/systemResume/milestone from
v2 (those three still read fine mood-wise, unlike the base rows) -
copied directly into rows 15-21 of the new sheet, since they were
already clean 84x84 cells needing no reprocessing.

**Known gap:** `hot` has no fan prop at all in this version (just tongue-
out panting and a sweat drop) - the original brief asked for one held
throughout. Not patched; would need a targeted regeneration of just that
row to fix properly.

**Processing bug found and fixed after first shipping this version:**
the very first pass cropped `spritesheet_raw3.png` assuming a uniform
6x15 grid (dividing the sheet's width/height evenly) - reported back as
"his legs are on his head." The rows in this upload are *not* evenly
spaced (measured directly: row heights ranged 74-99px, not a flat
~109px each), so a uniform division cut across actual row boundaries,
grabbing a sliver of the row above/below into each frame. Re-processed
using the same per-frame connected-component detection technique as v2
(find each dog's own tight bounding box directly via `scipy.ndimage.label`
on the alpha channel, rather than assuming any grid spacing) - this
sheet already had real alpha transparency, which made that detection
reliable without needing a chroma-key pass first. **Lesson for next
time:** never assume a "clean-looking" uniform grid actually has uniform
cell spacing - measure the real content boundaries directly, even when
an upload looks tidy at a glance.

## v2 history (superseded by v3 above for the base rows - the hidden-legs fix and the bonk/systemResume/milestone rows it introduced still stand)

Replaced the entire sheet using the new "one reference image, then one
row at a time" workflow from the README's AI-art template, specifically
to fix the `hacking` row hiding his legs (see "Known limitation" below,
now resolved) and the general animation-quality issues the original
single-shot-grid sheet had. The new upload (`spritesheet_raw2.png`)
wasn't a uniform grid - it's the individually-generated row strips
arranged loosely into a two-column layout with uneven gaps, not a fixed
column/row count - so the usual "assume an evenly-spaced grid" cropping
didn't apply. Processed instead with connected-component detection
(`scipy.ndimage.label` on a green-background chroma mask) to find each
individual frame's own tight bounding box directly, then cropped each
frame to its own bbox + 8px margin (clamped to image bounds), padded to
square, and resized to 84x84 - avoids the bleeding-between-rows artifact
a single fixed-size crop window produced when tried first (rows were
only 13-20px apart, closer than a sensibly-sized shared window).

What actually came back, and what changed from the original brief:
- **`answerCall` and `sunny` weren't generated this round** - no phone-
  call or plain-sunglasses-relaxed row anywhere in the upload. Left out
  of `character.json` entirely (same as any other character missing an
  optional row) rather than faked - add them later with their own
  per-row generation whenever convenient.
- **One frame got merged into its neighbor** by the chroma-mask
  connected-component detection (`lowBattery` frames 1-2 were touching,
  same silhouette blob) - split by hand at the visual midpoint once
  found, rather than needed for every character, just this one pair.
- **`sleep` and `wake` both got generated with an overlapping "curled up,
  eyes closed" starting pose** (my per-row wake prompt explicitly asked
  for that as frame 1, same as the sleep prompt) - used the cleanest
  pure-sleep 3-frame group (no alert ending) for `sleep`, and a separate
  4-frame group that runs all the way to a clear sitting-up-alert last
  frame for `wake`. A leftover pair of "wide awake, sitting" frames from
  the same generation batch went unused rather than duplicating `wake`.
- **Frame counts came back lower than the per-row prompts asked for** in
  most rows (e.g. `idle` asked for 6, got 5; `dance`/`walk` asked for 8,
  got 6) - used whatever was actually generated rather than padding
  artificially. `hacking` is the one exception that came back *larger*
  than asked (7, combined from two separate laptop-pose generations that
  turned out consistent enough to use together) - kept all 7 for a
  smoother loop.
- **`watch`'s popcorn prop still isn't in every frame** (present in 2 of
  6) - the exact "prop must persist every frame" lesson from the
  prompt template, not fully followed by this generation. Left as-is
  rather than re-patching by hand this time; a stricter re-prompt for
  just this row would fix it properly if it reads as jarring in practice.
- **`rainy` only came back with 1 frame** (just the umbrella pose, no
  variation) - kept as a single static frame rather than dropped
  entirely, so the row still exists; a fuller multi-frame `rainy` needs
  its own regeneration.

## v1 history (superseded by v2 above - kept for the general cleanup lessons, which still apply)

### Expansion sheet (rows 15-18)

Added `eating`, `playing`, `lowBattery`, and `snapshot` as a separate
6x4 sheet, appended onto the bottom of the existing 15-row sheet. No
grid lines this time (content-gap detection instead), and no cleanup
issues at all - came out clean on the first pass, no patches needed
(unlike Fox/Cat's versions of this same expansion, which both needed a
battery-icon fix in `lowBattery`).

A black pug with a cute underbite, generated from the AI-art prompt
template in the main README, same green-background/grid-line approach as
Cat and Dinosaur.

### Solid black fur needed two adjustments from the standard cleanup

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

### No frame patches needed this time

Unlike Cat and Dinosaur, no looping animation had its defining prop
missing on frame 1 (dance/hacking/hot all stay consistent frame-to-frame
here) - nothing to patch.

### Frame counts

Every animation came back with a full 6 frames except `wake` and
`pickUp` (2 each, per the prompt) - no partial rows like Cat/Dinosaur's
5-frame `idle` this time.

### Known limitation: `hacking` never showed his legs - fixed in v2

Reported as "legs cut off" after seeing it run - checked both
`spritesheet.png` and `spritesheet_raw.png` directly (cropped each frame
of row 9 out and looked at the actual pixels), and it wasn't a cropping
bug: the raw, unprocessed art already had him drawn sitting low behind
the laptop with his lower body/paws entirely hidden behind it in all six
frames, consistently. No extra artwork just outside the crop to reveal by
nudging it - the background was clean right down to the frame boundary.
Fixed in v2 by regenerating the row via the per-row Gemini workflow with
an explicit "size and position the prop so all limbs stay visible"
instruction - the new `hacking` row shows all four paws in every frame.

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Dave
```
