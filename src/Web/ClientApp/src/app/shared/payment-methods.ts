import { PaymentMethod } from '../web-api-client';

// How money changes hands, in the order a counter reads them out. Three screens
// draw this list — the payment dialog, the return dialog's deposit refund, and
// the payments list — so the order and the labels are stated once. Same reason
// renting-fees.ts exists.
export const PAYMENT_METHODS: PaymentMethod[] = [
  PaymentMethod.Cash,
  PaymentMethod.Card,
  PaymentMethod.Transfer,
  PaymentMethod.Cheque,
];

// The enum is numeric over the wire, so the key cannot be built from the value.
const LABELS: Record<number, string> = {
  [PaymentMethod.Cash]: 'enums.paymentMethod.cash',
  [PaymentMethod.Card]: 'enums.paymentMethod.card',
  [PaymentMethod.Transfer]: 'enums.paymentMethod.transfer',
  [PaymentMethod.Cheque]: 'enums.paymentMethod.cheque',
};

export function paymentMethodLabelKey(method?: PaymentMethod): string {
  return LABELS[method ?? PaymentMethod.Cash] ?? LABELS[PaymentMethod.Cash];
}
