import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import {
  ExtraServiceTypesClient, ExtraServicesTypeDto,
  CreateExtraServicesTypeCommand, UpdateExtraServicesTypeCommand,
  ExtraServiceTypeTemplatesClient, ExtraServicesTypeTemplateDto,
  CreateExtraServicesTypeTemplateCommand, UpdateExtraServicesTypeTemplateCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { AuthService } from '../shared/auth.service';
import { ImpersonationService } from '../shared/impersonation.service';
import { TranslocoService } from '@jsverse/transloco';
import { catalogName } from '../shared/catalog-name.pipe';

// One row shape for both lists; a template has no price and no origin flags.
type ExtraServiceTypeRow = ExtraServicesTypeDto | ExtraServicesTypeTemplateDto;

/**
 * Two catalogs on one screen, as for expense types: the platform's templates
 * outside any workspace, the agency's own catalog everywhere else. Only the
 * agency's catalog carries a price — it is in the agency's currency, which a
 * template has no way to know.
 */
@Component({
  selector: 'app-extra-service-type',
  templateUrl: './extra-service-type.component.html',
  styleUrls: ['./extra-service-type.component.css']
})
export class ExtraServiceTypeComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly auth = inject(AuthService);
  private readonly impersonation = inject(ImpersonationService);
  private readonly templatesClient = inject(ExtraServiceTypeTemplatesClient);

  /** Editing the platform's templates rather than an agency's catalog. */
  platformMode = false;

  types: ExtraServiceTypeRow[] = [];
  dataSource = new MatTableDataSource<ExtraServiceTypeRow>([]);

  @ViewChild(MatSort) sort!: MatSort;
  displayedColumns: string[] = ['name', 'amount', 'active', 'actions'];
  errorMessage = '';

  // Edit buffer: when editingId is set the form updates that row, otherwise it
  // creates a new type.
  editingId?: number;
  editingStandard = false;
  name = '';
  // A standard copy's key and how the form showed it: left as shown, the key is
  // what gets saved, so opening and saving a row doesn't customise it.
  private storedName = '';
  private shownName = '';
  amount: number | null = null;
  isActive = true;

  constructor(private client: ExtraServiceTypesClient) {
    this.dataSource.sortingDataAccessor = (type, column) => {
      switch (column) {
        case 'amount': return this.price(type)?.amount ?? 0;
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
      this.displayedColumns = this.platformMode
        ? ['name', 'active', 'actions']
        : ['name', 'amount', 'active', 'actions'];
      this.load();
    });
  }

  load() {
    const types$: Observable<ExtraServiceTypeRow[]> = this.platformMode
      ? this.templatesClient.getExtraServiceTypeTemplates()
      : this.client.getExtraServiceTypes(false);

    types$.subscribe({
      next: types => {
        this.types = types || [];
        this.dataSource.data = this.types;
      },
      error: err => console.error(err)
    });
  }

  price(type: ExtraServiceTypeRow) {
    return (type as ExtraServicesTypeDto).amount;
  }

  isStandard(type: ExtraServiceTypeRow): boolean {
    return (type as ExtraServicesTypeDto).isStandard === true;
  }

  isCustomized(type: ExtraServiceTypeRow): boolean {
    return (type as ExtraServicesTypeDto).isCustomized === true;
  }

  edit(type: ExtraServiceTypeRow) {
    this.editingId = type.id;
    this.editingStandard = this.isStandard(type) && !this.isCustomized(type);
    // The platform edits the key itself; an agency edits the words it reads.
    this.storedName = type.name ?? '';
    this.shownName = this.platformMode ? this.storedName : catalogName(this.transloco, this.storedName);
    this.name = this.shownName;
    this.amount = this.price(type)?.amount ?? null;
    this.isActive = type.isActive ?? true;
  }

  resetForm() {
    this.editingId = undefined;
    this.editingStandard = false;
    this.name = '';
    this.storedName = '';
    this.shownName = '';
    this.amount = null;
    this.isActive = true;
    this.errorMessage = '';
  }

  save() {
    if (!this.name.trim()) {
      this.errorMessage = this.transloco.translate('extraServiceType.nameRequired');
      return;
    }
    this.errorMessage = '';

    const names = {
      name: this.name.trim() === this.shownName ? this.storedName : this.name.trim()
    };
    const done = {
      next: () => { this.resetForm(); this.load(); },
      error: (err: unknown) => this.handleError(err)
    };

    if (this.platformMode) {
      if (this.editingId) {
        const command = new UpdateExtraServicesTypeTemplateCommand({ id: this.editingId, isActive: this.isActive, ...names });
        this.templatesClient.updateExtraServiceTypeTemplate(this.editingId, command).subscribe(done);
      } else {
        this.templatesClient.createExtraServiceTypeTemplate(new CreateExtraServicesTypeTemplateCommand(names)).subscribe(done);
      }
    } else if (this.editingId) {
      const command = new UpdateExtraServicesTypeCommand({
        id: this.editingId, isActive: this.isActive, amount: this.amount ?? undefined, ...names
      });
      this.client.updateExtraServiceType(this.editingId, command).subscribe(done);
    } else {
      const command = new CreateExtraServicesTypeCommand({ amount: this.amount ?? undefined, ...names });
      this.client.createExtraServiceType(command).subscribe(done);
    }
  }

  deactivate(type: ExtraServiceTypeRow) {
    if (!type.id) return;

    const key = this.platformMode ? 'catalog.confirmRetire' : 'extraServiceType.confirmDeactivate';
    if (!confirm(this.transloco.translate(key, { name: catalogName(this.transloco, type.name) }))) return;

    const request$ = this.platformMode
      ? this.templatesClient.deactivateExtraServiceTypeTemplate(type.id)
      : this.client.deactivateExtraServiceType(type.id);

    request$.subscribe({
      next: () => this.load(),
      error: err => this.handleError(err)
    });
  }

  /** Back to the platform's name; the agency's price stays. */
  reset(type: ExtraServiceTypeRow) {
    if (!type.id) return;
    if (!confirm(this.transloco.translate('extraServiceType.confirmReset', { name: catalogName(this.transloco, type.name) }))) return;

    this.client.resetExtraServiceType(type.id).subscribe({
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
