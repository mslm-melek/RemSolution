import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { map } from 'rxjs/operators';
import { AuthService } from './auth.service';

// Where a locked account is sent: the profile page owns the change-password
// form, so it is both the destination and the one route the guard lets through.
const PASSWORD_ROUTE = 'profile';

/**
 * Keeps an account that is still on its emailed temporary password on the one
 * screen that can replace it. This mirrors PasswordChangeRequiredMiddleware
 * rather than replacing it — the server is what actually refuses the calls;
 * this only spares the user an app whose every request comes back 403.
 */
export const mustChangePasswordGuard: CanActivateFn = () => {
  // Both resolved here: inject() only works synchronously inside the guard, not
  // later from the map callback.
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.mustChangePassword$.pipe(
    map(mustChange => !mustChange || router.parseUrl(`/${PASSWORD_ROUTE}`))
  );
};

/**
 * Applies the guard to every route except the password screen itself, so a new
 * route cannot be added without it and quietly become a hole. Routes that
 * already declare canActivate keep theirs and gain this one.
 */
export function guardRoutes(routes: Routes): Routes {
  return routes.map(route =>
    route.path === PASSWORD_ROUTE
      ? route
      : { ...route, canActivate: [...(route.canActivate ?? []), mustChangePasswordGuard] });
}
