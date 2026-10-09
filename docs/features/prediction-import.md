# Entering predictions for participants

Most of the pool sends its predictions as WhatsApp messages. Admins enter them by hand or import
screenshots through OCR — always reviewed before anything is saved.

## Manual entry by the admin

In a round (admin → **Round detail → Enter predictions**, route
`/admin/rounds/:id/manual-predictions`) the admin picks a participant, fills in the score of
**all** matches and saves. Endpoint: `POST /api/admin/rounds/{roundId}/predictions/manual`.
A match the participant has no prediction for (a first entry, or a line the OCR import missed)
starts **empty** and must be typed — saving is refused while any score is empty, so the screen
never saves a 0x0 the admin did not enter.

- **The admin ignores the deadline.** The board often only gets the WhatsApp screenshots in after
  the round's deadline, and nothing closes a round on its own, so admin entry follows the
  **status** alone (`Common/AdminPredictionWindow`, shared with the OCR import in [OCR import (Tesseract)](#ocr-import-tesseract)):

  | Round status | Admin entry (manual and OCR) |
  |---|---|
  | `Published`, `Locked` | open — before or after the deadline, no override needed |
  | `Scored` (**Finalize round**) | refused (`adminPrediction.roundScored`): **reopen** the round (back to `Locked`, scores kept), enter, then **Finalize** again, which re-scores it |
  | `Draft` / `Cancelled` | refused (`adminPrediction.roundNotPublished` / `roundCancelled`) |

  Participants still close at the deadline, one minute before the first kickoff ([Overview](../domain-rules.md#overview)). The manual and OCR screens show the reason when
  entry is refused, with a link back to the round.
- **Override**: `allowAfterDeadline` + justification is now only needed for an **eliminated**
  participant (recorded in the AuditLog). The field keeps its old name so the API contract does not
  move; the screen offers the justification when the selected participant is eliminated.
- If predictions already exist, `overwriteExisting = true` is required (confirmation).
- Predictions are marked with **`Source = AdminManual`** and `CreatedBy/UpdatedBy`.
- **Flávio Rule**: what the admin enters is stamped with the time it is entered ([Flávio Rule](../domain-rules.md#flávio-rule)). Once a
  leader's special deadline has passed, the manual and OCR screens say so and point at the round's
  **Flávio rule** panel, where a prediction that arrived on time over WhatsApp can be exempted.

## OCR import (Tesseract)

Admin → **Import from image** (`/admin/rounds/:id/import-predictions`): upload a screenshot
(PNG/JPG/JPEG/WEBP, ≤ 10 MB), the backend processes it with **Tesseract**, generates prediction
**candidates** (participant + match + score), the admin **reviews/corrects** them and only then
**confirms**. Confirmed predictions are marked with **`Source = AdminOcr`**.

**A whole round at once.** Several screenshots can be picked (or dropped) together. They are sent
one at a time — OCR is CPU-bound and the endpoint is throttled per admin; a throttled upload waits
out the window (`Retry-After`) and goes again — and each becomes its own import. The page then lists
the round's **pending imports** with their participant and how many rows still need a look: one with
nothing flagged can be confirmed straight from the list (or all of them with **Confirm ready ones**),
anything flagged is opened for review first.

**Name each file after its participant** (`Renato.png`, `Renato1.jpeg`, `DeCastro.jpeg`): the file
name is the admin's own label, so it decides whose predictions the rows are — ahead of anything OCR
reads off the image. It is cut at the first digit, camel case is split, and generic names
(`IMG_2041`, `WhatsApp Image …`, `9.png`) are ignored, which leaves the header to decide. Resolution
is stricter than for OCR text: a learned alias, the same name, whole words of it (`Renato` for
`Renato Paiva`), or one wrong letter in a longer word (`Cabacao`) — never a bare substring. When a
header in the image names somebody else, the rows keep the file's participant but go to review.

Flow (never saves without review):
`upload → OCR → candidates → review → confirm`.
Endpoints: `POST /api/admin/rounds/{id}/predictions/import-image` (per-admin rate limited,
`RateLimiting:Ocr`), `GET /api/admin/ocr-imports/{batchId}`,
`PUT /api/admin/ocr-imports/{batchId}/candidates/{candidateId}`,
`DELETE .../candidates/{candidateId}` (discard a noise candidate),
`POST .../confirm`, `POST .../cancel`. A confirmed batch is immutable (confirm/cancel/edit
return 4xx), and confirm rejects duplicate participant+match candidates.

**Overwrites are announced.** A confirm replaces whatever the participant already has for those
matches and keeps no copy of the old values, so a screenshot filed under the wrong person silently
replaces that person's own round — which is what happened in production (round 8: Cabaça's
screenshot filed under Coelho). A batch still under review therefore reports, per participant,
how many predictions they already have in the round and how many of its rows would change one
(`overwrites` on the batch, `overwriteCount` on the pending list). Rows that restate the stored
score — the same screenshot imported again — or fill a match with no prediction are not counted.
The review screen shows the warning and asks before confirming, and such a batch is never
confirmed straight from the pending list (**Confirm ready ones** skips it).

The import follows the same **status window as manual entry** ([Manual entry by the admin](#manual-entry-by-the-admin)): it works before or after the
deadline on a `Published` or `Locked` round, and is refused on a `Scored`, `Draft` or `Cancelled`
one. Both the **upload** (before any batch or image is stored) and the **confirm** check it, since a
round can be finalized while a batch sits in review. Editing, discarding and cancelling candidates
stay open — they write no predictions — so a batch left from before can still be cleaned up.

### Install/configure Tesseract

The `Tesseract` NuGet package ships the native libraries. The **language files**
(`traineddata`) are gitignored (~38 MB), so a fresh clone has none. **Local setup:**

1. Download `por.traineddata` and `eng.traineddata` from
   https://github.com/tesseract-ocr/tessdata
2. Place them in **`backend/tessdata/`** (`backend/tessdata/por.traineddata`,
   `backend/tessdata/eng.traineddata`). See [backend/tessdata/README.md](../../backend/tessdata/README.md).
3. The path can be overridden via `Ocr:TessdataPath` (env `Ocr__TessdataPath`). On `dotnet run`
   (dev), point it to the absolute path of `backend/tessdata`.

**Deployed environments need no manual step** — the deploy workflows download the same two models
(pinned to a `tessdata` commit and checksum-verified) into `backend/tessdata/` before
`dotnet publish`, so they ride along in the published output. Do **not** copy them onto the server
by hand: the deploy mirrors the publish folder with `robocopy /MIR`, which purges anything that is
not in it. `GET /api/health/ocr` reports whether the models are in place (`503` naming the missing
codes when they are not).

### How a screenshot is read

The page is read **three times** — the original bytes, a preprocessed copy, and that copy inverted
— and the reading that **resolves the most fixtures of the round** wins; Tesseract's own confidence
only breaks ties. Confidence alone kept a reading with fewer fixtures on 6 of 51 real screenshots
(557 of 575 fixtures instead of 565). The readings also check each other: a score they read
differently goes to review with the readings named (`1x1 / 0x1`). That is not a vote — on one
screenshot two of the three readings got `Lincoln 1x1` as `0x1`, and a majority would have kept the
wrong one. Preprocessing turns the image grayscale,
**enlarges** it towards ~1100px wide (capped at 4×) and binarizes it, because a phone screenshot of
a single chat bubble arrives ~300px wide with ~9px glyphs, which Tesseract otherwise reads as noise;
larger screenshots are left alone. The inverted copy is there because a dark-mode bubble is
white-on-dark, the reverse of what Tesseract expects, and nothing in the bytes says which theme the
sender uses.

**The original competes on purpose.** Binarization is a lossy bet, and on a dark-mode screenshot
whose text is grey on near-black a global Otsu threshold flattens most of the page into the
background. Measured on a 391×712 dark-mode screenshot: the original read every one of its 23
fixtures at 83% confidence, the binarized copy one at 66%, and the binarized-and-inverted copy
three at 67%. While only the prepared copies competed, that import came back with **three** rows
out of twenty-three and looked like a parser bug. The log names every reading and its confidence
for exactly this reason, and then which one was kept — when an import comes back short, those lines
say how each variant read. `Ocr:Preprocess = false` drops the two prepared readings.

**Measure before changing any of this.** `OcrSamplesTests` runs a folder of real screenshots
through the real engine and the real import and prints what came out per image (fixtures resolved,
participant, rows flagged, lines left out). It is skipped unless `OCR_SAMPLES_DIR` points at the
folder — `<round>/fixtures.txt` (`Home | Away` per line) next to that round's screenshots, and an
optional `participants.txt` — because the screenshots carry real people's names and stay out of the
repository. `OCR_TESSDATA` points at the models and `OCR_SAMPLES_REPORT` also writes the report to a
file. Two deduced fixes shipped here and missed; the measured ones did not.

Before any comparison, **`m` is rewritten as `rn`** on both sides. Those are the same handful of
pixels at screenshot resolution, so Tesseract returns `Blackbum`, `Bumley` and `Boumesmouth` for
Blackburn, Burnley and Bournemouth — each two edits from its club, one past the budget, and the
budget cannot grow because Barnsley and Burnley are two edits apart as well. Rewriting both sides
makes the pair identical without giving anything else more room; it is a normalisation, like accent
folding, not a guess. Across ten real screenshots of one round it recovered seven rows. The names
are compared without the rewrite as well, because OCR also makes the reverse mistake — it adds an
`m` (`Bournemmouth`, `Blackburmn`), which is one edit away as written and two once rewritten. The
learned alias key deliberately does **not** get this treatment — it is stored, and rewriting it
would orphan every alias a group already has.

Name matching is strict first (exact, then containment, then the alias map). Only when *nothing*
matched does it retry allowing **one wrong character** — `Coventy` → `Coventry City`, `Joao` →
`João` — and only when that retry finds exactly one fixture. That retry also goes *through* the
alias map, because the short names the group message prints are nowhere near their club by edit
distance: `Weolves` is one edit from `Wolves`, which is how it reaches Wolverhampton Wanderers. A
name that already matched two fixtures stays ambiguous; the tolerance never breaks a tie.
`OcrShortNameRoundTripTests` sweeps the whole seeded catalogue to prove no two clubs collide under
that budget, and that no alias reaches a club that is not its own.

**Approximate matches.** The small WhatsApp Desktop screenshots (~9px text) garble one side of a
line beyond any one-edit budget — `Torrenham 2 x 1 Aston Villa`, `Newcastle 3 x 1 Hll`,
`Lmon 1 x 0 Bradford`. A last tier resolves those through the side that still reads cleanly: a club
plays once per round, so that side alone pins the fixture (home against home, away against away),
and the other side only has to be plausible for the one club it sits against (within 40% of its
length, measured against the club's distinctive words and short names). It refuses when the
"garbled" side is really another club — `Brentford 1x1 Nottingham` from another round's list is not
a misreading of `Brentford x Tottenham` — and `OcrShortNameRoundTripTests` sweeps every pair of
clubs to hold that line. It only runs with the club catalogue at hand, and every row it resolves
goes to review, pre-filled, with a note saying so.

**Reading a WhatsApp screenshot.** What surrounds the scores is furniture, and the parser is built
to ignore it: the clock stamped on every screenshot (`Cardiff 1x2 Wrexham 17:35`) is stripped before
anything else — its colon otherwise reads as `Name: content` and swallows the fixture — as are the
emphasis markers and quotes around a name (`*Flavio*`, `"Careca"`), the day separators (`Hoje`)
and the app's own vocabulary.

The participant is read from `PALPITES <nome>` (the line the group actually writes, ALL-CAPS
included) or from `<Nome>, Rodada N`, where the comma is optional and the keyword itself may carry
one wrong character — `Tniaqgo, Kodada 1` is a real header, and a capital R read as a K used to
throw the whole name away. The two arrive combined often enough (`PALPITES TB, Rodada 1`) that the
round is peeled off before the name is judged; left in, its comma and digit fail the shape check and
the header is lost, so the sender's contact name at the top of the bubble wins instead. The season
title in that same shape is rejected rather than filed as a person.

A **bare** name-shaped line only stands until one of those two announces a name. OCR returns
`Championshio`, `Leaque One` and `Premier Leaque` for the headings, which are exactly the shape of a
person, and each one used to take the participant away for every row below it — and to be learned as
a permanent group alias on confirm. Headings are therefore recognised with up to two wrong
characters, and no word of the round message itself (`Palpitão`, `Rodada`, `Regra`, `Nome`,
`palpitar` — the tail of the Flávio rule once the bubble wraps it) is ever a name.

`Nome: fixtures` still labels the fixtures on its line, but only when what follows the colon really
holds a fixture: the message's own `REGRA FLÁVIO: @Diego tem até 24 horas` line used to name the
participant of every row below it, and a `(×2)` tag OCR read as `(:2)` used to turn the Luton line
into a name. Initials are accepted in a round header (`CN Rodada 9`, `TB Rodada 8`), and the `Nome`
placeholder the message prints is dropped when it is left in (`Nome, Decastro, Rodada 9` →
`Decastro`; an untouched `Nome, Rodada 5` names nobody).

A score whose zeros OCR returned as the letter `O` on **both** sides is accepted when it stands
alone as its own token (`Norwich O x O West Brom`), while `Arsenal x Leeds` — where the same letters
are stolen from the ends of two club names — stays rejected. The separator may come back doubled
(`Millwall 2xX1 Norwich`): that is one glyph read twice, not two scores — and so may a zero right
against it (`Bolton OxO0 Cardiff`). A score read off any letter other than `O` (`bxO0` was a `5x0`)
is a guess, and goes to review unless another reading got the same score from real digits. Across a
colon or a dash, a digit glued to another digit is part of a longer number — a phone number next to
a non-contact sender (`+55 00 91234-0476`), a mangled time — never a score.

**Two rounds in one screenshot.** Some people send the next round's predictions in the same message
(`Luís, Rodada 6 e 7`, one header over both lists). A line that is one of the season's *other*
fixtures, read cleanly, is left out of this round's import and counted, and the upload says so
("11 lines from another round (round 7) were left out"); import the same screenshot into the other
round for those. A line that fits no round at all stays for review — a prediction is never dropped
just because it could not be read.

A fixture the bubble wrapped onto a second line (`Birmingham 2 x 0` / `Bristol City`) is stitched
back together before anything reads the lines, and only when the two halves really form a fixture —
the top half carries the score, so the bottom half must carry none. The orphan half is not merely a
lost row: it is name-shaped, so left alone it becomes the participant and takes every fixture below
it with it. A competition heading or a `PALPITES` line sitting under a dangling score is never
swallowed.

**Learned participant aliases.** When an admin confirms a batch after correcting who a name belongs
to, that correction is remembered per group (`OcrParticipantAliases`) and consulted on the next
import, so a nickname the roster does not carry (`Careca` → `TB`) is only fixed once. Only
names that resolve to **nobody** on their own are stored — never one that already points at a
participant, or a mislabelled `Luis.jpg` filed under Diego would take Luís's own name away from him
— only names shaped like a name and not a word of the round message, and only when every row
bearing that name agreed on the same participant; a later confirmation re-points an alias that
turned out to be wrong. When the file name names someone, it is the file name that is learned
(`CN.jpeg` for a member registered as Caio Nunes), and the names read off the image are left alone.

They are not a black box: **Admin → Apelidos** (`/admin/ocr-aliases`) lists what the group has
learned, re-points an alias at another participant, deletes one, and teaches one by hand before any
screenshot has needed it. The alias text itself is immutable — it is the normalized key the lookup
runs on, so changing it means deleting and creating. `OcrAliasService` owns the table for both the
import and the screen, which is what keeps the normalization and the one-meaning-per-group rule in
a single place.

### Limitations and why review is needed

OCR is heuristic: it depends on the image quality and the text format. The parser recognizes
common formats (`Arsenal 2x1 Chelsea`, `Maria: Arsenal 1-0 Chelsea`, `Bento - Arsenal 2 Chelsea 1`)
and tries to match names/abbreviations (`Man City`, `Spurs`...), but **any uncertain item is marked
`NeedsReview` and is never saved without admin confirmation** — hence the mandatory review screen.
A complete row can be flagged too, and the card says why: readings that disagreed on the score, a
score read off a letter, an approximate match, or a header naming someone other than the file.
Filing rows under a participant ("apply to all") does not clear those flags; touching the score or
the fixture does.
