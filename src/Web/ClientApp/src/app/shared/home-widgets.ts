import { CurrentUserDto } from '../web-api-client';
import { AuthService } from './auth.service';

// What an agency user can pin to their home screen. Most are shortcut tiles — a
// count plus a link into the list it counts; a `panel` one is a whole block
// rendered under the tile row instead (the calendar).
//
// `key` MUST match the server's Domain.Constants.HomeWidgets constants: the
// user's choice is stored by key and the API refuses one it does not know. The
// rest is presentation, which stays here — the same split the navigation uses,
// where the feature/permission pairs are also SPA-side literals.

export interface WidgetGate {
  feature: string;
  permission: string;
}

export interface HomeWidgetMeta {
  key: string;
  /** Transloco key under `home.widgets.*`. Says what the number is, not just which list. */
  labelKey: string;
  icon: string;
  /** Where the tile leads. Unused by a `panel` widget, which links nowhere itself. */
  link?: string;
  /**
   * The filter the tile's figure was counted with (see home.component's
   * `countOf`). A tile that counts a subset opens the list showing that subset
   * and nothing else — the lists read their filters from the query string.
   */
  queryParams?: Record<string, string>;
  /**
   * Rendered as a block under the tile row rather than as a count tile in it, so
   * it carries no figure and no link. Mirrors HomeWidgets.Panels on the server,
   * which exempts these from MaxPinned for the same reason: they do not compete
   * for the row's space.
   */
  panel?: boolean;
  // Any one satisfied pair makes the tile offerable. Only the paperwork-layout
  // screen needs two: either document module opens it (as in the nav's config menu).
  gates: WidgetGate[];
  /**
   * Reference data — offered to agency administrators only, mirroring the
   * navigation's Configuration menu. The screens themselves accept either
   * administrator role, so this never hides something an admin could use.
   */
  adminOnly?: boolean;
}

const gate = (feature: string, permission: string): WidgetGate[] => [{ feature, permission }];

// Order matters twice: it is the order the customize panel offers them in, and
// the order a never-customized home falls back to.
export const HOME_WIDGETS: HomeWidgetMeta[] = [
  {
    // The month of pickups and returns. Gated on the overview permission and not
    // on the booking modules, exactly as its query is (see GetBookingCalendarQuery):
    // it crosses rentings and reservations, and half a calendar is worse than none.
    key: 'Calendar', labelKey: 'home.widgets.calendar', icon: 'calendar_month',
    panel: true, gates: gate('Dashboard', 'Dashboard.View')
  },
  {
    key: 'Cars', labelKey: 'home.widgets.cars', icon: 'directions_car',
    link: '/car', gates: gate('Cars', 'Car.Read')
  },
  {
    key: 'Reservations', labelKey: 'home.widgets.reservations', icon: 'event_available',
    link: '/reservation', queryParams: { status: 'PendingConfirmation' },
    gates: gate('Reservations', 'Reservation.Read')
  },
  {
    key: 'Rentings', labelKey: 'home.widgets.rentings', icon: 'vpn_key',
    link: '/renting', queryParams: { state: 'InProgress' },
    gates: gate('Rentings', 'Renting.Read')
  },
  {
    key: 'Clients', labelKey: 'home.widgets.clients', icon: 'group',
    link: '/client', gates: gate('Clients', 'Client.Read')
  },
  {
    key: 'Chat', labelKey: 'home.widgets.chat', icon: 'forum',
    link: '/chat', queryParams: { unread: 'true' }, gates: gate('Chat', 'Chat.View')
  },
  {
    // The unsettled-expense count opens the finance screen's payable tab narrowed
    // to those rows. Gated on the expense module and not on Credits: the figure
    // itself is counted with the expense list query (see home's countOf), so a
    // credits-only user would get a tile that could never show its number.
    key: 'Expenses', labelKey: 'home.widgets.expenses', icon: 'payments',
    link: '/credit', queryParams: { tab: 'expenses', unpaid: 'true' },
    gates: gate('Expenses', 'Expense.Read')
  },
  {
    key: 'Credits', labelKey: 'home.widgets.credits', icon: 'request_quote',
    link: '/credit', gates: gate('Credits', 'Credit.Read')
  },
  {
    key: 'Brands', labelKey: 'home.widgets.brands', icon: 'sell',
    link: '/brand', gates: gate('Cars', 'Car.Read'), adminOnly: true
  },
  {
    key: 'CarModels', labelKey: 'home.widgets.carModels', icon: 'category',
    link: '/model-car', gates: gate('Cars', 'Car.Read'), adminOnly: true
  },
  {
    key: 'ExpenseTypes', labelKey: 'home.widgets.expenseTypes', icon: 'receipt_long',
    link: '/expense-type', gates: gate('Expenses', 'Expense.Read'), adminOnly: true
  },
  {
    key: 'ExtraServiceTypes', labelKey: 'home.widgets.extraServiceTypes', icon: 'add_shopping_cart',
    link: '/extra-service-type', gates: gate('ExtraServices', 'ExtraService.Read'), adminOnly: true
  },
  {
    key: 'DocumentTemplates', labelKey: 'home.widgets.documentTemplates', icon: 'description',
    link: '/document-template',
    gates: [
      { feature: 'Contracts', permission: 'Contract.Read' },
      { feature: 'Factures', permission: 'Facture.Read' }
    ],
    adminOnly: true
  }
];

// What a user who has never customized their home sees — the day's work, not the
// reference data. Any of these the user cannot reach is simply left out.
export const DEFAULT_HOME_WIDGETS = ['Calendar', 'Cars', 'Reservations', 'Rentings', 'Clients'];

// Mirrors HomeWidgets.MaxPinned on the server, which rejects a longer list.
// Counts tiles only, like the server's validator: see `panel` above.
export const MAX_HOME_WIDGETS = 8;

/** How many of these keys count against MAX_HOME_WIDGETS. */
export function countTiles(keys: string[], available: HomeWidgetMeta[]): number {
  return keys.filter(key => !available.find(w => w.key === key)?.panel).length;
}

/**
 * The tiles this user could pin: feature enabled for the agency, read permission
 * held, and — for reference-data screens — an administrator. Same rule the
 * navigation applies, so a tile is never offered for a screen that would 403.
 */
export function availableHomeWidgets(user: CurrentUserDto, isAgencyAdmin: boolean): HomeWidgetMeta[] {
  return HOME_WIDGETS.filter(widget =>
    (!widget.adminOnly || isAgencyAdmin) &&
    widget.gates.some(g => AuthService.canAccessModule(user, g.feature, g.permission)));
}

/**
 * The stored choice, kept to what the user can currently reach and in their
 * order. A pinned tile whose feature was switched off (or whose permission was
 * revoked) drops out here rather than rendering a link that 403s, and the stored
 * row is left alone — so it comes back with the entitlement, unless the user
 * saves a new selection in the meantime (the picker only ever offers, and
 * therefore only ever saves, tiles they can currently reach).
 */
export function resolveHomeWidgets(
  stored: string[] | null | undefined,
  available: HomeWidgetMeta[]
): HomeWidgetMeta[] {
  // undefined/null = never chosen ⇒ defaults. An empty array is a real choice.
  if (stored == null) {
    const defaults = available.filter(w => DEFAULT_HOME_WIDGETS.includes(w.key));
    // An agency whose plan covers none of the default modules still gets a
    // useful home rather than an empty one.
    return defaults.length ? defaults : available.slice(0, 4);
  }

  return stored
    .map(key => available.find(w => w.key === key))
    .filter((w): w is HomeWidgetMeta => w !== undefined);
}
