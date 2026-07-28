import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ImpersonationService } from './impersonation.service';

// Endpoint groups that are the platform's own rather than an agency's: the agency
// register, the plan catalogue and subscriptions, the platform dashboard, and the
// global reference catalogues (countries, brands, car models, expense and
// extra-service types), none of which are tenant data. Stamping the header on
// these would change nothing and would bury the impersonation audit trail —
// which records a row per impersonated request — under rows about no agency.
const PLATFORM_ENDPOINTS =
  /\/api\/(Agencies|AgencySubscriptions|SubscriptionPlans|PlatformDashboard|Countries|Brands|ModelCars|ExpenseTypes|ExtraServiceTypes)\b/i;

// Adds X-Impersonate-Agency while a platform administrator has an agency
// workspace open, so the ordinary tenant-scoped endpoints read and write that
// agency's data. The server only honours the header for a platform
// administrator, so an agency user gains nothing by sending it.
@Injectable({ providedIn: 'root' })
export class ImpersonationInterceptor implements HttpInterceptor {
  constructor(private impersonation: ImpersonationService) { }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const agencyId = this.impersonation.currentId;

    if (agencyId !== null && /\/api\//i.test(req.url) && !PLATFORM_ENDPOINTS.test(req.url)) {
      const cloned = req.clone({
        setHeaders: { 'X-Impersonate-Agency': String(agencyId) }
      });
      return next.handle(cloned);
    }

    return next.handle(req);
  }
}
