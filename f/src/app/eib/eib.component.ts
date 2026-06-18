import { Component, OnDestroy, OnInit } from '@angular/core';
import { formatDate } from '../core/utils/helper';
import { EBIListRequest } from '../core/models/ebi';
import { ProcessConfigListSearchDropdown } from '../core/models/processConfigurationlist';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CreateEibComponent } from './create-eib/create-eib.component';
import { Router } from '@angular/router';
import { state } from '@angular/animations';
import { EibService } from '../core/services/eib/eib.service';
import { DBView, EIB, EIBConfigurationDetails, EIBGenerationStatus, EIBListRequest, EIBListResponse, MappingDetails } from '../core/models/EIB/dbView';
import { APIResponse } from '../core/models/apiResponse';
import { StatusService } from '../core/services/status.service';
import { ToastrService } from 'ngx-toastr';
import { EIBQueueStatus } from '../shared/enum';
import { Subject, switchMap } from 'rxjs';

@Component({
    selector: 'app-eib',
    templateUrl: './eib.component.html',
    styleUrl: './eib.component.css',
    standalone: false
})
export class EibComponent implements OnInit, OnDestroy {
  createEIB = false;
  isEditing: boolean = false;
  viewLog: boolean = false;
  EIB : EIB;
  public first: number = 0;
  public rows: number = 10;
  public page: number = 0;
  public pageLinks: number;
  public searchDate: Date[] | null = null;
  public loggedInUser: string | null = null;
  public searchTerm: string | null = null;
  public totalRecords: number = 0;
  public selectedSearchValue: string | null = null;
  public searchLoading: boolean = false;
  public apiErrorMessage: string = '';
  public selectedSearchOnFields: string | null = null;
  public disableSearchInput: boolean = true;
  private dateChange$ = new Subject<{ monday: string; sunday: string }>();

  //todo: 
  public searchOnFields: ProcessConfigListSearchDropdown[] = [
    { databaseColumnName: 'c.eibname', databaseLabel: 'EIB Name' },
    { databaseColumnName: 'c.[description]', databaseLabel: 'Description' },
    { databaseColumnName: 'c.createdBy', databaseLabel: 'Created By' },
    // { databaseColumnName: 'q.[status]', databaseLabel: 'Status' }
  ];

  EIBs: EIB[] = [];
  isEIBLoading: boolean = false;
  QueueStatusNames = EIBQueueStatus;
  monday: any;
  sunday: any;

  statusIconMap = {
    [this.QueueStatusNames.Queued]: 'pending',
    [this.QueueStatusNames.Generating]: 'library-books',
    [this.QueueStatusNames.Done]: 'done_all',
    [this.QueueStatusNames.Error]: 'error'
  };

  statusIconClassMap = {
    [this.QueueStatusNames.Queued]: 'text-primary',
    [this.QueueStatusNames.Generating]: 'library-books',
    [this.QueueStatusNames.Done]: 'text-success',
    [this.QueueStatusNames.Error]: 'text-danger'
  };


  constructor(
    private router: Router,
    private eibService: EibService,
    private statusService: StatusService,
    private toastr: ToastrService
  ) {
  }

  async ngOnInit(): Promise<void> {
    this.loggedInUser = sessionStorage.getItem('upn');
    this.statusService.startConnection();
    this.statusService.addStatusListener('ReceiveGenerateEIBStatus', (statuses: EIBGenerationStatus[]) => {
      //console.log(status);
      if (this.EIBs.length > 0) {
        statuses.forEach(status => {
          var eib = this.EIBs.find(e => e.eibId === status.eibId);
          if (eib) {
            eib.status = +status.status;
            eib.hasActiveFileURL = status?.hasActiveFileURL;
            eib.errorMessage = status?.errorMessage;
            eib.generationStartDateTime = status?.generationStartDateTime
            
            
          }
        });

      }
    });


    //default - we will get the current week


    const today = new Date();

    // Get the current day of the week (0 = Sunday, 1 = Monday, ..., 6 = Saturday)
    const dayOfWeek = today.getDay()

    // Calculate how many days to subtract to get Monday
    const diffToMonday = dayOfWeek === 0 ? -6 : 1 - dayOfWeek;
    this.monday = new Date(today);
    this.monday.setDate(today.getDate() + diffToMonday);

    // Calculate how many days to add to get Sunday
    const diffToSunday = dayOfWeek === 0 ? 0 : 7 - dayOfWeek;
    this.sunday = new Date(today);
    this.sunday.setDate(today.getDate() + diffToSunday);

    this.dateChange$
      .pipe(
        switchMap(({ monday, sunday }) =>
          this.eibService.getEIBGenerationStatus(monday, sunday)
        )
      )
      .subscribe(response => {
        console.log('latest response:', response);
      })

    this.getAllEIBs();

    // this.eibService.getEIBGenerationStatus(formatDate(String(this.monday)), formatDate(String(this.sunday))).subscribe({
    //   next: (res: any) => {
    //     //console.log(res);
    //   },
    //   error: (error) => {

    //   }
    // });
  }

