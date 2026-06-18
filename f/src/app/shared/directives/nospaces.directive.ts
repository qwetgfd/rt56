import { Directive, HostListener } from '@angular/core';
import { NgControl, NgModel } from '@angular/forms';

@Directive({
    selector: '[appNospaces]',
    standalone: false
})
export class NospacesDirective {



  @HostListener('keydown', ['$event'])
  onKeyDown(event: KeyboardEvent) {
    if (event.key === ' ') {
      event.preventDefault();
    }
  }

}

@Directive({
    selector: '[appNumberOnly]',
    standalone: false
})
export class NumberOnlyDirective {
  @HostListener('input', ['$event']) onInput(event: Event) {
    const input = event.target as HTMLInputElement;
    input.value = input.value.replace(/[^0-9]/g, '');
  }
}

@Directive({
    selector: '[appDecimalFourPlaces]',
    standalone: false
})
export class DecimalFourPlacesDirective {

  constructor(private ngControl: NgControl) { }

  // A common approach is to update the value accessor on initialization.
  // This is especially useful for reactive forms.
  ngOnInit() {
    const initialOnChange = (this.ngControl.valueAccessor as any).onChange;
    (this.ngControl.valueAccessor as any).onChange = (value: string) => {
      const processedValue = this.processInput(value);
      initialOnChange(processedValue);
    };
  }

  @HostListener('input', ['$event']) onInput(event: Event) {
    const input = event.target as HTMLInputElement;
    let value = input.value;
    const processedValue = this.processInput(value);
    this.ngControl.valueAccessor.writeValue(processedValue);
  }


  // The logic for processing the input is separated into its own method
  // to avoid duplication.



  private processInput(value: string): string {
    if (typeof value !== 'string') return '';

    // Detect a leading minus (only if it's the first visible char)
    const isNegative = value.trimStart().startsWith('-');

    // Keep only digits and dot (we'll re-apply minus ourselves)
    let core = value.replace(/[^0-9.]/g, '');

    // Preserve a lone minus while the user is typing
    if (isNegative && core === '') {
      return '-';
    }

    // Ensure only one decimal point
    const parts = core.split('.');
    if (parts.length > 2) {
      core = parts[0] + '.' + parts.slice(1).join('');
    }

    // Detect if the user has just typed a trailing dot (e.g., "10.")
    const hasTrailingDot = core.endsWith('.');

    // Recompute after sanitation
    const p = core.split('.');
    const integerPartRaw = p[0] || '';
    let fractionPart = p[1] ?? '';

    // Limit to 4 decimal places
    if (fractionPart.length > 4) {
      fractionPart = fractionPart.substring(0, 4);
    }

    // Normalize leading zeros in integer part (keep "0.xxx"; reduce "00012" -> "12")
    let integerPart = integerPartRaw.replace(/^0+(?=\d)/, '');

    // Rebuild number
    let processed = integerPart;

    if (fractionPart.length > 0) {
      // Decimal with digits after the dot
      processed += '.' + fractionPart;
    } else if (hasTrailingDot) {
      // Keep the trailing dot when the user typed it (e.g., "10.")
      processed += '.';
    }

    // Normalize edge cases
    if (processed === '.') processed = '0.'; // "." -> "0."

    // Re-apply minus
    if (isNegative && processed !== '') {
      processed = '-' + processed;
    }

    // Handle "-." becoming "-0."
    if (processed === '-.') processed = '-0.';

    return processed;
  }




}


