import { Component, OnInit } from '@angular/core';
import { ProcessConfigListSearchDropdown } from '../core/models/processConfigurationlist';
import {
  FlpProcessedFile,
  FlpProcessedFileListResponse,
  ProcessedFileRequest,
} from '../core/models/processedFileList';
import { Router } from '@angular/router';
import { ConfigurationService } from '../core/services/configuration.service';
import { FileUploadStatusService } from '../core/services/file-status.service';
import { APIResponse } from '../shared/models/apiResponse';
import { DashboardService } from '../core/services/dashboard.service';
import { config } from 'rxjs';

@Component({
    selector: 'app-processed-file-list',
    templateUrl: './processed-file-list.component.html',
    styleUrl: './processed-file-list.component.css',
    standalone: false
})
export class ProcessedFileListComponent implements OnInit {
  public processedFileList: FlpProcessedFile[];
  fileConfigurationId: string = '';
  tabName : string = null;
  uploadFileId: string = '';
  logFileConfigurationId: string = '';
  logUploadFileId: string = '';
  logFileName: string = '';
  public searchDate: string | null = null;
  public selectedNgModelValue: string[];
  public selectedSearchField: string[] = [];
  public selectedSearchValue: string | null = null;
  public disableInput: boolean = true;
  public searchTerm: string | null = null;
  public apiErrorMessage: string = '';
  public loading: boolean = false;
  public loggedInUser: string | null = null;
  public pageLinks: number;
  public first: number = 0;
  public rows: number = 20;
  public page: number = 0;
  public totalRecords: number = 0;

  public searchOnFields: ProcessConfigListSearchDropdown[] = [
    { databaseColumnName: 'flp.process_name', databaseLabel: 'Process Name' },
    { databaseColumnName: 'pst.statusName', databaseLabel: 'Status Name' },
    { databaseColumnName: 'flp.[region]', databaseLabel: 'Region' },
    { databaseColumnName: 'flp.[subregion]', databaseLabel: 'Sub Region' },
    { databaseColumnName: 'flp.[clientName]', databaseLabel: 'Client Name' },
    { databaseColumnName: 'dbc.databaseName', databaseLabel: 'Database Name' },
    { databaseColumnName: 'ctm.tableName', databaseLabel: 'Table Name' }, 
    { databaseColumnName: 'uf.uploadFileId', databaseLabel: 'File ID' },
    { databaseColumnName: 'uf.uploadedBy', databaseLabel: 'Processed By' },
    // { databaseColumnName: 'flp.created_date', databaseLabel: 'ProcessConfigListSearchDropdown' },
    // { databaseColumnName: 'flp.userName', databaseLabel: 'Created By' },
  ]; //field name should be same as defined in FlpProcessConfiguration model

  constructor(
    private router: Router,
    private configurationService: ConfigurationService,
    private fileUploadService: FileUploadStatusService,
    private dashboardService: DashboardService
  ) { }
  ngOnInit(): void {
    this.loggedInUser = sessionStorage.getItem('upn');
    this.GetProcessedFileList();
  }

  clearDateFilter() {
    if (this.searchDate) {
      this.searchDate = '';
      //this.GetProcessConfigList();
    }
  }

  onDropdownChange(event: ProcessConfigListSearchDropdown[]) {
    this.selectedSearchField = event.map((col) => col.databaseColumnName);
    this.selectedSearchValue = this.selectedSearchField.toString();
    this.disableInput = this.selectedSearchField.length > 0 ? false : true;
  }

  clearSearch() {
    if (this.searchTerm) {
      this.searchTerm = '';
      //this.GetProcessConfigList();
    }
  }

  GetSearchedProcessedFileList() {
    this.page = 0;
    this.first = 0;
    this.rows = 20;
    this.GetProcessedFileList();
  }

