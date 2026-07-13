import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, shareReplay } from 'rxjs/operators';
import { UsersClient, CurrentUserDto } from '../web-api-client';

// Login, register and logout are full-page Razor flows, so the auth state can
// only change across page reloads — one fetch per app load is enough.
@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly currentUser$: Observable<CurrentUserDto>;

  constructor(client: UsersClient) {
    this.currentUser$ = client.getCurrentUser().pipe(
      catchError(() => of(new CurrentUserDto({ isAuthenticated: false }))),
      shareReplay(1)
    );
  }
}
