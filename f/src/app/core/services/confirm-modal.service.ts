import { Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialogComponent } from '../../confirm-dialog/confirm-dialog.component';
import { CustomEIBViewComponent } from '../../eib/custom-eib-view/custom-eib-view.component';
import { DBView } from '../models/EIB/dbView';

@Injectable({
  providedIn: 'root'
})
export class ModalService {
  constructor(private modalService: NgbModal) { }


  confirm(title: string, message: string): Promise<boolean> {
    const modalRef = this.modalService.open(ConfirmDialogComponent, {
      backdrop : 'static', //prevent closing on outside click
      keyboard : false //prevent closing on ESC key
    });
    modalRef.componentInstance.title = title;
    modalRef.componentInstance.message = message;

    return modalRef.result.then(
      (result) => !!result,
      () => false // Handle dismiss (e.g., ESC or clicking outside)
    );
  }

  //can become reusable pass T in data
  customizeEIB(title: string, businessProcessName: string, data : DBView[]) : Promise<{payload : DBView[], confirm : boolean}> {
    const modalRef = this.modalService.open(CustomEIBViewComponent, 
      {
        backdrop: 'static',
        keyboard : false
      });
    modalRef.componentInstance.data = data;
    modalRef.componentInstance.businessProcessName = businessProcessName;
    
    return modalRef.result.then(
      (result) => result as {payload : DBView[]; confirm: boolean},
      () => ({payload : [], confirm: false}) // Handle dismiss (e.g., ESC or clicking outside)
    );
  }

}
