import { inject, Injectable } from '@angular/core';
import { Resolve, ResolveFn } from '@angular/router';
import { SchedulerType, StorageAccount } from '../models/fileProcessConfig';
import { forkJoin, Observable } from 'rxjs';

import { ProcessConfigService } from '../services/process-config.service';
import { HttpClient } from '@angular/common/http';
import { ConfigurationService } from '../services/configuration.service';

@Injectable({
  providedIn: 'root',
})
export class OfflineModuleResolver implements Resolve<{
  storageAccounts: StorageAccount[], dateTimeFormats: any[], fileExtensions: any[], dataSliceConfig: any, processTypes: any, serverDetails: any, weekDays: any,
  schedulerTypes: SchedulerType[]
}> {
  constructor(
    private http: HttpClient,
    private processService: ProcessConfigService,
    private configService: ConfigurationService
  ) { }

  resolve(): Observable<{
    storageAccounts: StorageAccount[], dateTimeFormats: any[], fileExtensions: any[], dataSliceConfig: any, processTypes: any, serverDetails: any, weekDays: any,
    schedulerTypes : SchedulerType[]
  }> {
    console.log(`DIApiToken: ${localStorage.getItem('DIApiToken')}`);

    // This resolver can be used to perform any necessary setup or data fetching before the module is loaded.
    return forkJoin({
      storageAccounts: this.processService.getStorageAccountDetails$(),
      dateTimeFormats: this.configService.getAllDataTimeFormats$(),
      fileExtensions: this.configService.getFileExtensionNames$(),
      dataSliceConfig: this.processService.getDataSliceConfiguration$(1),
      processTypes: this.processService.getProcessType$(),
      serverDetails: this.processService.getServerDetails$(),
      weekDays: this.processService.getWeekDayName$(),
      schedulerTypes : this.processService.getSchedulerTypes$()
    });
  }
}


