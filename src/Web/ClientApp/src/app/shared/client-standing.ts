// What a client row answers when asked "can I hand this person a car?".
//
// Two facts decide it, and they are asked in this order because that is the order
// a counter asks them in:
//   - the agency raised its bad-client flag on them (see FlagClientCommand), which
//     is a judgement somebody made on purpose and outranks everything else;
//   - the papers are not all on file yet — the CIN image and the driving licence,
//     which is what has to be produced if the car is stopped.
// Everything else is a client in good standing.
//
// It is the same reading the fleet list gets from shared/car-availability.ts, and
// it lives here for the same reason: the list, the panel and anything that comes
// later must not each combine the two facts their own way.
//
// A passport is deliberately not part of "complete": it is only ever needed for a
// foreign client, so demanding it would leave most of the book permanently amber.
// The server filters on the same pair (see GetClientsWithPaginationQuery's
// DocumentsComplete), so what the pills select and what the chips say agree.
export type ClientStanding = 'verified' | 'pending' | 'flagged';

// The fields the answer needs — a ClientDto satisfies it.
export interface ClientStandingFacts {
  isFlagged?: boolean;
  cinImageUrl?: string;
  drivingLicenceImageUrl?: string;
}

export function clientStanding(client: ClientStandingFacts): ClientStanding {
  if (client.isFlagged) return 'flagged';
  if (!client.cinImageUrl || !client.drivingLicenceImageUrl) return 'pending';
  return 'verified';
}

export function clientStandingLabelKey(standing: ClientStanding): string {
  return `client.standing.${standing}`;
}

// Chip tone (see styles.scss): papers on file and no flag reads as good, papers
// still to collect as work outstanding, a flagged client as a refusal to make
// quietly.
export function clientStandingClass(standing: ClientStanding): string {
  switch (standing) {
    case 'verified': return 'ok';
    case 'pending': return 'warn';
    default: return 'danger';
  }
}

export function clientStandingIcon(standing: ClientStanding): string {
  switch (standing) {
    case 'verified': return 'verified_user';
    case 'pending': return 'pending_actions';
    default: return 'flag';
  }
}
