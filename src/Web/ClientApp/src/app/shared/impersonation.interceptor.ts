import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ImpersonationService } from './impersonation.service';

// Adds the X-Impersonate-Agency header to Cars/Clients requests while a
// platform admin is browsing a specific agency. The header is only honoured by
// the server for a platform admin on a GET, so stamping it on other requests is
// harmless — but we scope it to the tenant-data endpoints to be explicit.
@Injectable({ providedIn: 'root' })
export class ImpersonationInterceptor implements HttpInterceptor {
  constructor(private impersonation: ImpersonationService) { }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const agencyId = this.impersonation.current;

    if (agencyId !== null && /\/api\/(Cars|Clients)\b/i.test(req.url)) {
      const cloned = req.clone({
        setHeaders: { 'X-Impersonate-Agency': String(agencyId) }
      });
      return next.handle(cloned);
    }

    return next.handle(req);
  }
}
