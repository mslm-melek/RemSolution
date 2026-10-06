import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

/**
 * Shows an expense or add-on type's name. A standard type is named by a
 * translation key (`oilChange`) whose wording lives under `catalogItems.*`; a
 * name with no entry there — a type the agency added or renamed — is shown as
 * typed. The server's resx `CatalogItem.*` mirrors this list for the invoice and
 * the mail, so add a key to both.
 *
 * Impure so it follows a language switch; the lookup is one property read.
 */
@Pipe({ name: 'catalogName', pure: false })
export class CatalogNamePipe implements PipeTransform {
  private readonly transloco = inject(TranslocoService);

  transform(name: string | null | undefined): string {
    return catalogName(this.transloco, name);
  }
}

export function catalogName(transloco: TranslocoService, name: string | null | undefined): string {
  if (!name) return '';

  // Read from the loaded translation rather than translate(): that would answer
  // a missing key with the key path and log it, and a missing key is normal here.
  const translation = transloco.getTranslation(transloco.getActiveLang()) as Record<string, string>;
  return translation[`catalogItems.${name}`] || name;
}
