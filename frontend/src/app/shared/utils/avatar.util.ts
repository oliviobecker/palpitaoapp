/**
 * The name tile used wherever a participant is listed. Shared so the same person gets the
 * same two letters and the same colour on the dashboard, the standings and the public link
 * — three screens that used to carry three near-identical copies of this.
 */

/** Initials for the avatar tile, e.g. "Joao Silva" -> "JS". */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
  return (first + last).toUpperCase();
}

/** Deterministic colour per name: no storage, and stable across sessions and devices. */
export function avatarColor(name: string): string {
  let hash = 0;
  for (const ch of name) {
    hash = (hash * 31 + ch.charCodeAt(0)) % 360;
  }
  return `hsl(${hash}, 52%, 42%)`;
}
