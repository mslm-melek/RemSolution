import { Component, OnInit } from '@angular/core';
import {
  ExtraServiceTypesClient, ExtraServicesTypeDto,
  CreateExtraServicesTypeCommand, UpdateExtraServicesTypeCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-extra-service-type',
  templateUrl: './extra-service-type.component.html',
  styleUrls: ['./extra-service-type.component.css']
})
export class ExtraServiceTypeComponent implements OnInit {
  types: ExtraServicesTypeDto[] = [];
  displayedColumns: string[] = ['name', 'amount', 'active', 'actions'];
  errorMessage = '';

  // Edit buffer: when editingId is set the form updates that row, otherwise it
  // creates a new type.
  editingId?: number;
  name = '';
  amount: number | null = null;
  isActive = true;

  constructor(private client: ExtraServiceTypesClient) { }

  ngOnInit() {
    this.load();
  }

  load() {
    this.client.getExtraServiceTypes(false).subscribe({
      next: types => this.types = types || [],
      error: err => console.error(err)
    });
  }

  edit(type: ExtraServicesTypeDto) {
    this.editingId = type.id;
    this.name = type.name ?? '';
    this.amount = type.amount ?? null;
    this.isActive = type.isActive ?? true;
  }

  resetForm() {
    this.editingId = undefined;
    this.name = '';
    this.amount = null;
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
      const command = new UpdateExtraServicesTypeCommand({
        id: this.editingId,
        name: this.name.trim(),
        amount: this.amount ?? undefined,
        isActive: this.isActive
      });
      this.client.updateExtraServiceType(this.editingId, command).subscribe({
        next: () => { this.resetForm(); this.load(); },
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateExtraServicesTypeCommand({
        name: this.name.trim(),
        amount: this.amount ?? undefined
      });
      this.client.createExtraServiceType(command).subscribe({
        next: () => { this.resetForm(); this.load(); },
        error: err => this.handleError(err)
      });
    }
  }

  deactivate(type: ExtraServicesTypeDto) {
    if (!type.id) return;
    if (!confirm(`Deactivate "${type.name}"? It stays on past extra services but is hidden from new ones.`)) return;
    this.client.deactivateExtraServiceType(type.id).subscribe({
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
