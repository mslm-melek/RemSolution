import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, shareReplay } from 'rxjs/operators';
import { UsersClient, CurrentUserDto } from '../web-api-client';
import { ImpersonationService } from './impersonation.service';

// Login, register and logout are full-page Razor flows, so the auth state can
// only change across page reloads — one fetch per app load is enough.
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly currentUser$: Observable<CurrentUserDto>;

  // Set by the profile page the moment a password change succeeds. The probe
  // above is fetched once and replayed, so without this the user would keep
  // being bounced back to the password form by a cached "true" they have
  // already acted on — the one piece of auth state that changes without a page
  // reload.
  private passwordChanged = false;

  // Same idea for the home-screen tiles: the probe is fetched once and replayed,
  // so a user who customizes their home, navigates away and comes back would
  // otherwise be handed the choice they had before saving. Null = nothing saved
  // this session, so the probe's value stands.
  private homeWidgetsOverride: string[] | null = null;

  // Same again for the landing screen's quick actions, saved separately from the
  // tiles.
  private homeActionsOverride: string[] | null = null;

  constructor(client: UsersClient, impersonation: ImpersonationService) {
    this.currentUser$ = client.getCurrentUser().pipe(
      catchError(() => {
        // An open agency workspace stamps its header on this call too, so an
        // agency that has since been deleted makes the server refuse it — and
        // every other request with it. Left alone that reads as "signed out"
        // across the whole app, for a reason the user cannot see. Drop the
        // workspace and reload into the admin's own context instead; the reload
        // cannot loop, because the second pass sends no impersonation header.
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
   * True while the account is still on the temporary password it was
   * provisioned with. The API refuses everything but the change-password call
   * in that state, so the SPA keeps the user on the one screen that can end it.
   */
  // A getter, not a field: field initializers run before the constructor body,
  // where currentUser$ is assigned.
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
   * The tiles the user pinned to their home screen, in their order, or null when
   * they have never chosen (the home screen then shows its defaults). An empty
   * array is the deliberate "no tiles" and is returned as such.
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
   * The quick actions the user keeps on their landing screen, in their order, or
   * null when they have never chosen (the screen then shows its defaults). An
   * empty array is the deliberate "no actions" and is returned as such.
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

  // A module is visible when the agency has the feature switched on AND the
  // user holds the module's read permission (agency administrators get every
  // permission from the API). Names must match the Domain constants
  // (FeatureFlags / Permissions); the API enforces the same pair, so hiding
  // here never out-privileges the backend.
  static canAccessModule(user: CurrentUserDto, feature: string, readPermission: string): boolean {
    return !!user.features?.includes(feature)
        && !!user.permissions?.includes(readPermission);
  }

  // The platform administrator (app owner) gets the agency-grouped admin
  // console; agency users get the flat module navigation. Must match the
  // Domain Roles constant. The backend enforces the same role on every admin
  // endpoint, so branching here never out-privileges the API.
  static isPlatformAdmin(user: CurrentUserDto): boolean {
    return user.role === 'PlatformAdministrator';
  }

  // A self-registered marketplace customer — gets the browse/booking experience
  // instead of the staff/admin navigation.
  static isCustomer(user: CurrentUserDto): boolean {
    return user.role === 'Customer';
  }
}
