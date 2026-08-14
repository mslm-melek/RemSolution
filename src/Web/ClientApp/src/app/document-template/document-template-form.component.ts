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

// A placeholder found in the blocks, plus how it should be filled.
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

  /** Said out loud when a field was copied instead of inserted (see insertToken). */
  noticeMessage = '';

  /** Whether the stored template is still in the pickers — drives "Retire". */
  isActive = true;

  /** The blocks being edited. Plain objects, sent as-is to the API. */
  blocks: DocumentBlock[] = [];

  /** The block whose tools are open; the rest show as they will print. */
  selected = -1;

  /**
   * The last text box typed in. Insert-field writes at its caret; with none, the
   * token goes to the clipboard instead.
   */
  private lastField?: HTMLInputElement | HTMLTextAreaElement;

  /** One row per distinct placeholder in the blocks (plus any kept from before). */
  bindings: BindingRow[] = [];

  placeholders: DocumentPlaceholderDto[] = [];
  placeholderGroups: { group: string; items: DocumentPlaceholderDto[] }[] = [];

  private rowVersion?: string;

  DocumentTemplateKind = DocumentTemplateKind;
  DocumentBlockType = DocumentBlockType;
  DocumentFieldBinding = DocumentFieldBinding;

  blockTypes = [
    { value: DocumentBlockType.Heading, labelKey: 'documentTemplate.blockHeading', icon: 'title' },
    { value: DocumentBlockType.Paragraph, labelKey: 'documentTemplate.blockParagraph', icon: 'notes' },
    { value: DocumentBlockType.Fields, labelKey: 'documentTemplate.blockFields', icon: 'view_agenda' },
    { value: DocumentBlockType.LineItems, labelKey: 'documentTemplate.blockLineItems', icon: 'receipt_long' },
    { value: DocumentBlockType.Signatures, labelKey: 'documentTemplate.blockSignatures', icon: 'draw' },
    { value: DocumentBlockType.PageBreak, labelKey: 'documentTemplate.blockPageBreak', icon: 'insert_page_break' },
    { value: DocumentBlockType.Spacer, labelKey: 'documentTemplate.blockSpacer', icon: 'space_bar' }
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

  /**
   * Which way the sheet reads: the template's own language, not the screen's, so
   * the preview matches what the renderer will print.
   */
  get sheetDirection(): 'rtl' | 'ltr' {
    return this.form.get('language')?.value === 'ar' ? 'rtl' : 'ltr';
  }

  /** The eyebrow over the title: which kind of document this prints. */
  get kindLabelKey(): string {
    return this.kind === DocumentTemplateKind.Facture
      ? 'documentTemplate.kindFacture'
      : 'documentTemplate.kindContract';
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

    // A draft from a clone or an import; otherwise a blank template of the
    // kind the query string asked for.
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

    // The bindings are reconciled once the catalog arrives (see refreshBindings).
    this.loadPlaceholders();
  }

  private populate(dto: DocumentTemplateDto) {
    this.form.patchValue({ name: dto.name, kind: dto.kind, language: dto.language });
    this.rowVersion = dto.rowVersion;
    this.isActive = !!dto.isActive;
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
        // Grouped by leading segment, so the palette reads client / car / renting.
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
    // A new block opens with its options showing.
    this.selected = this.blocks.length - 1;
    this.refreshBindings();
  }

  removeBlock(index: number) {
    this.blocks = this.blocks.filter((_, i) => i !== index);
    this.selected = -1;
    this.refreshBindings();
  }

  moveBlock(index: number, offset: number) {
    const target = index + offset;
    if (target < 0 || target >= this.blocks.length) return;

    const blocks = [...this.blocks];
    [blocks[index], blocks[target]] = [blocks[target], blocks[index]];
    this.blocks = blocks;
    // The selection follows the block, not the position it used to hold.
    if (this.selected === index) this.selected = target;
    else if (this.selected === target) this.selected = index;
  }

  /** True where the block has switches of its own to show while selected. */
  hasOptions(block: DocumentBlock): boolean {
    return block.type === DocumentBlockType.Paragraph
        || block.type === DocumentBlockType.Fields
        || block.type === DocumentBlockType.LineItems
        || block.type === DocumentBlockType.Spacer;
  }

  /** A paragraph grows with its text; the wrap estimate is deliberately rough. */
  paragraphRows(text?: string): number {
    const value = text || '';
    const lines = value.split('\n').length + Math.floor(value.length / 90);
    return Math.min(24, Math.max(2, lines));
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

  /**
   * Remembers where the caret is, so "Insert field" has somewhere to write. Bound
   * once on the sheet (focusin bubbles); buttons and number boxes are ignored.
   */
  rememberField(event: FocusEvent) {
    const target = event.target;

    if ((target instanceof HTMLInputElement && target.type === 'text')
        || target instanceof HTMLTextAreaElement) {
      this.lastField = target;
    }
  }

  /**
   * Writes the placeholder at the caret; with no live text box the token goes to
   * the clipboard instead. An `input` event is dispatched because the box is bound
   * with ngModel, which would otherwise overwrite the assignment.
   */
  insertToken(placeholder: DocumentPlaceholderDto) {
    const token = placeholder.token || this.token(placeholder.path || '');
    const field = this.lastField;

    if (!field || !field.isConnected) {
      navigator.clipboard?.writeText(token);
      this.noticeMessage = this.transloco.translate('documentTemplate.tokenCopied', { token });
      return;
    }

    const start = field.selectionStart ?? field.value.length;
    const end = field.selectionEnd ?? start;

    field.value = field.value.slice(0, start) + token + field.value.slice(end);
    field.dispatchEvent(new Event('input', { bubbles: true }));

    // A closing menu hands focus back to its button, so the caret is claimed
    // again on the next turn of the loop.
    const caret = start + token.length;
    const restore = () => {
      field.focus();
      field.setSelectionRange(caret, caret);
    };

    restore();
    setTimeout(restore);

    this.noticeMessage = '';
    this.refreshBindings();
  }

  // --- bindings ---

  /**
   * Rescans the blocks and reconciles the binding table, mirroring the server's
   * rule: a placeholder named after a data path binds to it, anything else becomes
   * ask-each-time, and rows for placeholders no longer used are kept but marked.
   *
   * No-op until the catalog has arrived: an empty one would mark every name
   * ask-each-time, and later passes preserve existing rows.
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
   * The placeholder as it appears in a block. Built here because an escaped `{{`
   * in Angular markup would open an interpolation of its own.
   */
  token(placeholder: string): string {
    return `{{${placeholder}}}`;
  }

  // Looked up by value: indexing would mislabel blocks if blockTypes were reordered.
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

  /**
   * Takes the template out of the pickers, from the screen it is being edited on.
   * Retiring rather than deleting is the whole model: documents already issued
   * from this template have to keep resolving (see SetDocumentTemplateActive).
   */
  retire() {
    if (!this.templateId) return;
    if (!confirm(this.transloco.translate('documentTemplate.confirmRetire'))) return;

    this.client.setDocumentTemplateActive(this.templateId, false).subscribe({
      next: () => this.router.navigate(['/document-template']),
      error: err => this.handleError(err)
    });
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
