import { NgxDropdownConfig } from 'ngx-select-dropdown';

/** Registered application / library dropdowns (ngx-select-dropdown). */
export const SP_APP_DROPDOWN_CONFIG: NgxDropdownConfig = {
  displayKey: 'label',
  search: true,
  height: '220px',
  placeholder: 'Select application',
  searchPlaceholder: 'Search applications',
  noResultsFound: 'No applications found',
  clearOnSelection: false,
  limitTo: 0,
};

export const SP_LIBRARY_DROPDOWN_CONFIG: NgxDropdownConfig = {
  displayKey: 'label',
  search: true,
  height: '200px',
  placeholder: 'Select library',
  searchPlaceholder: 'Search libraries',
  noResultsFound: 'No libraries found',
  searchOnKey: 'label',
  clearOnSelection: false,
  limitTo: 0,
};

export interface SpDropdownOption {
  id: string;
  label: string;
  /** Used for search matching only (site · host). */
  searchText?: string;
}

/** ngx-select-dropdown emits `{ value }`; ngModel uses the option object directly. */
export function unwrapNgxDropdownSelection(
  model: unknown,
): SpDropdownOption | null {
  if (model == null) return null;
  if (typeof model === 'object' && 'value' in model) {
    return unwrapNgxDropdownSelection((model as { value: unknown }).value);
  }
  if (Array.isArray(model)) {
    const first = model[0];
    return first && typeof first === 'object' && 'id' in first ? (first as SpDropdownOption) : null;
  }
  if (typeof model === 'object' && 'id' in model && 'label' in model) {
    return model as SpDropdownOption;
  }
  return null;
}
