// Mirror of the backend Domain FeatureCatalog / FeatureFlags / Permissions.
// The strings MUST match the server constants — the API enforces the same
// mapping, so this never out-privileges the backend. Used by the plan-feature
// editor and the agency Team screen (permission editing grouped by feature).

export interface FeatureMeta {
  key: string;
  label: string;
}

// Every feature a plan can include (full list).
export const FEATURES: FeatureMeta[] = [
  { key: 'Cars', label: 'Cars' },
  { key: 'Clients', label: 'Clients' },
  { key: 'Branches', label: 'Branches' },
  { key: 'Rentings', label: 'Rentings' },
  { key: 'Reservations', label: 'Reservations' },
  { key: 'Expenses', label: 'Expenses' },
  { key: 'ExtraServices', label: 'Extra Services' },
  { key: 'Payments', label: 'Payments' },
  { key: 'Contracts', label: 'Contracts + e-signature' },
  { key: 'Factures', label: 'Invoices' },
  { key: 'Credits', label: 'Credits' },
  { key: 'Dashboard', label: 'Dashboard' },
  { key: 'Chat', label: 'Chat' },
  { key: 'OnlineReservations', label: 'Online reservations' },
  { key: 'OnlinePayment', label: 'Online payment' }
];

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
  Contracts: ['Contract.Generate'],
  Factures: ['Facture.Read', 'Facture.Generate'],
  Credits: ['Credit.Read'],
  Dashboard: ['Dashboard.View'],
  Chat: ['Chat.View'],
  OnlineReservations: [],
  OnlinePayment: []
};

export function featureLabel(key: string): string {
  return FEATURES.find(f => f.key === key)?.label ?? key;
}
