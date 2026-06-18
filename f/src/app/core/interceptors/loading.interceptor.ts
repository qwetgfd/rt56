import {
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { finalize, Observable } from 'rxjs';
import { BusyService } from '../services/busy.service';

// export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
//   return next(req);
// };

@Injectable()
export class LoadingInterceptor implements HttpInterceptor {
  constructor(private busyService: BusyService) { }

  intercept(
    request: HttpRequest<unknown>,
    next: HttpHandler
  ): Observable<HttpEvent<unknown>> {
    const urlsWithBusy = [
      'Dashboard',
      '/GetStorageAccountDetails',
      'ProcessConfiguration/InsertFlpConfigurationDetails',
      'ProcessConfiguration/InsertConfiguration',
      'ProcessConfiguration/GetFileProcessConfigurationList',
      'ProcessConfiguration/UpdateActiveStatusByFlpConfigurationId',
      'ProcessConfiguration/getRegionBySecurityGroupId',
      'ProcessConfiguration/GetConfigurationDetailsById',
      'ProcessConfiguration/SaveSecurityGroup',
      'Status/GetProcessedFileList',
      '/GetDIRuleSetByRuleSetNameId',
      '/InsertRuleSets',
      '/GetConfigurationById',
      '/InsertEIBDetails',
      '/GetEIBByEIBId',
      '/GetEIBRequiredBPKeyword',
      '/authenticate',
      '/GetCampaignUserAccessInfo',
      //'/MovedFileToLandingLayer',
      '/LandingLayerInsertConfiguration'
    ]

    // Determine if the requested URL should trigger the busy service
    const isBusyUrl = urlsWithBusy.some(url => request.url.includes(url)); 
    if (isBusyUrl) {
      //console.log(`busyService.busy: ${performance.now()}, ${request.url}`);
      this.busyService.busy();
    }

    // if (
    //   request.url.includes('ProcessConfiguration/InsertConfiguration') ||
    //   request.url.includes('Dashboard') ||
    //   request.url.includes('/GetStorageAccountDetails') ||
    //   //request.url.includes('ProcessConfiguration/GetDatabaseNames') ||
    //   request.url.includes('ProcessConfiguration/InsertFlpConfigurationDetails') ||
    //   request.url.includes('ProcessConfiguration/GetFileProcessConfigurationList') ||
    //   request.url.includes('ProcessConfiguration/UpdateActiveStatusByFlpConfigurationId') ||
    //   request.url.includes('ProcessConfiguration/getRegionBySecurityGroupId')
    //   || request.url.includes('ProcessConfiguration/GetConfigurationDetailsById')
    //   || request.url.includes('ProcessConfiguration/SaveSecurityGroup')
    //   || request.url.includes('Status/GetProcessedFileList')
    //   || request.url.includes('/GetDIRuleSetByRuleSetNameId')
    //   || request.url.includes('/InsertRuleSets')
    //   || request.url.includes('/GetConfigurationById')
    //   // || request.url.includes('ProcessConfiguration/GetDISubRules')      
    // ) {
    //   console.log(`busyService.busy: ${performance.now()}, ${request.url}`);
    //   this.busyService.busy();
    // } else {
    //   //console.log('dont show loading')
    // }

    return next.handle(request).pipe(
      //delay(1000),
      finalize(() => {
        if(isBusyUrl){
          //console.log(`busyService.idle: ${performance.now()}, ${request.url}`);
          this.busyService.idle();
        }
        // //console.log(`busy idle: ${performance.now()}, ${request.url}`);
        // if (
        //   request.url.toLowerCase().includes('/getdiconditionaloperators') ||
        //   request.url.toLowerCase().includes('/getdirulesetnamesbysecgrpid') ||
        //   request.url.toLowerCase().includes('processconfiguration/getdsconfiguration') ||
        //   request.url.toLowerCase().includes('processconfiguration/getalldatatimeformats')         
        //   //request.url.toLowerCase().includes('processconfiguration/getstorageaccountdetails')
        // ) {
        //   //do nothing
        // } else {
        //   console.log(`busyService.idle: ${performance.now()}, ${request.url}`);
        //   this.busyService.idle();
        // }

      })
    );
  }
}

