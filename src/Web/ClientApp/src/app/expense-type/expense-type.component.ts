import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import {
  ExpenseTypesClient, ExpenseTypeDto,
  CreateExpenseTypeCommand, UpdateExpenseTypeCommand,
  ExpenseTypeTemplatesClient, ExpenseTypeTemplateDto,
  CreateExpenseTypeTemplateCommand, UpdateExpenseTypeTemplateCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService } from '../shared/impersonation.service';
import { TranslocoService } from '@jsverse/transloco';
import { catalogName } from '../shared/catalog-name.pipe';

// One row shape for both lists; the template DTO simply never sets the last two.
type ExpenseTypeRow = ExpenseTypeDto | ExpenseTypeTemplateDto;

/**
 * Two catalogs on one screen. Outside any workspace the platform administrator
 * edits the standard types every agency is given (templates); everyone else —
 * the platform administrator inside a workspace included — edits their agency's
 * own catalog: its copies of those standard types plus what it added.
 */
@Component({
  selector: 'app-expense-type',
  templateUrl: './expense-type.component.html',
  styleUrls: ['./expense-type.component.css']
})
export class ExpenseTypeComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly templatesClient = inject(ExpenseTypeTemplatesClient);

  /** Editing the platform's templates rather than an agency's catalog. */
  platformMode = false;

  types: ExpenseTypeRow[] = [];
  dataSource = new MatTableDataSource<ExpenseTypeRow>([]);

  @ViewChild(MatSort) sort!: MatSort;
  displayedColumns: string[] = ['name', 'schedule', 'notify', 'active', 'actions'];
  errorMessage = '';

  // Edit buffer: when editingId is set the form updates that row, else creates.
  editingId?: number;
  editingStandard = false;
  name = '';
  // A standard copy's key and how the form showed it: left as shown, the key is
  // what gets saved, so opening and saving a row doesn't customise it.
  private storedName = '';
  private shownName = '';
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
        default: return catalogName(this.transloco, type.name);
      }
    };
  }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.auth.currentUser$.pipe(take(1)).subscribe(user => {
      this.platformMode = AuthService.isPlatformAdmin(user) && !this.impersonation.current;
      this.load();
    });
  }

  load() {
    const types$: Observable<ExpenseTypeRow[]> = this.platformMode
      ? this.templatesClient.getExpenseTypeTemplates()
      : this.client.getExpenseTypes(false);

    types$.subscribe({
      next: types => {
        this.types = types || [];
        this.dataSource.data = this.types;
      },
      error: err => console.error(err)
    });
  }

  isStandard(type: ExpenseTypeRow): boolean {
    return (type as ExpenseTypeDto).isStandard === true;
  }

  isCustomized(type: ExpenseTypeRow): boolean {
    return (type as ExpenseTypeDto).isCustomized === true;
  }

  edit(type: ExpenseTypeRow) {
    this.editingId = type.id;
    this.editingStandard = this.isStandard(type) && !this.isCustomized(type);
    // The platform edits the key itself; an agency edits the words it reads.
    this.storedName = type.name ?? '';
    this.shownName = this.platformMode ? this.storedName : catalogName(this.transloco, this.storedName);
    this.name = this.shownName;
    this.withNotif = type.withNotif ?? false;
    this.afterKilometer = type.afterKilometer ?? null;
    this.afterMonth = type.afterMonth ?? null;
    this.isActive = type.isActive ?? true;
  }

  resetForm() {
    this.editingId = undefined;
    this.editingStandard = false;
    this.name = '';
    this.storedName = '';
    this.shownName = '';
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

    const fields = {
      name: this.name.trim() === this.shownName ? this.storedName : this.name.trim(),
      withNotif: this.withNotif,
      afterKilometer: this.afterKilometer ?? undefined,
      afterMonth: this.afterMonth ?? undefined
    };
    const done = {
      next: () => { this.resetForm(); this.load(); },
      error: (err: unknown) => this.handleError(err)
    };

    if (this.platformMode) {
      if (this.editingId) {
        const command = new UpdateExpenseTypeTemplateCommand({ id: this.editingId, isActive: this.isActive, ...fields });
        this.templatesClient.updateExpenseTypeTemplate(this.editingId, command).subscribe(done);
      } else {
        this.templatesClient.createExpenseTypeTemplate(new CreateExpenseTypeTemplateCommand(fields)).subscribe(done);
      }
    } else if (this.editingId) {
      const command = new UpdateExpenseTypeCommand({ id: this.editingId, isActive: this.isActive, ...fields });
      this.client.updateExpenseType(this.editingId, command).subscribe(done);
    } else {
      this.client.createExpenseType(new CreateExpenseTypeCommand(fields)).subscribe(done);
    }
  }

  deactivate(type: ExpenseTypeRow) {
    if (!type.id) return;

    const key = this.platformMode ? 'catalog.confirmRetire' : 'expenseType.confirmDeactivate';
    if (!confirm(this.transloco.translate(key, { name: catalogName(this.transloco, type.name) }))) return;

    const request$ = this.platformMode
      ? this.templatesClient.deactivateExpenseTypeTemplate(type.id)
      : this.client.deactivateExpenseType(type.id);

    request$.subscribe({
      next: () => this.load(),
      error: err => this.handleError(err)
    });
  }

  /** Back to the platform's version; the type's on/off state stays as it is. */
  reset(type: ExpenseTypeRow) {
    if (!type.id) return;
    if (!confirm(this.transloco.translate('expenseType.confirmReset', { name: catalogName(this.transloco, type.name) }))) return;

    this.client.resetExpenseType(type.id).subscribe({
      next: () => { this.resetForm(); this.load(); },
      error: err => this.handleError(err)
    });
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
