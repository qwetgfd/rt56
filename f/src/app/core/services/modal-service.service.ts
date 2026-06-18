import { Injectable } from '@angular/core';
import { BsModalRef, BsModalService, ModalOptions } from 'ngx-bootstrap/modal';
import { NotificationComponent } from '../../shared/components/notification/notification.component';
import { FilePreviewComponent } from '../../new-process-configuration/file-preview/file-preview.component';
import { ColumnNameDatatypeName } from '../models/columnNameDatatypeName';
import { RegexBuilderComponent } from '../../regex-builder/regex-builder.component';

@Injectable({
  providedIn: 'root'
})
export class ModalServiceService {
  bsModalRef? : BsModalRef

  constructor(
    private modalService : BsModalService
  ) { }

  showNotification(isSuccess: boolean, title: string, message: string) {
    const initialState: ModalOptions = {
      initialState: {
        isSuccess,
        title,
        message
      }
    };

    this.bsModalRef = this.modalService.show(NotificationComponent, initialState);
  }

  showFilePreview(
    delimiter : string, 
    flexCheckHasHeaders : boolean, 
    txtQuoteCharacter : string,
    skipRows : number,
    skipFooterRows : number,
    fileName : string,
    recordHeaders : ColumnNameDatatypeName[] | null
   ){
    const config: ModalOptions = {
      backdrop: 'static',
      keyboard: false,
      ignoreBackdropClick: true,
      class: 'custom-class',
      initialState : {
        fileName,
        recordHeaders,
        delimiter,
        flexCheckHasHeaders,
        txtQuoteCharacter,
        skipRows,
        skipFooterRows
      }
    }
    return this.bsModalRef = this.modalService.show(FilePreviewComponent, config);
  }

  showCustomRegexBuilder(existingRegex? : string ){
    const config: ModalOptions = {
      backdrop: 'static',
      keyboard: false,
      ignoreBackdropClick: true,
      class: 'custom-class',
      initialState : {
        existingRegex:  existingRegex ?? null
      }
    }
    return this.bsModalRef = this.modalService.show(RegexBuilderComponent, config);
  }
}
