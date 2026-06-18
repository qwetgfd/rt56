import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { EibService } from '../core/services/eib/eib.service';
import { APIResponse } from '../core/models/apiResponse';
import { ProfilingRunLog, ProfilingSPNames } from '../core/models/EIB/dbView';
import { StatusService } from '../core/services/status.service';
import { animate, group, query, stagger, state, style, transition, trigger } from '@angular/animations';
import { distinctUntilChanged, map, merge, Subscription, timer } from 'rxjs';



@Component({
  selector: 'app-profiling',
  standalone: false,
  templateUrl: './profiling.component.html',
  styleUrl: './profiling.component.css',
  //changeDetection : ChangeDetectionStrategy.OnPush,
  // component.ts


})

export class ProfilingComponent implements OnInit, OnDestroy {
  /**
   *
   */
  constructor(
    private fb: FormBuilder,
    private toastr: ToastrService,
    private route: ActivatedRoute,
    private router: Router,
    private eibService: EibService,
    private statusService: StatusService,
    private cdr: ChangeDetectorRef
  ) { }

  profilingFormGroup: FormGroup = new FormGroup({});
  profilingSPNames: ProfilingSPNames[] = [];
  private username = sessionStorage.getItem('username');
  status: ProfilingRunLog[] = [];
  readonly signalRSendPDMProflingSatusBySPId = 'SendPDMProflingSatusBySPId';
  viewLog: boolean = false;
  ngOnInit(): void {
    this.SPLatestStatusLoading = true;
    //this.statusService.startConnection();
    //this.startListeningForLogs();
    this.initializeForm();
    this.getAllProfilingSPNames();
    

  }
  oldStatuses: ProfilingRunLog[] = [];
  lastSeendId: number = 0;
  batchToggle = 0; // This will flip 0 -> 1 -> 0
  firstRowFlag: boolean = false;
  rowsVersion = 0;

  rotateIndex = 0; // controls the starting color
  greenPalette = [
    '#63eaf7ff', '#5bebf8ff', '#52eaf8ff', '#48e8f7ff', '#3ee9f8ff',
    '#a2f3f2', '#88f1f1ff', '#7df0eeff', '#80f1f5ff', '#6ce8f3ff'
  ];

  // startListeningForLogs() {
  //   this.statusService.addStatusListener(
  //     this.signalRSendPDMProflingSatusBySPId,
  //     (statuses: ProfilingRunLog[]) => {
  //       //if (!statuses || statuses.length === 0) return;

  //       const last = statuses.at(-1)!;
  //       this.spNameRunningStatus = last.runningStatus?.trim().toLowerCase();



  //       if (this.spNameRunningStatus === 'ended' || this.spNameRunningStatus === 'failed') {
  //         this.statusService.stopConnection();
  //         this.onSubmitLoading = false;
  //       } else {
  //         this.batchToggle = this.batchToggle === 0 ? 1 : 0;
  //         this.firstRowFlag = this.batchToggle === 1;
  //         // Always map colors consistently
  //         this.status = statuses; //this.assignStableColors(statuses);
  //       }
  //     }
  //   );
  // }
  startWithGreen = true;

  private readonly MAX_ROWS = 10; //limit table size

  private getKey = (s: ProfilingRunLog) =>
    s.id ?? `${s.insertedAt ?? ''}|${s.id ?? ''}|${s.insertedAt ?? ''} ?? ''`;
  private _listening = false;
  private _stopped = false;

