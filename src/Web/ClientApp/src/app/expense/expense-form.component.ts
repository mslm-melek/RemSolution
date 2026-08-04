import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ExpensesClient, ExpenseDto, CreateExpenseCommand, UpdateExpenseCommand,
  CarsClient, CarDto, ExpenseTypesClient, ExpenseTypeDto, FileParameter
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-expense-form',
  templateUrl: './expense-form.component.html',
  styleUrls: ['./expense-form.component.css']
})
export class ExpenseFormComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  form: FormGroup;
  cars: CarDto[] = [];
  types: ExpenseTypeDto[] = [];
  expenseId?: number;
  saving = false;
  errorMessage = '';
  // Read-only on edit: the settled total is moved by the settle action on the
  // finance screen, never by re-typing it here (see RecordExpensePaymentCommand).
  settled = 0;
  currency = '';
  // The supplier invoice; only attachable once the expense exists.
  factureUrl = '';
  factureName = '';
  uploadingFacture = false;

  constructor(
    private fb: FormBuilder,
    private client: ExpensesClient,
    private carsClient: CarsClient,
    private typesClient: ExpenseTypesClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      carId: [null, Validators.required],
      expenseTypeId: [null, Validators.required],
      expenseDate: [this.today(), Validators.required],
      amount: [null, [Validators.required, Validators.min(0.01)]],
      // Only offered when booking a new expense; an existing one is settled
      // through the finance screen's settle action.
      paidAmount: [0, Validators.min(0)],
      description: ['', Validators.maxLength(1000)]
    });
  }

  get isEdit(): boolean {
    return this.expenseId !== undefined;
  }

  ngOnInit() {
    this.carsClient.getCars(1, 1000, null, null, null, null, null, null, null, null, false).subscribe({
      next: result => this.cars = result.items || [],
      error: err => console.error(err)
    });

    // Only active types can be booked against (the API refuses the others).
    this.typesClient.getExpenseTypes(true).subscribe({
      next: types => this.types = types || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.expenseId = +idParam;
      this.client.getExpenseById(this.expenseId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
    }
  }

  carLabel(car: CarDto): string {
    return car.modelName ? `${car.matricule} — ${car.modelName}` : (car.matricule ?? '');
  }

  private today(): string {
    return new Date().toISOString().substring(0, 10);
  }

  private populate(dto: ExpenseDto) {
    this.settled = dto.paidAmount?.amount ?? 0;
    this.currency = dto.expenseAmount?.currency ?? '';
    this.factureUrl = dto.factureFileUrl ?? '';
    this.factureName = dto.factureFileName ?? '';

    this.form.patchValue({
      carId: dto.carId ?? null,
      expenseTypeId: dto.expenseTypeId ?? null,
      expenseDate: dto.expenseDate
        ? new Date(dto.expenseDate).toISOString().substring(0, 10)
        : this.today(),
      amount: dto.expenseAmount?.amount ?? null,
      description: dto.description ?? ''
    });

    this.form.get('paidAmount')!.disable();
  }

  onFactureSelected(input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !this.expenseId) return;

    this.uploadingFacture = true;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.client.uploadExpenseFacture(this.expenseId, parameter).subscribe({
      next: url => {
        this.factureUrl = url;
        this.factureName = file.name;
        this.uploadingFacture = false;
      },
      error: err => {
        this.uploadingFacture = false;
        this.handleError(err);
      }
    });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const v = this.form.value;
    // Dates are sent as UTC midnight of the picked day: the amount belongs to a
    // calendar day, not to a clock time.
    const expenseDate = new Date(`${v.expenseDate}T00:00:00.000Z`);

    if (this.isEdit) {
      const command = new UpdateExpenseCommand({
        id: this.expenseId,
        carId: v.carId,
        expenseTypeId: v.expenseTypeId,
        expenseDate,
        amount: v.amount,
        description: v.description || undefined
      });
      this.client.updateExpense(this.expenseId!, command).subscribe({
        next: () => this.backToExpenses(),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateExpenseCommand({
        carId: v.carId,
        expenseTypeId: v.expenseTypeId,
        expenseDate,
        amount: v.amount,
        paidAmount: v.paidAmount || 0,
        description: v.description || undefined
      });
      this.client.createExpense(command).subscribe({
        next: () => this.backToExpenses(),
        error: err => this.handleError(err)
      });
    }
  }

  cancel() {
    this.backToExpenses();
  }

  // The expense register is the finance screen's payable tab, which has to be
  // asked for by name — landing on /credit alone opens the receivable side.
  private backToExpenses() {
    this.router.navigate(['/credit'], { queryParams: { tab: 'expenses' } });
  }

  private handleError(err: any) {
    this.saving = false;
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
