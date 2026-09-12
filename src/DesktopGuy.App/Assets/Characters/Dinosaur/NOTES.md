# Dinosaur character notes

`spritesheet.png` is the cleaned-up sheet actually used by the app.
`spritesheet_raw.png` is the original upload, kept as a backup - not used
by the app, safe to delete if you don't want it around.

Generated from the AI-art prompt template in the main README, same as
Cat - green background, drawn-in grid lines (used directly for boundary
detection, same approach as Cat's `clean_cat.py`/`clean_dino.py`, not
checked into the repo).

## The turquoise skin needed a different background-removal strategy

Cat and Fox's colors (orange, beige/black) sit far from green in hue, so
a simple "is green the dominant channel" test cleanly separated character
from background. This dinosaur's turquoise/pale-cyan skin sits much
closer to green - reusing Cat's exact cleanup script caused two different
failures before landing on what's actually in the sheet now:

- **A hue-ratio "fringe" pass (catches anti-aliased edges blended with
  the green background) misfired broadly**, treating large stretches of
  the pale belly as fringe and eating chunks out of it, since the belly's
  color genuinely satisfies "green-ish" by a hue-ratio test. Replacing it
  with a check against the exact sampled background color didn't fully
  fix this either - still misfired on some belly pixels.
- **The genuinely useful fix turned out to be different: per-pixel noise
  baked into the source art** occasionally tips an isolated belly pixel
  over the strict background threshold on its own (confirmed by checking
  raw pixel values directly - this isn't a cleanup-logic bug, the noise
  is really there). A `binary_opening` pass (erode then dilate, `scipy.
  ndimage`) on the background mask clears these isolated specks before
  removal. The strength matters a lot: 2 iterations also erased a
  genuine, small (3px-tall) sliver of real background between the legs,
  which then showed up as an ugly opaque gray bar under the feet in every
  pose - 1 iteration is gentle enough to leave that real gap alone while
  still clearing most of the noise. A handful of tiny (1-3px) specks
  still get through even at 1 iteration - a minor, barely-visible
  imperfection left as a reasonable tradeoff rather than chasing a
  perfect result.

**Takeaway for future characters:** if a character's skin/fur color is
itself green-adjacent (teal, lime, olive...), expect this and budget time
for it - a color further from green (like Cat's or Fox's) has a much
wider safety margin and doesn't need any of this.

## A prompt-writing mistake: don't ask for a green prop

The "hot" row's brief (from the README template) asked for the dinosaur
"fanning itself with a small leaf" - a green leaf on a green background
gets chroma-keyed out along with the background, because there's nothing
to tell them apart. The leaf is gone entirely in three of this row's six
frames, leaving small pale artifact patches (highlight/shading pixels
that weren't quite green enough to remove) near where a hand should be
holding it. Left as-is rather than patched - the pose still reads fine
as "sunglasses + a sweat drop," just without the leaf itself, and the
artifacts are small enough to be barely noticeable at normal display
size. If you want this fixed properly, regenerate just that row with a
non-green prop (a small paper fan, like Cat's version, works fine).
**Worth updating the main README's template prompt too**, so this
mistake doesn't get repeated for the next character - any hand-held prop
suggestion should specify a color other than green.

## Frame patched for consistency

Same issue as Cat's sheet: `dance` (row 6) had no headphones on frame 1,
which would've visibly popped in on every loop - replaced with a copy of
frame 2. Everything else that varies frame-to-frame (the popcorn box in
`watch`, sun-ray marks in `sunny`) is a decorative flourish on top of a
constant core pose, not a loop-breaking issue - left as-is.

## Frame counts that don't match a full 6

Same as Cat: `idle` came back with 5 frames, `wake`/`pickUp` with 2 -
`character.json` reflects that.

## Try it

```
dotnet run --project src/DesktopGuy.App -- --character Dinosaur
```
