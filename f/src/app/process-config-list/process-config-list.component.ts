import { Component, OnInit, ViewChild } from '@angular/core';
import { FlpProcessConfigurationService } from '../core/services/flp-configuration-list.service';
import {
  FlpProcessConfiguration,
  FlpProcessConfigurationResponse,
  ProcessConfigListSearchDropdown,
  ProcessConfigurationRequest,
  UpdateActiveStatusByFlpConfigurationIdRequest,
  UpdateActiveStatusByFlpConfigurationIdResponse,
} from '../core/models/processConfigurationlist';
import { APIResponse } from '../shared/models/apiResponse';
import { Router } from '@angular/router';
import { ProcessConfigService } from '../core/services/process-config.service';
import { FileConfigurationDetails } from '../core/models/fileProcessConfig';
import { ConfigurationService } from '../core/services/configuration.service';
import { config, finalize } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { DataSourceType, ToastrMessages } from '../shared/enum';
import { formatDate, Helper } from '../core/utils/helper';
import { NavigateService } from '../core/services/navigate.service';
import { FileNameExtension } from '../core/models/LandingLayer/landingLayer';
import { DateTimeFormats } from '../core/models/datatypeNames';
import { RegexItem } from '../core/models/additionalSettings';
// #region Sharepoint Workspace - AY
import { SP_INTEGRATION } from '../sharepoint/core/sharepoint.messages';
import { SHAREPOINT_PROCESS_TYPE_ID } from '../sharepoint/integration/configuration-first.sharepoint';
// #endregion

@Component({
  selector: 'app-process-config-list',
  templateUrl: './process-config-list.component.html',
  styleUrl: './process-config-list.component.css',
  standalone: false
})
export class ProcessConfigListComponent implements OnInit {
  [x: string]: any;
  readonly spListDetail = SP_INTEGRATION.listDetail;
  // #region Sharepoint Workspace - AY
  readonly sharePointProcessTypeId = SHAREPOINT_PROCESS_TYPE_ID;
  // #endregion
  @ViewChild('header') header!: HTMLElement;
  @ViewChild('tableScroll') tableScroll!: HTMLElement;
  public processConfigList: FlpProcessConfiguration[];
  public apiErrorMessage: string = '';
  public loading: boolean = false;
  //Table properties
  public first: number = 0;
  public rows: number = 20;
  public page: number = 0;
  fileConfigurationId: string = '';
  tabName: string = null;
  configurationDetails: FileConfigurationDetails;

  openDetails = false;
  public searchOnFields: ProcessConfigListSearchDropdown[] = [
    { databaseColumnName: 'flp.process_name', databaseLabel: 'Process Name' },
    { databaseColumnName: 'pt.processTypeName', databaseLabel: 'Process Type' },
    { databaseColumnName: 'flp.[region]', databaseLabel: 'Region' },
    { databaseColumnName: 'flp.[subRegion]', databaseLabel: 'Sub Region' },
    { databaseColumnName: 'flp.[clientName]', databaseLabel: 'Client Name' },
    { databaseColumnName: 'dbc.databaseName', databaseLabel: 'Database Name' },
    { databaseColumnName: 'ctm.tableName', databaseLabel: 'Table Name' },
    // { databaseColumnName: 'flp.created_date', databaseLabel: 'ProcessConfigListSearchDropdown' },
    { databaseColumnName: 'flp.userName', databaseLabel: 'Created By' },
    { databaseColumnName: 'fpst.serverName', databaseLabel: 'Ingestion Type' },
    { databaseColumnName: 'flp.[Description]', databaseLabel: 'Description' }
  ]; //field name should be same as defined in FlpProcessConfiguration model
  //Table property ends
  public totalRecords: number = 0;
  public pageLinks: number;
  public loggedInUser: string | null = null;
  public searchTerm: string | null = null;
  public searchDate: Date[] | null = null;
  public fromDate: string | null = null;
  public toDate: string | null = null;
  public selectedSearchField: string[] = [];
  public selectedNgModelValue: string[];
  public selectedSearchValue: string | null = null;
  public disableInput: boolean = true;
  showActiveProcesses: boolean = true;
  DataSourceTypes = DataSourceType;
  constructor(
    private processConfigService: FlpProcessConfigurationService,
    private router: Router,
    private configurationService: ConfigurationService,
    private toastrService: ToastrService,
    private navigateService: NavigateService,
    private helperUtil: Helper,
  ) { }

