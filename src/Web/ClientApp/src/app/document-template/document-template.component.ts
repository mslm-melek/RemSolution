import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  DocumentTemplatesClient, DocumentTemplateDto, DocumentTemplateExampleDto,
  DocumentTemplateKind, FileParameter
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { LanguageService } from '../shared/language.service';
import { TranslocoService } from '@jsverse/transloco';

// The draft a clone, a duplicate or an import hands to the editor. Held here
// rather than in the URL: it is a whole document.
export interface TemplateDraft {
  name: string;
  kind: DocumentTemplateKind;
  language: string;
  blocks: any[];
  fields: any[];
}

/** Which slice of the shelf is on screen. Mirrors the three tabs. */
type TemplateTab = 'all' | 'contract' | 'facture';

@Component({
  selector: 'app-document-template',
  templateUrl: './document-template.component.html',
  styleUrls: ['./document-template.component.css']
})
export class DocumentTemplateComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  templates: DocumentTemplateDto[] = [];

  /** The templates the current tab shows — what the header counts. */
  shown: DocumentTemplateDto[] = [];

  examples: DocumentTemplateExampleDto[] = [];
  loading = false;
  importing = false;
  errorMessage = '';

  // Retired templates are hidden until asked for.
  showInactive = false;

  tab: TemplateTab = 'all';

  tabs: { key: TemplateTab; labelKey: string }[] = [
    { key: 'all', labelKey: 'documentTemplate.tabAll' },
    { key: 'contract', labelKey: 'documentTemplate.tabContracts' },
    { key: 'facture', labelKey: 'documentTemplate.tabInvoices' }
  ];

  DocumentTemplateKind = DocumentTemplateKind;

  constructor(
    private client: DocumentTemplatesClient,
    private router: Router
  ) { }

  ngOnInit() {
    this.reload();

    // Examples come back in the session's language; the API takes no argument for it.
    this.client.getDocumentTemplateExamples().subscribe({
      next: list => this.examples = list || [],
      error: err => console.error(err)
    });
  }

  reload() {
    this.loading = true;
    this.client.getDocumentTemplates(null, null, this.showInactive).subscribe({
      next: list => {
        this.templates = list || [];
        this.applyTab();
        this.loading = false;
      },
      error: err => {
        this.loading = false;
        this.handleError(err);
      }
    });
  }

  toggleInactive() {
    this.showInactive = !this.showInactive;
    this.reload();
  }

  selectTab(tab: TemplateTab) {
    this.tab = tab;
    this.applyTab();
  }

  /** What each tab would show, for the counts beside the labels. */
  countFor(tab: TemplateTab): number {
    return this.filter(tab).length;
  }

  newTemplate(kind: DocumentTemplateKind) {
    this.router.navigate(['/document-template/new'], { queryParams: { kind } });
  }

  // A client-side copy into a new, unsaved template; nothing is stored yet.
  cloneExample(example: DocumentTemplateExampleDto) {
    DocumentTemplateComponent.draft = {
      name: this.transloco.translate('documentTemplate.copyOf', { name: example.name }),
      kind: example.kind!,
      language: example.language!,
      blocks: (example.blocks || []).map(b => b.toJSON()),
      fields: []
    };

    this.router.navigate(['/document-template/new']);
  }

  // Same road: blocks and bindings are copied into an unsaved draft.
  duplicate(template: DocumentTemplateDto) {
    DocumentTemplateComponent.draft = {
      name: this.transloco.translate('documentTemplate.copyOf', { name: template.name }),
      kind: template.kind!,
      language: template.language!,
      blocks: (template.blocks || []).map(b => b.toJSON()),
      fields: (template.fields || []).map(f => f.toJSON())
    };

    this.router.navigate(['/document-template/new']);
  }

  onImportSelected(kind: DocumentTemplateKind, input: HTMLInputElement) {
    const file = input.files?.[0];
    input.value = ''; // allow re-selecting the same file
    if (!file) return;

    this.importing = true;
    this.errorMessage = '';
    const parameter: FileParameter = { data: file, fileName: file.name };

    this.client.importDocumentTemplate(kind, this.language.current, parameter).subscribe({
      next: draft => {
        this.importing = false;
        DocumentTemplateComponent.draft = {
          name: draft.name || '',
          kind: draft.kind!,
          language: draft.language!,
          blocks: (draft.blocks || []).map(b => b.toJSON()),
          fields: (draft.fields || []).map(f => f.toJSON())
        };
        this.router.navigate(['/document-template/new']);
      },
      error: err => {
        this.importing = false;
        this.handleError(err);
      }
    });
  }

  setDefault(template: DocumentTemplateDto) {
    if (!template.id) return;
    this.client.setDefaultDocumentTemplate(template.id).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  setActive(template: DocumentTemplateDto, isActive: boolean) {
    if (!template.id) return;
    if (!isActive && !confirm(this.transloco.translate('documentTemplate.confirmRetire'))) return;

    this.client.setDocumentTemplateActive(template.id, isActive).subscribe({
      next: () => this.reload(),
      error: err => this.handleError(err)
    });
  }

  kindLabelKey(kind?: DocumentTemplateKind): string {
    return kind === DocumentTemplateKind.Facture
      ? 'documentTemplate.kindFacture'
      : 'documentTemplate.kindContract';
  }

  // One chip per card: default (what gets printed), retired, or in use.
  statusFor(template: DocumentTemplateDto): { labelKey: string; icon: string; tone: string } {
    if (!template.isActive) {
      return { labelKey: 'documentTemplate.retired', icon: 'inventory_2', tone: 'neutral' };
    }
    if (template.isDefault) {
      return { labelKey: 'documentTemplate.default', icon: 'check_circle', tone: 'ok' };
    }
    return { labelKey: 'common.active', icon: 'radio_button_checked', tone: 'neutral' };
  }

  // Cleared once read, so a later visit to /new starts blank.
  private static draft?: TemplateDraft;

  static takeDraft(): TemplateDraft | undefined {
    const draft = DocumentTemplateComponent.draft;
    DocumentTemplateComponent.draft = undefined;
    return draft;
  }

  private applyTab() {
    this.shown = this.filter(this.tab);
  }

  private filter(tab: TemplateTab): DocumentTemplateDto[] {
    if (tab === 'all') return this.templates;

    const kind = tab === 'contract' ? DocumentTemplateKind.Contract : DocumentTemplateKind.Facture;
    return this.templates.filter(template => template.kind === kind);
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
