import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ClientsClient, CountriesClient, CountryDto, ClientDto,
  CreateClientCommand, UpdateClientCommand, ClientDocumentType, FileParameter
} from '../web-api-client';
import { toDateInput, fromDateInput, extractValidationErrors } from '../shared/form-utils';

interface DocumentSlot {
  type: ClientDocumentType;
  label: string;
  url?: string;
  uploading: boolean;
}

@Component({
  selector: 'app-client-form',
  templateUrl: './client-form.component.html',
  styleUrls: ['./client-form.component.css']
})
export class ClientFormComponent implements OnInit {
  form: FormGroup;
  countries: CountryDto[] = [];
  clientId?: number;
  saving = false;
  errorMessage = '';

  documents: DocumentSlot[] = [
    { type: ClientDocumentType.CIN, label: 'CIN', uploading: false },
    { type: ClientDocumentType.DrivingLicence, label: 'Driving Licence', uploading: false },
    { type: ClientDocumentType.Passeport, label: 'Passeport', uploading: false }
  ];

  constructor(
    private fb: FormBuilder,
    private client: ClientsClient,
    private countriesClient: CountriesClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      birthDate: ['', Validators.required],
      birthPlace: [''],
      birthCountryId: [null],
      cin: [''],
      cinDeliveranceDate: [''],
      cinDeliverancePlace: [''],
      cinDeliveranceCountryId: [null],
      passeportNumber: [''],
      passeportDeliveranceDate: [''],
      passeportDeliverancePlace: [''],
      passeportDeliveranceCountryId: [null],
      drivingLicenceNumber: [''],
      drivingLicenceDeliveranceDate: [''],
      drivingLicenceDeliverancePlace: [''],
      drivingLicenceDeliveranceCountryId: [null],
      description: ['']
    });
  }

  get isEdit(): boolean {
    return this.clientId !== undefined;
  }

  ngOnInit() {
    this.countriesClient.getCountries().subscribe({
      next: countries => this.countries = countries || [],
      error: err => console.error(err)
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.clientId = +idParam;
      this.client.getClientById(this.clientId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
    }
  }

  private populate(dto: ClientDto) {
    this.form.patchValue({
      firstName: dto.firstName ?? '',
      lastName: dto.lastName ?? '',
      birthDate: toDateInput(dto.birthDate),
      birthPlace: dto.birthPlace ?? '',
      birthCountryId: dto.birthCountryId ?? null,
      cin: dto.cin ?? '',
      cinDeliveranceDate: toDateInput(dto.cinDeliveranceDate),
      cinDeliverancePlace: dto.cinDeliverancePlace ?? '',
      cinDeliveranceCountryId: dto.cinDeliveranceCountryId ?? null,
      passeportNumber: dto.passeportNumber ?? '',
      passeportDeliveranceDate: toDateInput(dto.passeportDeliveranceDate),
      passeportDeliverancePlace: dto.passeportDeliverancePlace ?? '',
      passeportDeliveranceCountryId: dto.passeportDeliveranceCountryId ?? null,
      drivingLicenceNumber: dto.drivingLicenceNumber ?? '',
      drivingLicenceDeliveranceDate: toDateInput(dto.drivingLicenceDeliveranceDate),
      drivingLicenceDeliverancePlace: dto.drivingLicenceDeliverancePlace ?? '',
      drivingLicenceDeliveranceCountryId: dto.drivingLicenceDeliveranceCountryId ?? null,
      description: dto.description ?? ''
    });

    this.documents[0].url = dto.cinImageUrl;
    this.documents[1].url = dto.drivingLicenceImageUrl;
    this.documents[2].url = dto.passerportImageUrl;
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const payload = this.toPayload();

    if (this.isEdit) {
      const command = new UpdateClientCommand({ id: this.clientId, ...payload });
      this.client.updateClient(this.clientId!, command).subscribe({
        next: () => this.router.navigate(['/client']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateClientCommand(payload);
      this.client.createClient(command).subscribe({
        // Land on the edit page so documents can be uploaded right away.
        next: id => this.router.navigate(['/client', id]),
        error: err => this.handleError(err)
      });
    }
  }

  onFileSelected(slot: DocumentSlot, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file || !this.clientId) return;

    slot.uploading = true;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.client.uploadClientDocument(this.clientId, slot.type, parameter).subscribe({
      next: url => {
        slot.url = url;
        slot.uploading = false;
      },
      error: err => {
        slot.uploading = false;
        this.handleError(err);
      }
    });
  }

  private toPayload() {
    const v = this.form.value;
    return {
      firstName: v.firstName,
      lastName: v.lastName,
      birthDate: fromDateInput(v.birthDate),
      birthPlace: v.birthPlace || undefined,
      birthCountryId: v.birthCountryId ?? undefined,
      cin: v.cin || undefined,
      cinDeliveranceDate: fromDateInput(v.cinDeliveranceDate),
      cinDeliverancePlace: v.cinDeliverancePlace || undefined,
      cinDeliveranceCountryId: v.cinDeliveranceCountryId ?? undefined,
      passeportNumber: v.passeportNumber || undefined,
      passeportDeliveranceDate: fromDateInput(v.passeportDeliveranceDate),
      passeportDeliverancePlace: v.passeportDeliverancePlace || undefined,
      passeportDeliveranceCountryId: v.passeportDeliveranceCountryId ?? undefined,
      drivingLicenceNumber: v.drivingLicenceNumber || undefined,
      drivingLicenceDeliveranceDate: fromDateInput(v.drivingLicenceDeliveranceDate),
      drivingLicenceDeliverancePlace: v.drivingLicenceDeliverancePlace || undefined,
      drivingLicenceDeliveranceCountryId: v.drivingLicenceDeliveranceCountryId ?? undefined,
      description: v.description || undefined
    };
  }

  private handleError(err: any) {
    this.saving = false;

    const validationErrors = extractValidationErrors(err);
    if (validationErrors) {
      this.errorMessage = validationErrors;
    } else {
      this.errorMessage = 'An unexpected error occurred. Please try again.';
      console.error(err);
    }
  }
}
