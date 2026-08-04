import {
  booleanAttribute, Component, DoCheck, Input, OnDestroy, OnInit, inject
} from '@angular/core';
import { ControlValueAccessor, FormControl, NgControl } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import { Subscription } from 'rxjs';
import { toDateInput } from './form-utils';

/** Time a day gets when a date is picked and no hour has been chosen yet. */
const DEFAULT_TIME = '08:00';

/**
 * Every date the app asks for, so none of them is a box you have to type a date
 * into: one outlined field with Material's calendar behind the toggle, and — for
 * a rental period, where the hour is part of the deal — a time next to it.
 *
 * It speaks the same strings the forms already hold: `yyyy-MM-dd`, or
 * `yyyy-MM-ddTHH:mm` with `withTime`, both of which form-utils' `fromDateInput`
 * turns into the Date the API wants. That is why it is a value accessor rather
 * than a block of markup to copy: `formControlName` / `[(ngModel)]` keep working
 * and nothing around it had to learn about Dates.
 *
 * The calendar itself needs a Date, so the component keeps one privately
 * (`dateControl`) and mirrors the outer control's errors onto it — Material only
 * renders a `mat-error` while the input it wraps is in an error state, and that
 * inner input is the one it can see.
 */
@Component({
  selector: 'app-date-field',
  templateUrl: './date-field.component.html',
  styleUrls: ['./date-field.component.css']
})
export class DateFieldComponent implements ControlValueAccessor, OnInit, DoCheck, OnDestroy {
  private readonly transloco = inject(TranslocoService);
  // Self, optional: the field is always driven by a form directive in practice,
  // but reaching for NgControl through the normal NG_VALUE_ACCESSOR provider
  // would be a circular dependency — hence the accessor is registered by hand
  // below instead.
  private readonly ngControl = inject(NgControl, { self: true, optional: true });

  @Input() label = '';
  /** Shown under the field, like any mat-hint. */
  @Input() hint?: string;
  /** Shown instead once the outer control is invalid and has been touched. */
  @Input() error?: string;
  /** Adds the hour to the value: `yyyy-MM-ddTHH:mm` rather than `yyyy-MM-dd`. */
  @Input({ transform: booleanAttribute }) withTime = false;
  /** Only the asterisk on the label; the validator itself stays on the control. */
  @Input({ transform: booleanAttribute }) required = false;
  @Input() defaultTime = DEFAULT_TIME;

  /** Both bounds are the same `yyyy-MM-dd` strings the value uses. */
  @Input()
  set min(value: string | null | undefined) { this.minDate = this.toDate(value); }

  @Input()
  set max(value: string | null | undefined) { this.maxDate = this.toDate(value); }

  minDate: Date | null = null;
  maxDate: Date | null = null;

  /** The date half, as the calendar needs it. */
  readonly dateControl = new FormControl<Date | null>(null, () => this.outerErrors());
  /** The hour half, 'HH:mm' — empty until a date makes it mean something. */
  timePart = '';

  private onChange: (value: string) => void = () => { };
  private onTouched: () => void = () => { };
  private sub?: Subscription;
  // Last mirrored error set, so the inner control is only revalidated when the
  // outer one actually changed its mind (ngDoCheck runs on every cycle).
  private mirrored = 'null';

  constructor() {
    if (this.ngControl) this.ngControl.valueAccessor = this;
  }

  get timeLabel(): string {
    return this.transloco.translate('common.time');
  }

  ngOnInit() {
    this.sub = this.dateControl.valueChanges.subscribe(() => {
      // A date without an hour is not a moment: give it the default rather than
      // sending something the field does not show.
      if (this.withTime && this.dateControl.value && !this.timePart) {
        this.timePart = this.defaultTime;
      }
      this.emit();
    });
  }

  ngDoCheck() {
    const outer = this.ngControl?.control;
    if (!outer) return;

    // "Touched" is what turns a required error into a visible one, and it is set
    // on the outer control — by a blur here, or by markAllAsTouched() on submit.
    if (outer.touched && !this.dateControl.touched) this.dateControl.markAsTouched();
    if (!outer.touched && this.dateControl.touched) this.dateControl.markAsUntouched();

    const errors = JSON.stringify(outer.errors ?? null);
    if (errors !== this.mirrored) {
      this.mirrored = errors;
      this.dateControl.updateValueAndValidity({ emitEvent: false });
    }
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  // --- ControlValueAccessor ------------------------------------------------

  writeValue(value: any) {
    const raw = typeof value === 'string' ? value : '';
    const [datePart, timePart] = raw.split('T');
    const date = this.toDate(datePart);

    this.dateControl.setValue(date, { emitEvent: false });
    this.timePart = timePart ? timePart.slice(0, 5) : '';

    // A day written in without one (nothing does today, but a caller could):
    // show the hour it would be saved with rather than an empty box.
    if (this.withTime && date && !this.timePart) this.timePart = this.defaultTime;
  }

  registerOnChange(fn: (value: string) => void) {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void) {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean) {
    disabled ? this.dateControl.disable({ emitEvent: false })
             : this.dateControl.enable({ emitEvent: false });
  }

  // --- Events --------------------------------------------------------------

  // A native time input reads '' while it is half filled in — typing the hour of
  // 10:30 goes through a moment where there is no value yet — and it fires on
  // every one of those steps. Nothing is recorded until the hour is whole, or
  // `[value]` would write the default over what is being typed.
  onTimeInput(value: string) {
    if (!value) return;

    this.timePart = value;
    this.emit();
  }

  // Left empty (cleared, or abandoned half typed): a date that has an hour still
  // has to show it, so the field goes back to what the value says. Assigned
  // directly because `timePart` has not changed and the binding only writes when
  // it does.
  onTimeBlur(input: HTMLInputElement) {
    if (!input.value) input.value = this.timePart || this.defaultTime;

    this.onTouched();
  }

  onBlur() {
    this.onTouched();
  }

  // --- Value ---------------------------------------------------------------

  private emit() {
    const date = this.dateControl.value;

    // Empty, or half-typed into an unparseable date: '' is what an empty date
    // input has always handed the forms, so required validators still fire.
    if (!date || isNaN(date.getTime())) {
      this.onChange('');
      return;
    }

    const datePart = toDateInput(date);
    this.onChange(this.withTime ? `${datePart}T${this.timePart || this.defaultTime}` : datePart);
  }

  // Reported on the inner control so Material shows the error state; the message
  // itself is the caller's `error` input.
  private outerErrors() {
    return this.ngControl?.control?.errors ?? null;
  }

  private toDate(value: string | null | undefined): Date | null {
    if (!value) return null;

    const [year, month, day] = value.split('T')[0].split('-').map(Number);
    if (!year || !month || !day) return null;

    return new Date(year, month - 1, day);
  }
}
