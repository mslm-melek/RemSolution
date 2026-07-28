import { Component, OnInit, inject } from '@angular/core';
import { PageEvent } from '@angular/material/paginator';
import {
  ExpensesClient, ExpenseDto, CarsClient, CarDto,
  ExpenseTypesClient, ExpenseTypeDto, RecordExpensePaymentCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-expense',
  templateUrl: './expense.component.html',
  styleUrls: ['./expense.component.css']
})
export class ExpenseComponent implements OnInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  expenses: ExpenseDto[] = [];
  displayedColumns: string[] = ['date', 'car', 'type', 'amount', 'paid', 'outstanding', 'actions'];
  errorMessage = '';

  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // Filters.
  carId: number | null = null;
  expenseTypeId: number | null = null;
  onlyUnpaid = false;

  cars: CarDto[] = [];
  types: ExpenseTypeDto[] = [];

  // A settlement is an update of the running paid total, so it rides on the
  // update permission (matching the API).
  canUpdate = false;
  canDelete = false;
  canCreate = false;

  constructor(
    private client: ExpensesClient,
    private carsClient: CarsClient,
    private typesClient: ExpenseTypesClient,
    private auth: AuthService) { }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      this.canCreate = AuthService.canAccessModule(user, 'Expenses', 'Expense.Create');
      this.canUpdate = AuthService.canAccessModule(user, 'Expenses', 'Expense.Update');
      this.canDelete = AuthService.canAccessModule(user, 'Expenses', 'Expense.Delete');
    });

    // Filter pickers: one page big enough to hold an agency's fleet/catalog.
    this.carsClient.getCars(1, 1000, null, null, null).subscribe({
      next: result => this.cars = result.items || [],
      error: err => console.error(err)
    });
    this.typesClient.getExpenseTypes(false).subscribe({
      next: types => this.types = types || [],
      error: err => console.error(err)
    });

    this.load();
  }

  load() {
    this.client.getExpenses(
      this.pageNumber, this.pageSize, this.carId, this.expenseTypeId, null, null, this.onlyUnpaid
    ).subscribe({
      next: result => {
        this.expenses = result.items || [];
        this.totalCount = result.totalCount || 0;
      },
      error: err => this.handleError(err)
    });
  }

  onFilter() {
    this.pageNumber = 1;
    this.load();
  }

  clearFilters() {
    this.carId = null;
    this.expenseTypeId = null;
    this.onlyUnpaid = false;
    this.onFilter();
  }

  onPage(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  carLabel(car: CarDto): string {
    return car.modelName ? `${car.matricule} — ${car.modelName}` : (car.matricule ?? '');
  }

  isSettled(expense: ExpenseDto): boolean {
    return (expense.outstanding?.amount ?? 0) <= 0;
  }

  // Settles part of an expense. The API takes a delta and enforces the ceiling,
  // so the amount typed here is added to what is already settled.
  settle(expense: ExpenseDto) {
    if (!expense.id) return;

    const outstanding = expense.outstanding?.amount ?? 0;
    const answer = prompt(
      this.transloco.translate('expense.settlePrompt', { amount: outstanding }),
      String(outstanding));

    if (answer === null) return;

    const amount = Number(answer.replace(',', '.'));

    if (!isFinite(amount) || amount === 0) {
      this.errorMessage = this.transloco.translate('expense.settleInvalid');
      return;
    }

    this.errorMessage = '';
    const command = new RecordExpensePaymentCommand({ id: expense.id, amount });
    this.client.recordExpensePayment(expense.id, command).subscribe({
      next: () => this.load(),
      error: err => this.handleError(err)
    });
  }

  delete(expense: ExpenseDto) {
    if (!expense.id) return;
    if (!confirm(this.transloco.translate('expense.confirmDelete'))) return;

    this.client.deleteExpense(expense.id).subscribe({
      next: () => this.load(),
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
