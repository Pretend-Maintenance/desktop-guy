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
clean, uniform 128x128-per-frame grid.

## Rows worth double-checking against what you actually wanted

The sheet mostly follows the intended 6-column x 15-row layout (see the
main README's AI-art prompt), but a few rows drifted from their intended
pose - they're wired up as-is (nothing crashes, they just don't quite
match their label) rather than left broken:

- **`wake`** (row 4, index from 0): came out as more of an
  annoyed/startled expression than "waking up sleepily."
- **`pickUp`** (row 10): came out as a sneezing pose rather than a
  startled "just been grabbed" reaction.
- **`cold`** (row 11): came out as the fox reading a book, not wearing a
  jacket/breathing condensation - it still triggers correctly when it's
  cold outside, it just doesn't visually read as "cold."
- **`hot`** (row 12): came out as holding a lollipop/ice pop rather than a
  fan - close enough thematically (cooling off) that this one's fine as is.

If you want any of these regenerated, the AI-art prompt in the main
README documents exactly what each row is supposed to show - you could
re-run just those rows through your art tool and splice them back in.

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Fox
```