  startListeningForLogs() {
    if (this._listening) return;

    this._listening = true;
    this._stopped = false;

    this.statusService.addStatusListener(this.signalRSendPDMProflingSatusBySPId, (incoming: ProfilingRunLog) => {
      if (!incoming) return;

      //1) check terminal
      const lastStatus = incoming[0].runningStatus?.trim().toLowerCase();
      this.spNameRunningStatus = lastStatus;
      if (lastStatus === 'ended' || lastStatus === 'failed') {
        if (!this._stopped) {
          this._stopped = true;
          try {
            this.statusService.stopConnection();
          } finally {
            this.onSubmitLoading = false;
          }
        }
        return;
      }

      // 2) deduplicate
      const k = this.getKey(incoming[0]);
      const existing = this.status ?? [];
      if (existing.length && this.getKey(existing[0]) === k) {
        //already at the top; no -re-render needed.
        return;
      }
      const filtered = existing.filter(r => this.getKey(r) !== k);

      // 3) prepend new row - newest is always row 1
      let merged = [incoming[0], ...filtered];

      //4) optinal trim
      if (merged.length > this.MAX_ROWS) merged.length = this.MAX_ROWS;

      this.batchToggle = this.batchToggle === 0 ? 1 : 0;
      this.firstRowFlag = this.batchToggle === 1;
      this.status = merged;
      this.rowsVersion++;

      // this.spNameRunningStatus = statuses.at(-1)?.runningStatus?.trim().toLowerCase();
      // const batch : ProfilingRunLog[] = Array.isArray(statuses) ? statuses : [statuses];
      // if(batch.length === 0) return;

      // //console.log(runningStatus);
      // if (this.spNameRunningStatus === 'ended' || this.spNameRunningStatus === 'failed') {
      //   this.statusService.stopConnection();
      //   this.onSubmitLoading = false;
      //   return;
      // }
      // else {
      //   const existing = this.status ?? [];
      //   const existingKeySet = new Set(existing.map(this.getKey));

      //   const newOnes = batch.filter(s=> {
      //     const k = this.getKey(s);
      //     if(existingKeySet.has(k)) return false;
      //     existingKeySet.add(k);
      //     return true;
      //   });

      //   if(newOnes.length === 0){
      //     return;
      //   }

      //   const prependOrdered = [...newOnes].reverse();

      //   const merged = [
      //     ...prependOrdered,
      //     ...existing
      //   ];

      //   if(this.MAX_ROWS && merged.length > this.MAX_ROWS){
      //     merged.length = this.MAX_ROWS;
      //   }


      //   this.batchToggle = this.batchToggle === 0 ? 1 : 0;
      //   this.firstRowFlag = this.batchToggle === 1;

      //   // Map backend rows -> view model with a randomized color per row
      //   const vm: ProfilingRunLog[] = statuses.map((s, i) => ({
      //     ...s,
      //     color: this.greenPalette[(this.rotateIndex + i) % this.greenPalette.length]
      //   }));

      //   this.startWithGreen = !this.startWithGreen


      //   this.status = vm;
      //   this.rowsVersion++;


      // }
    });
  }

  trackById(index: number, item: ProfilingRunLog) {
    return item.id ?? item.runId ?? index;
  }

  async ngOnDestroy(): Promise<void> {
    await this.statusService.stopConnection();
    //removed this, cause stopConnection will remove all statusListener
    //this.statusService.removeStatusListener('SendPDMProflingSatusBySPId');
    console.log('SignalR connection Stopped');
  }

  initializeForm() {
    this.profilingFormGroup = this.fb.group({
      SPName: [null, Validators.required],
      description: [''],
      createdBy: [''],
      dateCreated: ['']
    });
  }

  profilingSPNameLoading: boolean = false;
  getAllProfilingSPNames() {
    this.profilingSPNameLoading = true;
    this.eibService.GetAllProfilingSPNames().subscribe({
      next: (response: APIResponse<ProfilingSPNames[]>) => {
        if (response?.responseCode === 200 && response.result) {
          this.profilingSPNameLoading = false;
          this.profilingSPNames = response.result;
        }
        else {
          this.profilingSPNameLoading = false;
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }

      },
      error: error => {
        this.profilingSPNameLoading = false;
        console.error("Error retrieving Profiling SP Names");
      }
    });
  }
  spNameRunningStatus: string = '';
  enableExecuteButton() {
    const form = this.profilingFormGroup
    const val = form?.get('SPName')?.value;
    //if no spname return true;
    //if running status is running return true
    //if (!val && (this.spNameRunningStatus !== 'running')) return true;
    if (val) {
      if (this.spNameRunningStatus === 'running' || this.SPLatestStatusLoading)
        return true;  //disable
      return false; //dont disable
    } else {
      return true; //disable
    }
  }

