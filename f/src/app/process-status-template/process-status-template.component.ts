import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
} from '@angular/core';
import { APIResponse } from '../core/models/apiResponse';
import {
  Duration,
  FileUploadDetailedStatus,
} from '../shared/models/fileUploadStatus';
import { FileUploadStatusService } from '../core/services/file-status.service';
import { Subscription } from 'rxjs/internal/Subscription';
import { environment } from '../environments/environment';
import { interval } from 'rxjs';
import { DataSourceType } from '../shared/enum';

@Component({
    selector: 'app-process-status-template',
    templateUrl: './process-status-template.component.html',
    styleUrl: './process-status-template.component.css',
    standalone: false
})
export class ProcessStatusTemplateComponent implements OnInit, OnDestroy {
  @Input() configurationID: string;
  @Input() fileId: string;
  @Input() tabName: string=null;
  @Input() fileProcessingServerTypeId : number = null;
  @Output() completionTime = new EventEmitter<Duration>();
  DataSourceTypes = DataSourceType;
  fileUploadDetailedStatus!: FileUploadDetailedStatus;
  intervalId: any;
  dataSubscription: Subscription = new Subscription();
  refreshInterval = environment.refreshInterval;
  apiErrorMessage: string = '';
  loading: boolean = false;
  UIValidationStatusError : boolean = false;
  BEValidationStatusError : boolean = false;
  UIValidationFileHyperLink : string = '';
  UIValidationFileStatusCode : string = '';
  
  constructor(private fileUploadService: FileUploadStatusService) {}
  ngOnInit(): void {
    console.log('ON initt')
    console.log(this.configurationID, this.fileId, this.tabName)
    if (this.configurationID && this.fileId) {
      this.loading = true;
      this.getDetailedStatus(this.configurationID, this.fileId,this.tabName);
      this.loading = false;
      this.dataSubscription.add(
        interval(this.refreshInterval).subscribe(() =>

          this.getDetailedStatus(this.configurationID, this.fileId,this.tabName)
        )
      );
    }
  }
  getDetailedStatus(flpConfigurationID: string, uploadFileId: string,tabName:string = null) {    
    this.apiErrorMessage = '';
     //this.fileUploadDetailedStatus = null
    try {
      if (flpConfigurationID && uploadFileId) {
        this.fileUploadService
          .getFileUploadDetailedStatus(flpConfigurationID, uploadFileId,tabName)
          .subscribe({
            next: (response: APIResponse<FileUploadDetailedStatus | null>) => {
         
              if (response) {
                if (response.responseCode === 200)
                  if (
                    response.result &&
                    response.responseMessage[0] == 'Success'
                  ) {
                    this.fileUploadDetailedStatus = response.result;
                   
                    let startTime,
                      endTime,
                      totalDuration = 0;
                    for (
                      let i = 0;
                      i < this.fileUploadDetailedStatus.fileStatus.length;
                      i++
                    ) {
                      if (i == 0)
                        startTime =
                          this.fileUploadDetailedStatus.fileStatus[i]
                            .statusStartTime;
                      // if (
                      //   this.fileUploadDetailedStatus.fileStatus[i]
                      //     .statusCompletionTime &&
                      //   this.fileUploadDetailedStatus.fileStatus[i]
                      //     .statusCompletionTime != '0001-01-01T00:00:00'
                      // )
                      // endTime =
                      //   this.fileUploadDetailedStatus.fileStatus[i]
                      //     .statusCompletionTime;
                      totalDuration +=
                        this.fileUploadDetailedStatus.fileStatus[i]
                          .durationInSeconds;
                    }
                    if (
                      this.fileUploadDetailedStatus.fileStatus[
                        this.fileUploadDetailedStatus.fileStatus.length - 1
                      ].statusCompletionTime != '0001-01-01T00:00:00'
                    )
                      endTime =
                        this.fileUploadDetailedStatus.fileStatus[
                          this.fileUploadDetailedStatus.fileStatus.length - 1
                        ].statusCompletionTime;
                    let data: Duration = {
                      databaseName: this.fileUploadDetailedStatus.databaseName,
                      tableName: this.fileUploadDetailedStatus.tableName,
                      totalRecords: this.fileUploadDetailedStatus.totalRecords,
                      processedRecords:
                        this.fileUploadDetailedStatus.processedRecords,
                      duplicateRecords:
                        this.fileUploadDetailedStatus.duplicateRecords,
                      uploadFileId: this.fileUploadDetailedStatus.uploadFileID,
                      startTime: startTime,
                      endTime: endTime,
                      totalDuration: totalDuration,
                      description : this.fileUploadDetailedStatus.description,
                      tabName: this.fileUploadDetailedStatus.tabName
                    };
                    //console.log(data);
                    this.UIValidationFileStatusCode = this.fileUploadDetailedStatus?.statusCode;
                    this.UIValidationStatusError = this.fileUploadDetailedStatus?.fileStatus?.find(x=> x.statusName.toLowerCase() === 'validations')?.status?.toLowerCase() === 'error';
                    if(this.UIValidationStatusError){
                      //find the link and display on browser UIValidationFileHyperLink
                      this.UIValidationFileHyperLink = this.fileUploadDetailedStatus?.blobName
                      
                    }

                    this.BEValidationStatusError = this.fileUploadDetailedStatus?.fileStatus?.find(x=> x.statusName.toLowerCase() === 'backend checks')?.status?.toLowerCase() === 'error';
                    if(this.BEValidationStatusError){
                      //find the link and display on browser UIValidationFileHyperLink
                      this.UIValidationFileHyperLink = this.fileUploadDetailedStatus?.blobName
                    }
                    this.completionTime.emit(data);
                  } else {
                    this.apiErrorMessage = response.responseMessage[0];
                  }
              } else {
                console.warn('not got any file upload status data!');
              }
            },
            error: (error) => {
              console.log(error);
            },
          });
      } else {
        this.apiErrorMessage = 'Invalid ConfigurationID or UploadedFileID!';
      }
    } catch (error) {
      console.error('error in getDetailedStatus', error);
      this.loading = false;
    }
  }


  

download() {
  const sasUrl = this.UIValidationFileHyperLink; // your SAS URL
  this.fileUploadService.downloadFile(sasUrl).subscribe(response => {
     const pdfUrl = URL.createObjectURL(response);
        const link = document.createElement('a');
        link.href = pdfUrl;
        const fileName = this.getFileNameFromUrl(sasUrl);
        link.download = fileName; // Set the desired file name
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(pdfUrl);
    // const blob = new Blob([response.body!], {
    //   type: response.headers.get('Content-Type') || 'application/octet-stream'
    // });

    // const fileName = this.getFileName(response);

    // const link = document.createElement('a');
    // link.href = window.URL.createObjectURL(blob);
    // link.download = fileName;
    // link.click();
  });
}

getFileNameFromUrl(url: string): string {
  const path = url.split('?')[0]; // Remove the query string
  const fileName = path.substring(path.lastIndexOf('/') + 1);
  const decodedFileName = decodeURIComponent(fileName);
  return decodedFileName; // Extract the filename
}


  ngOnDestroy(): void {
    if (this.dataSubscription) {
      this.dataSubscription.unsubscribe();
    }
    if (this.intervalId) clearInterval(this.intervalId);
  }
}
