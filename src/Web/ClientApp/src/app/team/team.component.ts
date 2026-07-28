import { AfterViewInit, Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  UsersClient, AgencyUserDto,
  CreateAgencyUserCommand, UpdateMyAgencyUserCommand, SetMyAgencyUserActiveCommand
} from '../web-api-client';
import { AuthService } from '../shared/auth.service';
import { FEATURES, PERMISSIONS_BY_FEATURE } from '../shared/feature-catalog';
import { extractValidationErrors } from '../shared/form-utils';
import { TranslocoService } from '@jsverse/transloco';

interface FeatureGroup {
  key: string;
  labelKey: string;
  permissions: string[];
}

@Component({
  selector: 'app-team',
  templateUrl: './team.component.html',
  styleUrls: ['./team.component.css']
})
export class TeamComponent implements OnInit, AfterViewInit {
  // Confirm/prompt dialogs and error banners are plain strings, so they are
  // translated imperatively rather than through the template pipe.
  private readonly transloco = inject(TranslocoService);
  users: AgencyUserDto[] = [];
  dataSource = new MatTableDataSource<AgencyUserDto>([]);

  @ViewChild(MatSort) sort!: MatSort;
  displayedColumns = ['userName', 'role', 'status', 'permissions', 'actions'];

  // Only the agency's enabled features can carry grantable permissions.
  groups: FeatureGroup[] = [];

  addForm: FormGroup;
  showAdd = false;
  addPermissions = new Set<string>();

  editingUserId: string | null = null;
  editPermissions = new Set<string>();

  errorMessage = '';

  constructor(private fb: FormBuilder, private client: UsersClient, private auth: AuthService) {
    this.addForm = this.fb.group({
      userName: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required]
    });

    this.dataSource.sortingDataAccessor = (user, column) => {
      switch (column) {
        case 'role': return user.role ?? '';
        // Sorting by status puts the deactivated accounts together.
        case 'status': return user.isLockedOut ? 1 : 0;
        case 'permissions': return (user.permissions ?? []).length;
        default: return user.userName ?? '';
      }
    };
  }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
  }

  ngOnInit() {
    this.auth.currentUser$.subscribe(user => {
      const enabled = new Set(user.features ?? []);
      this.groups = FEATURES
        .filter(f => enabled.has(f.key) && PERMISSIONS_BY_FEATURE[f.key]?.length)
        .map(f => ({ key: f.key, labelKey: f.labelKey, permissions: PERMISSIONS_BY_FEATURE[f.key] }));
    });
    this.load();
  }

  load() {
    this.client.getMyAgencyUsers().subscribe({
      next: u => {
        this.users = u || [];
        this.dataSource.data = this.users;
      },
      error: err => console.error(err)
    });
  }

  // --- Add staff ---
  toggleAddPermission(p: string, checked: boolean) {
    checked ? this.addPermissions.add(p) : this.addPermissions.delete(p);
  }

  addStaff() {
    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }
    this.errorMessage = '';
    const command = new CreateAgencyUserCommand({
      userName: this.addForm.value.userName,
      password: this.addForm.value.password,
      permissions: Array.from(this.addPermissions)
    });
    this.client.createAgencyUser(command).subscribe({
      next: () => {
        this.showAdd = false;
        this.addForm.reset();
        this.addPermissions.clear();
        this.load();
      },
      error: err => this.errorMessage = extractValidationErrors(err) || 'Could not create the user.'
    });
  }

  // --- Edit permissions ---
  startEdit(user: AgencyUserDto) {
    this.editingUserId = user.id!;
    this.editPermissions = new Set(user.permissions ?? []);
  }

  cancelEdit() {
    this.editingUserId = null;
  }

  toggleEditPermission(p: string, checked: boolean) {
    checked ? this.editPermissions.add(p) : this.editPermissions.delete(p);
  }

  hasEditPermission(p: string): boolean {
    return this.editPermissions.has(p);
  }

  saveEdit() {
    if (!this.editingUserId) return;
    this.errorMessage = '';
    const command = new UpdateMyAgencyUserCommand({
      userId: this.editingUserId,
      permissions: Array.from(this.editPermissions)
    });
    this.client.updateMyAgencyUser(this.editingUserId, command).subscribe({
      next: () => {
        this.editingUserId = null;
        this.load();
      },
      error: err => this.errorMessage = extractValidationErrors(err) || 'Could not update permissions.'
    });
  }

  toggleActive(user: AgencyUserDto) {
    if (!user.id) return;
    const activate = !!user.isLockedOut;
    const verb = this.transloco.translate(activate ? 'common.reactivate' : 'common.deactivate');
    if (!confirm(this.transloco.translate('team.confirmToggle', { verb, user: user.userName }))) return;
    const command = new SetMyAgencyUserActiveCommand({ userId: user.id, isActive: activate });
    this.client.setMyAgencyUserActive(user.id, command).subscribe({
      next: () => this.load(),
      error: err => console.error(err)
    });
  }
}
