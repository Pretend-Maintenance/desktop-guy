# Fox character notes

`spritesheet.png` is the uploaded AI-generated sheet, cleaned up (see
below). `spritesheet_raw.png` and `spritesheet_raw_v2.png` are the two
original uploads, kept as backups - not used by the app, safe to delete
if you don't want them around.

## Second pass: the v2 (green-screen) regeneration

You regenerated the whole sheet on a solid green background instead of
the checkerboard one, since that's a much more reliable target for
chroma-keying out cleanly than trying to guess which gray pixels are
"real" background. That's now the basis for `spritesheet.png` - same
84x84 grid, but with genuinely clean edges (no green fringe) and, as a
bonus, every row now has up to 6 usable frames instead of 4-5, so
animations play noticeably smoother.

A few things came up processing it:

- **Row 10 (`pickUp`) had an unrelated bear in it** - an AI generation
  slip, not fox content at all. That row was skipped entirely during
  processing; `pickUp` still uses the earlier recomposed frames (see
  below), untouched.
- **A hole in the headphone band let the green background show through**
  on the `dance` row - a gap in the raw art itself, not a cleanup bug.
  Since green never appears anywhere in the fox's real color palette (a
  key advantage of chroma-keying over the old checkerboard approach),
  cleanup could safely remove it globally rather than only from the
  cell's outer edge, which fixed this automatically.
- **The weather rows came out correct, just shifted by one.** The actual
  art order in the sheet is fan-cooling / sunglasses-in-the-sun /
  umbrella-in-rain / coat-with-cold-breath, but the previous
  `character.json` mapped those rows to `cold`/`hot`/`sunny`/`rainy` in
  sheet order, which was wrong. Row numbers in `character.json` were
  remapped to match what's actually drawn in each row instead of moving
  the art around - `hot` -> row 11, `sunny` -> row 12, `rainy` -> row 13,
  `cold` -> row 14. All four now visually match their state.
- **The regenerated `wake` row wasn't usable** - frame 0 rendered in a
  completely different muted brown/gray palette (another generation
  slip), and the remaining frames weren't in a clean sleepy-to-alert
  order. Left the earlier recomposed version in place for this row
  instead (see below).

## What got fixed

Most cells were fine (properly transparent), but a chunk of frames -
mainly in `sleep`, `cold`, `sunny`, and `rainy` - had a literal gray/white
checkerboard pattern baked in as opaque pixels instead of real
transparency (an AI-art export quirk, not real alpha). Those are now
properly transparent, matching the rest of the sheet. Frame size also
turned out to be ~109x109px natively, and not perfectly evenly spaced
(rows range ~100-119px tall); everything's been resampled onto a clean,
uniform 84x84-per-frame grid, displayed at 2x scale (168x168px on screen -
a bit smaller than the original 96px/192px version, and a whole-number
scale, see below).

Two follow-up bugs turned up after testing on an actual Windows machine,
both from the cleanup script rather than the original art, now fixed:

- **A stray fragment floated above the character.** The first cleanup
  pass assumed the sheet's rows were evenly spaced and cropped each cell
  with simple even division - since they're not evenly spaced, a sliver
  of the row above got caught in some cells and baked permanently into
  the cleaned file. Fixed by detecting each row's actual boundary from
  the real low-content gaps between rows instead of assuming even spacing.
  (A 128px-frame/1.5x-scale version was tried in between and also showed
  this - that was a red herring: the real cause was the file itself, not
  the display scale. Non-integer scale is still worth avoiding on general
  principle - keep `scale` in `character.json` a whole number - but it
  wasn't what caused this.)
- **The headphones (in `dance`) went transparent.** The cleanup script's
  background removal treated all near-black pixels as "probably
  background", which turned out to be wrong - there's no real opaque
  black background anywhere in this sheet (true background is either
  already properly transparent, or the checkerboard artifact above,
  which is light/mid gray, never black). The headphones are dark
  charcoal and sit close to the frame's top edge, so they got swept away
  by the same border-connected-region logic meant for the checkerboard.
  Fixed by only ever treating near-neutral gray/white as background,
  never near-black - real character details (headphones, pupils,
  sunglasses, terminal bezels) stay untouched now.
- **Thin dark lines at some cell edges.** The original upload has faint
  leftover grid-divider lines baked into the art - both vertical and
  horizontal - at several of the actual cell boundaries, matching the
  literal grid lines visible in the very first version of this sheet you
  shared. They sit at inconsistent distances from the boundary from one
  edge to the next, so a fixed trim margin wasn't reliable (a 3px margin
  missed some, a 7px margin started cutting into character art
  elsewhere). Fixed properly with a dedicated pass: any row or column
  that's almost entirely dark is stripped as a line, regardless of where
  it falls - a solid dark bar spanning nearly the full width/height of a
  cell isn't something any real character pose produces (even dark props
  like sunglasses or headphones only ever cover part of a row), so this
  is a safe, specific signature to target directly instead of guessing
  margins.

## Rows worth knowing about

All four weather rows now visually match their state (see the v2 section
above for the row-remapping that fixed this) - no outstanding mismatches
there.

`wake` and `pickUp` are still hand-recomposed rather than real generated
art - there's no AI image-generation tool available in this dev
environment, and neither the original nor the v2 upload produced usable
content for these two (an annoyed expression / a sneeze the first time,
an off-palette frame and a jumbled frame order the second time). Rather
than leave them mismatched, they're built by recomposing the fox's own
existing frames: `wake` is a dimmed "groggy" idle pose brightening into
the normal idle pose, and `pickUp` reuses `drag`'s already-good startled
pose for frame 1 and a squashed + motion-lined version of it for frame 2.
Both are only 2 frames (the rest of their rows are left transparent and
unused) - if you'd rather have real distinct art for these two at the
same 6-frame smoothness as everything else, the AI-art prompt in the
README documents exactly what each should show.

## Smoother animation

`character.json` has `"smoothTransitions": true` - the engine now
cross-fades between frames instead of hard-cutting, which makes the
existing 4-ish poses per animation read as noticeably smoother motion
without needing more art. This is a general engine feature (see
`smoothTransitions` in the main README), not something specific to this
character - Blob leaves it off since a cross-fade looks wrong against
blocky pixel art, but it suits Fox's smooth-shaded style well.

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Fox
```
