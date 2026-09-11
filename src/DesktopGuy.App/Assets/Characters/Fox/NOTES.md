# Fox character notes

`spritesheet.png` is the uploaded AI-generated sheet, cleaned up (see
below). `spritesheet_raw.png` is the original upload, kept as a backup -
not used by the app, safe to delete if you don't want it around.

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

## Rows worth double-checking against what you actually wanted

The sheet mostly follows the intended 6-column x 15-row layout (see the
main README's AI-art prompt), but a couple of rows drifted from their
intended pose:

- **`cold`** (row 11): came out as the fox reading a book, not wearing a
  jacket/breathing condensation - it still triggers correctly when it's
  cold outside, it just doesn't visually read as "cold."
- **`hot`** (row 12): came out as holding a lollipop/ice pop rather than a
  fan - close enough thematically (cooling off) that this one's fine as is.

If you want either regenerated, the AI-art prompt in the main README
documents exactly what each row is supposed to show - re-run just those
rows through your art tool and splice them back in.

`wake` and `pickUp` originally had the same problem (an annoyed
expression and a sneeze, respectively, instead of "waking up" / "just
grabbed") - there's no AI image-generation tool available in this dev
environment, so rather than leave them mismatched, they were rebuilt by
recomposing the fox's own existing frames instead of new art: `wake` is a
dimmed "groggy" idle pose brightening into the normal idle pose, and
`pickUp` reuses `drag`'s already-good startled pose for frame 1 and a
squashed + motion-lined version of it for frame 2. If you'd rather have
real distinct art for these two, they're also in the AI-art prompt in the
README.

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
