import { Component, OnInit, Output, EventEmitter } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { BsModalRef } from 'ngx-bootstrap/modal';

@Component({
  selector: 'app-regex-builder',
  templateUrl: './regex-builder.component.html',
  styleUrls: ['./regex-builder.component.css'],
  standalone: false
})
export class RegexBuilderComponent implements OnInit {

  mode: 'basic' | 'advanced' = 'basic';
  form: FormGroup;
  generatedRegex = '';
  generatedDescription: string = '';
  testFilename = '';
  testResult: 'valid' | 'invalid' | '' = '';
  result: string | null = null;
  showAdvancedHelp = false;
  existingRegex: string | null = null;
  isEditingExistingRegex = false;
  regexError: string = 'Select at least one character or pattern rule.';
  @Output() onClose = new EventEmitter<{ action: 'save' | 'cancel' }>();
  constructor(private fb: FormBuilder, public bsModalRef: BsModalRef) {

  }
  ngOnInit(): void {
    this.initializeForm();
    if (this.existingRegex) {
      try {
        this.isEditingExistingRegex = true;
        this.populateBasicFormFromRegex(this.existingRegex);
        this.generateRegex();
      } catch (e) {
        // If parsing fails, just start with an empty form
        this.initializeForm();
      }
    }

    this.form.get('noSpaces')?.valueChanges.subscribe(noSpaces => {
      const spaceControl = this.form.get('space');
      if (noSpaces) {
        spaceControl?.disable();
        spaceControl?.setValue(false);
      } else {
        spaceControl?.enable();
      }
    });
  }

  initializeForm() {
    this.form = this.fb.group({
      letters: false,
      numbers: false,
      underscore: false,
      dash: false,
      space: false,
      noSpaces: false,
      customChars: '',

      minLength: '',
      maxLength: '',

      startsWith: '',
      endsWith: '',
      mustContain: '',
      mustNotContain: '',

      advancedRegex: ''
    });
  }

  get hasBaseRule(): boolean {
    const f = this.form.value;

    return !!(
      f.letters ||
      f.numbers ||
      f.underscore ||
      f.dash ||
      f.space ||
      f.customChars ||
      f.startsWith ||
      f.endsWith ||
      f.mustContain ||
      f.mustNotContain ||
      f.noSpaces
    );
  }






  generateRegex() {
    this.regexError = '';
    const f = this.form.value;

    /* -------------------------------------------------
     * Base rule check (length alone is meaningless)
     * ------------------------------------------------- */

    if (!this.hasBaseRule) {
      this.generatedRegex = '';
      this.generatedDescription =
        'Select at least one character or pattern rule.';
      return;
    }

    /* -------------------------------------------------
     * Lookaheads (must / must not / noSpaces)
     * ------------------------------------------------- */
    let lookaheads = '';

    if (f.mustContain) {
      lookaheads += `(?=.*${this.escape(f.mustContain)})`;
    }

    if (f.mustNotContain) {
      lookaheads += `(?!.*${this.escape(f.mustNotContain)})`;
    }

    if (f.noSpaces) {
      lookaheads += `(?!.*\\s)`;
    }

    /* -------------------------------------------------
     * Character class
     * ------------------------------------------------- */
    let charClass = '';

    if (f.letters) charClass += 'A-Za-z';
    if (f.numbers) charClass += '0-9';
    if (f.underscore) charClass += '_';
    if (f.dash) charClass += '\\-'; // literal dash
    if (f.space && !f.noSpaces) charClass += ' ';
    if (f.customChars) charClass += this.escapeForCharClass(f.customChars);

    const hasCharRules = charClass.length > 0;

    /* -------------------------------------------------
     * Length handling (FULL filename length)
     * ------------------------------------------------- */
    const min = f.minLength !== '' && f.minLength !== null
      ? Number(f.minLength)
      : null;

    const max = f.maxLength !== '' && f.maxLength !== null
      ? Number(f.maxLength)
      : null;

    const startsLen = f.startsWith?.length ?? 0;
    const endsLen = f.endsWith?.length ?? 0;


    let fullMin: number | null = null;
    let fullMax: number | null = null;

    if (this.isEditingExistingRegex) {
      // min/max currently represent MIDDLE bounds
      if (min !== null) fullMin = min + startsLen + endsLen;
      if (max !== null) fullMax = max + startsLen + endsLen;
    } else {
      // min/max are already FULL bounds
      fullMin = min;
      fullMax = max;
    }

    const totalFixed = startsLen + endsLen;

    if (fullMin !== null && totalFixed > fullMin) {
      this.regexError = 'Start and end exceed minimum filename length.';
      return;
    }

    if (fullMax !== null && totalFixed > fullMax) {
      this.regexError = 'Start and end exceed maximum filename length.';
      return;
    }

    const remainingMin =
      fullMin !== null ? Math.max(fullMin - totalFixed, 0) : null;

    const remainingMax =
      fullMax !== null ? Math.max(fullMax - totalFixed, 0) : null;

    /* -------------------------------------------------
     * Middle (core) matcher
     * ------------------------------------------------- */
    let middle = '';

    if (!hasCharRules) {
      // Structural rules only (must / noSpaces / start/end)
      middle = '.*';
    } else if (remainingMin !== null || remainingMax !== null) {
      middle = `[${charClass}]{${remainingMin ?? 0},${remainingMax ?? ''}}`;
    } else {
      // Default when chars exist but no length specified
      middle = `[${charClass}]+`;
    }

    /* -------------------------------------------------
     * Final pattern assembly
     * ------------------------------------------------- */
    const startPart = f.startsWith ? this.escape(f.startsWith) : '';
    const endPart = f.endsWith ? this.escape(f.endsWith) : '';

    this.generatedRegex = `^${lookaheads}${startPart}${middle}${endPart}$`;
    this.generatedDescription = this.generateDescription();
    this.test();
  }

  
  test() {
    if (!this.generatedRegex || !this.testFilename) {
      this.testResult = '';
      return;
    }

    try {
      const r = new RegExp(this.generatedRegex);
      this.testResult = r.test(this.testFilename) ? 'valid' : 'invalid';
    } catch {
      this.testResult = 'invalid';
    }
  }

