import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { TreeviewItem } from  '@ccondrup/ngx-treeview';
import { FileUploadStatusService } from '../core/services/file-status.service';
import { APIResponse } from '../shared/models/apiResponse';
import { Subscription, interval } from 'rxjs';
import {
  Duration,
  FileConfigAndStatus,
  FileStatus,
  FileUploadDetailedStatus,
  FlpConfigurationResponse,
  FlpUploadedFileStatusResponse,
} from '../shared/models/fileUploadStatus';
import { ActivatedRoute, Router } from '@angular/router';
import { environment } from '../environments/environment';
import { ProcessStatusTemplateComponent } from '../process-status-template/process-status-template.component';
import { DashboardService } from '../core/services/dashboard.service';
import { DataSourceType } from '../shared/enum';

@Component({
    selector: 'app-file-processing-status',
    templateUrl: './file-processing-status.component.html',
    styleUrl: './file-processing-status.component.css',
    standalone: false
})
export class FileProcessingStatusComponent implements OnInit, OnDestroy {
  @ViewChild(ProcessStatusTemplateComponent)
  processStatus!: ProcessStatusTemplateComponent;
  fileUploadStatusView: TreeviewItem[];
  fileUploadStatus: FileStatus[];
  fileConfigAndStatus: FileConfigAndStatus[];
  fileUploadDetailedStatus: FileUploadDetailedStatus;
  flpUploadedFileStatusResponse: FlpUploadedFileStatusResponse[];
  selectedValues: string[];
  dataSubscription: Subscription = new Subscription();
  refreshInterval = environment.refreshInterval;
  intervalId: any;
  timeDuration: Duration;
  apiErrorMessage: string = '';
  flpConfigurationID: string = '';
  uploadFileId: string = '';
  paramsId: string = '';
  collapsedId: string = '';
  collapsedChildId: string = '';
  isIntervalStarted: boolean = false;
  expandParent: boolean = false;
  expandChild: boolean = false;
  isDetailedStatus: boolean = true;
  tabName:string = null;
  DataSourceTypes = DataSourceType;
  constructor(
    private fileUploadService: FileUploadStatusService,
    private dashboardService: DashboardService,
    private router: Router,
    private route: ActivatedRoute
  ) {}
  ngOnInit(): void {
    this.route.params.subscribe((params) => {
      this.paramsId = params['id'];
    });
    this.getFileUploadedStatus();
    this.dataSubscription.add(
      interval(this.refreshInterval).subscribe(() => this.getStatus())
    );
    this.apiErrorMessage = '';
    this.fileUploadStatus = [];
  }
  updateStatus(data: Duration) {
    if (data) {
      this.timeDuration = data;
      // let indx = this.timeDuration.indexOf(data);
      // if (indx != -1) this.timeDuration.splice(indx, 1);
      // this.timeDuration.push(data);
    }
  }
  // getUpdatedDuration(uploadFileId: string ="") {
  //   this.timeDuration.find((x) => x.uploadFileId == uploadFileId).endTime;
  // }    
  getFileIdByConfigId(
    responses: FlpUploadedFileStatusResponse[],
    flpConfigurationID: string
  ) {
    return responses.flatMap((response) =>
      response.fileConfigurationStatusList
        .filter(
          (configuration) =>
            configuration.flpConfigurationID == flpConfigurationID
        )
        .flatMap((configuration) =>
          configuration.uploadedFiles.map((file) => file.uploadFileId)
        )
    );
  }

  getConfigIdByClientId(
    responses: FlpUploadedFileStatusResponse[],
    clientId: number
  ) {
    return responses
      .filter((response) => response.clientId == clientId)
      .flatMap((response) =>
        response.fileConfigurationStatusList.map(
          (configuration) => configuration.flpConfigurationID
        )
      );
  }

