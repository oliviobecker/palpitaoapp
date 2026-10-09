# Public standings link

Each **season** carries an auto-generated **public key** — 12 uppercase hex characters, shown as
`A7C3-9F2E-4BD8` and stored unhyphenated — that addresses a **read-only standings and scoring
audit** requiring no account:

```
/p/A7C3-9F2E-4BD8                                  → overall standings
/p?key=A7C39F2E4BD8                                → same, key via query string
/p/A7C3-9F2E-4BD8?rodada=18                        → that round's breakdown
/p/A7C3-9F2E-4BD8?rodada=10.2                      → part 2 of a round played in parts ([Absences](../domain-rules.md#absences))
/p/A7C3-9F2E-4BD8?rodada=18&participante=<userId>  → with that participant expanded
```

A link outlives regrouping: when the exact round is gone, the page (and the API) fall back to the
lowest visible part of the same number — `?rodada=10` shared before round 10 was split opens
`10.1`, and `?rodada=10.2` after the parts were merged back opens `10`.

**Publishing is off by default.** Every season has a key, but `Season.PublicStandingsEnabled`
starts `false` and the link answers **404** until an admin turns it on in *Admin → Seasons*. So
deploying this feature exposes nothing on its own. The admin can also **regenerate** the key
(`POST /api/seasons/{id}/public-key/regenerate`), which kills the previously shared link
immediately; both actions are audited (`SeasonUpdated`, `SeasonPublicKeyRegenerated`).

**What the link shows.** Two tabs:

- **Geral** — the official standings (position, name, points, rounds, absences, penalties,
  eliminated), with the podium and the same name tiles as the in-app screen. Opening a row reveals
  the gap to the leader and to the row above, plus a **round-by-round history** (one chip per
  scored round, showing the points, absence and Flávio markers); pressing a chip deep-links into
  that round's breakdown for that participant.
- **Rodada** — a round, sliced two ways. *Por participante* gives each player's per-match
  breakdown; *Por jogo* transposes it to show, for one match, what everybody predicted and scored,
  best first. The pivot is client-side — the round payload already carries both sides.

Every match line prints the prediction, the category ([Scoring](../domain-rules.md#scoring)), `base points × multiplier = points`,
and the rule context — classic pair, manual multiplier override, phase ([Multipliers](../domain-rules.md#multipliers)) — plus absence and
Flávio Rule ([Flávio Rule](../domain-rules.md#flávio-rule)) markers. A reader can mark one row as their own; the choice is kept in that
browser's `localStorage` and never leaves the device.

**What it never shows.** Only **closed** rounds (`Locked`/`Scored`) appear; `Draft`, `Published`
and `Cancelled` are invisible and requesting them returns 404. Predictions are therefore never
readable while they could still be copied. A `Scored` round reports exactly what the scoring pass
persisted (so the Flávio halving and absence penalties stay visible); a `Locked` round is computed
live from the results so far and flagged `isPartial`, without absences, elimination or the Flávio
Rule — the same rule as the temporary standings ([Refreshing results and temporary standings](fixtures-and-results.md#refreshing-results-and-temporary-standings)). No e-mail, no admin justification text.

⚠️ **Relation to [Prediction visibility](../domain-rules.md#prediction-visibility).** Publishing the link makes the predictions of closed rounds readable by
anyone holding it, **regardless of `AllowParticipantsToViewOthersPredictions`** — an audit that
hides the prediction explains nothing. The admin screen states this in full before the toggle.

**Staying out of search.** Three layers, because each covers a different thing: `robots.txt`
disallows `/p/`, the page itself sets a `noindex, nofollow` robots **meta tag** while it is open
(that is what de-indexes a URL somebody already pasted somewhere public — a crawler indexes the
HTML document, never the XHR), and the API responses carry `X-Robots-Tag` for completeness.

The Open Graph tags in `index.html` are deliberately **generic** (product name, product line,
`og-cover.jpg`) and name no group, season or participant: the messaging crawler that builds the
paste preview does not run JS and could never see a season anyway, and a card that carried names
would give away exactly what `noindex` protects.

**Getting the link to people.** Copying it out of *Admin → Seasons* is not where it is needed. When
a round is scored, the copy-ready WhatsApp closing message ([Creating a round by period and importing matches](fixtures-and-results.md#creating-a-round-by-period-and-importing-matches)) ends with a deep link to that
round's audit — `…/p/<key>?rodada=N` — so the group gets the numbers and the way to check them in
the same paste. The admin card also shows the full URL and offers **Pré-visualizar**, which opens
the public page in a new tab before anything is shared.

**Endpoints** (all anonymous, rate-limited per IP via `RateLimiting:Public`, and served with
`X-Robots-Tag: noindex, nofollow`):

| Endpoint | Returns |
|---|---|
| `GET /api/public/seasons/{key}` | group and season names, visible rounds, base-points ruleset |
| `GET /api/public/seasons/{key}/standings` | the official standings, each row with its scored-round history |
| `GET /api/public/seasons/{key}/rounds/{number}?part=N` | that round's per-participant breakdown (`part` for a part of a round played in parts; omit it for a standalone round) |

An unknown key, a malformed key and an unpublished season all return the **same 404**, so probing
cannot tell them apart.

**Multi-tenant note.** These endpoints carry `[IgnoreRequestGroup]`, so `RequestGroupContext`
reports no request group even if a signed-in browser sends `X-Group-Id` (which would otherwise
filter the season away and 404 a perfectly good link). With no request group the EF global filter
matches *every* group rather than none ([Groups (multi-tenant)](accounts-and-groups.md#groups-multi-tenant)), so `PublicStandingsService` derives the tenant from
the season the key resolved to and scopes every query explicitly, with `IgnoreQueryFilters()`.
The frontend also marks these calls `SKIP_TENANT_HEADERS` so neither the token nor the group is
sent at all.
