import { Component, Input, Output, EventEmitter } from '@angular/core';


@Component({
  selector: 'app-error-box',
  standalone: false,
  templateUrl: './error-box.component.html',
  styleUrl: './error-box.component.css'
})
export class ErrorBoxComponent {

  @Input() error?: string;
  @Output() close = new EventEmitter<void>();

  hideError(event : Event) : void {
    event.stopPropagation();    
    this.error = '';
  }

}
