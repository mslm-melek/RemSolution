import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  DocumentTemplatesClient, DocumentTemplateDto, DocumentPlaceholderDto,
  CreateDocumentTemplateCommand, UpdateDocumentTemplateCommand,
  DocumentTemplateKind, DocumentFieldBinding,
  DocumentBlock, DocumentBlockType, DocumentBlockField, DocumentTemplateFieldInput
} from '../web-api-client';
import { extractValidationErrors, isConcurrencyConflict } from '../shared/form-utils';
import { DocumentTemplateComponent } from './document-template.component';
import { LanguageService } from '../shared/language.service';
import { TranslocoService } from '@jsverse/transloco';

// A binding row as the editor works with it: the placeholder found in the blocks,
// plus how it should be filled.
interface BindingRow {
  placeholder: string;
  binding: DocumentFieldBinding;
  dataPath?: string;
  fixedValue?: string;
  label?: string;
  isRequired: boolean;
  /** True when the placeholder is no longer used by any block — shown greyed. */
  unused: boolean;
}

@Component({
  selector: 'app-document-template-form',
  templateUrl: './document-template-form.component.html',
  styleUrls: ['./document-template-form.component.css']
})
export class DocumentTemplateFormComponent implements OnInit {
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  form: FormGroup;
  templateId?: number;
  saving = false;
  errorMessage = '';

  /** The blocks being edited. Plain objects, sent as-is to the API. */
  blocks: DocumentBlock[] = [];

  /** One row per distinct placeholder in the blocks (plus any kept from before). */
  bindings: BindingRow[] = [];

  placeholders: DocumentPlaceholderDto[] = [];
  placeholderGroups: { group: string; items: DocumentPlaceholderDto[] }[] = [];

  private rowVersion?: string;

  DocumentTemplateKind = DocumentTemplateKind;
  DocumentBlockType = DocumentBlockType;
  DocumentFieldBinding = DocumentFieldBinding;

  blockTypes = [
    { value: DocumentBlockType.Heading, labelKey: 'documentTemplate.blockHeading' },
    { value: DocumentBlockType.Paragraph, labelKey: 'documentTemplate.blockParagraph' },
    { value: DocumentBlockType.Fields, labelKey: 'documentTemplate.blockFields' },
    { value: DocumentBlockType.LineItems, labelKey: 'documentTemplate.blockLineItems' },
    { value: DocumentBlockType.Signatures, labelKey: 'documentTemplate.blockSignatures' },
    { value: DocumentBlockType.PageBreak, labelKey: 'documentTemplate.blockPageBreak' },
    { value: DocumentBlockType.Spacer, labelKey: 'documentTemplate.blockSpacer' }
  ];

  bindingOptions = [
    { value: DocumentFieldBinding.DataField, labelKey: 'documentTemplate.bindingDataField' },
    { value: DocumentFieldBinding.AskEachTime, labelKey: 'documentTemplate.bindingAskEachTime' },
    { value: DocumentFieldBinding.FixedValue, labelKey: 'documentTemplate.bindingFixedValue' },
    { value: DocumentFieldBinding.Blank, labelKey: 'documentTemplate.bindingBlank' }
  ];

