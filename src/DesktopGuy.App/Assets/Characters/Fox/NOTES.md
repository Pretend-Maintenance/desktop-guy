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
turned out to be ~109x109px natively; everything's been resampled onto a
clean, uniform 96x96-per-frame grid, displayed at 2x scale (192x192px on
screen - matches Blob's size).

That resolution/scale combo isn't arbitrary: an earlier version used
128x128 frames at 1.5x scale, which caused a visible rendering glitch (a
stray fragment of a neighboring frame floating above the character) -
non-integer display scales are a known source of that kind of edge bleed
with nearest-neighbor sprite cropping in WPF. 96px @ 2x avoids it by
keeping the scale a whole number. If you regenerate any art for this
character, keep `scale` in `character.json` an integer.

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

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Fox
```
