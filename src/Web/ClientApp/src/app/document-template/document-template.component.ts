import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { MatSort } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import {
  DocumentTemplatesClient, DocumentTemplateDto, DocumentTemplateExampleDto,
  DocumentTemplateKind, FileParameter
} from '../web-api-client';
import { extractValidationErrors } from '../shared/form-utils';
import { LanguageService } from '../shared/language.service';
import { TranslocoService } from '@jsverse/transloco';

// The draft an example-clone or an import hands to the editor. Held in the
// service below rather than passed through the URL: it is a whole document, and a
// query string is the wrong place for one.
export interface TemplateDraft {
  name: string;
  kind: DocumentTemplateKind;
  language: string;
  blocks: any[];
  fields: any[];
}

@Component({
  selector: 'app-document-template',
  templateUrl: './document-template.component.html',
  styleUrls: ['./document-template.component.css']
})
export class DocumentTemplateComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  templates: DocumentTemplateDto[] = [];
  dataSource = new MatTableDataSource<DocumentTemplateDto>([]);

  // The table only exists once there is at least one template, so the sort
  // header arrives later than ngAfterViewInit — take it through a setter.
  @ViewChild(MatSort) set tableSort(sort: MatSort | undefined) {
    if (sort) this.dataSource.sort = sort;
  }
  examples: DocumentTemplateExampleDto[] = [];
  loading = false;
  importing = false;
  errorMessage = '';

  // Retired templates are hidden until asked for: the list is a working tool,
  // not an archive.
  showInactive = false;

  DocumentTemplateKind = DocumentTemplateKind;
  columns = ['name', 'kind', 'language', 'status', 'actions'];

  constructor(
    private client: DocumentTemplatesClient,
    private router: Router
  ) {
    this.dataSource.sortingDataAccessor = (template, column) => {
      switch (column) {
        case 'kind': return template.kind ?? 0;
        case 'language': return template.language ?? '';
        // Status sorts the default template first, retired ones last.
        case 'status': return template.isDefault ? 0 : (template.isActive ? 1 : 2);
        default: return template.name ?? '';
      }
    };
  }

  ngOnInit() {
    this.reload();

    // Examples come back in the language this session is working in; the API takes
    // no language argument so the wording and the tag can never disagree.
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
        this.dataSource.data = this.templates;
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

  newTemplate(kind: DocumentTemplateKind) {
    this.router.navigate(['/document-template/new'], { queryParams: { kind } });
  }

  // Cloning is a client-side copy of the example's blocks into a new, unsaved
  // template — the admin reviews and names it before anything is stored.
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

  // Handed to the editor on the next navigation and cleared once read, so a
  // later visit to /new starts blank instead of resurrecting an old draft.
  private static draft?: TemplateDraft;

  static takeDraft(): TemplateDraft | undefined {
    const draft = DocumentTemplateComponent.draft;
    DocumentTemplateComponent.draft = undefined;
    return draft;
  }

  private handleError(err: any) {
    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
