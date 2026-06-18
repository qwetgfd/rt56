import { NgModule } from "@angular/core";
import { RouterModule } from "@angular/router";
import { NgxSpinnerModule } from "ngx-spinner";
import { ToastrModule } from "ngx-toastr";
import { ModalModule } from 'ngx-bootstrap/modal';
import { CommonModule } from "@angular/common";
import { SharedModule } from "../shared/shared.module";

@NgModule({
    declarations: [
      
    ],
    imports: [            
      RouterModule,
      SharedModule,
      NgxSpinnerModule,
      ToastrModule.forRoot({
        positionClass: 'toast-bottom-right',
        preventDuplicates:true
      }),
      ModalModule
    ],
    exports : [
      //TopNavComponent,
      NgxSpinnerModule
    ]
  })
  export class CoreModule { }