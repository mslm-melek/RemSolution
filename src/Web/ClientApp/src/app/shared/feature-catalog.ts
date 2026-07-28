// Mirror of the backend Domain FeatureCatalog / FeatureFlags / Permissions.
// The strings MUST match the server constants — the API enforces the same
// mapping, so this never out-privileges the backend. Used by the plan-feature
// editor and the agency Team screen (permission editing grouped by feature).

export interface FeatureMeta {
  key: string;
  /** Transloco key under `features.*` — resolved in the template, not here. */
  labelKey: string;
}

// Every feature a plan can include (full list). The display name lives in the
// translation files; only the server-matching key lives here.
export const FEATURES: FeatureMeta[] = [
  'Cars',
  'Clients',
  'Branches',
  'Rentings',
  'Reservations',
  'Expenses',
  'ExtraServices',
  'Payments',
  'Contracts',
  'Factures',
  'Credits',
  'Dashboard',
  'Chat',
  'OnlineReservations',
  'OnlinePayment'
].map(key => ({ key, labelKey: `features.${key}` }));

// Permissions grouped by their feature (empty for capability-only features).
export const PERMISSIONS_BY_FEATURE: Record<string, string[]> = {
  Cars: ['Car.Create', 'Car.Read', 'Car.Update', 'Car.Delete'],
  Clients: ['Client.Create', 'Client.Read', 'Client.Update', 'Client.Delete'],
  Branches: ['Branch.Create', 'Branch.Read', 'Branch.Update', 'Branch.Delete'],
  Rentings: ['Renting.Create', 'Renting.Read', 'Renting.Update', 'Renting.Delete'],
  Reservations: ['Reservation.Create', 'Reservation.Read', 'Reservation.Update', 'Reservation.Delete'],
  Expenses: ['Expense.Create', 'Expense.Read', 'Expense.Update', 'Expense.Delete'],
  ExtraServices: ['ExtraService.Create', 'ExtraService.Read', 'ExtraService.Update', 'ExtraService.Delete'],
  Payments: ['Payment.Create', 'Payment.Read', 'Payment.Update', 'Payment.Delete'],
  Contracts: ['Contract.Read', 'Contract.Generate'],
  Factures: ['Facture.Read', 'Facture.Generate'],
  Credits: ['Credit.Read'],
  Dashboard: ['Dashboard.View'],
  Chat: ['Chat.View', 'Chat.Send'],
  OnlineReservations: [],
  OnlinePayment: []
};

export function featureLabelKey(key: string): string {
  return `features.${key}`;
}
