import { Component, OnInit } from '@angular/core';
import {
  ExpenseTypesClient, ExpenseTypeDto,
  CreateExpenseTypeCommand, UpdateExpenseTypeCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-expense-type',
  templateUrl: './expense-type.component.html',
  styleUrls: ['./expense-type.component.css']
})
export class ExpenseTypeComponent implements OnInit {
  types: ExpenseTypeDto[] = [];
  displayedColumns: string[] = ['name', 'schedule', 'notify', 'active', 'actions'];
  errorMessage = '';

  // Edit buffer: when editingId is set the form updates that row, else creates.
  editingId?: number;
  name = '';
  withNotif = false;
  afterKilometer: number | null = null;
  afterMonth: number | null = null;
  isActive = true;

  constructor(private client: ExpenseTypesClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getExpenseTypes(false).subscribe({
      next: types => this.types = types || [],
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
      this.errorMessage = 'Name is required.';
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
    if (!confirm(`Deactivate "${type.name}"? It stays on past expenses but is hidden from new ones.`)) return;
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
