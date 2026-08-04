import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { RentingsClient, RentingDto, CancelRentingCommand } from '../web-api-client';
import { AuthService } from './auth.service';
import { extractValidationErrors } from './form-utils';

export interface CancelDialogData {
  rentingId: number;
  // Shown under the title when the caller already knows them, so the dialog has
  // something to say before the re-read lands.
  carLabel?: string;
  clientName?: string;
}

// Calling a hire off, and facing the money while doing it. A cancellation moves
// two figures and the agency has to say what happens to each: what the client
// still owes (a fee kept, or nothing at all) and whether what they have already
// paid beyond that goes back to them now or stays as a credit on their account.
// Neither can be guessed, which is why this replaced the confirm() box.
//
// The renting is re-read on open rather than passed in: a list row's price and
// paid figures age while the page sits open, and every figure here is derived
// from them.
@Component({
  selector: 'app-cancel-dialog',
  templateUrl: './cancel-dialog.component.html',
  styleUrls: ['./cancel-dialog.component.css']
})
export class CancelDialogComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  readonly data = inject<CancelDialogData>(MAT_DIALOG_DATA);

  renting?: RentingDto;
  form: FormGroup;
  loading = true;
  saving = false;
  errorMessage = '';

  // Recording the refund writes to the payment ledger, so it needs the Payments
  // module. Without it the choice is not offered and the money simply stays as a
  // credit — the server would refuse the write anyway.
  canRefund = false;

  constructor(
    private fb: FormBuilder,
    private rentings: RentingsClient,
    private auth: AuthService,
    private dialog: MatDialogRef<CancelDialogComponent, boolean>
  ) {
    this.form = this.fb.group({
      // Nothing kept by default: cancelling for free is the common case, and a
      // fee is a decision somebody has to make on purpose.
      fee: [0, [Validators.min(0)]],
      refundExcess: [false]
    });
  }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canRefund = AuthService.canAccessModule(user, 'Payments', 'Payment.Create');
    });

    this.rentings.getRentingById(this.data.rentingId).subscribe({
      next: renting => {
        this.renting = renting;
        this.form.get('fee')!.addValidators(
          Validators.max(renting.price?.amount ?? 0));
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.handleError(err);
      }
    });
  }

  get carLabel(): string {
    return this.data.carLabel
      ?? [this.renting?.carMatricule, this.renting?.carModelName].filter(Boolean).join(' · ');
  }

  get clientName(): string {
    return this.data.clientName ?? this.renting?.clientName ?? '';
  }

  get currency(): string {
    return this.renting?.price?.currency ?? '';
  }

  get price(): number {
    return this.renting?.price?.amount ?? 0;
  }

  /** Net collected against this hire — refunds already made are negative. */
  get paid(): number {
    return this.renting?.paid?.amount ?? 0;
  }

  get fee(): number {
    const value = this.form.value.fee;
    return value === null || value === '' ? 0 : Number(value);
  }

  /** What the client is left owing once the fee replaces the price. */
  get stillOwed(): number {
    return Math.max(0, this.fee - this.paid);
  }

  /** What they have paid beyond the fee, which is the agency's to hand back. */
  get refundable(): number {
    return Math.max(0, this.paid - this.fee);
  }

  /** The part of the price the client is being let off. */
  get writtenOff(): number {
    return Math.max(0, this.price - this.fee);
  }

  submit() {
    if (this.form.invalid || !this.renting || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';

    // The amount refunded is the server's arithmetic, not ours: this only says
    // whether to hand the excess back (see CancelRentingCommand).
    const command = new CancelRentingCommand({
      id: this.renting.id,
      rowVersion: this.renting.rowVersion,
      cancellationFee: this.fee > 0 ? this.fee : undefined,
      refundExcess: this.canRefund && this.refundable > 0 && !!this.form.value.refundExcess
    });

    this.rentings.cancelRenting(this.renting.id!, command).subscribe({
      next: () => this.dialog.close(true),
      error: err => {
        this.saving = false;
        this.handleError(err);
      }
    });
  }

  close() {
    this.dialog.close(false);
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
