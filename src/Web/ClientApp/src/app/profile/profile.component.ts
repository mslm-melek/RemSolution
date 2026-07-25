import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  UsersClient, MyProfileDto, UpdateMyProfileCommand, ChangeMyPasswordCommand, CurrentUserDto
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { extractValidationErrors } from '../shared/form-utils';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  profileForm: FormGroup;
  passwordForm: FormGroup;

  userName = '';
  role?: string | null;
  agencyName?: string | null;
  // Header display (current saved values, not the live edit form).
  headerName = '';
  initial = '?';

  savingProfile = false;
  savingPassword = false;
  profileError = '';
  profileSuccess = '';
  passwordError = '';
  passwordSuccess = '';

  constructor(
    private fb: FormBuilder,
    private client: UsersClient,
    private auth: AuthService
  ) {
    this.profileForm = this.fb.group({
      fullName: ['', Validators.maxLength(200)],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]]
    });
    this.passwordForm = this.fb.group({
      currentPassword: ['', Validators.required],
      newPassword: ['', Validators.required],
      confirmPassword: ['', Validators.required]
    });
  }

  ngOnInit() {
    this.client.getMyProfile().subscribe({
      next: (p: MyProfileDto) => {
        this.userName = p.userName ?? '';
        this.headerName = p.fullName || p.userName || '';
        this.initial = (this.headerName || '?').trim().charAt(0).toUpperCase() || '?';
        this.profileForm.patchValue({ fullName: p.fullName ?? '', email: p.email ?? '' });
      },
      error: err => this.profileError = extractValidationErrors(err) ?? 'Could not load your profile.'
    });

    // Role and agency are read-only context, taken from the current-user probe.
    this.auth.currentUser$.subscribe((user: CurrentUserDto) => {
      this.role = user.role;
      this.agencyName = user.agencyName;
    });
  }

  saveProfile() {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    this.savingProfile = true;
    this.profileError = '';
    this.profileSuccess = '';
    const v = this.profileForm.value;

    const command = new UpdateMyProfileCommand({
      fullName: v.fullName || undefined,
      email: v.email
    });

    this.client.updateMyProfile(command).subscribe({
      next: () => {
        // The name in the nav bar and (on email change) the login are cached in
        // the SPA; reload so both reflect the change.
        window.location.reload();
      },
      error: err => {
        this.savingProfile = false;
        this.profileError = extractValidationErrors(err) ?? 'Could not save your profile.';
      }
    });
  }

  changePassword() {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }
    const v = this.passwordForm.value;
    if (v.newPassword !== v.confirmPassword) {
      this.passwordError = 'The new password and its confirmation do not match.';
      return;
    }
    this.savingPassword = true;
    this.passwordError = '';
    this.passwordSuccess = '';

    const command = new ChangeMyPasswordCommand({
      currentPassword: v.currentPassword,
      newPassword: v.newPassword
    });

    this.client.changeMyPassword(command).subscribe({
      next: () => {
        this.savingPassword = false;
        this.passwordSuccess = 'Your password has been changed.';
        this.passwordForm.reset();
      },
      error: err => {
        this.savingPassword = false;
        this.passwordError = extractValidationErrors(err) ?? 'Could not change your password.';
      }
    });
  }
}
