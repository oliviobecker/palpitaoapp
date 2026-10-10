# ADR 0005: OCR imports are always reviewed before anything is saved

- **Status:** Accepted (recorded retroactively, October 2026)
- **Area:** backend, frontend, OCR

## Context

Many participants send their predictions as WhatsApp messages, and admins used to type them in by
hand. Importing screenshots through OCR saves that work, but OCR on phone screenshots is
heuristic: tiny glyphs, dark-mode bubbles, club names garbled by one or two letters, a sender's
phone number that looks like a score. A wrong prediction silently stored under a participant
changes the standings, and nobody would notice until the round is scored.

## Decision

- An import never writes predictions directly. It produces **candidates** (participant + match +
  score) that an admin reviews, corrects and confirms; only the confirmation writes, through the
  same status window and all-matches rule as manual entry.
- Every uncertain row is flagged with the reason — readings that disagreed on a score, a score read
  off a letter, an approximate club match, a header naming someone other than the file's participant
  — and a batch that would overwrite existing predictions says so before confirming.
- Matching is deliberately strict and **measured**: improvements to the reader or the matcher are
  checked against real screenshots (`OcrSamplesTests`, opt-in because the images carry real names
  and never enter the repository) before shipping, and a sweep over the club catalogue proves no
  two clubs collide under the tolerated edit budget.

## Consequences

- OCR is an accelerator, never an authority: the worst case is a row the admin fixes, not a wrong
  score in the standings.
- The review screen is mandatory work for the admin, which is why clean rows can be confirmed in bulk
  and only flagged ones need a look.
- A confirmed batch is immutable and learned name aliases are only stored when every row agreed, so a
  mislabelled screenshot cannot teach the system a wrong person.
