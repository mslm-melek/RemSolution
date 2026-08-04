import { Component, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { ActivatedRoute } from '@angular/router';
import {
  ClientsClient, ClientDto,
  RentingsClient, RentingDto, RentingState,
  PaymentsClient, PaymentDto, PaymentMethod,
  CreditsClient
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { PaymentDialogComponent } from '../shared/payment-dialog.component';
import { ReturnDialogComponent } from '../shared/return-dialog.component';
import { TranslocoService } from '@jsverse/transloco';

// How much of the payment ledger the money tab reads in one go.
const PAYMENT_PAGE_SIZE = 50;

// What the agency knows about one client in one place: who they are, everything
// they have ever hired, and where they stand on money. The form at
// /client/:id/edit is for changing those first facts; this page is for reading
// them and acting on them (hire to them, take a payment, bring a car back).
@Component({
  selector: 'app-client-detail',
  templateUrl: './client-detail.component.html',
  styleUrls: ['./client-detail.component.css']
})
export class ClientDetailComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly dialog = inject(MatDialog);

  clientId!: number;
  client?: ClientDto;
  errorMessage = '';

  // --- History (server-paged, like the rentings list itself) ---
  rentings: RentingDto[] = [];
  rentingColumns: string[] = ['period', 'car', 'state', 'price', 'outstanding', 'actions'];
  rentingsTotal = 0;
  rentingsPage = 1;
  rentingsPageSize = 10;

  // --- Money ---
  // One shape whichever module answered: the credits view (Credit.Read) and the
  // payment ledger's balance (Payment.Read) compute the same three figures, and
  // an agency may have granted either one alone.
  money?: { charged?: number; paid?: number; outstanding?: number; currency?: string };
  payments: PaymentDto[] = [];
  paymentColumns = ['date', 'amount', 'method', 'proof'];
  // The ledger is read newest-first and capped (see loadMoney); the total is kept
  // so the page can say so rather than look like the whole history.
  paymentsTotal = 0;
  // Payment id whose proof is uploading, so only its own button waits.
  uploadingProofFor: number | null = null;

  canSeeRentings = false;
  canRent = false;
  canReturn = false;
  canSeeCredit = false;
  canReadPayments = false;
  canPay = false;
  canAttachProof = false;

  private readonly stateLabelKeys: Record<number, string> = {
    [RentingState.NotYet]: 'enums.rentingState.notYet',
    [RentingState.InProgress]: 'enums.rentingState.inProgress',
    [RentingState.Done]: 'enums.rentingState.done',
    [RentingState.Cancelled]: 'enums.rentingState.cancelled'
  };

  paymentMethods = [
    { value: PaymentMethod.Cash, labelKey: 'enums.paymentMethod.cash' },
    { value: PaymentMethod.Card, labelKey: 'enums.paymentMethod.card' },
    { value: PaymentMethod.Transfer, labelKey: 'enums.paymentMethod.transfer' },
    { value: PaymentMethod.Cheque, labelKey: 'enums.paymentMethod.cheque' }
  ];

  constructor(
    private clients: ClientsClient,
    private rentingsClient: RentingsClient,
    private paymentsClient: PaymentsClient,
    private creditsClient: CreditsClient,
    private auth: AuthService,
    private route: ActivatedRoute
  ) { }

  ngOnInit() {
    this.clientId = +this.route.snapshot.paramMap.get('id')!;

    this.clients.getClientById(this.clientId).subscribe({
      next: client => this.client = client,
      error: err => console.error(err)
    });

    // Every panel below belongs to a module of its own, so what is loaded
    // follows what this user is allowed to see (the API enforces the same).
    this.auth.currentUser$.subscribe(user => {
      this.canSeeRentings = AuthService.canAccessModule(user, 'Rentings', 'Renting.Read');
      this.canRent = AuthService.canAccessModule(user, 'Rentings', 'Renting.Create');
      this.canReturn = AuthService.canAccessModule(user, 'Rentings', 'Renting.Update');
      this.canSeeCredit = AuthService.canAccessModule(user, 'Credits', 'Credit.Read');
      this.canReadPayments = AuthService.canAccessModule(user, 'Payments', 'Payment.Read');
      this.canPay = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
      this.canAttachProof = AuthService.canAccessModule(user, 'Payments', 'Payment.Update');

      if (this.canSeeRentings) this.loadRentings();
      this.loadMoney();
    });
  }

  get name(): string {
    return [this.client?.firstName, this.client?.lastName].filter(Boolean).join(' ');
  }

  get hasMoneyPanel(): boolean {
    return this.canSeeCredit || this.canReadPayments;
  }

  // --- History ---------------------------------------------------------------

  // Server-side paging, and the second-driver hires are included: the API's
  // client filter matches either seat (see GetRentingsWithPaginationQuery), which
  // is the honest answer to "what has this person hired?".
  loadRentings() {
    this.rentingsClient.getRentings(
      this.rentingsPage, this.rentingsPageSize, null, this.clientId, null,
      null, null, undefined, false, 'period', true
    ).subscribe({
      next: result => {
        this.rentings = result.items || [];
        this.rentingsTotal = result.totalCount || 0;
      },
      error: err => console.error(err)
    });
  }

  onRentingsPage(event: PageEvent) {
    this.rentingsPage = event.pageIndex + 1;
    this.rentingsPageSize = event.pageSize;
    this.loadRentings();
  }

  // Returns a transloco key; the template pipes it, so a language switch
  // re-renders the value.
  stateLabelKey(state?: RentingState): string {
    return state === undefined ? '' : this.stateLabelKeys[state] ?? '';
  }

  // Same tones the rentings list uses, so a state reads alike everywhere.
  stateClass(state?: RentingState): string {
    switch (state) {
      case RentingState.InProgress: return 'ok';
      case RentingState.NotYet: return 'info';
      case RentingState.Cancelled: return 'danger';
      default: return 'neutral';
    }
  }

  canTakeBack(renting: RentingDto): boolean {
    return this.canReturn && renting.rentingState === RentingState.InProgress && !!renting.id;
  }

  // Brings the car back in from the client's own page — the same dialog the cars
  // list uses. Both panels are reloaded: the hire closes and its price may move.
  returnCar(renting: RentingDto) {
    if (!renting.id) return;

    this.dialog.open(ReturnDialogComponent, {
      data: {
        rentingId: renting.id,
        carLabel: [renting.carMatricule, renting.carModelName].filter(Boolean).join(' · '),
        clientName: renting.clientName
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(returned => {
      if (returned) {
        this.loadRentings();
        this.loadMoney();
      }
    });
  }

  // --- Money -----------------------------------------------------------------

  private loadMoney() {
    if (this.canSeeCredit) {
      this.creditsClient.getClientCreditsByIds([this.clientId]).subscribe({
        next: rows => {
          const row = (rows || [])[0];
          if (row) {
            this.money = {
              charged: row.charged?.amount,
              paid: row.paid?.amount,
              outstanding: row.outstanding?.amount,
              currency: row.outstanding?.currency
            };
          }
        },
        error: err => console.error(err)
      });
    } else if (this.canReadPayments) {
      this.paymentsClient.getClientBalance(this.clientId).subscribe({
        next: balance => this.money = {
          charged: balance.totalCharged?.amount,
          paid: balance.totalPaid?.amount,
          outstanding: balance.balance?.amount,
          currency: balance.currency
        },
        error: err => console.error(err)
      });
    }

    if (this.canReadPayments) {
      // Newest first (see GetPaymentsWithPaginationQuery); one page is what a
      // counter reads, and the template says when there are older ones.
      this.paymentsClient.getPayments(1, PAYMENT_PAGE_SIZE, null, this.clientId, null).subscribe({
        next: result => {
          this.payments = result.items || [];
          this.paymentsTotal = result.totalCount || 0;
        },
        error: err => console.error(err)
      });
    }
  }

  // Takes money against the client's overall balance rather than one booking —
  // what a counter payment settling arrears actually is.
  pay() {
    this.dialog.open(PaymentDialogComponent, {
      data: {
        target: { kind: 'client', id: this.clientId },
        subtitle: this.name || undefined,
        outstanding: this.money?.outstanding,
        currency: this.money?.currency
      },
      autoFocus: 'first-tabbable'
    }).afterClosed().subscribe(recorded => {
      if (recorded) {
        this.loadMoney();
        if (this.canSeeRentings) this.loadRentings();
      }
    });
  }

  // Attaching the proof to an entry already recorded (the payment dialog offers
  // it at the moment of payment; this is for the ones taken before, or by phone).
  onProofSelected(payment: PaymentDto, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !payment.id) return;

    this.uploadingProofFor = payment.id;
    this.errorMessage = '';

    this.paymentsClient.uploadPaymentProof(payment.id, { data: file, fileName: file.name }).subscribe({
      next: () => {
        this.uploadingProofFor = null;
        this.loadMoney();
      },
      error: err => {
        this.uploadingProofFor = null;
        this.errorMessage = this.transloco.translate('payment.proofFailed');
        console.error(err);
      }
    });
  }

  // Returns a transloco key; the template pipes it.
  methodLabelKey(method?: PaymentMethod): string {
    return this.paymentMethods.find(m => m.value === method)?.labelKey ?? '';
  }
}