  formatFileStatus(fileUploadStatus: FlpUploadedFileStatusResponse[]) {
    this.fileUploadStatusView = [];
    try {
      fileUploadStatus.forEach((x) => {
        let item = new TreeviewItem({
          text: x.clientName,
          value: x.clientId,
          children: [{ text: '', value: 0 }],
        });
        x.fileConfigurationStatusList.forEach((y) => {
          let tree2 = new TreeviewItem({
            text: y.flpConfigurationName,
            value: y.flpConfigurationID,
            children: [{ text: '', value: 0 }],
          });
          
          y.uploadedFiles.forEach((z) => {
            if (
              this.paramsId &&
              this.paramsId != '' &&
              z.uploadFileId == this.paramsId
            ) {
              item.collapsed = true;
              tree2.collapsed = true;
              this.collapsedChildId = tree2.value;
              this.collapsedId = item.value;
              if (this.uploadFileId != this.paramsId) {
                this.getDetailedStatus(y.flpConfigurationID, z.uploadFileId,z.tabName);
              }
            } else if (
              this.collapsedId != '' &&
              this.collapsedId == item.value
            ) {
              item.collapsed = this.expandParent;
              if (
                this.collapsedChildId != '' &&
                this.collapsedChildId == tree2.value
              )
                tree2.collapsed = this.expandChild;
            }
            let tree3 = new TreeviewItem({ text: z.uploadFileName, value: z });
            if (tree2.children) tree2.children.push(tree3);
          });
          if (item.children) item.children.push(tree2);
        });
        
        this.fileUploadStatusView.push(item);
      });
      // if (this.paramsId && this.paramsId != '') {
      //   this.expandNode(this.paramsId);
      // }
    } catch (error) {
      console.error('error occurred in conversion', error);
    }
  }
  updateFileUploadedStatus(
    flpUploadedFileStatusResponse: FlpUploadedFileStatusResponse[]
  ) {
    try {
      this.fileUploadStatusView.forEach((x) => {
        if (x.children) {
          x.children.forEach((y) => {
            if(y.children){
            y.children.forEach((z) => {
              if (z.value.uploadFileId != '') {
                let processStatus = this.getProcessStatusName(
                  flpUploadedFileStatusResponse,
                  z.value.uploadFileId, z.value.tabName
                );
                if (processStatus != '' && z.value.fileProcessstatusName)
                  z.value.fileProcessstatusName = processStatus;
              }
            });
          }
          });
        }
      });
    } catch (error) {
      console.log(error);
    }
  }


  getProcessStatusName(
  flpUploadedFileStatusResponse: FlpUploadedFileStatusResponse[],
  uploadFileId: string,
  tabName: string = null
): string {
  if (!flpUploadedFileStatusResponse) return '';

  const processStatus = flpUploadedFileStatusResponse
    .flatMap((uploadedFiles) =>
      uploadedFiles.fileConfigurationStatusList.flatMap(
        (fileConfig) => fileConfig.uploadedFiles || []
      )
    )
    .find((file) =>
      file.uploadFileId == uploadFileId &&
      (
        tabName == null ? true : file.tabName == tabName
      )
    );

  return processStatus ? processStatus.fileProcessstatusName : '';
}
  // getProcessStatusName(
  //   flpUploadedFileStatusResponse: FlpUploadedFileStatusResponse[],
  //   uploadFileId: string,
  //   tabName:string = null
  // ): string {
    
