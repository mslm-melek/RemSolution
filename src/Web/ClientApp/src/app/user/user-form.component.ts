import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  UsersClient, AgencyUserDto,
  CreateAgencyUserByAdminCommand, UpdateAgencyUserCommand, ResetAgencyUserPasswordCommand
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';

// Must match the Domain Permissions constants; the API rejects anything else.
const ALL_PERMISSIONS = [
  'Car.Create', 'Car.Read', 'Car.Update', 'Car.Delete',
  'Client.Create', 'Client.Read', 'Client.Update', 'Client.Delete'
];

@Component({
  selector: 'app-user-form',
  templateUrl: './user-form.component.html',
  styleUrls: ['./user-form.component.css']
})
export class UserFormComponent implements OnInit {
  agencyId!: number;
  userId?: string;
  form: FormGroup;
  passwordForm: FormGroup;
  saving = false;
  resetting = false;
  errorMessage = '';
  passwordMessage = '';

  readonly allPermissions = ALL_PERMISSIONS;
  // The staff role is 'AgencyStaff'; administrators hold every permission implicitly.
  readonly staffRole = 'AgencyStaff';
  readonly adminRole = 'AgencyAdministrator';

  constructor(
    private fb: FormBuilder,
    private client: UsersClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      userName: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      password: ['', Validators.required],
      role: [this.staffRole, Validators.required],
      permissions: this.fb.control<string[]>([])
    });

    this.passwordForm = this.fb.group({
      newPassword: ['', Validators.required]
    });
  }

  get isEdit(): boolean {
    return this.userId !== undefined;
  }

  get isStaff(): boolean {
    return this.form.get('role')!.value === this.staffRole;
  }

  ngOnInit() {
    this.agencyId = +this.route.snapshot.paramMap.get('id')!;
    const userIdParam = this.route.snapshot.paramMap.get('userId');

    if (userIdParam) {
      this.userId = userIdParam;
      // Editing: username is fixed, password is reset separately.
      this.form.get('userName')!.disable();
      this.form.get('password')!.clearValidators();
      this.form.get('password')!.updateValueAndValidity();

      this.client.getAgencyUserById(this.userId).subscribe({
        next: dto => this.populate(dto),
        error: err => console.error(err)
      });
    }
  }

  private populate(dto: AgencyUserDto) {
    this.form.patchValue({
      userName: dto.userName ?? '',
      role: dto.role ?? this.staffRole,
      permissions: dto.permissions ?? []
    });
  }

  hasPermission(permission: string): boolean {
    return (this.form.get('permissions')!.value as string[]).includes(permission);
  }

  togglePermission(permission: string, checked: boolean) {
    const current = new Set(this.form.get('permissions')!.value as string[]);
    checked ? current.add(permission) : current.delete(permission);
    this.form.get('permissions')!.setValue(Array.from(current));
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    const v = this.form.getRawValue();
    // Administrators hold every permission implicitly.
    const permissions = v.role === this.adminRole ? [] : v.permissions;

    if (this.isEdit) {
      const command = new UpdateAgencyUserCommand({ userId: this.userId, role: v.role, permissions });
      this.client.updateAgencyUser(this.userId!, command).subscribe({
        next: () => this.router.navigate(['/agency', this.agencyId]),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateAgencyUserByAdminCommand({
        agencyId: this.agencyId,
        userName: v.userName,
        password: v.password,
        role: v.role,
        permissions
      });
      this.client.createAgencyUserByAdmin(command).subscribe({
        next: () => this.router.navigate(['/agency', this.agencyId]),
        error: err => this.handleError(err)
      });
    }
  }

  resetPassword() {
    if (this.passwordForm.invalid || !this.userId) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    this.resetting = true;
    this.passwordMessage = '';
    const command = new ResetAgencyUserPasswordCommand({
      userId: this.userId,
      newPassword: this.passwordForm.value.newPassword
    });
    this.client.resetAgencyUserPassword(this.userId, command).subscribe({
      next: () => {
        this.resetting = false;
        this.passwordForm.reset();
        this.passwordMessage = 'Password updated.';
      },
      error: err => {
        this.resetting = false;
        this.passwordMessage = extractValidationErrors(err) || 'Could not update the password.';
      }
    });
  }

  private handleError(err: any) {
    this.saving = false;
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors || 'An unexpected error occurred. Please try again.';
    if (!validationErrors) console.error(err);
  }
}