  generateDescription(): string {
    const f = this.form.value;
    const parts: string[] = [];
    const notes: string[] = [];
    const warnings: string[] = [];

    /* -----------------------------
     * Allowed characters
     * ----------------------------- */
    const allowed: string[] = [];

    if (f.letters) allowed.push('letters (A–Z, a–z)');
    if (f.numbers) allowed.push('numbers (0–9)');
    if (f.underscore) allowed.push('underscore (_)');
    if (f.dash) allowed.push('dash (-)');
    if (f.space && !f.noSpaces) allowed.push('spaces');
    if (f.customChars) allowed.push(`custom characters (${f.customChars})`);

    if (allowed.length) {
      parts.push(`Allows ${allowed.join(', ')}.`);
    }

    if (f.noSpaces) {
      parts.push('Spaces are not allowed.');
    }

    /* -----------------------------
     * FULL length calculation
     * ----------------------------- */
    const startsLen = f.startsWith?.length ?? 0;
    const endsLen = f.endsWith?.length ?? 0;
    const middleMin = f.minLength ? Number(f.minLength) : null;
    const middleMax = f.maxLength ? Number(f.maxLength) : null;

    let fullMin: number | null = null;
    let fullMax: number | null = null;

    if (middleMin !== null) {
      fullMin = middleMin + startsLen + endsLen;
    }

    if (middleMax !== null) {
      fullMax = middleMax + startsLen + endsLen;
    }

    if (fullMin !== null && fullMax !== null && fullMin === fullMax) {
      parts.push(`Filename must be exactly ${fullMin} characters long.`);
    } else if (fullMin !== null && fullMax !== null) {
      parts.push(`Filename must be between ${fullMin} and ${fullMax} characters long.`);
    } else if (fullMin !== null) {
      parts.push(`Filename must be at least ${fullMin} characters long.`);
    } else if (fullMax !== null) {
      parts.push(`Filename must be at most ${fullMax} characters long.`);
    }

    /* -----------------------------
     * Special patterns (structural)
     * ----------------------------- */
    if (f.startsWith) {
      parts.push(`Must start with "${f.startsWith}".`);
    }

    if (f.endsWith) {
      parts.push(`Must end with "${f.endsWith}".`);
    }

    /* -----------------------------
     * Special patterns (global)
     * ----------------------------- */
    if (f.mustContain) {
      parts.push(`Must contain "${f.mustContain}" somewhere.`);
      notes.push(
        'Note: "Must contain" does not reduce the allowed filename length. It checks for the presence of the text anywhere.'
      );
    }

    if (f.mustNotContain) {
      parts.push(`Must NOT contain "${f.mustNotContain}".`);
    }

    /* -----------------------------
     * Overlap warning logic
     * ----------------------------- */
    if (f.mustContain && f.startsWith && f.mustContain.startsWith(f.startsWith)) {
      warnings.push(
        `Warning: "Must contain \"${f.mustContain}\"" overlaps with "Starts with \"${f.startsWith}\"". This condition may already be satisfied by the start text.`
      );
    }

    if (f.mustContain && f.endsWith && f.mustContain.endsWith(f.endsWith)) {
      warnings.push(
        `Warning: "Must contain \"${f.mustContain}\"" overlaps with "Ends with \"${f.endsWith}\"". This condition may already be satisfied by the end text.`
      );
    }

    /* -----------------------------
     * Final formatting
     * ----------------------------- */
    let description = parts.join(' ');

    if (notes.length) {
      description += `\n\n${notes.join(' ')}`;
    }

    if (warnings.length) {
      description += `\n\n${warnings.join(' ')}`;
    }

    return description;
  }