  ngOnInit(): void {
    // this.loggedInUser = sessionStorage.getItem('username');
    // this.selectedSearchValue.push(this.searchOnFields.find(x=> x.databaseColumnName === 'flp.process_name').databaseColumnName);
    // this.selectedSearchField.push(this.searchOnFields.find(x=> x.databaseColumnName === 'flp.process_name').databaseLabel);
    this.getFileExtensions();
    this.getAllDateTimeFormats();
    this.GetProcessConfigList();
    
  }

  modifySettings = false;

  modifySettingsOpen() {
    this.modifySettings = true;
  }
  modifySettingsClose() {
    this.modifySettings = false;
  }

  fileNameExtensions: FileNameExtension[] = [];
  getFileExtensions() {

    this.configurationService.getFileExtensionNames().subscribe({
      next: (response: APIResponse<FileNameExtension[]>) => {
        if (response) {
          if (response.responseCode === 200) {

            this.fileNameExtensions = response.result;
          }
        }
      },
      error: error => {
        console.log(error);

      }
    });
  }
  dateTimeNames: DateTimeFormats[] = [];
  dateOnlyFormats: DateTimeFormats[] = [];
  timeOnlyFormats: DateTimeFormats[] = [];
  getAllDateTimeFormats() {
    //var showDefault = this.configurationProcessType === DataSourceType.LandingLayer;
    this.configurationService.getAllDataTimeFormats(true).subscribe({
      next: (response: APIResponse<DateTimeFormats[] | null>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              this.dateTimeNames = response.result;
              this.dateOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'date');
              this.timeOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'time');
            }
          }
        }
      },
      error: (error) => {
        //this.toastr.error("Can't find all date time formats");
        console.log(`Unable to retrieve date time formats ${error}`);
      },
    });
  }

  GetSearchedProcessConfigList(btnType: string, isActive: boolean = true) {
    if (btnType === 'showActiveInactive') {
      this.searchTerm = '';
      this.searchDate = [];
      this.selectedNgModelValue = [];
    }
    this.page = 0;
    this.first = 0;
    this.rows = 20;
    this.showActiveProcesses = isActive;
    this.selectedProcessForDelete = [];
    const checkboxDeleteAll = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
    checkboxDeleteAll.checked = false;
    this.GetProcessConfigList(isActive);
  }

  GetProcessConfigList(isActive: boolean = true) {
    try {
      let pageNo = this.page + 1; // == 0 ? this.page + 1 : this.page;
      let fromDate = this.searchDate
        ? this.searchDate[0]
          ? formatDate(this.searchDate[0])
          : null
        : null;
      let toDate = this.searchDate
        ? this.searchDate[1]
          ? formatDate(this.searchDate[1])
          : null
        : null;
      let data: ProcessConfigurationRequest = {
        createdBy: this.loggedInUser ? this.loggedInUser : null,
        pageSize: this.rows,
        pageNumber: pageNo,
        searchValue: this.searchTerm
          ? this.searchTerm.toLocaleLowerCase().trim()
          : null,
        totalCount: this.totalRecords,
        fromDate: fromDate,
        toDate: toDate,
        searchOnColumn: this.searchTerm ? this.selectedSearchValue : null,
        isActive: isActive
        // creationDate: this.searchDate ? this.formatDate(this.searchDate) : null,
      };
      this.loading = true;
      this.processConfigService.getProcessConfigList(data).pipe(finalize(() => this.loading = false)).subscribe({
        next: (response: APIResponse<FlpProcessConfigurationResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.processConfigList = response.result.response;
              this.totalRecords = response.result.totalCount;
              //console.log(this.processConfigList);
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
      this.apiErrorMessage = 'Some error ocurred while fetching the data!';
    } finally {
      //this.loading = false;
    }
  }

  fileNameExtensionDisplay: string = '';
  landingLayerRegexDisplay: RegexItem[] = [];
  dateFormatDisplay: string = '';
  timeFormatDisplay : string = '';
  viewDetails(configId: string, tabName: string) {
    if (this.fileConfigurationId === configId) {
      this.fileConfigurationId = '';
      return;
    }
    this.fileConfigurationId = configId;
    this.tabName = tabName;
    this.configurationDetails = null;
    this.configurationService.getConfigurationDetailsV2(configId, tabName).subscribe({
      next: (response: APIResponse<FileConfigurationDetails>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.configurationDetails = response.result;
            const rawFileExtensions = this.configurationDetails.fileConfigurationDetails[0].landingLayerFileExtension.split(',').map(ext => Number(ext.trim()));
            this.fileNameExtensionDisplay = this.fileNameExtensions.filter(ext => rawFileExtensions.includes(ext.id)).map(ext => ext.fileExtension).join(', ');

            this.landingLayerRegexDisplay = this.helperUtil.toRegexArray(this.configurationDetails.fileConfigurationDetails[0].landingLayerRegex);
            this.dateFormatDisplay = this.dateTimeNames.find(format => format.formatId === Number(this.configurationDetails.fileConfigurationDetails[0].dateFormatId))?.format;
            this.timeFormatDisplay = this.dateTimeNames.find(format => format.formatId === Number(this.configurationDetails.fileConfigurationDetails[0].timeFormatId))?.format;
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
  }
  editDetails(configId: string, tabName: string) {
    if (tabName === null || tabName === '') {
      tabName = '';
    }
    //this.fileConfigurationId = configId;
    var processType = this.processConfigList.find(x => x.configurationId === configId);
    // //console.log(processTypeId);

    // // Validate that the numeric value is a valid enum member
    // const isValid = DataSourceType[processTypeId.ingestionTypeId as keyof typeof DataSourceType] !== undefined;
    // if (!isValid) {
    //   //throw new Error(`Invalid processTypeId=${processTypeId} for DataSourceType`);
    //   this.toastrService.error(ToastrMessages.SomethingWentWrong);
    //   return;
    // }


    this.navigateService.configurationProcess = +processType.ingestionTypeId as DataSourceType;
    this.router.navigate(['/process-configuration', configId, tabName]);

  }

  //Table Paginator events
  onPageChange(event) {
    //console.log(event);

    this.first = event.first;
    this.pageLinks = event.pageCount;
    //if user changed rows-per-page, reset to first page
    if (event.rows !== this.rows) {
      this.rows = event.rows;
      this.first = 0;
      this.page = 0;
    } else {
      this.first = event.first;
      this.page = event.page;
    }

    try {
      let pageNo = this.page + 1; //== 0 ? this.page + 1 : this.page;
      let fromDate = this.searchDate
        ? this.searchDate[0]
          ? formatDate(this.searchDate[0])
          : null
        : null;
      let toDate = this.searchDate
        ? this.searchDate[1]
          ? formatDate(this.searchDate[1])
          : null
        : null;
      let data: ProcessConfigurationRequest = {
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
        isActive: this.showActiveProcesses
        // creationDate: this.searchDate ? this.formatDate(this.searchDate) : null,
      };
      this.loading = true;
      this.processConfigService.getProcessConfigList(data).subscribe({
        next: (response: APIResponse<FlpProcessConfigurationResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.selectedProcessForDelete = [];
              const checkboxDeleteAll = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
              checkboxDeleteAll.checked = false;
              this.processConfigList = response.result.response;
              this.totalRecords = response.result.totalCount;
              //console.log(this.processConfigList);
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
  //

  clearSearch() {
    if (this.searchTerm) {
      this.searchTerm = '';
      this.GetProcessConfigList();
    }
  }

  clearDateFilter() {
    if (this.searchDate) {
      this.searchDate = [];
      this.GetProcessConfigList();
    }
  }

  onDropdownChange(event: ProcessConfigListSearchDropdown[]) {
    //console.log(event);
    if (event.length === 0) {
      this.disableInput = true;
      this.searchTerm = null;
    } else {
      this.selectedSearchField = event.map((col) => col.databaseColumnName);
      this.selectedSearchValue = this.selectedSearchField.toString();
      this.disableInput = this.selectedSearchField.length > 0 ? false : true;
    }
    // console.log(this.selectedSearchField);
    // console.log(this.selectedSearchValue);
    // console.log(this.disableInput);
    // let totalColumns = event.map(col => `${col.databaseColumnName} LIKE %${this.searchTerm}%`).join(' OR ');
    // console.log(totalColumns);
    // let sql: string = `AND ( ${totalColumns} );`;
    // console.log(sql)
  }

  resetData() {
    try {
      this.loading = true;
      this.first = 0;
      this.rows = 20;
      this.searchDate = null;
      this.searchTerm = null;
      this.selectedSearchField = [];
      this.selectedSearchValue = null;
      this.selectedNgModelValue = [];
      this.disableInput = true;
      this.selectedProcessForDelete = [];
      let data: ProcessConfigurationRequest = {
        createdBy: null,
        pageSize: this.rows,
        pageNumber: 1,
        searchValue: null,
        totalCount: this.totalRecords,
        fromDate: null,
        toDate: null,
        searchOnColumn: null,
        isActive: this.showActiveProcesses
      };
      //console.log(data);
      const elem1 = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
      elem1.checked = false;
      //this.showActiveProcesses = true;

      this.processConfigService.getProcessConfigList(data).subscribe({
        next: (response: APIResponse<FlpProcessConfigurationResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.processConfigList = response.result.response;
              this.totalRecords = response.result.totalCount;
              //console.log(this.processConfigList);
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

  OpenDetails() {
    this.openDetails = true;
  }
  OpenDetailsClose() {
    this.openDetails = false;
  }
  expandedTd: string | null = null;
  toggleText(flpConfigurationId: string) {

    this.expandedTd = this.expandedTd === flpConfigurationId ? null : flpConfigurationId;
  }
  selectedProcessForDelete: string[] = [];
  onDeleteSelected(configurationId: string) {
    if (!this.selectedProcessForDelete.some(x => x === configurationId)) {
      this.selectedProcessForDelete.push(configurationId);
    } else {
      this.selectedProcessForDelete.splice(this.selectedProcessForDelete.findIndex(x => x === configurationId), 1);
    }
  }

  onDeleteAll(event: any) {
    this.emptySelectedProcessForDelete(event.target.checked);
  }

  emptySelectedProcessForDelete(status: boolean) {
    this.selectedProcessForDelete = [];
    if (status) {
      this.processConfigList.forEach(i => {
        this.selectedProcessForDelete.push(i.configurationId);
        const elem = document.getElementById(`checkbox_${i.configurationId}`) as HTMLInputElement;
        if (elem) elem.checked = true;
      });
    } else {
      this.processConfigList.forEach(i => {
        const elem = document.getElementById(`checkbox_${i.configurationId}`) as HTMLInputElement;
        if (elem) elem.checked = false;
      });

      const checkboxDeleteAll = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
      checkboxDeleteAll.checked = false;
    }
  }

  UpdateActiveStatusByFlpConfigurationId(activeStatus: boolean) {
    let data: UpdateActiveStatusByFlpConfigurationIdRequest = {
      flpConfigurationIds: this.selectedProcessForDelete.join(','),
      activeStatus: activeStatus,
      created_by: sessionStorage.getItem('upn')?.split('@')[0],
      userName: sessionStorage.getItem('username')
    };
    this.processConfigService.UpdateActiveStatusByFlpConfigurationId(data).subscribe({
      next: (response: APIResponse<UpdateActiveStatusByFlpConfigurationIdResponse>) => {
        if (response) {
          if (response.responseCode === 200) {
            let msg = '';
            if (this.selectedProcessForDelete.length > 1) {
              msg = activeStatus ? 'Processes have been enabled successfully' : 'Processes have been disabled successfully';
            } else {
              msg = activeStatus ? 'Process has been enabled successfully' : 'Process has been disabled successfully';
            }
            this.toastrService.success(msg);

            this.resetData();
          }
        }
      }
    });
  }

}
