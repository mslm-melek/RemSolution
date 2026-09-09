import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';

/**
 * Saves a file served by the API.
 *
 * Fetched as a blob rather than linked to directly: every download route is
 * permission-checked, and going through HttpClient keeps the auth / language /
 * impersonation interceptors on the request. The NSwag-generated methods discard
 * the body (their endpoints have no JSON schema), which is why callers pass a URL.
 */
export function downloadFile(
  http: HttpClient, url: string, fallbackName: string): Observable<void> {
  return http.get(url, { observe: 'response', responseType: 'blob' }).pipe(
    map(response => {
      const blob = response.body;
      if (!blob) return;

      const name = fileNameFrom(response.headers.get('content-disposition')) ?? fallbackName;

      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = name;
      link.click();
      // Revoking immediately would race the click on some browsers.
      setTimeout(() => URL.revokeObjectURL(objectUrl), 10000);
    }));
}

/**
 * The server's own name for the file. RFC 5987 `filename*` first — it is the one
 * that survives the non-ASCII names an agency can produce; the plain `filename`
 * is the fallback.
 */
function fileNameFrom(header: string | null): string | null {
  if (!header) return null;

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim());
    } catch {
      // A malformed header is not worth failing a download over.
    }
  }

  const plain = /filename="?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : null;
}