  close() {
    this.onClose.emit({ action: 'cancel' });
    this.bsModalRef.hide();
  }

  save() {

    if (this.mode === 'advanced') {
      const regex = this.form.get('advancedRegex')?.value;

      if (!this.isValidRegex(regex)) {
        return;
      }

      this.result = regex;
    }
    else {
      this.result = this.generatedRegex;
    }
    this.onClose.emit({ action: 'save' });
    this.bsModalRef.hide();

  }

  populateBasicFormFromRegex(regex: string) {
    const f = this.form;

    // STARTS WITH
    const startsMatch = regex.match(/^\^([A-Za-z0-9_\-\s]+)/);
    f.get('startsWith')?.setValue(startsMatch ? startsMatch[1] : '');

    // ENDS WITH
    const endsMatch = regex.match(/([A-Za-z0-9_\-\s]+)\$$/);
    f.get('endsWith')?.setValue(endsMatch ? endsMatch[1] : '');

    // MUST CONTAIN
    const mustContainMatch = regex.match(/\(\?=\.\*(.*?)\)/);
    f.get('mustContain')?.setValue(mustContainMatch ? mustContainMatch[1] : '');

    // MUST NOT CONTAIN
    const notContainMatch = regex.match(/\(\?!.*?(.*?)\)/);
    f.get('mustNotContain')?.setValue(notContainMatch ? notContainMatch[1] : '');

    // LENGTH {min,max}
    const lengthMatch = regex.match(/\{(\d*),(\d*)\}/);
    if (lengthMatch) {
      f.get('minLength')?.setValue(lengthMatch[1] || '');
      f.get('maxLength')?.setValue(lengthMatch[2] || '');
    }

    // CHARACTER SET
    const charsetMatch = regex.match(/\[([^\]]+)\]/);
    if (charsetMatch) {
      const charset = charsetMatch[1];

      f.get('letters')?.setValue(/[A-Za-z]/.test(charset));
      f.get('numbers')?.setValue(/[0-9]/.test(charset));
      f.get('underscore')?.setValue(charset.includes('_'));
      f.get('dash')?.setValue(charset.includes('\\-'));
      f.get('space')?.setValue(charset.includes(' '));
      f.get('noSpaces')?.setValue(regex.includes('(?!.*\\s)'));

      // CUSTOM CHARS = leftover chars

      const custom = charset
        .replace(/A-Za-z/g, '')
        .replace(/a-z/g, '')
        .replace(/0-9/g, '')
        .replace(/_/g, '')
        .replace(/\\-/g, '')
        .replace(/-/g, '')
        .replace(/ /g, '');

      f.get('customChars')?.setValue(custom || '');
    }
  }

  private isValidRegex(value: string): boolean {
    try {
      new RegExp(value);
      return true;
    } catch {
      return false;
    }
  }

  private escape(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  private escapeForCharClass(value: string): string {
    return value.replace(/[-\\^]/g, '\\$&');
  }
}