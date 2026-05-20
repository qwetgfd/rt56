import {
  Component,
  ElementRef,
  forwardRef,
  HostListener,
  Input,
  OnChanges,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ControlValueAccessor,
  FormsModule,
  NG_VALUE_ACCESSOR,
} from '@angular/forms';
import { SharepointIconComponent } from './sharepoint-icon.component';

export interface SpSearchSelectOption {
  value: string;
  label: string;
  description?: string;
}

@Component({
  selector: 'sp-search-select',
  standalone: true,
  imports: [CommonModule, FormsModule, SharepointIconComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SharepointSearchSelectComponent),
      multi: true,
    },
  ],
  template: `
    <div
      class="sp-search-select"
      [class.sp-search-select--open]="open"
      [class.sp-search-select--disabled]="disabled">
      <button
        type="button"
        class="sp-search-select__trigger sp-input"
        [id]="inputId"
        [disabled]="disabled"
        [attr.aria-expanded]="open"
        aria-haspopup="listbox"
        (click)="togglePanel($event)">
        <span class="sp-search-select__value" [class.sp-search-select__placeholder]="!selectedLabel">
          {{ selectedLabel || placeholder }}
        </span>
        <sp-icon class="sp-search-select__chevron" name="chevron-right" size="sm" />
      </button>

      <div class="sp-search-select__panel" *ngIf="open" role="listbox" [attr.aria-labelledby]="inputId">
        <div class="sp-search-select__search">
          <sp-icon name="search" size="sm" />
          <input
            type="search"
            class="sp-search-select__query"
            [(ngModel)]="query"
            [placeholder]="searchPlaceholder"
            (keydown)="onSearchKeydown($event)"
            autocomplete="off" />
        </div>
        <ul class="sp-search-select__list">
          <li *ngIf="clearable && !query.trim()">
            <button
              type="button"
              class="sp-search-select__option sp-search-select__option--clear"
              role="option"
              (mousedown)="selectOption({ value: '', label: '' }, $event)">
              <span class="sp-search-select__option-label">{{ clearLabel }}</span>
            </button>
          </li>
          <li *ngIf="!filteredOptions.length && (query.trim() || !clearable)" class="sp-search-select__empty">{{ emptyMessage }}</li>
          <li *ngFor="let opt of filteredOptions; let i = index">
            <button
              type="button"
              class="sp-search-select__option"
              [class.sp-search-select__option--active]="highlightIndex === i"
              [class.sp-search-select__option--selected]="opt.value === value"
              role="option"
              [attr.aria-selected]="opt.value === value"
              [attr.title]="hintOnHover && opt.description ? opt.description : null"
              (mousedown)="selectOption(opt, $event)">
              <span class="sp-search-select__option-label">{{ opt.label }}</span>
              <span class="sp-search-select__option-desc" *ngIf="opt.description && !hintOnHover">{{ opt.description }}</span>
            </button>
          </li>
        </ul>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        width: 100%;
      }
      .sp-search-select {
        position: relative;
        width: 100%;
      }
      .sp-search-select__trigger {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        width: 100%;
        min-height: 42px;
        text-align: left;
        cursor: pointer;
      }
      .sp-search-select__trigger:disabled {
        opacity: 0.55;
        cursor: not-allowed;
      }
      .sp-search-select__value {
        flex: 1;
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
        color: var(--sp-text);
      }
      .sp-search-select__placeholder {
        color: var(--sp-text-3);
      }
      .sp-search-select__chevron {
        transform: rotate(90deg);
        transition: transform 0.12s ease;
        flex-shrink: 0;
      }
      .sp-search-select--open .sp-search-select__chevron {
        transform: rotate(-90deg);
      }
      .sp-search-select__panel {
        position: absolute;
        z-index: 50;
        top: calc(100% + 4px);
        left: 0;
        right: 0;
        background: var(--sp-surface);
        border: 1px solid var(--sp-border-2);
        border-radius: var(--sp-radius);
        box-shadow: var(--sp-shadow, 0 8px 24px rgba(0, 0, 0, 0.12));
        overflow: hidden;
      }
      .sp-search-select__search {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 8px 10px;
        border-bottom: 1px solid var(--sp-border);
        background: var(--sp-surface-2);
      }
      .sp-search-select__query {
        flex: 1;
        min-width: 0;
        border: none;
        background: transparent;
        font-size: 14px;
        color: var(--sp-text);
        outline: none;
      }
      .sp-search-select__list {
        list-style: none;
        margin: 0;
        padding: 4px 0;
        max-height: 220px;
        overflow-y: auto;
      }
      .sp-search-select__empty {
        padding: 12px 14px;
        font-size: 13px;
        color: var(--sp-text-3);
      }
      .sp-search-select__option {
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        gap: 2px;
        width: 100%;
        padding: 8px 14px;
        min-height: 36px;
        border: none;
        background: transparent;
        text-align: left;
        cursor: pointer;
        font-size: 14px;
        color: var(--sp-text);
      }
      .sp-search-select__option:hover,
      .sp-search-select__option--active {
        background: var(--sp-surface-2);
      }
      .sp-search-select__option--selected {
        background: var(--sp-primary-light, #dfe8ff);
        color: var(--sp-primary, #304cb2);
      }
      .sp-search-select__option-label {
        font-weight: 500;
      }
      .sp-search-select__option-desc {
        font-size: 12px;
        color: var(--sp-text-3);
      }
      .sp-search-select__option--selected .sp-search-select__option-desc {
        color: var(--sp-internal-text, #304cb2);
        opacity: 0.85;
      }
    `,
  ],
})
export class SharepointSearchSelectComponent implements ControlValueAccessor, OnChanges {
  @Input() options: SpSearchSelectOption[] = [];
  @Input() placeholder = 'Select…';
  @Input() searchPlaceholder = 'Search…';
  @Input() emptyMessage = 'No matches';
  @Input() inputId = '';
  @Input() disabled = false;
  /** When true, list includes a row to clear the current value. */
  @Input() clearable = false;
  @Input() clearLabel = 'Clear selection';
  /** When true, description is tooltip-only (search still matches description). */
  @Input() hintOnHover = false;

