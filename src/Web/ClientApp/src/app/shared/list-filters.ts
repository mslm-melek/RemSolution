import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { fromDateInput } from './form-utils';

// The list screens take their filters from the query string.
//
// The home tiles and the dashboard's counts are links, and each one counts a
// subset — running rentings, unconfirmed requests, unpaid expenses — so the link
// carries the filter that produced the figure and the list opens showing exactly
// the rows that were counted. Keeping the filters in the URL rather than in a
// component field also means a filtered list survives a reload, can be shared,
// and is cleared by the plain navigation link in the menu.

/** `?flag`, `?flag=true`, `?flag=1` — all true; `false`/`0` — false. */
export function boolParam(params: ParamMap, key: string): boolean | null {
  const raw = params.get(key);
  if (raw === null) return null;
  if (raw === '' || raw === 'true' || raw === '1') return true;
  if (raw === 'false' || raw === '0') return false;
  return null;
}

/**
 * A numeric enum member, read from its name ("InProgress" — what the links use,
 * so the URL stays readable) or from its number.
 */
export function enumParam(params: ParamMap, key: string, members: object): number | null {
  const raw = params.get(key);
  if (!raw) return null;

  const byName = (members as Record<string, unknown>)[raw];
  if (typeof byName === 'number') return byName;

  // TypeScript's numeric enums carry the reverse mapping, so an unknown number
  // is one that has no member.
  const numeric = Number(raw);
  return Number.isInteger(numeric) && (members as Record<number, unknown>)[numeric] !== undefined
    ? numeric
    : null;
}

/** The member name of a numeric enum value, for writing it back to the URL. */
export function enumName(members: object, value: number | null | undefined): string | null {
  if (value === null || value === undefined) return null;
  return (members as Record<number, string>)[value] ?? null;
}

/**
 * A `yyyy-MM-dd` param, read as UTC midnight — the instant the API's half-open
 * windows are built from, and the one the dashboard measures its period over.
 */
export function dateParam(params: ParamMap, key: string): Date | null {
  const raw = params.get(key);
  if (!raw || !/^\d{4}-\d{2}-\d{2}$/.test(raw)) return null;
  return fromDateInput(raw) ?? null;
}

/**
 * A window as a chip reads it. The raw params are used rather than the parsed
 * dates: they are already `yyyy-MM-dd`, and reformatting a UTC midnight through
 * the local calendar would show the day before in a negative offset.
 */
export function rangeText(from: string | null, to: string | null): string {
  if (from && to) return `${from} → ${to}`;
  return from ? `≥ ${from}` : `< ${to}`;
}

/**
 * A filter that arrived by link and has no control of its own on the screen.
 * Rendered as a removable chip, so the list always says why it is showing fewer
 * rows than the user expects.
 */
export interface FilterChip {
  /** The query params this chip stands for; clearing it drops all of them. */
  params: string[];
  /** Transloco key under `filters.*`. */
  labelKey: string;
  labelArgs?: Record<string, unknown>;
}

/**
 * Puts the list's filters in the URL. The whole query string is replaced, so a
 * param left out is a filter cleared, and the components' own queryParamMap
 * subscription is what actually reloads the rows. `replaceUrl` keeps filtering
 * from stacking up entries the back button has to walk through.
 */
export function applyListFilters(router: Router, route: ActivatedRoute, params: Params): void {
  router.navigate([], { relativeTo: route, queryParams: params, replaceUrl: true });
}

/** The same URL with the given params dropped — how a chip's ✕ is served. */
export function withoutParams(params: ParamMap, drop: string[]): Params {
  const kept: Params = {};

  for (const key of params.keys) {
    if (!drop.includes(key)) kept[key] = params.get(key);
  }

  return kept;
}
