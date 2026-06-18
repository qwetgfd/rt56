import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { DBView } from '../../core/models/EIB/dbView';
import { ToastrService } from 'ngx-toastr';
import { debounceTime, Subject } from 'rxjs';

@Component({
    selector: 'app-custom-eib-view',
    templateUrl: './custom-eib-view.component.html',
    styleUrl: './custom-eib-view.component.css',
    standalone: false
})
export class CustomEIBViewComponent {

  // @Input() title: string = '';
  // @Input() businessProcessName: string = '';
  // @Input() data: DBView[];

  // private fromColumnChange$ = new Subject<{ value: number; index: number }>();

  // constructor(public activeModal: NgbActiveModal, private toastr: ToastrService) {


  //   this.fromColumnChange$
  //     .pipe(debounceTime(500)) // Wait 500ms after user stops typing
  //     .subscribe(({ value, index }) => {
  //       this.validateFromColumn(index);
  //     });

  // }

  // onClose() {
  //   this.activeModal.dismiss(); // or pass data if needed
  // }

  // onYesClick() {
  //   this.activeModal.close({ processName: this.businessProcessName, payload: this.data, confirm: true }); // or any other payload
  // }

  // isRowEditable(index: number): boolean {
  //   if (index === 0) return true; // First row is always editable
  //   const prev = this.data[index - 1];
  //   return !!(prev.fromColumn && prev.toColumn);
  // }

  // onFromColumnChange(value: number, index: number): void {
  //   this.fromColumnChange$.next({ value, index });
  // }


  // //todo: add more validations 
  // validateFromColumn(index: number): void {
  //   if (index === 0) return;

  //   const current = this.data[index];
  //   const prev = this.data[index - 1];

  //   const currentFrom = Number(current.fromColumn);
  //   const prevTo = Number(prev.toColumn);

  //   if (
  //     !isNaN(currentFrom) &&
  //     !isNaN(prevTo) &&
  //     currentFrom <= prevTo
  //   ) {
  //     alert(
  //       `From Column in row ${index + 1} must be greater than To Column in row ${index}`
  //     );
  //     current.fromColumn = null;
  //   }
  // }
}