  async ngOnDestroy(): Promise<void> {
    await this.statusService.stopConnection();
    this.statusService.removeStatusListener('ReceiveGenerateEIBStatus');
    console.log('SignalR connection Stopped');
  }

  getAllEIBs() {

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
      let data: EIBListRequest = {
        createdBy: this.loggedInUser,
        pageSize: this.rows,
        pageNumber: pageNo,
        searchValue: this.searchTerm
          ? this.searchTerm.toLocaleLowerCase().trim()
          : null,
        totalCount: this.totalRecords,
        fromDate: fromDate,
        toDate: toDate,
        searchOnColumn: this.searchTerm ? this.selectedSearchValue : null
      };
      this.isEIBLoading = true;
      this.dateChange$.next({ monday: fromDate ?? formatDate(this.monday), sunday: toDate ?? formatDate(this.sunday) });
      // this.eibService.getEIBGenerationStatus(fromDate ?? formatDate(String(this.monday)), toDate ?? formatDate(String(this.sunday))).subscribe({
      //   next: (res: any) => {
      //     //console.log(res);
      //   },
      //   error: (error) => {

      //   }
      // });

      this.eibService.GetAllEIBs(data).subscribe({
        next: (response: APIResponse<EIBListResponse>) => {
          if (response?.responseCode === 200 && response.result) {

            this.EIBs = response.result?.response;
            this.totalRecords = response.result?.totalCount;

          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            // Optionally show a message to the user or handle fallback logic here
          }
          this.isEIBLoading = false;

        },
        error: error => {
          console.error("Error retrieving EIBs");
          this.isEIBLoading = false;
        }
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occurred while fetching the data!';
    } finally {
      //this.loading = false;
    }

  }

  onEIBListChange(event: any) {

  }

  clearDateFilter() {
    if (this.searchDate) {
      this.searchDate = [];
    }
  }

  onPageChange(event: any) {
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
      let data: EBIListRequest = {
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
        isActive: true
      };
      this.isEIBLoading = true;

      this.eibService.GetAllEIBs(data).subscribe({
        next: (response: APIResponse<EIBListResponse>) => {
          if (response?.responseCode === 200 && response.result) {

            this.EIBs = response.result?.response;
            this.totalRecords = response.result?.totalCount;
          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            // Optionally show a message to the user or handle fallback logic here
          }
          this.isEIBLoading = false;

        },
        error: error => {
          console.error("Error retrieving EIBs");
          this.isEIBLoading = false;
        }
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occured while fetching the data!';
    } finally {

    }
  }

  onDropdownChange(event: ProcessConfigListSearchDropdown) {
    this.selectedSearchOnFields = event.databaseColumnName;
    this.selectedSearchValue = this.selectedSearchOnFields.toString();
    this.disableSearchInput = this.selectedSearchOnFields.length > 0 ? false : true;
  }

  clearSearch() {
    if (this.searchTerm) {
      this.searchTerm = '';
    }
  }

  expandedTd: string | null = null;
  toggleText(eibId: string) {
    this.expandedTd = this.expandedTd === eibId ? null : eibId;
  }

  isEIBSentForQueue: string = '';
  onGenerateEIB(eib: EIB) {
    
    this.isEIBSentForQueue = eib.eibId;
    this.eibService.generateEIB(eib.eibId).subscribe({
      next: (response: APIResponse<boolean>) => {
        if (response?.responseCode === 200) {
          if (response.result) {
            this.toastr.success(`${eib.eibName} has been enqueued.`);
            this.isEIBSentForQueue = '';
          } else {
            this.toastr.error(`${eib.eibName} unable to enqueue. Something went wrong. Please contact web admin.`);
            this.isEIBSentForQueue = '';
          }
        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          this.isEIBSentForQueue = '';
          // Optionally show a message to the user or handle fallback logic here
        }
      }
    });
  }


  getEIBStatusName(statusId: number): string {
    const statusMap: { [key: number]: string } = {
      [EIBQueueStatus.Done]: 'Done',
      [EIBQueueStatus.Error]: 'Error',
      [EIBQueueStatus.Generating]: 'Generating',
      [EIBQueueStatus.Queued]: 'Queued'
    };

    return statusMap[statusId] || '';
  }
  resetALLEIB() {
    this.page = 0;
    this.first = 0;
    this.rows = 10;
    this.searchDate = null;
    this.searchTerm = null;
    //this.selectedSearchField = '';
    this.selectedSearchValue = null;
    this.selectedSearchOnFields = null;
    this.disableSearchInput = true;
    this.totalRecords = 0;
    // const checkboxDeleteAll = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
    // checkboxDeleteAll.checked = false;    
    this.getAllEIBs();
  }

  onEditEIB(eib: EIB) {
    this.router.navigate(['/create-eib'], {
      queryParams: { eibid: eib.eibId },
      state: { fromCreateEIB: true }
    });
  }

  sidePanelOpen() {
    //this.createEIB = true;
    //this.currentRuleSetNameId = '';
    // const modalRef = this.modalService.open(CreateEibComponent, { backdrop : 'static', keyboard : false});
    this.router.navigate(['/create-eib'], { state: { fromCreateEIB: true } });
  }


  onDownload(eib: EIB) {
    if (!eib) return;
    this.EIB = eib;
    this.viewLog = true;

    // Create a hidden download link
    // const downloadLink = document.createElement('a');
    // downloadLink.href = eib.fileUrl;
    // //downloadLink.download = 'example.txt'; // Set desired file name
    // document.body.appendChild(downloadLink);
    // downloadLink.click();

  //    const sasUrl =  eib.fileUrl; // your SAS URL
  //     this.eibService.downloadFile(sasUrl).subscribe(response => {
  //    const pdfUrl = URL.createObjectURL(response);
  //       const link = document.createElement('a');
  //       link.href = pdfUrl;
  //       const fileName = this.getFileNameFromUrl(sasUrl);
  //       link.download = fileName; // Set the desired file name
  //       document.body.appendChild(link);
  //       link.click();
  //       document.body.removeChild(link);
  //       URL.revokeObjectURL(pdfUrl);
  //   // const blob = new Blob([response.body!], {
  //   //   type: response.headers.get('Content-Type') || 'application/octet-stream'
  //   // });

  //   // const fileName = this.getFileName(response);

  //   // const link = document.createElement('a');
  //   // link.href = window.URL.createObjectURL(blob);
  //   // link.download = fileName;
  //   // link.click();
  // });

  }

  getFileNameFromUrl(url: string): string {
  const path = url.split('?')[0]; // Remove the query string
  return path.substring(path.lastIndexOf('/') + 1); // Extract the filename
}


    
  download() {
    
  }

  viewEIBDetails: string = '';
  eibConfiguration: EIBConfigurationDetails;
  totalNumberOfBusinessProcess: number = 0;
  mapping: MappingDetails[] = [];
  mappingDetails: string = '';
  country: string = '';
  viewDetails(eibId: string) {
    if (this.viewEIBDetails === eibId) {
      this.viewEIBDetails = '';
      return;
    }
    this.viewEIBDetails = eibId;
    this.country = '';
    //lets get eibdetails    
    this.eibService.GetEIBByEIBId(eibId).subscribe({
      next: (response: APIResponse<EIBConfigurationDetails>) => {
        if (response?.responseCode === 200 && response.result) {
          this.eibConfiguration = response.result;
          this.totalNumberOfBusinessProcess = this.eibConfiguration.businessProcessNames.length;
          this.country = this.eibConfiguration.countryName;
          const map = new Map<string, {id : number, processName: string; dbView: DBView[], isDeleted : boolean }>();
          for (const [i,item] of this.eibConfiguration.businessProcessDBViewMapping.entries()) {
            const dbViewEntry = {
              dbViewId: item.bpnViewId,
              viewId: item.viewNameId,
              columnCount: item.columnCount,
              fromColumn: item.fromColumn,
              toColumn: item.toColumn,
              viewName: item.viewName,
              businessProcessNameId: item.businessProcessNameId

            }

            if (map.has(item.businessProcessName)) {
              map.get(item.businessProcessName)!.dbView.push(dbViewEntry);
            } else {
              map.set(item.businessProcessName,
                {
                  id : map.size,
                  processName: item.businessProcessName,
                  dbView: [dbViewEntry],
                  isDeleted : false
                });
            }
          }
          this.mapping = Array.from(map.values());

          const details = this.mapping.flatMap(m => m.dbView || [])
            .map((view, index) => {
              const bpname = this.eibConfiguration.businessProcessNames.find(bp => bp.businessProcessNameId === view.businessProcessNameId).businessProcessName;
              const name = view.viewName || 'Unnamed View';
              const from = view.fromColumn || '';
              const to = view.toColumn || '';
              return `${index + 1}.) ${bpname} > ${name} (${from}-${to})`;
            })
          this.mappingDetails = details.join(', ')
          // this.mappingDetails = this.mapping[0].dbView?.map(view => {
          //   const name = view.viewName || 'Unnamed View';
          //   const from = view.fromColumn || '';
          //   const to = view.toColumn || '';
          //   return `${name} (${from}-${to})`;
          // }).join(', ') || '';

        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }
      }
    });
  }

  viewLogClose(){
    this.viewLog = false;
  }
}
