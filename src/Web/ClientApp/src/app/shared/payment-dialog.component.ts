import { Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import {
  PaymentsClient, ExpensesClient, CreatePaymentCommand, RecordExpensePaymentCommand,
  PaymentMethod, FileParameter
} from '../web-api-client';
import { extractValidationErrors } from './form-utils';

// What the money is being recorded against. An incoming payment targets exactly
// one of a renting, a reservation or a client (see CreatePaymentCommand);
// 'expense' is the outgoing direction and moves the expense's settled total
// instead of writing a Payment row.
export type PaymentDialogTarget =
  | { kind: 'renting'; id: number }
  | { kind: 'reservation'; id: number }
  | { kind: 'client'; id: number }
  | { kind: 'expense'; id: number };

// Zero would be a no-op write, which is the one delta the settlement API refuses.
function nonZero(control: AbstractControl): ValidationErrors | null {
  const value = Number(control.value);
  return control.value === null || control.value === '' || value === 0 ? { nonZero: true } : null;
}

export interface PaymentDialogData {
  target: PaymentDialogTarget;
  // Who or what the money concerns, shown under the title (client name, plate…).
  subtitle?: string;
  // Prefills the amount and states the ceiling the server enforces anyway.
  outstanding?: number;
  currency?: string;
  // The agreed price, for a caller that knows what was charged but not what has
  // been collected — a renting row carries no paid figure. The dialog then works
  // the remaining balance out from the entries themselves.
  charged?: number;
}

// Records money in one step from wherever the debt is displayed — the credits
// screen, a client, a renting or a reservation row — instead of making the user
// open the booking first. The optional proof rides along: the file can only be
// attached once the entry exists, so it is uploaded straight after the create.
@Component({
  selector: 'app-payment-dialog',
  templateUrl: './payment-dialog.component.html',
  styleUrls: ['./payment-dialog.component.css']
})
export class PaymentDialogComponent {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<PaymentDialogData>(MAT_DIALOG_DATA);

  form: FormGroup;
  saving = false;
  errorMessage = '';
  proofFile: File | null = null;
  // What is still owed, once known: either handed in by the caller or worked out
  // from the entries (see resolveOutstanding).
  outstanding?: number;

  // Set once the entry is written. A retry after a failed proof upload must not
  // create a second payment, so the id gates the create step.
  private recordedPaymentId?: number;

  PaymentMethod = PaymentMethod;
  paymentMethods = [
    { value: PaymentMethod.Cash, labelKey: 'enums.paymentMethod.cash' },
    { value: PaymentMethod.Card, labelKey: 'enums.paymentMethod.card' },
    { value: PaymentMethod.Transfer, labelKey: 'enums.paymentMethod.transfer' },
    { value: PaymentMethod.Cheque, labelKey: 'enums.paymentMethod.cheque' }
  ];

  constructor(
    private fb: FormBuilder,
    private payments: PaymentsClient,
    private expenses: ExpensesClient,
    private dialog: MatDialogRef<PaymentDialogComponent, boolean>
  ) {
    // An outstanding balance is the amount being asked for in the common case,
    // so it is offered pre-filled rather than retyped.
    this.outstanding = this.data.outstanding ?? undefined;

    this.form = this.fb.group({
      // A settlement is a DELTA on the expense's settled total, and a negative one
      // is how an over-recorded settlement is corrected (see
      // RecordExpensePaymentCommand) — so only money coming IN must be positive.
      amount: [
        this.prefillAmount(),
        this.isExpense
          ? [Validators.required, nonZero]
          : [Validators.required, Validators.min(0.01)]
      ],
      method: [PaymentMethod.Cash],
      date: [this.today()],
      notes: ['', Validators.maxLength(1000)]
    });

    if (this.outstanding === undefined) this.resolveOutstanding();
  }

  private prefillAmount(): number | null {
    return this.outstanding !== undefined && this.outstanding > 0 ? this.outstanding : null;
  }

  // A booking row knows its agreed price but not what has been collected against
  // it, so the balance is read from the entries. Needs Payment.Read: without it
  // the amount is simply left blank rather than guessed at.
  private resolveOutstanding() {
    const target = this.data.target;
    const charged = this.data.charged;

    if (charged === undefined || (target.kind !== 'renting' && target.kind !== 'reservation')) return;

    this.payments.getPayments(
      1, 200,
      target.kind === 'renting' ? target.id : null,
      null,
      target.kind === 'reservation' ? target.id : null
    ).subscribe({
      next: result => {
        const net = (result.items || [])
          .reduce((sum, p) => sum + (p.payementAmount?.amount ?? 0), 0);
        // Rounded back to cents: summing decimals in floating point otherwise
        // leaves a tail the amount field would show.
        this.outstanding = Math.max(0, Math.round((charged - net) * 100) / 100);

        if (!this.form.get('amount')!.value) {
          this.form.patchValue({ amount: this.prefillAmount() });
        }
      },
      error: () => { /* no read permission: leave the amount to the user */ }
    });
  }

  // The outgoing direction settles an expense: no method, no notes, no proof of
  // payment — the invoice belongs to the expense record itself.
  get isExpense(): boolean {
    return this.data.target.kind === 'expense';
  }

  get titleKey(): string {
    return this.isExpense ? 'payment.settleTitle' : 'payment.title';
  }

  // The entry is written and only its proof is still missing (a failed upload),
  // so the button now offers that one remaining step.
  get awaitingProof(): boolean {
    return this.recordedPaymentId !== undefined;
  }

  get submitKey(): string {
    return this.awaitingProof ? 'payment.attachProof' : 'payment.record';
  }

  onFileSelected(input: HTMLInputElement) {
    this.proofFile = input.files?.[0] ?? null;
    input.value = ''; // allow re-selecting the same file
  }

  clearFile() {
    this.proofFile = null;
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    const amount = Number(this.form.value.amount);

    if (this.isExpense) {
      const id = this.data.target.id;
      const command = new RecordExpensePaymentCommand({ id, amount });
      this.expenses.recordExpensePayment(id, command).subscribe({
        next: () => this.dialog.close(true),
        error: err => this.handleError(err)
      });
      return;
    }

    // A proof upload that failed leaves the payment recorded: go straight back
    // to attaching it rather than posting the amount twice.
    if (this.recordedPaymentId !== undefined) {
      this.attachProofThenClose(this.recordedPaymentId);
      return;
    }

    const target = this.data.target;
    const v = this.form.value;

    this.payments.createPayment(new CreatePaymentCommand({
      rentingId: target.kind === 'renting' ? target.id : undefined,
      reservationId: target.kind === 'reservation' ? target.id : undefined,
      clientId: target.kind === 'client' ? target.id : undefined,
      amount,
      method: v.method,
      payementDate: this.paymentDate(v.date),
      notes: v.notes || undefined
    })).subscribe({
      next: id => {
        this.recordedPaymentId = id;
        // The money is booked and cannot be re-typed from here; if the proof
        // upload now fails, the dialog is only about the file.
        this.form.disable();
        this.attachProofThenClose(id);
      },
      error: err => this.handleError(err)
    });
  }

  cancel() {
    // A recorded payment means the caller's figures are stale even though the
    // proof never made it, so closing still asks for a reload.
    this.dialog.close(this.recordedPaymentId !== undefined);
  }

  private attachProofThenClose(paymentId: number) {
    if (!this.proofFile) {
      this.dialog.close(true);
      return;
    }

    const parameter: FileParameter = { data: this.proofFile, fileName: this.proofFile.name };

    this.payments.uploadPaymentProof(paymentId, parameter).subscribe({
      next: () => this.dialog.close(true),
      error: err => {
        // Say plainly that the money is recorded: the retry only re-uploads.
        this.saving = false;
        this.errorMessage = extractValidationErrors(err)
          ?? this.transloco.translate('payment.proofFailed');
      }
    });
  }

  private today(): string {
    const d = new Date();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${month}-${day}`;
  }

  // Today is left to the server so the entry carries the real clock time; a
  // backdated entry is sent as UTC midnight of the day picked, like every other
  // date-only value (see form-utils).
  private paymentDate(value: string): Date | undefined {
    if (!value || value === this.today()) return undefined;

    const [year, month, day] = value.split('-').map(Number);
    return new Date(Date.UTC(year, month - 1, day));
  }

  private handleError(err: any) {
    this.saving = false;
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
