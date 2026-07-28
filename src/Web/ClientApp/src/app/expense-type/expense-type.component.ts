import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import {
  ExpenseTypesClient, ExpenseTypeDto,
  CreateExpenseTypeCommand, UpdateExpenseTypeCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

@Component({
  selector: 'app-expense-type',
  templateUrl: './expense-type.component.html',
  styleUrls: ['./expense-type.component.css']
})
export class ExpenseTypeComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  types: ExpenseTypeDto[] = [];
  dataSource = new MatTableDataSource<ExpenseTypeDto>([]);

  @ViewChild(MatSort) sort!: MatSort;
  displayedColumns: string[] = ['name', 'schedule', 'notify', 'active', 'actions'];
  errorMessage = '';

  // Edit buffer: when editingId is set the form updates that row, else creates.
  editingId?: number;
  name = '';
  withNotif = false;
  afterKilometer: number | null = null;
  afterMonth: number | null = null;
  isActive = true;

  constructor(private client: ExpenseTypesClient) {
    // "Due" shows kilometres and/or months in one column; it sorts by the
    // interval that is actually set, months first.
    this.dataSource.sortingDataAccessor = (type, column) => {
      switch (column) {
        case 'schedule': return type.afterMonth ?? type.afterKilometer ?? 0;
        case 'notify': return type.withNotif ? 1 : 0;
        case 'active': return type.isActive ? 1 : 0;
        default: return type.name ?? '';
      }
    };
  }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getExpenseTypes(false).subscribe({
      next: types => {
        this.types = types || [];
        this.dataSource.data = this.types;
      },
      error: err => console.error(err)
    });
  }

  edit(type: ExpenseTypeDto) {
    this.editingId = type.id;
    this.name = type.name ?? '';
    this.withNotif = type.withNotif ?? false;
    this.afterKilometer = type.afterKilometer ?? null;
    this.afterMonth = type.afterMonth ?? null;
    this.isActive = type.isActive ?? true;
  }

  resetForm() {
    this.editingId = undefined;
    this.name = '';
    this.withNotif = false;
    this.afterKilometer = null;
    this.afterMonth = null;
    this.isActive = true;
    this.errorMessage = '';
  }

  save() {
    if (!this.name.trim()) {
      this.errorMessage = this.transloco.translate('expenseType.nameRequired');
      return;
    }
    this.errorMessage = '';

    if (this.editingId) {
      const command = new UpdateExpenseTypeCommand({
        id: this.editingId,
        name: this.name.trim(),
        isActive: this.isActive,
        withNotif: this.withNotif,
        afterKilometer: this.afterKilometer ?? undefined,
        afterMonth: this.afterMonth ?? undefined
      });
      this.client.updateExpenseType(this.editingId, command).subscribe({
        next: () => { this.resetForm(); this.load(); },
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateExpenseTypeCommand({
        name: this.name.trim(),
        withNotif: this.withNotif,
        afterKilometer: this.afterKilometer ?? undefined,
        afterMonth: this.afterMonth ?? undefined
      });
      this.client.createExpenseType(command).subscribe({
        next: () => { this.resetForm(); this.load(); },
        error: err => this.handleError(err)
      });
    }
  }

  deactivate(type: ExpenseTypeDto) {
    if (!type.id) return;
    if (!confirm(this.transloco.translate('expenseType.confirmDeactivate', { name: type.name }))) return;
    this.client.deactivateExpenseType(type.id).subscribe({
      next: () => this.load(),
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