  //   if (!flpUploadedFileStatusResponse) return '';
  //   const processStatus = flpUploadedFileStatusResponse
  //     .flatMap((uploadedFiles) =>
  //       uploadedFiles.fileConfigurationStatusList.flatMap(
  //         (fileConfig) => fileConfig.uploadedFiles || []
  //       )
  //     )
  //     .find((file) => (file.uploadFileId == uploadFileId));
  //     console.log(uploadFileId);
  //     console.log('process status');
  //     console.log(processStatus);
  //   return processStatus ? processStatus.fileProcessstatusName : '';
  // }
  getStatus() {
    this.fileUploadService.getFileUploadStatus().subscribe({
      next: (response: APIResponse<FlpUploadedFileStatusResponse[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.flpUploadedFileStatusResponse = response.result;
              this.updateFileUploadedStatus(this.flpUploadedFileStatusResponse);
            } else {
              //this.apiErrorMessage = response.responseMessage[0];
            }
        } else {
          console.warn('not got any file upload status data!');
        }
      },
      error: (error) => {
        console.log(error);
      },
    });
  }
  getFileUploadedStatus() {
    this.uploadFileId = '';
    //this.fileUploadStatusView = [];
    this.flpUploadedFileStatusResponse = [];
    this.fileUploadStatus = [];
    this.apiErrorMessage = '';
    this.fileUploadService.getFileUploadStatus().subscribe({
      next: (response: APIResponse<FlpUploadedFileStatusResponse[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              console.log(`status response ${response}`)
              this.formatFileStatus(response.result);
            } else {
              //this.apiErrorMessage = response.responseMessage[0];
            }
        } else {
          console.warn('not got any file upload status data!');
        }
      },
      error: (error) => {
        console.log(error);
      },
    });
  }

  getDetailedStatus(configurationID: string, uploadFileID: string,tabName:string = null, fileProcessingServerTypeId: number = null) {
    // this.collapsedChildId = '';
    // this.collapsedId = '';
    this.isDetailedStatus = !this.isDetailedStatus;
    this.timeDuration = null;
    this.router.navigate(['/file-processing-status', uploadFileID]);
    this.isIntervalStarted = true;
    this.flpConfigurationID = configurationID;
    this.uploadFileId = uploadFileID;
    this.tabName = tabName;
    this.expandParent = true;
    this.expandChild = true;
  }
  onSelectedChange(values: string[]) {
    this.selectedValues = values;
  }
  expandedItemId: string | null = null;
  toggleNode(item: TreeviewItem) {
    this.isDetailedStatus = true;
    this.paramsId = '';
    this.collapsedChildId = item.value;
    //this.fileUploadStatus = [];
    item.collapsed = !item.collapsed;
    this.expandChild =  item.collapsed ;
    this.fileUploadStatusView.forEach((x) => {
      if (x.children) {
        x.children.forEach((y) => {
          if (this.collapsedChildId == y.value) y.collapsed = item.collapsed;
          else y.collapsed = false;
        });
      }
    });
    this.getFileUploadedStatus() ;
  }
  clientToggleNode(client: TreeviewItem) {
    this.isDetailedStatus = true;
    this.paramsId = '';
    this.collapsedChildId = '';
    this.collapsedId = client.value;
    client.collapsed = !client.collapsed;
    this.expandParent =  client.collapsed ;
    this.fileUploadStatusView.forEach((x) => {
      if (this.collapsedId == x.value) x.collapsed = client.collapsed;
      else x.collapsed = false;
      if (x.children && client.collapsed) {
        x.children.forEach((y) => {
          y.collapsed = false;
        });
      }
    });
    this.getFileUploadedStatus() ;
  }
  expandNode(val: string) {
    for (const child of this.fileUploadStatusView) {
      for (const node of child.children) {
        //if (node.value == val) {
        // node.collapsed = true;
        // child.collapsed = true;
        for (let child of node.children) {
          if (val != child.value.uploadFileId) {
            node.collapsed = true;
            child.collapsed = true;
            this.getDetailedStatus(val, child.value.uploadFileId, child.value.tabName, child.value.fileProcessingServerTypeId);
            return;
          }
        }
      }
    }
  }
  getFileExtension(fileName: string): string {
    return this.dashboardService.getFileExtension(fileName) + '.png';
  }
  ngOnDestroy(): void {
    if (this.dataSubscription) {
      this.dataSubscription.unsubscribe();
    }
    if (this.intervalId) clearInterval(this.intervalId);
  }
}