  open = false;
  query = '';
  value = '';
  highlightIndex = 0;

  private onChange: (v: string) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['options'] && this.open) {
      this.highlightIndex = 0;
    }
  }

  get selectedLabel(): string {
    return this.options.find((o) => o.value === this.value)?.label ?? '';
  }

  get filteredOptions(): SpSearchSelectOption[] {
    const q = this.query.trim().toLowerCase();
    if (!q) return this.options;
    return this.options.filter(
      (o) =>
        o.label.toLowerCase().includes(q) ||
        o.value.toLowerCase().includes(q) ||
        (o.description?.toLowerCase().includes(q) ?? false),
    );
  }

  writeValue(v: string | null): void {
    this.value = v ?? '';
  }

  registerOnChange(fn: (v: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
    if (isDisabled) this.close();
  }

  togglePanel(event: Event): void {
    event.stopPropagation();
    if (this.disabled) return;
    this.open = !this.open;
    if (this.open) {
      this.query = '';
      this.highlightIndex = 0;
    } else {
      this.onTouched();
    }
  }

  selectOption(opt: SpSearchSelectOption, event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.value = opt.value;
    this.onChange(opt.value);
    this.close();
    this.onTouched();
  }

  close(): void {
    this.open = false;
    this.query = '';
    this.highlightIndex = 0;
  }

  onSearchKeydown(event: KeyboardEvent): void {
    const list = this.filteredOptions;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.highlightIndex = Math.min(this.highlightIndex + 1, Math.max(list.length - 1, 0));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.highlightIndex = Math.max(this.highlightIndex - 1, 0);
    } else if (event.key === 'Enter' && list[this.highlightIndex]) {
      event.preventDefault();
      this.selectOption(list[this.highlightIndex], event);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.open) return;
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.close();
      this.onTouched();
    }
  }
}