  constructor(
    private fb: FormBuilder,
    private client: DocumentTemplatesClient,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      kind: [DocumentTemplateKind.Contract, Validators.required],
      language: [this.language.current, Validators.required]
    });
  }

  get isEdit(): boolean {
    return this.templateId !== undefined;
  }

  get kind(): DocumentTemplateKind {
    return this.form.get('kind')!.value;
  }

  ngOnInit() {
    const idParam = this.route.snapshot.paramMap.get('id');

    if (idParam) {
      this.templateId = +idParam;
      this.client.getDocumentTemplateById(this.templateId).subscribe({
        next: dto => this.populate(dto),
        error: err => this.handleError(err)
      });
      return;
    }

    // A draft left by "use as a start" or by an import; otherwise a blank
    // template of the kind the query string asked for.
    const draft = DocumentTemplateComponent.takeDraft();

    if (draft) {
      this.form.patchValue({ name: draft.name, kind: draft.kind, language: draft.language });
      this.blocks = draft.blocks.map(b => DocumentBlock.fromJS(b));
      this.bindings = draft.fields.map(f => ({
        placeholder: f.placeholder,
        binding: f.binding,
        dataPath: f.dataPath,
        fixedValue: f.fixedValue,
        label: f.label,
        isRequired: !!f.isRequired,
        unused: false
      }));
    } else {
      const kindParam = this.route.snapshot.queryParamMap.get('kind');
      if (kindParam !== null) {
        this.form.patchValue({ kind: +kindParam });
      }
      // A brand-new template starts with a title, which every document needs.
      this.blocks = [DocumentBlock.fromJS({ type: DocumentBlockType.Heading, text: '' })];
    }

    // Reconciling the bindings waits on the catalog — loadPlaceholders does it
    // once the list arrives (see refreshBindings).
    this.loadPlaceholders();
  }

  private populate(dto: DocumentTemplateDto) {
    this.form.patchValue({ name: dto.name, kind: dto.kind, language: dto.language });
    this.rowVersion = dto.rowVersion;
    this.blocks = (dto.blocks || []).map(b => DocumentBlock.fromJS(b.toJSON()));
    this.bindings = (dto.fields || []).map(f => ({
      placeholder: f.placeholder!,
      binding: f.binding!,
      dataPath: f.dataPath,
      fixedValue: f.fixedValue,
      label: f.label,
      isRequired: !!f.isRequired,
      unused: false
    }));

    // Kind is fixed after creation: the bindings were validated against it.
    this.form.get('kind')!.disable();

    this.loadPlaceholders();
  }

  private loadPlaceholders() {
    this.client.getDocumentPlaceholders(this.kind).subscribe({
      next: list => {
        this.placeholders = list || [];
        // Grouped by leading segment, so the palette reads client / car / renting
        // rather than one flat list of forty paths.
        const groups = new Map<string, DocumentPlaceholderDto[]>();
        for (const item of this.placeholders) {
          const key = item.group || '';
          if (!groups.has(key)) groups.set(key, []);
          groups.get(key)!.push(item);
        }
        this.placeholderGroups = [...groups.entries()].map(([group, items]) => ({ group, items }));
        this.refreshBindings();
      },
      error: err => console.error(err)
    });
  }

  // --- blocks ---

  addBlock(type: DocumentBlockType) {
    const block: any = { type };

    if (type === DocumentBlockType.Fields) block.fields = [{ label: '', value: '' }];
    if (type === DocumentBlockType.Signatures) {
      block.labels = [
        this.transloco.translate('documentTemplate.signatureLessor'),
        this.transloco.translate('documentTemplate.signatureRenter')
      ];
    }
    if (type === DocumentBlockType.LineItems) block.showTotals = true;
    if (type === DocumentBlockType.Spacer) block.height = 12;

    this.blocks = [...this.blocks, DocumentBlock.fromJS(block)];
    this.refreshBindings();
  }

  removeBlock(index: number) {
    this.blocks = this.blocks.filter((_, i) => i !== index);
    this.refreshBindings();
  }

  moveBlock(index: number, offset: number) {
    const target = index + offset;
    if (target < 0 || target >= this.blocks.length) return;

    const blocks = [...this.blocks];
    [blocks[index], blocks[target]] = [blocks[target], blocks[index]];
    this.blocks = blocks;
  }

  addField(block: DocumentBlock) {
    block.fields = [...(block.fields || []), DocumentBlockField.fromJS({ label: '', value: '' })];
  }

  removeField(block: DocumentBlock, index: number) {
    block.fields = (block.fields || []).filter((_, i) => i !== index);
    this.refreshBindings();
  }

  addSignature(block: DocumentBlock) {
    block.labels = [...(block.labels || []), ''];
  }

  removeSignature(block: DocumentBlock, index: number) {
    block.labels = (block.labels || []).filter((_, i) => i !== index);
  }

  // Copies the token so the admin can paste it wherever they want it. Inserting at
  // the caret would need a directive per input; the clipboard is honest and works
  // in every field on the page.
  copyToken(placeholder: DocumentPlaceholderDto) {
    navigator.clipboard?.writeText(placeholder.token || '');
    this.errorMessage = '';
  }

  // --- bindings ---

  /**
   * Rescans the blocks and reconciles the binding table, mirroring the server's
   * rule: a placeholder named after a data path binds to it, anything else becomes
   * ask-each-time, and rows for placeholders no longer used are kept but marked.
   *
   * No-op until the placeholder catalog has arrived. Without this guard, a first
   * pass with an empty catalog would see every name as unrecognised and mark it
   * ask-each-time — and because the next pass preserves existing rows as the
   * user's own choice, that wrong classification would stick.
   */
  refreshBindings() {
    if (!this.placeholders.length) {
      return;
    }

    const found = this.findPlaceholders();
    const known = new Set(this.placeholders.map(p => p.path));
    const existing = new Map(this.bindings.map(b => [b.placeholder, b]));

    const rows: BindingRow[] = [];

    for (const name of found) {
      const row = existing.get(name);
      if (row) {
        rows.push({ ...row, unused: false });
        continue;
      }

      rows.push(known.has(name)
        ? { placeholder: name, binding: DocumentFieldBinding.DataField, dataPath: name, isRequired: false, unused: false }
        : { placeholder: name, binding: DocumentFieldBinding.AskEachTime, label: name, isRequired: false, unused: false });
    }

    // Kept, not dropped: a block deleted mid-edit must not lose its configuration.
    for (const row of this.bindings) {
      if (!found.includes(row.placeholder)) {
        rows.push({ ...row, unused: true });
      }
    }

    this.bindings = rows;
  }

  /**
   * The placeholder as it appears in a block. Built here rather than in the
   * template because an escaped `{{` in Angular markup is decoded before
   * interpolation is parsed, and would open an interpolation of its own.
   */
  token(placeholder: string): string {
    return `{{${placeholder}}}`;
  }

  // Looked up by value rather than indexed by it: indexing would silently
  // mislabel every block if blockTypes were ever reordered.
  blockTypeLabelKey(type?: DocumentBlockType): string {
    return this.blockTypes.find(t => t.value === type)?.labelKey ?? '';
  }

  get unboundCount(): number {
    return this.bindings.filter(b => !b.unused && b.binding === DocumentFieldBinding.AskEachTime).length;
  }

  // Mirrors the server-side regex in DocumentTemplateBlocks.
  private findPlaceholders(): string[] {
    const pattern = /\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*\}\}/g;
    const found: string[] = [];

    for (const text of this.templatedTexts()) {
      let match: RegExpExecArray | null;
      pattern.lastIndex = 0;
      while ((match = pattern.exec(text)) !== null) {
        if (!found.includes(match[1])) found.push(match[1]);
      }
    }

    return found;
  }

  private templatedTexts(): string[] {
    const texts: string[] = [];

    for (const block of this.blocks) {
      if (block.text) texts.push(block.text);
      if (block.title) texts.push(block.title);
      for (const field of block.fields || []) {
        if (field.label) texts.push(field.label);
        if (field.value) texts.push(field.value);
      }
      for (const label of block.labels || []) {
        if (label) texts.push(label);
      }
    }

    return texts;
  }

  // --- save ---

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.refreshBindings();
    this.saving = true;
    this.errorMessage = '';

    const v = this.form.getRawValue();
    const payload = {
      name: v.name,
      kind: v.kind,
      language: v.language,
      blocks: this.blocks,
      fields: this.bindings.map(row => new DocumentTemplateFieldInput({
        placeholder: row.placeholder,
        binding: row.binding,
        dataPath: row.binding === DocumentFieldBinding.DataField ? (row.dataPath || row.placeholder) : undefined,
        fixedValue: row.binding === DocumentFieldBinding.FixedValue ? row.fixedValue : undefined,
        label: row.binding === DocumentFieldBinding.AskEachTime ? (row.label || row.placeholder) : undefined,
        isRequired: row.binding === DocumentFieldBinding.AskEachTime && row.isRequired
      }))
    };

    if (this.isEdit) {
      const command = new UpdateDocumentTemplateCommand({
        id: this.templateId,
        rowVersion: this.rowVersion,
        ...payload
      });
      this.client.updateDocumentTemplate(this.templateId!, command).subscribe({
        next: () => this.router.navigate(['/document-template']),
        error: err => this.handleError(err)
      });
    } else {
      const command = new CreateDocumentTemplateCommand(payload);
      this.client.createDocumentTemplate(command).subscribe({
        next: () => this.router.navigate(['/document-template']),
        error: err => this.handleError(err)
      });
    }
  }

  private handleError(err: any) {
    this.saving = false;

    if (isConcurrencyConflict(err)) {
      this.errorMessage = this.transloco.translate('documentTemplate.concurrency');
      return;
    }

    const validationErrors = extractValidationErrors(err);
    this.errorMessage = validationErrors ?? this.transloco.translate('common.unexpectedError');
    if (!validationErrors) console.error(err);
  }
}
