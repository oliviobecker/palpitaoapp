/**
 * The public standings link. The key is stored (and matched by the API) as a bare run of
 * hex characters; the hyphens exist only so a human can read it out loud or copy it from a
 * message without losing their place.
 */

/** Groups the stored key as XXXX-XXXX-XXXX. Display only — the API normalizes either form. */
export function formatPublicKey(key: string): string {
  return (key.match(/.{1,4}/g) ?? [key]).join('-');
}

/**
 * Absolute URL of a season's public page. Pass `round` to land straight on that round's
 * audit — the whole point of sharing the link right after a round closes. It is the round's
 * label: 41, or "10.2" for a part of a round played in parts (see `roundLabel`).
 */
export function publicStandingsUrl(key: string, round?: number | string | null): string {
  const base = `${window.location.origin}/p/${formatPublicKey(key)}`;
  return round == null ? base : `${base}?rodada=${round}`;
}