  SPLatestStatusLoading: boolean = false;
  spNameDetails: ProfilingSPNames;
  showRunningLogs: boolean = false;
  onSPNameChange(evt: any) {
    this.profilingFormGroup.get('description').setValue('');
    this.spNameRunningStatus = '';
    if (this.profilingSPNames && evt) {
      var desc = this.profilingSPNames.find(x => x.id === evt.id);
      this.profilingFormGroup.get('description').setValue(desc.description);
      this.profilingFormGroup.get('createdBy').setValue(desc.createdBy);
      this.profilingFormGroup.get('dateCreated').setValue(desc.insertedAt);
      //get the status of the sp
      //disable execute if status is running
      //todo:
      this.SPLatestStatusLoading = true;
      
      this.eibService.GetCurrentStatusOfPDMSP(desc.id).subscribe({
        next: async (response: APIResponse<ProfilingSPNames>) => {
          if (response?.responseCode === 200 && response.result) {
            this.spNameDetails = response.result;
            this.spNameRunningStatus = this.spNameDetails.latestStatus.toLowerCase();
            this.SPLatestStatusLoading = false;
            if (this.spNameRunningStatus === 'running') {
              await this.statusService.startConnection();

              // Wait for connectionId to exist
              await this.statusService.waitUntilConnected();

              this.startListeningForLogs();
              //setTimeout to wait for the signalR to finish first
              setTimeout(() => {
                //this.eibService.SendPDMProflingSatusBySPId(response.result.id, response.result.runId, this.lastSeendId).subscribe();
                this.startProfiling(response.result.id, response.result.runId);
                this.showRunningLogs = true;
              }, 500);

            } else {
              //cancel the signalr
              this.statusService.stopConnection();
            }
            
          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            // Optionally show a message to the user or handle fallback logic here
            this.SPLatestStatusLoading = false;
            
          }
        }, error: error => {
          this.SPLatestStatusLoading = false;
          
          console.error("Error retrieving Profiling SP Configuration");
        }
      });
    } else {
      this.onClearForm();
    }

  }

  onClearForm() {

    this.spNameRunningStatus = '';
    this.showRunningLogs = false;
    this.profilingFormGroup.get('SPName').setValue(null);
    this.profilingFormGroup.get('description').setValue('');
    this.profilingFormGroup.get('createdBy').setValue('');
    this.profilingFormGroup.get('dateCreated').setValue('');
    this.status = [];
    //cancel the signalr
    this.statusService.stopConnection();
    this.stopProfiling();

  }

  onSubmitLoading: boolean = false;
  async onSubmit() {
    this.onSubmitLoading = true;
    this.showRunningLogs = false;
    this.status = [];
    var procedureNameId = this.profilingFormGroup.get('SPName').value;
    this.eibService.RegisterPDMRProfilingSPRun(procedureNameId, this.username).subscribe({
      next: async (response: APIResponse<string>) => {
        if (response?.responseCode === 200 && response.result) {
          //console.log(response.result);
          //disable execute
          this.spNameRunningStatus = 'running';
          await this.statusService.startConnection();

          // Wait for connectionId to exist
          await this.statusService.waitUntilConnected();

          this.startListeningForLogs();
          setTimeout(() => {
            //this.eibService.SendPDMProflingSatusBySPId(procedureNameId, response.result, this.lastSeendId).subscribe();
            this.startProfiling(procedureNameId, response.result);
            this.showRunningLogs = true;
          }, 500);
        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }
        this.onSubmitLoading = false;
      }, error: err => {
        this.showRunningLogs = false;
        this.toastr.error('Something went wrong. Please contact administrator.');
        console.log(err);
        this.onSubmitLoading = false;
      }
    });
  }

  viewLogClose() {
    this.viewLog = false;
  }

  onShowSPRunHistory() {
    this.viewLog = true;
  }



  private profilingSubscription: Subscription | null = null;

  startProfiling(procedureNameId: number, runId: string) {

    const connectionId = this.statusService.getConnectionId();
    if (!connectionId) {
      this.toastr.error("SignalR Hub not ready yet");
      return;
    }

    this._listening = false;
    this.profilingSubscription = this.eibService
      .SendPDMProflingSatusBySPId(procedureNameId, runId, connectionId, this.lastSeendId)
      .subscribe();
  }

  stopProfiling() {
    if (this.profilingSubscription) {
      this.profilingSubscription.unsubscribe();
      this.profilingSubscription = null;
    }
  }
}
