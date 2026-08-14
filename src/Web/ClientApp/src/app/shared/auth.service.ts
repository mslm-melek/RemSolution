import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay, tap } from 'rxjs/operators';
import { UsersClient, CurrentUserDto } from '../web-api-client';
import { ImpersonationService } from './impersonation.service';

// Auth state only changes across page reloads (login/logout are Razor flows), so
// one fetch per app load is enough.
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly currentUser$: Observable<CurrentUserDto>;

  // Set by the profile page on a successful change: the probe is replayed, so a
  // cached "true" would keep bouncing the user back to the password form.
  private passwordChanged = false;

  // Same for the home tiles, so a fresh choice is not overwritten by the replayed
  // probe. Null = nothing saved this session.
  private homeWidgetsOverride: string[] | null = null;

  // Same again for the landing screen's quick actions.
  private homeActionsOverride: string[] | null = null;

  constructor(client: UsersClient, impersonation: ImpersonationService) {
    this.currentUser$ = client.getCurrentUser().pipe(
      // The stored workspace is per tab and survives a sign-out, so it is checked
      // against whoever is signed in now. Upstream of shareReplay, so every
      // subscriber sees the settled state.
      tap(user => {
        if (!impersonation.reconcile(user.userName, user.isImpersonating === true)) return;

        // This call answered under the workspace just dropped, so fetch it again.
        // It cannot loop: the second pass sends no impersonation header.
        if (user.isAuthenticated) window.location.reload();
      }),
      catchError(() => {
        // A deleted agency makes the server refuse this call and every other one
        // carrying the header, which reads as "signed out". Drop and reload into
        // the admin's own context instead; the second pass sends no header.
        if (impersonation.current) {
          impersonation.discard();
          window.location.reload();
        }

        return of(new CurrentUserDto({ isAuthenticated: false }));
      }),
      shareReplay(1)
    );
  }

  /**
   * True while the account is still on its provisioned temporary password; the
   * API refuses everything but the change-password call in that state.
   */
  // A getter, not a field: initializers run before currentUser$ is assigned.
  get mustChangePassword$(): Observable<boolean> {
    return this.currentUser$.pipe(
      map(user => user.mustChangePassword === true && !this.passwordChanged)
    );
  }

  /** Called once the user has chosen their own password. */
  markPasswordChanged() {
    this.passwordChanged = true;
  }

  /**
   * The tiles pinned to the home screen, in order, or null when never chosen
   * (defaults then show). An empty array is a deliberate "no tiles".
   */
  get homeWidgets$(): Observable<string[] | null> {
    return this.currentUser$.pipe(
      map(user => this.homeWidgetsOverride ?? user.homeWidgets ?? null)
    );
  }

  /** Called once a new selection has been saved on the account. */
  markHomeWidgets(widgets: string[]) {
    this.homeWidgetsOverride = widgets;
  }

  /**
   * The landing screen's quick actions, in order, or null when never chosen. An
   * empty array is a deliberate "no actions".
   */
  get homeActions$(): Observable<string[] | null> {
    return this.currentUser$.pipe(
      map(user => this.homeActionsOverride ?? user.homeActions ?? null)
    );
  }

  /** Called once a new action selection has been saved on the account. */
  markHomeActions(actions: string[]) {
    this.homeActionsOverride = actions;
  }

  // Feature on AND read permission held. Names must match the Domain constants;
  // the API enforces the same pair, so this never out-privileges the backend.
  static canAccessModule(user: CurrentUserDto, feature: string, readPermission: string): boolean {
    return !!user.features?.includes(feature)
        && !!user.permissions?.includes(readPermission);
  }

  // Platform admins get the console nav, agency users the module nav. Must match
  // the Domain Roles constant, which the backend enforces anyway.
  static isPlatformAdmin(user: CurrentUserDto): boolean {
    return user.role === 'PlatformAdministrator';
  }

  // A self-registered marketplace customer: browse/booking, not staff navigation.
  static isCustomer(user: CurrentUserDto): boolean {
    return user.role === 'Customer';
  }
}
