import { Component, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslocoService } from '@jsverse/transloco';
import { CountryDto } from '../web-api-client';
import { MapPickerComponent, PickedLocation } from './map-picker.component';

// One branch as the screen holds it, saved or not: `id` is set once the server
// has one. Coordinates are a pair or absent, which is what the API validates.
export interface BranchDraft {
  id?: number;
  name: string;
  countryId: number | null;
  address: string | null;
  latitude: number | null;
  longitude: number | null;
}

// An edit to a row that is already in the list. `target` is the object the
// parent holds, so it can find the row without an id — which is how a branch
// entered for an agency that does not exist yet is identified.
export interface BranchEdit {
  target: BranchDraft;
  values: BranchDraft;
}

/**
 * Edits an agency's branches — the places customers actually collect cars from.
 *
 * Deliberately does no saving of its own: it reports what the user did and
 * redisplays whatever `branches` it is given back. That is what lets the same
 * component serve a new agency, where rows are held in memory until the agency
 * exists and are then created with it in one transaction, and an existing one,
 * where each change is a request — against a different endpoint depending on
 * whether the caller is the agency's administrator or the platform's.
 *
 * `branches` is read as an immutable list: parents replace the array rather than
 * mutating it, which is also what the table below needs in order to redraw.
 */
@Component({
  selector: 'app-branches-editor',
  templateUrl: './branches-editor.component.html',
  styleUrls: ['./branches-editor.component.css']
})
export class BranchesEditorComponent {
  // Confirm dialogs are plain strings, so they are translated imperatively
  // rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  @ViewChild(MapPickerComponent) private picker?: MapPickerComponent;

  @Input() branches: BranchDraft[] = [];
  @Input() countries: CountryDto[] = [];
  // Set while the parent has a request in flight, so a double click cannot send
  // the same branch twice.
  @Input() saving = false;
  // Country of the agency the branches belong to: the sensible default for a new
  // branch, since most agencies operate in one country.
  @Input() defaultCountryId: number | null = null;

  @Output() added = new EventEmitter<BranchDraft>();
  @Output() updated = new EventEmitter<BranchEdit>();
  @Output() removed = new EventEmitter<BranchDraft>();

  readonly columns = ['name', 'address', 'country', 'pin', 'actions'];

  form: FormGroup;
  // The row being edited, or null when the panel is adding a new one.
  editing: BranchDraft | null = null;
  panelOpen = false;

  latitude: number | null = null;
  longitude: number | null = null;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      countryId: [null, Validators.required],
      address: ['', Validators.maxLength(500)]
    });
  }

  openNew() {
    this.editing = null;
    this.panelOpen = true;
    this.latitude = null;
    this.longitude = null;
    this.form.reset({ name: '', countryId: this.defaultCountryId, address: '' });
  }

  edit(branch: BranchDraft) {
    this.editing = branch;
    this.panelOpen = true;
    this.latitude = branch.latitude;
    this.longitude = branch.longitude;
    this.form.reset({
      name: branch.name ?? '',
      countryId: branch.countryId,
      address: branch.address ?? ''
    });
  }

  cancel() {
    this.panelOpen = false;
    this.editing = null;
  }

  // The picker reports coordinates first and the reverse-geocoded address a
  // moment later (see MapPickerComponent), so an address of null here means
  // "nothing to suggest yet", never "clear what is typed".
  onPicked(picked: PickedLocation) {
    this.latitude = picked.latitude;
    this.longitude = picked.longitude;

    if (picked.address) {
      this.form.patchValue({ address: picked.address });
    }
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.value;
    const values: BranchDraft = {
      id: this.editing?.id,
      name: (value.name ?? '').trim(),
      countryId: value.countryId,
      address: (value.address ?? '').trim() || null,
      latitude: this.latitude,
      longitude: this.longitude
    };

    if (this.editing) {
      this.updated.emit({ target: this.editing, values });
    } else {
      this.added.emit(values);
    }

    this.cancel();
  }

  remove(branch: BranchDraft) {
    // Only a saved branch is worth confirming: discarding a row that has not
    // been sent anywhere costs nothing to redo.
    if (branch.id !== undefined &&
        !confirm(this.transloco.translate('branch.confirmDelete', { name: branch.name }))) {
      return;
    }

    if (this.editing === branch) this.cancel();

    this.removed.emit(branch);
  }

  countryName(countryId: number | null): string {
    return this.countries.find(country => country.id === countryId)?.name ?? '';
  }

  // Leaflet needs re-measuring when the editor was built inside a hidden tab;
  // the My agency page forwards its tab change here.
  refresh() {
    this.picker?.refresh();
  }
}
