import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { DateAdapter, MAT_DATE_FORMATS, MAT_DATE_LOCALE } from '@angular/material/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { TranslocoService } from '@jsverse/transloco';
import { AppDateAdapter, APP_DATE_FORMATS } from './date-adapter';
import { DateFieldComponent } from './date-field.component';

// The date field's whole point is that the forms keep holding the strings they
// always held while the user gets a calendar, so that is what is pinned here:
// what goes in, what comes out, and that a required date still blocks a save.
@Component({
  template: `
    <form [formGroup]="form">
      <app-date-field label="Start" formControlName="start" required
                      error="Start date is required"></app-date-field>
      <app-date-field label="From" formControlName="when" withTime></app-date-field>
    </form>`
})
class HostComponent {
  form = new FormGroup({
    start: new FormControl(''),
    when: new FormControl('')
  });
}

describe('DateFieldComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DateFieldComponent, HostComponent],
      imports: [
        ReactiveFormsModule, NoopAnimationsModule,
        MatFormFieldModule, MatInputModule, MatDatepickerModule
      ],
      providers: [
        { provide: MAT_DATE_LOCALE, useValue: 'fr' },
        { provide: DateAdapter, useClass: AppDateAdapter },
        { provide: MAT_DATE_FORMATS, useValue: APP_DATE_FORMATS },
        { provide: TranslocoService, useValue: { translate: () => 'Time' } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  // The date the form holds, shown the way the app shows dates everywhere.
  function dateInputs(): HTMLInputElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('input:not([type=time])'));
  }

  function timeInput(): HTMLInputElement {
    return fixture.nativeElement.querySelector('input[type=time]');
  }

  function type(input: HTMLInputElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('shows a stored yyyy-MM-dd value as dd/MM/yyyy', () => {
    host.form.patchValue({ start: '2026-08-14' });
    fixture.detectChanges();

    expect(dateInputs()[0].value).toBe('14/08/2026');
  });

  it('hands the form a yyyy-MM-dd string for the date picked', () => {
    type(dateInputs()[0], '14/08/2026');

    expect(host.form.value.start).toBe('2026-08-14');
  });

  it('accepts the ISO form as typed input too', () => {
    type(dateInputs()[0], '2026-08-14');

    expect(host.form.value.start).toBe('2026-08-14');
  });

  it('empties the value when the date is cleared', () => {
    type(dateInputs()[0], '14/08/2026');
    type(dateInputs()[0], '');

    expect(host.form.value.start).toBe('');
  });

  it('adds the default hour to a date picked in a withTime field', () => {
    type(dateInputs()[1], '14/08/2026');

    expect(host.form.value.when).toBe('2026-08-14T08:00');
    expect(timeInput().value).toBe('08:00');
  });

  it('takes the hour from the time input', () => {
    type(dateInputs()[1], '14/08/2026');
    type(timeInput(), '17:45');

    expect(host.form.value.when).toBe('2026-08-14T17:45');
  });

  it('splits a stored date-and-time value across the two inputs', () => {
    host.form.patchValue({ when: '2026-08-14T17:45' });
    fixture.detectChanges();

    expect(dateInputs()[1].value).toBe('14/08/2026');
    expect(timeInput().value).toBe('17:45');
  });

  it('keeps the required attribute a real validator on the outer control', () => {
    expect(host.form.get('start')!.hasError('required')).toBeTrue();

    type(dateInputs()[0], '14/08/2026');

    expect(host.form.get('start')!.valid).toBeTrue();
  });

  it('shows the error message once the control is touched', () => {
    host.form.get('start')!.markAsTouched();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('mat-error')?.textContent)
      .toContain('Start date is required');
  });

  it('records nothing from a half-typed hour, so the box is never overwritten', () => {
    type(dateInputs()[1], '14/08/2026');
    type(timeInput(), '17:45');

    // What Chrome reports while a segment is still being filled in.
    type(timeInput(), '');

    expect(host.form.value.when).toBe('2026-08-14T17:45');
  });

  it('puts the hour back when the box is left empty', () => {
    type(dateInputs()[1], '14/08/2026');
    type(timeInput(), '17:45');

    const input = timeInput();
    input.value = '';
    input.dispatchEvent(new Event('blur'));
    fixture.detectChanges();

    expect(input.value).toBe('17:45');
  });

  it('follows the form into the disabled state', () => {
    host.form.disable();
    fixture.detectChanges();

    expect(dateInputs()[0].disabled).toBeTrue();
    expect(timeInput().disabled).toBeTrue();
  });
});

// Four screens drive the field with [(ngModel)] rather than a form control (the
// dashboard's range, the two marketplace search bars, the change-end-date panel).
@Component({
  template: `<app-date-field label="From" [(ngModel)]="value" withTime></app-date-field>`
})
class NgModelHostComponent {
  value = '';
}

describe('DateFieldComponent with ngModel', () => {
  let fixture: ComponentFixture<NgModelHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DateFieldComponent, NgModelHostComponent],
      imports: [
        // Both: the caller here uses ngModel, the field's own template uses
        // [formControl] for the date half.
        FormsModule, ReactiveFormsModule, NoopAnimationsModule,
        MatFormFieldModule, MatInputModule, MatDatepickerModule
      ],
      providers: [
        { provide: MAT_DATE_LOCALE, useValue: 'fr' },
        { provide: DateAdapter, useClass: AppDateAdapter },
        { provide: MAT_DATE_FORMATS, useValue: APP_DATE_FORMATS },
        { provide: TranslocoService, useValue: { translate: () => 'Time' } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NgModelHostComponent);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('writes the picked moment back through the binding', () => {
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input:not([type=time])');
    input.value = '14/08/2026';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(fixture.componentInstance.value).toBe('2026-08-14T08:00');
  });

  it('shows a value set on the model', async () => {
    fixture.componentInstance.value = '2026-08-14T17:45';
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('input:not([type=time])').value).toBe('14/08/2026');
    expect(fixture.nativeElement.querySelector('input[type=time]').value).toBe('17:45');
  });
});
