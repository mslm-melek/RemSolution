import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { UsersClient, CurrentUserDto } from '../web-api-client';
import { ImpersonationService } from './impersonation.service';

// Login, register and logout are full-page Razor flows, so the auth state can
// only change across page reloads — one fetch per app load is enough.
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly currentUser$: Observable<CurrentUserDto>;

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
