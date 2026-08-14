import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  UsersClient, MyProfileDto, UpdateMyProfileCommand, ChangeMyPasswordCommand, CurrentUserDto
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';
import { LanguageService } from '../shared/language.service';
import { AppLanguage } from '../shared/language';
import { ThemeService } from '../shared/theme.service';
import { ThemeChoice } from '../shared/theme';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  // Error banners are plain strings, so they are translated imperatively.
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly theme = inject(ThemeService);

  readonly languages = this.language.available;
  readonly themes = this.theme.available;

  profileForm: FormGroup;
  passwordForm: FormGroup;

  userName = '';
  role?: string | null;
  agencyName?: string | null;
  // Header display (current saved values, not the live edit form).
  headerName = '';
  initial = '?';

  // Still on its invitation password: the rest of the app is closed until it changes.
  mustChangePassword = false;

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
      this.mustChangePassword = user.mustChangePassword === true;
    });
  }

  /** Transloco key for this account's role, under `roles.*` — as the rail does. */
  get roleLabelKey(): string {
    switch (this.role) {
      case 'PlatformAdministrator': return 'roles.platformAdmin';
      case 'AgencyAdministrator': return 'roles.agencyAdmin';
      case 'Customer': return 'roles.customer';
      default: return 'roles.agencyStaff';
    }
  }

  get currentLanguage(): AppLanguage {
    return this.language.current;
  }

  // Saves on the account and reloads — see LanguageService.use.
  setLanguage(language: AppLanguage) {
    this.language.use(language);
  }

  // What was chosen, which includes 'system' — not what is currently painted.
  get currentTheme(): ThemeChoice {
    return this.theme.choice;
  }

  // No reload: colours are custom properties, so the attribute on <html> is all
  // it takes (see ThemeService).
  setTheme(choice: ThemeChoice) {
    this.theme.use(choice);
  }

  themeIcon(choice: ThemeChoice): string {
    switch (choice) {
      case 'light': return 'light_mode';
      case 'dark': return 'dark_mode';
      default: return 'contrast';
    }
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
        // The nav name and the login are cached in the SPA; reload to refresh them.
        window.location.reload();
      },
      error: err => {
        this.savingProfile = false;
        this.profileError = extractValidationErrors(err) ?? this.transloco.translate('profile.saveFailed');
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
      this.passwordError = this.transloco.translate('profile.passwordMismatch');
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
        this.passwordSuccess = this.transloco.translate('profile.passwordChanged');
        this.passwordForm.reset();

        // Release the guard that pinned a provisioned account to this page.
        if (this.mustChangePassword) {
          this.mustChangePassword = false;
          this.auth.markPasswordChanged();
        }
      },
      error: err => {
        this.savingPassword = false;
        this.passwordError = extractValidationErrors(err) ?? this.transloco.translate('profile.passwordFailed');
      }
    });
  }
}
