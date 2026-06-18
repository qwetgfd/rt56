import { Component, Input } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

@Component({
    selector: 'app-confirm-dialog',
    templateUrl: './confirm-dialog.component.html',
    styleUrl: './confirm-dialog.component.css',
    standalone: false
})
export class ConfirmDialogComponent {
  @Input() title: string = 'Confirmation';
  @Input() message: string = 'Are you sure you want to proceed?';

  constructor(public activeModal: NgbActiveModal) {}

  onNoClick(): void {
    this.activeModal.dismiss('No click'); 
  }
  onYesClick(): void {
    this.activeModal.close(true); 
  }

}
