import { Component, OnInit } from '@angular/core';
import { DashboardService } from '../core/services/dashboard.service';
import { environment } from '../environments/environment';
import {
  ClientListResponse,
  DiUtilization,
  FileListResponse,
  ProcessListRequest,
  ProcessListResponse,
  RealTimeProcessing,
} from '../shared/models/dashboard';
import { APIResponse } from '../shared/models/apiResponse';
import {
  FlpConfigurationResponse,
  FlpUploadedFileStatusResponse,
} from '../shared/models/fileUploadStatus';
import { FileUploadStatusService } from '../core/services/file-status.service';
import { Router } from '@angular/router';
import { LoginService } from '../core/services/login.service';
//import { Chart, ChartConfiguration, ChartOptions } from 'chart.js';
@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrl: './dashboard.component.css',
    standalone: false
})
export class DashboardComponent implements OnInit {
  todayDate!: Date;
  userFullName!: string;
  flpConfigurationResponse: FlpConfigurationResponse[] = [];
  clientListResponse!: ClientListResponse;
  processListResponse!: ProcessListResponse;
  fileListResponse!: FileListResponse;
  canvas: any;
  ctx: any;
  fileProcessChartData: number[] = [];
  diUtilization: DiUtilization[] = [];
  realTimeProcessing: RealTimeProcessing[] = [];
  successPercentage!: number;
  constructor(
    private dashboardService: DashboardService,
    private fileUploadService: FileUploadStatusService,
    private router: Router
  ) {}
  ngOnInit(): void {
    //if (sessionStorage.getItem('GUID')) {
      this.getFileProcessCount();
      this.todayDate = new Date();
      this.userFullName = environment.userFullName;
      this.getProcessList();
      this.getClientList();
      this.getRealTimeProcessingStatus();
      if (!this.userFullName) this.setUser();
    //}else{console.log("guid not set")}
  }
  setUser() {
    this.dashboardService.getUserProfile().subscribe({
      next: (res) => {
        if (res) this.userFullName = res.displayName;
      },
    });
  }
  getProcessList() {
    this.dashboardService.getProcessesCreated().subscribe({
      next: (response: APIResponse<ProcessListResponse | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.processListResponse = response.result;
              this.processListResponse.currentMonthName =
                this.dashboardService.getMonthName(
                  response.result.currentMonth
                );
            } else {
            }
        } else {
          console.warn('process created data not found !');
        }
      },
      error: (error) => {
        console.log(error);
      },
    });
  }
  getClientList() {
    this.dashboardService.getClientList().subscribe({
      next: (response: APIResponse<ClientListResponse | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.clientListResponse = response.result;
              this.clientListResponse.currentMonthName =
                this.dashboardService.getMonthName(
                  response.result.currentMonth
                );
            } else {
            }
        } else {
          console.warn('process created data not found !');
        }
      },
      error: (error) => {
        console.log(error);
      },
    });
  }
  getFileProcessCount() {
    try {
      this.dashboardService.getFilesProcessed().subscribe({
        next: (response: APIResponse<FileListResponse | null>) => {
          if (response) {
            if (response.responseCode === 200)
              if (response.result && response.responseMessage[0] == 'Success') {
                this.fileListResponse = response.result;
                this.fileListResponse.currentMonthName =
                  this.dashboardService.getMonthName(
                    response.result.currentMonth
                  );
                let x = this.dashboardService.calculateSuccessPercentages(
                  response.result
                );
                this.successPercentage = Math.round(x.successPercentage);
                this.fileProcessChartData = [];
                this.fileProcessChartData[0] = Number(
                  x.successPercentage?.toFixed()
                );
                this.fileProcessChartData[1] = Number(
                  x.failurePercentage?.toFixed()
                );
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
    } catch (error) {
      console.log('error thrown ');
    }
  }
  getRealTimeProcessingStatus() {
    this.realTimeProcessing = [];
    this.dashboardService.getRealTimeProcessingStatus().subscribe({
      next: (response: APIResponse<RealTimeProcessing[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.realTimeProcessing = response.result;
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
  getFileProcessingStatus() {
    this.flpConfigurationResponse = [];
    this.fileUploadService.getFileUploadStatus().subscribe({
      next: (response: APIResponse<FlpUploadedFileStatusResponse[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              response.result.forEach((x) => {
                x.fileConfigurationStatusList.forEach((y) => {
                  this.flpConfigurationResponse.push(y);
                });
              });
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

  openStatusDetails(id: string): void {
    this.router.navigate(['/file-processing-status', id]);
  }
  getFileExtension(fileName: string): string {
    return this.dashboardService.getFileExtension(fileName) + '.png';
  }
  getFileName(fileName: string): string {
    return this.dashboardService.getFileName(fileName);
  }
}