  GetProcessedFileList() {
    try {
      let pageNo = this.page + 1; // == 0 ? this.page + 1 : this.page;
      let fromDate = this.searchDate
        ? this.searchDate[0]
          ? this.formatDate(this.searchDate[0])
          : null
        : null;

      let toDate = this.searchDate
        ? this.searchDate[1]
          ? this.formatDate(this.searchDate[1])
          : null
        : null;
      let data: ProcessedFileRequest = {
        createdBy: this.loggedInUser,
        pageSize: this.rows,
        pageNumber: pageNo,
        searchValue: this.searchTerm
          ? this.searchTerm.toLocaleLowerCase().trim()
          : null,
        totalCount: this.totalRecords,
        fromDate: fromDate,
        toDate: toDate,
        searchOnColumn: this.searchTerm ? this.selectedSearchValue : null,
      };
      this.loading = true;

      this.fileUploadService.getAllProcessedFile(data).subscribe({
        next: (response: APIResponse<FlpProcessedFileListResponse>) => {
          if (response) {
            if (response?.responseCode === 200) {
              this.processedFileList = response.result?.response;
              this.totalRecords = response.result?.totalCount;
            } else {
              this.apiErrorMessage = response.responseMessage[0];
            }
          } else {
            this.apiErrorMessage = 'Failed to get data!';
          }
          this.loading = false;
        },
        error(err) {
          console.log(err?.message);
        },
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occurred while fetching the data!';
    } finally {
      //this.loading = false;
    }
  }
  showDetails: boolean = false;
  viewDetails(configId: string, fileId: string,tabName : string=null) {

    if (this.fileConfigurationId === configId && this.uploadFileId === fileId) {
      this.fileConfigurationId = '';
      this.uploadFileId = '';
      this.tabName = null;
    } else {
      this.fileConfigurationId = configId;
      this.uploadFileId = fileId;
      this.tabName = tabName;
    }
    // if(document.getElementById(`tr_${configId}_${fileId}`)){
    //   document.getElementById(`tr_${configId}_${fileId}`).hidden = true;
    // }
  }

  resetData() {
    try {
      this.loading = true;
      this.first = 0;
      this.searchDate = null;
      this.searchTerm = null;
      this.selectedSearchField = [];
      this.selectedSearchValue = null;
      this.selectedNgModelValue = [];
      this.disableInput = true;

      let data: ProcessedFileRequest = {
        createdBy: this.loggedInUser,
        pageSize: this.rows,
        pageNumber: 1,
        searchValue: this.searchTerm
          ? this.searchTerm.toLocaleLowerCase().trim()
          : null,
        totalCount: this.totalRecords,
        fromDate: null,
        toDate: null,
        searchOnColumn: this.searchTerm ? this.selectedSearchValue : null,
      };

      this.fileUploadService.getAllProcessedFile(data).subscribe({
        next: (response: APIResponse<FlpProcessedFileListResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.processedFileList = response.result.response;
              this.totalRecords = response.result.totalCount;
            } else {
              this.apiErrorMessage = response.responseMessage[0];
            }
          } else {
            this.apiErrorMessage = 'Failed to get data!';
          }
        },
        error(err) {
          console.log(err?.message);
        },
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occured while fetching the data!';
    } finally {
      this.loading = false;
    }
  }

  onPageChange(event: any) {
    this.page = event.page;
    this.first = event.first;
    this.rows = event.rows;
    this.pageLinks = event.pageCount;
    this.viewLogClose();
    try {
      let pageNo = this.page + 1; //== 0 ? this.page + 1 : this.page;
      let fromDate = this.searchDate
        ? this.searchDate[0]
          ? this.formatDate(this.searchDate[0])
          : null
        : null;
      let toDate = this.searchDate
        ? this.searchDate[1]
          ? this.formatDate(this.searchDate[1])
          : null
        : null;
      let data: ProcessedFileRequest = {
        createdBy: this.loggedInUser ? this.loggedInUser : null,
        pageSize: this.rows,
        pageNumber: pageNo,
        searchValue: this.searchTerm
          ? this.searchTerm.toLocaleLowerCase().trim()
          : null,
        totalCount: 0,
        fromDate: fromDate,
        toDate: toDate,
        searchOnColumn: this.searchTerm ? this.selectedSearchValue : null,
        // creationDate: this.searchDate ? this.formatDate(this.searchDate) : null,
      };
      this.loading = true;
      this.fileUploadService.getAllProcessedFile(data).subscribe({
        next: (response: APIResponse<FlpProcessedFileListResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.processedFileList = response.result.response;
              this.totalRecords = response.result.totalCount;
              console.log(this.processedFileList);
            } else {
              this.apiErrorMessage = response.responseMessage[0];
            }
          } else {
            this.apiErrorMessage = 'Failed to get data!';
          }

          document.getElementById('topOfTable').focus();
        },
        error(err) {
          console.log(err?.message);
        },
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occured while fetching the data!';
    } finally {
      this.loading = false;
    }
  }

  formatDate(dateInput: string) {
    let date = new Date(dateInput);

    let dd = date.getDate();
    let mm = date.getMonth() + 1;
    let yyyy = date.getFullYear();

    return yyyy + '-' + mm + '-' + dd;
  }

  viewLog = false;

  viewLogOpen(configId: string, fileId: string, fileName: string,tabName : string=null) {
    this.viewLogClose()
    this.viewLog = true;
    this.logFileConfigurationId = configId;
    this.logUploadFileId = fileId;
    this.logFileName = fileName;
    this.tabName = tabName;
  }
  viewLogClose() {
    this.viewLog = false;
    this.logFileConfigurationId = '';
    this.logUploadFileId = '';
    this.logFileName = '';
    this.tabName = null;
  }

  getFileExtension(fileName: string): string {
    return this.dashboardService.getFileExtension(fileName) + '.png';
  }
}
