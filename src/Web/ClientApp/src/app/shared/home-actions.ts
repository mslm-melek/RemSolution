import { CurrentUserDto } from '../web-api-client';
import { AuthService } from './auth.service';

// The quick actions a user can keep on their landing screen — the platform
// admin's console dashboard, or an agency user's home. Each one is a labelled
// button that starts something (or opens the screen where it is started).
//
// `key` MUST match the server's Domain.Constants.HomeActions constants: the
// user's choice is stored by key and the API refuses one it does not know. Where
// an action points and what may offer it stays here, exactly as home-widgets.ts
// keeps the tiles' presentation.

export interface HomeActionMeta {
  key: string;
  /** Transloco key under `home.quick.*`. */
  labelKey: string;
  icon: string;
  link: string;
  /**
   * Which landing screen offers it. Platform actions live in the console (the
   * platform admin's own context); agency actions belong to a workspace and are
   * gated by the module they write into.
   */
  scope: 'platform' | 'agency';
  /** Agency actions only: feature enabled AND permission held, as the nav does. */
  feature?: string;
  permission?: string;
}

// Order matters twice: it is the order the picker offers them in, and the order
// a never-customized landing screen falls back to.
export const HOME_ACTIONS: HomeActionMeta[] = [
  // --- Platform-admin console -----------------------------------------------
  { key: 'NewAgency', labelKey: 'home.quick.newAgency', icon: 'add_business', link: '/agency/new', scope: 'platform' },
  { key: 'NewPlan', labelKey: 'home.quick.newPlan', icon: 'workspace_premium', link: '/subscription-plan/new', scope: 'platform' },
  { key: 'Agencies', labelKey: 'home.quick.agencies', icon: 'business', link: '/agency', scope: 'platform' },
  { key: 'SubscriptionPlans', labelKey: 'home.quick.plans', icon: 'list_alt', link: '/subscription-plan', scope: 'platform' },
  { key: 'NewCarModel', labelKey: 'home.quick.newCarModel', icon: 'add', link: '/model-car/new', scope: 'platform' },
  { key: 'CarBrands', labelKey: 'home.quick.carBrands', icon: 'sell', link: '/brand', scope: 'platform' },
  { key: 'CarModels', labelKey: 'home.quick.carModels', icon: 'category', link: '/model-car', scope: 'platform' },
  { key: 'ExpenseTypes', labelKey: 'home.quick.expenseTypes', icon: 'receipt_long', link: '/expense-type', scope: 'platform' },
  {
    key: 'ExtraServiceTypes', labelKey: 'home.quick.extraServiceTypes',
    icon: 'add_shopping_cart', link: '/extra-service-type', scope: 'platform'
  },
  { key: 'BrowseMarketplace', labelKey: 'home.quick.browseMarketplace', icon: 'travel_explore', link: '/browse', scope: 'platform' },

  // --- Agency workspace ------------------------------------------------------
  {
    key: 'NewCar', labelKey: 'home.quick.newCar', icon: 'add', link: '/car/new',
    scope: 'agency', feature: 'Cars', permission: 'Car.Create'
  },
  {
    key: 'NewRenting', labelKey: 'home.quick.newRenting', icon: 'vpn_key', link: '/renting/new',
    scope: 'agency', feature: 'Rentings', permission: 'Renting.Create'
  },
  {
    key: 'NewReservation', labelKey: 'home.quick.newReservation', icon: 'event_available', link: '/reservation/new',
    scope: 'agency', feature: 'Reservations', permission: 'Reservation.Create'
  },
  {
    key: 'NewClient', labelKey: 'home.quick.newClient', icon: 'person_add', link: '/client/new',
    scope: 'agency', feature: 'Clients', permission: 'Client.Create'
  },
  {
    key: 'NewExpense', labelKey: 'home.quick.newExpense', icon: 'payments', link: '/expense/new',
    scope: 'agency', feature: 'Expenses', permission: 'Expense.Create'
  }
];

// What a user who has never chosen sees: the two things a platform admin most
// often starts, and the day's work for an agency user — the sets that were
// hard-coded before the strip became customizable, so nobody's screen changed
// the day it did.
export const DEFAULT_PLATFORM_ACTIONS = ['NewAgency', 'NewPlan'];
export const DEFAULT_AGENCY_ACTIONS =
  ['NewCar', 'NewRenting', 'NewReservation', 'NewClient', 'NewExpense'];

// Mirrors HomeActions.MaxPinned on the server, which rejects a longer list.
export const MAX_HOME_ACTIONS = 6;

/**
 * The actions this user could keep. Platform actions are the console's, offered
 * to a platform administrator outside any agency workspace; agency actions need
 * the feature switched on and the create permission held — the same rule the
 * navigation applies, so an action is never offered for a screen that would 403.
 */
export function availableHomeActions(
  user: CurrentUserDto,
  scope: 'platform' | 'agency'
): HomeActionMeta[] {
  return HOME_ACTIONS.filter(action =>
    action.scope === scope &&
    (scope === 'platform' ||
      AuthService.canAccessModule(user, action.feature!, action.permission!)));
}

/** Which landing screen a stored key belongs to, or undefined if it is unknown. */
function keyScope(key: string): 'platform' | 'agency' | undefined {
  return HOME_ACTIONS.find(a => a.key === key)?.scope;
}

/**
 * The stored choice, kept to what the user can currently reach and in their
 * order. An action whose feature was switched off (or whose permission was
 * revoked) drops out here rather than rendering a link that 403s, and the stored
 * row is left alone — so it comes back with the entitlement.
 */
export function resolveHomeActions(
  stored: string[] | null | undefined,
  available: HomeActionMeta[],
  scope: 'platform' | 'agency'
): HomeActionMeta[] {
  const defaults = scope === 'platform' ? DEFAULT_PLATFORM_ACTIONS : DEFAULT_AGENCY_ACTIONS;

  // undefined/null = never chosen ⇒ defaults. An empty array is a real choice.
  //
  // The selection is one list per account, and the same account can land on both
  // screens: a platform administrator gets the console, and the agency home
  // whenever they open a workspace. A selection made on one screen therefore says
  // nothing about the other, so it falls back to that screen's defaults rather
  // than leaving it bare — while a deliberately emptied list (length 0) still
  // means "no actions" on both.
  if (stored == null ||
      (stored.length > 0 && !stored.some(key => keyScope(key) === scope))) {
    return available.filter(a => defaults.includes(a.key));
  }

  return stored
    .map(key => available.find(a => a.key === key))
    .filter((a): a is HomeActionMeta => a !== undefined);
}
