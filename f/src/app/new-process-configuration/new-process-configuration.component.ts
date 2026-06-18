declare var bootstrap: any;
import { Component, ElementRef, EventEmitter, Input, OnDestroy, OnInit, Output, ViewChild, } from '@angular/core';
import { AbstractControl, AsyncValidatorFn, FormArray, FormBuilder, FormControl, FormGroup, ValidationErrors, ValidatorFn, Validators, } from '@angular/forms';
import { debounceTime, finalize, lastValueFrom, map, Observable, Subject, Subscription, switchMap, take, } from 'rxjs';
import { FilePreviewValues, NewProcessSettings, RegexItem, } from '../core/models/additionalSettings';
import { APIResponse } from '../core/models/apiResponse';
import { ColumnNameDatatypeName, ColumnNameDatatypeNameForOfflineMode } from '../core/models/columnNameDatatypeName';
import { DIClientNames } from '../core/models/DIClientNames';
import { DIDatabaseNames } from '../core/models/DIDatabaseNames';
import { DIRegions } from '../core/models/DIRegions';
import { DISubRegions } from '../core/models/DISubRegions';
import { ConfigurationService } from '../core/services/configuration.service';
import { ModalServiceService } from '../core/services/modal-service.service';
import { ProcessConfigService } from '../core/services/process-config.service';
import {
  DataSourceType,
  FileType,
  ModalMessages,
  ModalTitles,
  PageNames,
  ToastrMessages,
} from '../shared/enum';
import { ProcessType } from '../shared/models/newProcess';

import { ActivatedRoute, Router } from '@angular/router';
import { MsalService } from '@azure/msal-angular';
import { NgbDate, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { ToastrService } from 'ngx-toastr';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { ExcelRule, PayLoad } from '../core/models/DataInsider';
import { DatatypeName, DateTimeFormats } from '../core/models/datatypeNames';
import {
  DdlData,
  RegionSubRegionClient,
  SubRegion,
} from '../core/models/dsRegion';
import { EnglishOnlyCharacters } from '../core/models/englistOnlyCharacters';
import {
  FileConfigurationDetails,
  FileProcessConfig,
  FrequencyHour,
  SchedulerType,
  ServerDetail,
  StorageAccount,
  WeekDayName,
} from '../core/models/fileProcessConfig';
import { FileNameExtension } from '../core/models/LandingLayer/landingLayer';
import { CampaignNames, SecurityGroup } from '../core/models/userDetails';
import { BusyService } from '../core/services/busy.service';
import { DataInsiderService } from '../core/services/data-insider.service';
import { DataSliceService } from '../core/services/dataslice.service';
import { cleanColumnName } from '../core/services/di-parser.service';
import { FabService } from '../core/services/FAB/fab-service.service';
import { GraphApiTokenService } from '../core/services/graph-api-token.service';
import { NavigateService } from '../core/services/navigate.service';
import { cleanBlobSourceLocation, cleanSourceLocation, Helper, noWhitespaceValidator, requiredArrayValidatorTemp } from '../core/utils/helper';
import { RegexBuilderComponent } from '../regex-builder/regex-builder.component';
import { FilePreviewComponent } from './file-preview/file-preview.component';
// #region Sharepoint Workspace - AY
import { ConfigurationFirstSharepointTabComponent } from './configuration-first-sharepoint-tab/configuration-first-sharepoint-tab.component';
import { ConfigurationFirstSharepointTabNavComponent } from './configuration-first-sharepoint-tab-nav/configuration-first-sharepoint-tab-nav.component';
import {
  activateSharePointSettingsTab,
  isSharePointProcessType,
  resolveLocationTypeId,
  SHAREPOINT_PROCESS_TYPE_ID,
  SHAREPOINT_SETTINGS_TAB_ID,
  sharePointFileProcessFields,
} from '../sharepoint/integration/configuration-first.sharepoint';
import { SP_INTEGRATION } from '../sharepoint/core/sharepoint.messages';
import { ProcessConfigSharePointSelection } from '../sharepoint/core/sharepoint.types';
// #endregion

@Component({
  selector: 'app-new-process-configuration',
  templateUrl: './new-process-configuration.component.html',
  styleUrl: './new-process-configuration.component.css',
  standalone: false
})
export class NewProcessConfigurationComponent implements OnInit, OnDestroy {
  @ViewChild('externalProjectSwitch') externalProjectSwitch!: ElementRef<HTMLInputElement>;
  [x: string]: any;
  processTypes: ProcessType[];
  processConfigurationForm: FormGroup;
  processNamePattern = '^_?([A-Za-z]_?)\\w*$';
  txtPattern: string = "^[a-zA-Z0-9 ,._'@]*$";
  @Input() formValues: NewProcessSettings;
  @Input() columnDatatype: ColumnNameDatatypeName[] = [];
  @Input() keyColumns: Observable<string>;
  private keyColumnsSubscription: Subscription;
  @Output() previewToParent = new EventEmitter<NewProcessSettings>();
  @Output() updateConfigOnly = new EventEmitter<NewProcessSettings>();
  @Output() closeWindow = new EventEmitter();
  @Output() processConfigurationFormHasError = new EventEmitter<boolean>();
  private formValuesEventSubscription: Subscription | undefined;
  @ViewChild('delimiter') delimiterInput: ElementRef | undefined;
  @ViewChild(FilePreviewComponent) filePreviewComponent!: FilePreviewComponent;
  @ViewChild('sharePointTab') sharePointTab?: ConfigurationFirstSharepointTabComponent;
  // #region Sharepoint Workspace - AY
  readonly sharePointProcessTypeId = SHAREPOINT_PROCESS_TYPE_ID;
  readonly sharePointSettingsTabId = SHAREPOINT_SETTINGS_TAB_ID;
  readonly isSharePointProcessType = isSharePointProcessType;
  sharePointInitialApplicationId: string | null = null;
  sharePointInitialApplicationSiteId: string | null = null;
  sharePointInitialLibraryName: string | null = null;
  sharePointInitialFolderPath: string | null = null;
  sharePointCachedSelection: ProcessConfigSharePointSelection | null = null;
  // #endregion
  pageName: PageNames;
  config: NewProcessSettings | undefined;
  schedulerTypes: SchedulerType[];
  databaseNames: DIDatabaseNames[] = [];
  DIRegions: DIRegions[] = [];
  DISubRegions: DISubRegions[] = [];
  DIClientnames: DIClientNames[] = [];
  datatypeNames: DatatypeName[] = [];
  dateTimeNames: DateTimeFormats[] = [];
  databaseNameId: number = 0;
  delimiter: string = ',';
  disableDelimiter = false;
  activeTab = 'client-tab';
  keyColumnsSelected: string = '';
  minDate: any;
  minToDate: any;
  maxDate: any;
  schedulerMinDate: Date;
  schedulerMaxDate: Date;
  schedulerEndMinDate: Date;
  hours: string[] = [];
  endHours: string[] = [];
  serverDetail: ServerDetail[];
  storageAccount: StorageAccount[];
  clientSettingsValuesValid = false;
  clientSettingsTabVisited = false;
  isSubmitted: boolean = false;
  tableName: string = '';
  paramsId: string = '';
  configurationDetails!: FileConfigurationDetails;
  weekDays: WeekDayName[];
  frequencyHours: FrequencyHour[];
  processName: string = '';
  updateSchedular: boolean = false;
  bsModalRef?: BsModalRef;
  filePreview = false;
  modifySettings = false;
  isReadonlyProcessDetails: boolean = false;
  isReadonlySchedular: boolean = false;
  selectedRegion: string = '';
  selectedSubRegion: string = '';
  //sample file preview variables
  recordHeaders: ColumnNameDatatypeNameForOfflineMode[] = [];
  fileName: string;
  filePreviewValues: FilePreviewValues;
  recordsArrayForDisplay: any[] = [];
  headersRow: string[] | undefined = [];
  file: any = null;
  regionSubRegion: RegionSubRegionClient[];
  dsRegion: DdlData[] = [];
  dsREgionTemp: DdlData[] = [];
  dsSubRegion: SubRegion[] = [];
  dsClient: DdlData[] = [];
  isRegionLoading: boolean = false;
  isSubRegionLoading: boolean = false;
  isClientLoading: boolean = false;
  isDatabaseNamesLoading: boolean = false;
  regionName: string;
  subRegionName: string;
  clientName: string = '';
  db_file_columnName_list: string = '';
  englishOnlyCharacters: EnglishOnlyCharacters[];
  configurationProcessType: number = 1;
  DataSourceTypes = DataSourceType;

  alphaNumericPattern = "^[a-zA-Z0-9]+$";
  onlyNumbersPattern = "^[0-9]+$";

  securityGroups: { id: string, displayName: string }[] = []; // To store fetched security groups
  selectedSecurityGroups: SecurityGroup[] = [];//  { id: string; name: string }[] = []; // Selected groups
  searchGroup$ = new Subject<string>(); // Observable for typeahead
  UserDefaultGroup: any = sessionStorage.getItem('UserDefaultGroup');
  userSelectedSecurityGroupIsAdded: boolean = false;
  tabName: string = null;
  nameOfParamChange: string;

  dateOnlyFormats: DateTimeFormats[] = [];
  timeOnlyFormats: DateTimeFormats[] = [];
  fileNameExtensions: FileNameExtension[] = [];
  regexList: RegexItem[] = [];
  editingRegexIndex: number | null = null;

  databaseDestinationSettingsError: string = '';

  constructor(
    private processService: ProcessConfigService,
    private fb: FormBuilder,
    private configService: ConfigurationService,
    private modalService: ModalServiceService,
    private router: Router,
    private toastr: ToastrService,
    private route: ActivatedRoute,
    private helperUtil: Helper,
    private dsService: DataSliceService,
    private navigateService: NavigateService,
    private msalService: MsalService,
    private tokenService: GraphApiTokenService,
    private busyService: BusyService,
    private confirmModalService: NgbModal,
    private dataInsiderService: DataInsiderService,
    private fabService: FabService
  ) {
    const today = new Date();
    this.schedulerMinDate = new Date(
      today.getUTCFullYear(),
      today.getUTCMonth(),
      today.getUTCDate()
    );
    this.schedulerMaxDate = new Date(
      today.getUTCFullYear() + 1,
      today.getUTCMonth(),
      today.getUTCDate()
    );
    this.schedulerEndMinDate = this.schedulerMinDate;
    this.maxDate = {
      year: today.getUTCFullYear() + 1,
      month: today.getUTCMonth() + 1,
      day: today.getUTCDate(),
    };
    this.minDate = {
      year: today.getUTCFullYear(),
      month: today.getUTCMonth() + 1,
      day: today.getUTCDate(),
    };
    this.minToDate = this.minDate;

    for (let hour = today.getUTCHours() + 1; hour < 24; hour++) {
      this.hours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
      this.endHours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
    }

    this.processConfigurationForm = this.fb.group({
      search_group: [''],
      isExternalProject: [false],
      campaignName: [{ value: null, disabled: true }],
    });
  }

  async ngOnInit(): Promise<void> {
    this.pageName = PageNames.Offline_Process;
    //debugger;
    this.configurationProcessType = this.navigateService.configurationProcess;
    // this.route.params.subscribe((params) => {
    //   this.paramsId = params['id'];
    // });
    // this.route.queryParamMap.subscribe(queryParams => {
    //   this.tabName = queryParams.get('tabName') || null;
    // });
    this.route.params.subscribe(params => {
      this.paramsId = params['id'];
      this.tabName = params['tabName'] || null;
    });


    const {
      storageAccounts,
      dateTimeFormats,
      fileExtensions,
      dataSliceConfig,
      processTypes,
      serverDetails,
      weekDays,
      schedulerTypes
    } = (this.route.snapshot.data['lookups'] ?? this.route.snapshot.data['offline']) as {
      storageAccounts: StorageAccount[],
      dateTimeFormats: DateTimeFormats[],
      fileExtensions: FileNameExtension[],
      dataSliceConfig: any,
      processTypes: ProcessType[],
      serverDetails: ServerDetail[],
      weekDays: WeekDayName[],
      schedulerTypes: SchedulerType[]
    };
    //debugger;
    this.storageAccount = storageAccounts.filter(acc => acc.configurationProcessType === this.configurationProcessType);
    this.blobStorageAccount = storageAccounts.filter(acc => acc.configurationProcessType === this.DataSourceTypes.Default);
    this.dateTimeNames = dateTimeFormats;
    this.dateOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'date');
    this.timeOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'time');
    this.fileNameExtensions = fileExtensions;
    this.processTypes = processTypes;
    this.serverDetail = serverDetails;
    this.weekDays = weekDays;
    this.schedulerTypes = schedulerTypes;


    //this.getStorageAccountDetails()
    this.activeTab = 'client-tab';
    //this.getDsConfiguration();
    this.initializeForm();

    //build weekday checkboxes
    if (this.weekDays) {
      this.weekDays.forEach(() =>
        this.getWeekDays.push(new FormControl(false))
      );
    }

    // if (this.configurationProcessType === DataSourceType.LandingLayer) {
    //this.getFileExtensions();
    // }
    //this.getAllDIRegions();
    //this.getProcessType();
    //this.getServerDetails();
    //this.getStorageAccountDetails();
    //await this.getWeekDayName();
    //this.getAllDISubRegions();
    //this.getAllDIClientnames();
    //this.getSchedulerType();

    this.getFrequencyHour();
    this.getDataColumns();
    //this.getAllDateTimeFormats();
    this.getAllEnglishCharactersOnly();

    // Production order (restore when deleting local block below):
    // if (this.paramsId == null || this.paramsId == '') this.getRegionBySecurityGroup();
    // this.processConfigurationForm.get('RegionId')?.disable();
    // this.processConfigurationForm.get('SubRegionId')?.disable();
    // this.processConfigurationForm.get('ClientId')?.disable();

    // Local environment to be deleted later -AY — guest/dev DataSlice resolves synchronously; disable before load
    this.processConfigurationForm.get('RegionId')?.disable();
    this.processConfigurationForm.get('SubRegionId')?.disable();
    this.processConfigurationForm.get('ClientId')?.disable();

    if (this.paramsId == null || this.paramsId == '')
      this.getRegionBySecurityGroup();
    //this.getRegion('116');
    //this.populateForm();
    // this.keyColumnsSubscription = this.keyColumns.subscribe(
    //   (val) => (this.keyColumnsSelected = val)
    // );
    this.processConfigurationForm
      .get('isSkipRow')
      ?.valueChanges.subscribe((checked: boolean) => {
        if (checked) {
          this.processConfigurationForm.get('skipRows')?.enable();
          this.processConfigurationForm.get('skipFooterRows')?.enable();
        } else {
          this.processConfigurationForm.get('skipRows').setValue(0);
          this.processConfigurationForm.get('skipFooterRows').setValue(0);
          this.processConfigurationForm.get('skipRows')?.disable();
          this.processConfigurationForm.get('skipFooterRows')?.disable();
        }
      });
    setTimeout(() => {
      this.patchFormData();
    }, 500);
    this.setupSearchGroup();
    this.onSearchGroup(sessionStorage.getItem('UserDefaultGroup'));
    //this.securityGroups.push({id:sessionStorage.getItem('GUID'), displayName : sessionStorage.getItem('UserDefaultGroup')});
  }


  // getSecurityToken(): void {
  //   this.processConfigurationForm.get('search_group')
  //     ?.valueChanges.pipe(
  //       debounceTime(300), // Wait for 300ms after the user stops typing
  //       distinctUntilChanged(), // Only trigger if the value changes
  //       switchMap((value: string) => {
  //         if (value.length >= 5) {
  //           return this.tokenService.getAccessToken(['Group.Read.All']).pipe(
  //             switchMap((accessToken) => {
  //               return this.processService.fetchSecurityGroups(value, accessToken); // Call the API with the token
  //             })
  //           );
  //         } else {
  //           this.securityGroups = []; // Clear the dropdown if less than 5 characters
  //           return of([]);
  //         }
  //       })
  //     )
  //     .subscribe({
  //       next: (groups: any) => {
  //         this.securityGroups = groups.value.slice(0, 10); // Limit to 10 records
  //       },
  //       error: (error) => {
  //         console.error('Error fetching security groups:', error);
  //       },
  //     });
  // }

  // Setup the typeahead logic for the dropdown
  //  setupSearchGroup(): void {
  //   this.searchGroup$
  //     .pipe(
  //       debounceTime(300), // Wait for 300ms after the user stops typing
  //       distinctUntilChanged(), // Only trigger if the value changes
  //       switchMap((searchTerm: string) => {
  //         if (searchTerm.length >= 5) {
  //           // Clear the dropdown options before making a new API call
  //           this.securityGroups = [];
  //           return this.tokenService.getAccessToken(['Group.Read.All']).pipe(
  //             switchMap((accessToken) =>
  //               this.processService.fetchSecurityGroups(searchTerm, accessToken)
  //             )
  //           );
  //         } else {
  //           return [];
  //         }
  //       })
  //     )
  //     .subscribe({
  //       next: (groups: any) => {
  //         this.securityGroups = groups.value.slice(0, 10); // Limit to 10 records
  //       },
  //       error: (error) => {
  //         console.error('Error fetching security groups:', error);
  //       },
  //     });
  // }

  setupSearchGroup(): void {
    this.searchGroup$
      .pipe(
        debounceTime(300),
        switchMap((uniqueSearchTerm: string) => {
          const searchTerm = uniqueSearchTerm;//.split('-')[0]; // Extract the original search term
          if (searchTerm.length >= 5) {
            this.securityGroups = [];
            return this.tokenService.getAccessToken(['Group.Read.All']).pipe(
              switchMap((accessToken) =>
                this.processService.fetchSecurityGroups(searchTerm, accessToken)
              )
            );
          } else {
            return [];
          }
        })
      )
      .subscribe({

        next: (groups: any) => {
          var tempAny: any[] = [];

          if (this.selectedSecurityGroups.length === 0 && this.securityGroups.length === 0) {
            //this.securityGroups = groups.value.slice(0, 10); // Limit to 10 records
            groups.value.slice(0, 10).forEach(c => {
              //this.securityGroups.push({ displayName: c.displayName, id: c.id });
              tempAny.push({ id: c.id, displayName: c.displayName });
            });

            this.securityGroups = tempAny;
          } else {

            for (var i = 0; i < groups.value.length; i++) {
              let val = groups.value[i];
              let tmp = this.selectedSecurityGroups.find(x => x.securityGroupId === val.id);
              if (!tmp) {
                tempAny.push({ id: val.id, displayName: val.displayName });
              }

              if (tempAny.length === 10) break;
            }

            this.securityGroups = tempAny;
          }
          if (this.formValues?.flpConfigurationId) {//if (this.paramsId && this.paramsId != '') {
            //this.securityGroups = []; //let's empty the group since it will be patch in onPatchFormData;
          } else {
            if (!this.userSelectedSecurityGroupIsAdded) {
              if (this.securityGroups.length > 0 && this.securityGroups[0].displayName.toLowerCase() === sessionStorage.getItem('UserDefaultGroup').toLowerCase()) { //TODO:
                let tempSG = this.securityGroups.find(x => x.displayName.toLowerCase() === sessionStorage.getItem('UserDefaultGroup').toLowerCase() && x.id === sessionStorage.getItem('GUID'))
                this.processConfigurationForm.get('security_group').setValue([tempSG]);
                this.selectedSecurityGroups.push({ securityGroupId: tempSG.id, securityGroupName: tempSG.displayName, userSelectedGroup: false });
                this.securityGroups = [];
              }
              this.userSelectedSecurityGroupIsAdded = true; //to terminate auto filling
            }
          }

        },
        error: (error) => {
          console.error('Error fetching security groups:', error);
        },
      });
  }


  // Handle selection of a security group
  onSecurityGroupSelect(selectedGroup: any): void {

    //clear the selectedSecurityGroups, then re-insert
    this.selectedSecurityGroups = [];

    if (selectedGroup) {
      selectedGroup.forEach(g => {
        this.selectedSecurityGroups.push({ securityGroupId: g.id, securityGroupName: g.displayName, userSelectedGroup: false });
      });
    }


    // Clear the dropdown options after selection
    this.securityGroups = [];
  }
  // Add a selected security group
  // addSecurityGroup(group: any): void {
  //   if (
  //     !this.selectedSecurityGroups.some(
  //       (selectedGroup) => selectedGroup.id === group.id
  //     )
  //   ) {
  //     this.selectedSecurityGroups.push({ id: group.id, name: group.displayName });
  //   }
  //   this.processConfigurationForm.get('search_group')?.setValue(''); // Clear the input
  //   this.securityGroups = []; // Clear the dropdown
  // }
  // Remove a selected security group
  // removeSecurityGroup(groupId: string): void {
  //   this.selectedSecurityGroups = this.selectedSecurityGroups.filter(
  //     (group) => group.id !== groupId
  //   );
  // }


  getDsConfiguration() {
    this.processService.getDataSliceConfiguration(1).subscribe({
      next: (response) => {
        if (response) {
          sessionStorage.setItem(
            'dsAppId',
            response.result.consumerApplicationId
          );
          sessionStorage.setItem('dsSrcId', response.result.sourceDataObjId);
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }



  getRegionBySecurityGroup() {

    this.isRegionLoading = true;

    this.dsService.getRegion2().subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {

          this.dsRegion = response.result.map(r => ({ id: r.region_ident, name: r.region }));
          this.dsREgionTemp = response.result.map(r => ({ id: r.region_ident, name: r.region }));
          if (this.fabService.isFabUserValue) {
            if (this.processConfigurationForm.get('isExternalProject').value === true) {
              this.processConfigurationForm.get('campaignName')?.enable();
              if (this.processConfigurationForm.get('campaignName').value) {
                this.processConfigurationForm.get('RegionId')?.disable();
              } else {
                this.processConfigurationForm.get('RegionId')?.enable();
              }
            } else {
              this.processConfigurationForm.get('RegionId')?.enable();
            }
          } else {
            this.processConfigurationForm.get('RegionId')?.enable();
          }
          this.isRegionLoading = false;
        }
      }
    });


  }
  getClient(regionId: string, subRegionId: string) {
    this.processConfigurationForm.get('ClientId')?.disable();
    this.dsClient = [];
    // const data: DsApiRequest = {
    //   consumerApplicationId: sessionStorage.getItem('dsAppId'),
    //   sourceDataObjId: sessionStorage.getItem('dsSrcId'),
    //   pageNo: 1,
    //   pageSize: 5000,
    //   filter: '',
    // };
    // this.dsService.getToken().then((token: string) => {
    //   if (token) {
    //     this.isClientLoading = true;
    //     data.filter = `[{'filterName':'subsubregion_code','filterValue':'${subRegionId}','operatorType':'equal'}]`;
    //     this.dsService.getRegion(token, data).then((client: Result) => {
    //       if (client?.data) {
    //         this.dsClient = client?.data
    //           ?.map((r) => ({
    //             id: r.client_ident,
    //             name: r.client_full_name,
    //           }))
    //           .filter(
    //             (value, index, self) =>
    //               index === self.findIndex((t) => t.id === value.id)
    //           );
    //         this.isClientLoading = false;
    //         this.processConfigurationForm.get('ClientId')?.enable();
    //       } else {
    //         this.isClientLoading = false;
    //         this.toastr.error(
    //           'Error in fetching the client. Please try again later.'
    //         );
    //       }
    //     });
    //   }
    // });
    const processConfigForm = this.processConfigurationForm;
    this.isClientLoading = true;
    this.dsService.getClients(regionId, subRegionId).subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {
          this.dsClient = response.result.map(r => ({ id: r.client_ident, name: r.client_full_name }));
          if (this.fabService.isFABUser$) {
            if (processConfigForm.get('isExternalProject').value === true) {
              if (processConfigForm.get('campaignName').value) {
                const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === processConfigForm.get('campaignName').value).map(x => x.clientId);
                this.dsClient = this.dsClient
                  .filter((c) => allowedIds.includes(c.id))
                  .map(c => ({ id: c.id, name: c.name }));
                this.processConfigurationForm.get('ClientId')?.setValue(this.dsClient[0]?.id || '', { emitEvent: true });
                this.getProcessName('C');
              }
            } else {
              if (processConfigForm.get('RegionId').value && processConfigForm.get('SubRegionId').value) {
                processConfigForm.get('ClientId')?.enable();
              }
            }

          } else {
            this.processConfigurationForm.get('ClientId')?.enable();
          }

          this.isClientLoading = false;
        }


      },
      error: error => {
        console.log(error);
      }
    });

  }





  // getRegion(regionId: string[]) {
  //   this.dsRegion = [];
  //   const data: DsApiRequest = {
  //     consumerApplicationId: sessionStorage.getItem('dsAppId'),
  //     sourceDataObjId: sessionStorage.getItem('dsSrcId'),
  //     pageNo: 1,
  //     pageSize: 5000,
  //     filter: '',
  //   };
  //   if (sessionStorage.getItem('dsAppId')) {
  //     this.dsService.getToken().then((token: string) => {
  //       if (token) {
  //         if (regionId) {
  //           this.isRegionLoading = true;
  //           if (regionId.length > 0)
  //             data.filter = `[{'filterName':'region_ident','filterValue':'${regionId}','operatorType':'in'}]`;
  //           //console.log(data)
  //           this.dsService.getRegion(token, data).then((reg: Result) => {
  //             if (reg?.data) {
  //               this.dsRegion = reg?.data
  //                 ?.map((r) => ({
  //                   id: r.region_ident,
  //                   name: r.region,
  //                 }))
  //                 .filter(
  //                   (value, index, self) =>
  //                     index === self.findIndex((t) => t.id === value.id)
  //                 );

  //               this.processConfigurationForm.get('RegionId')?.enable();
  //               this.isRegionLoading = false;
  //             } else {
  //               this.isRegionLoading = false;
  //               this.toastr.error(
  //                 'Error in fetching the region. Please try again later.'
  //               );
  //             }
  //           });
  //         }
  //       }
  //     });
  //   }
  // }
  getSubRegion(regionId: string) {
    this.dsSubRegion = [];
    this.dsClient = [];
    this.processConfigurationForm.get('SubRegionId')?.disable();
    // const data: DsApiRequest = {
    //   consumerApplicationId: sessionStorage.getItem('dsAppId'),
    //   sourceDataObjId: sessionStorage.getItem('dsSrcId'),
    //   pageNo: 1,
    //   pageSize: 5000,
    //   filter: '',
    // };
    // if (sessionStorage.getItem('dsAppId')) {
    //   this.dsService.getToken().then((token: string) => {
    //     if (token) {
    //       if (subRegionId) {
    //         this.isSubRegionLoading = true;
    //         data.filter = `[{'filterName':'region_ident','filterValue':'${subRegionId}','operatorType':'equal'}]`;
    //         this.dsService.getRegion(token, data).then((reg: Result) => {
    //           if (reg?.data) {
    //             this.dsSubRegion = reg?.data
    //               ?.map((r) => ({
    //                 id: r.subsubregion_code,
    //                 name: r.subsubregion,
    //               }))
    //               .filter(
    //                 (value, index, self) =>
    //                   index === self.findIndex((t) => t.id === value.id)
    //               );
    //             this.isSubRegionLoading = false;
    //             this.processConfigurationForm.get('SubRegionId')?.enable();
    //           } else {
    //             this.isSubRegionLoading = false;
    //             this.toastr.error(
    //               'Error in fetching the sub-region. Please try again later.'
    //             );
    //           }
    //         });
    //       }
    //     }
    //   });
    // }

    this.isSubRegionLoading = true;
    const processConfigForm = this.processConfigurationForm;
    this.dsService.getSubRegions(regionId).subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {

          this.dsSubRegion = response.result.map(r => ({ id: r.subsubregion_code, name: r.subsubregion }));

          if (this.fabService.isFabUserValue) {
            if (processConfigForm.get('isExternalProject').value === true) {
              if (processConfigForm.get('campaignName').value) {
                const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === processConfigForm.get('campaignName').value).map(x => x.subRegionId);
                this.dsSubRegion = this.dsSubRegion
                  .filter((r) => allowedIds.includes(r.id))
                  .map(r => ({ id: r.id, name: r.name }));

                this.processConfigurationForm.get('SubRegionId')?.setValue(this.dsSubRegion[0]?.id || '', { emitEvent: true });
                this.getProcessName('S');
              }
            } else {
              if (processConfigForm.get('RegionId').value) {
                processConfigForm.get('SubRegionId')?.enable();
              }
            }
            // && this.processConfigurationForm.get('campaignName').value) {

          } else {
            processConfigForm.get('SubRegionId')?.enable();
          }

          this.isSubRegionLoading = false;
        }


      },
      error: error => {
        console.log(error);
      }
    });
  }

  getSchedulerType() {
    this.processService.getSchedulerType().subscribe({
      next: (response: APIResponse<SchedulerType[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.schedulerTypes = response.result;
            } else {
            }
        } else {
          console.warn('error occurred while fetching process types data!');
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }
  async getWeekDayName() {
    this.processService.getWeekDayName().subscribe({
      next: (response: APIResponse<WeekDayName[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.weekDays = response.result;
              this.weekDays.forEach(() =>
                this.getWeekDays.push(new FormControl(false))
              );
            } else {
            }
        } else {
          console.warn('error occurred while fetching week days data!');
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }
  getFrequencyHour() {
    this.processService.getFrequencyHour().subscribe({
      next: (response: APIResponse<FrequencyHour[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.frequencyHours = response.result;
            } else {
            }
        } else {
          console.warn('error occurred while fetching Frequency Hours data!');
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }
  getProcessType() {
    this.processService.getProcessType().subscribe({
      next: (response: APIResponse<ProcessType[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.processTypes = response.result;
            } else {
            }
        } else {
          console.warn('error occurred while fetching process types data!');
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }
  getServerDetails() {
    this.processService.getServerDetails().subscribe({
      next: (response: APIResponse<ServerDetail[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.serverDetail = response.result;
            } else {
            }
        } else {
          console.warn('error occurred while fetching process types data!');
        }
      },
      error: (error) => {
        console.error(error);
      },
    });
  }
  blobStorageAccount: StorageAccount[] = [];
  getStorageAccountDetails() {
    this.storageAccount = [];
    if (this.configurationProcessType)
      this.processService.getStorageAccountDetails(this.configurationProcessType).subscribe({
        next: (response: APIResponse<StorageAccount[] | null>) => {
          if (response) {
            if (response.responseCode === 200)
              if (response.result && response.responseMessage[0] == 'Success') {
                this.storageAccount = response.result.filter(x => x.configurationProcessType === this.configurationProcessType);
                this.blobStorageAccount = response.result.filter(x => x.configurationProcessType === this.DataSourceTypes.Default);
              } else {
              }
          } else {
            console.warn('error occurred while fetching process types data!');
          }
        },
        error: (error) => {
          console.error(error);
        },
      });
  }
  getDataColumns() {
    this.configService.getAllDataTypeNames().subscribe({
      next: (response: APIResponse<DatatypeName[] | null>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              this.datatypeNames = response.result;
              if (this.configurationProcessType === DataSourceType.DataBricks) {
                this.datatypeNames = this.datatypeNames.filter(x => x.datatypeName !== 'date' && x.datatypeName !== 'datetime' && x.datatypeName !== 'time');
              }
            }
          }
        }
      },
      error: (error) => {
        this.toastr.error(ToastrMessages.SomethingWentWrong);
      },
    });
  }
  getAllDateTimeFormats() {
    var showDefault = this.configurationProcessType === DataSourceType.LandingLayer;
    this.configService.getAllDataTimeFormats(showDefault).subscribe({
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
        this.toastr.error("Can't find all date time formats");
      },
    });
  }

  // Handle input changes manually (optional)
  //  onSearchGroupChange(event: Event): void {

  //   const inputValue = (event.target as HTMLInputElement).value;
  //   if (inputValue.length >= 5) {
  //     this.tokenService.getAccessToken(['Group.Read.All']).subscribe({
  //       next: (accessToken) => {
  //         this.processService.fetchSecurityGroups(inputValue, accessToken).subscribe({
  //           next: (groups) => {
  //             this.securityGroups = groups.value.slice(0, 10); // Limit to 10 records
  //           },
  //           error: (error) => {
  //             console.error('Error fetching security groups:', error);
  //           },
  //         });
  //       },
  //       error: (error) => {
  //         console.error('Error acquiring token:', error);
  //       },
  //     });
  //   } else {
  //     this.securityGroups = [];
  //   }
  // }

  // Handle the search term emitted by the (search) event
  onSearchGroup(searchTerm: string): void {
    //debugger;
    if (searchTerm.length >= 5) {
      const uniqueSearchTerm = `${searchTerm}`;//-${new Date().getTime()}`; // Append a timestamp
      this.searchGroup$.next(uniqueSearchTerm); // Emit the unique search term
    } else {
      this.securityGroups = [];
    }
  }

  onBlurSearchGroup(event: any) {
    this.securityGroups = [];
  }
  //  onSearchGroupChange(event: Event): void {
  //   const inputValue = (event.target as HTMLInputElement).value; // Cast EventTarget to HTMLInputElement
  //   console.log('Search Group Input Value:', inputValue);
  //   debugger;
  //   // Add your logic here
  //   if (inputValue.length >= 5) {
  //    // this.fetchSecurityGroups(inputValue);
  //     this.msalService.acquireTokenSilent({
  //     account: this.msalService.instance.getAllAccounts()[0],
  //     scopes: ['Group.Read.All'], // Ensure the required scope is added
  //   }).pipe(
  //     catchError((error) => {
  //       console.log('Silent token acquisition failed, falling back to popup:', error);
  //       return this.msalService.acquireTokenPopup({
  //         scopes: ['Group.Read.All'], // Prompt the user for login
  //       });
  //     }),
  //     switchMap((response) => {
  //       debugger
  //       const accessToken = response.accessToken; // Get the access token
  //       return this.processService.fetchSecurityGroups(inputValue, accessToken); // Call the API with the token
  //     })
  //   );
  //   } else {
  //     this.securityGroups = []; // Clear the dropdown if less than 5 characters
  //   }
  // }
  initializeForm() {
    //
    this.processConfigurationForm = this.fb.group({
      processName: [
        '',
        [
          Validators.required,
          Validators.minLength(5),
          Validators.maxLength(40),
          Validators.pattern(this.processNamePattern),
        ],
        //[this.validateProcessName()],
      ],
      processType: ['', [Validators.required]],
      description: [
        '',
        [Validators.maxLength(1000)],
      ],
      delimiter: [
        { value: this.delimiter, disabled: this.disableDelimiter },
        Validators.required,
      ],
      flexCheckSkipEmptyLines: [true],
      flexCheckHasHeaders: [true],
      txtQuoteCharacter: ['"', [Validators.maxLength(1)]],
      is_active: [],
      do_not_archive_file: [false],
      ignore_duplicate_rows: [],
      order_by_column_list_for_dedup: [
        '',
        [Validators.maxLength(500), Validators.pattern(/^[a-zA-Z0-9, _]+$/)],
      ],
      order_by_column_list_name: [],
      order_by_column_list_name_sort_dir: [{ value: 'desc', disabled: true }],
      isSkipRow: [false],
      skipRows: [
        { value: 0, disabled: true },
        [Validators.maxLength(2), Validators.pattern(/^[0-9]*$/)],
      ],
      skipFooterRows: [
        { value: 0, disabled: true },
        [Validators.maxLength(2), Validators.pattern(/^[0-9]*$/)],
      ],
      //reg,sub/client
      RegionId: ['', Validators.required],
      SubRegionId: ['', Validators.required],
      ClientId: ['', Validators.required],
      databaseName: ['', Validators.required],
      databaseNameId: [],
      databaseConfigurationId: ['1', Validators.required],

      serverLocationId: ['', Validators.required],
      baseFolderName: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          // Validators.pattern(/^[a-zA-Z0-9_/\\]*$/),
        ],
      ],
      sourceFolderLocation: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          // Validators.pattern(/^[a-zA-Z0-9_/\\]*$/),
        ],
      ],
      scheduledId: ['', Validators.required],
      scheduleValue: '',
      scheduledDate: [null, Validators.required],
      scheduledTime: ['', Validators.required],
      scheduledEndDate: [null],
      scheduledEndTime: [''],
      blobStorageAccount: ['', Validators.required],
      blobContainerName: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          // Validators.pattern(/^[a-zA-Z0-9_-]*$/),
        ],
      ],
      blobSourcePath: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          // Validators.pattern(/^[a-zA-Z0-9_/\\]*$/),
        ],
      ],
      search_group: ['', [Validators.minLength(5)]], // Add this line
      //databaseServer : [],
      //databaseServerId : [],
      search_string_in_file_name: [
        '',
        [Validators.maxLength(100), Validators.pattern(/^[a-zA-Z0-9, _]+$/)],
      ],
      key_column_list: [
        '',
        [
          //Validators.maxLength(4000),
          Validators.pattern(
            /^[a-zA-Z0-9, _,á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+$/ //
          ),
        ],
      ],
      column_name_list: [
        '',
        [
          Validators.required,
          //Validators.maxLength(4000),
          Validators.pattern(
            /^[a-zA-Z0-9, _=,á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+$/ //¿,¡,
          ),
        ],
      ],
      db_column_name_list: [
        { value: '', disabled: false },
        [
          Validators.required,
          //Validators.maxLength(4000),
          Validators.pattern(
            /^[a-zA-Z0-9, _=,á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+$/ //¿,¡,
          ),
        ],
      ],
      convert_datatypes_column_list: [
        '',
        [
          Validators.required,
          //Validators.maxLength(4000),
          Validators.pattern(
            /^[a-zA-Z0-9_á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+(=[a-zA-Z0-9_]*(\|[0-9]+)?)?(,[a-zA-Z0-9_á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+(=[a-zA-Z0-9_]*(\|[0-9]+)?)?)*$/
            //¿,¡,
          ), //Validators.pattern(/^[a-zA-Z0-9_, ]*(=[a-zA-Z0-9_, ]*)?((,[a-zA-Z0-9_, ]*(=[a-zA-Z0-9_, ]*)?)*)$/),//Validators.pattern(/^[a-zA-Z0-9, _=]+$/),
        ],
      ],
      sender_communication_email: [
        '',
        [
          Validators.required,
          //Validators.maxLength(4000),
          Validators.pattern(
            (/^(?:[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,},\s*)*[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/)
          ), //
          ///^([\w\.-]+@[\w\.-]+\.[a-zA-Z]{2,})(;\s*[\w\.-]+@[\w\.-]+\.[a-zA-Z]{2,})*$/
        ],
      ],
      tableName: [
        '',
        [
          Validators.required,
          Validators.pattern(this.processNamePattern),
          Validators.maxLength(100),
          Validators.pattern(/^[a-zA-Z0-9_]*$/),
        ],
      ],
      drop_history_table: [],
      drop_main_table: [],
      dedup: [],
      is_validate_fileschema_with_target_table: [],
      hourFrequency: [],
      weekDays: new FormArray([]),
      spanish_to_english: [false],
      roman_numerals_only: [false],
      mergeData: [false],
      createHistoryTable: [false],
      deltaTableName: '',
      deltaServerNameId: '',
      // deltaJobId: ['', Validators.pattern(this.alphaNumericPattern)],
      deltaJobId: [''],
      deltaStorageAccountId: '',
      deltaContainerName: '',
      deltaSource: '',
      security_group: [[], requiredArrayValidatorTemp],
      RuleSetName: [''],
      isExternalProject: [false],
      campaignName: [{ value: null, disabled: true }],
    });
    this.deltaStorageAccountIdChange();
    this.landingLayerControls();
    // if (this.configurationProcessType === DataSourceType.DataBricks) {
    //   const deltaStorageAccountId = this.processConfigurationForm.get('deltaStorageAccountId');
    //   const deltaContainerName = this.processConfigurationForm.get('deltaContainerName');
    //   const deltaSource = this.processConfigurationForm.get('deltaSource');
    //   const deltaJobId = this.processConfigurationForm.get('deltaJobId');
    //   deltaStorageAccountId.setValidators([Validators.required]);
    //   deltaContainerName.setValidators([Validators.required, Validators.maxLength(100)]);
    //   deltaSource.setValidators([Validators.required, Validators.maxLength(100)]);
    //   deltaJobId.setValidators([Validators.pattern(this.alphaNumericPattern)]);
    //   this.processConfigurationForm.get('databaseName').clearValidators();
    // }
  }

  landingLayerControls() {
    if (this.configurationProcessType === this.DataSourceTypes.LandingLayer) {
      const deltaStorageAccountId = this.processConfigurationForm.get('deltaStorageAccountId');
      const deltaContainerName = this.processConfigurationForm.get('deltaContainerName');


      this.processConfigurationForm.addControl('landingLayerFileExtension', new FormControl([], Validators.required));
      this.processConfigurationForm.addControl('landingLayerRegex', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerDateformat', new FormControl(null));
      this.processConfigurationForm.addControl('landingLayerTimeformat', new FormControl(null));
      this.processConfigurationForm.addControl('landingLayerPrefix', new FormControl('', Validators.required));
      this.processConfigurationForm.addControl('landingLayerPrefixCheckbox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerDateformatCheckbox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerTimeformatCheckBox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerAcceptedPath', new FormControl('', [Validators.required, Validators.minLength(5), Validators.maxLength(2000)]));
      this.processConfigurationForm.addControl('landingLayerRejectedPath', new FormControl('', [Validators.required, Validators.minLength(5), Validators.maxLength(2000)]));


      this.setupCheckboxToggle('landingLayerDateformatCheckbox', 'landingLayerDateformat');
      this.setupCheckboxToggle('landingLayerPrefixCheckbox', 'landingLayerPrefix');
      this.setupCheckboxToggle('landingLayerTimeformatCheckBox', 'landingLayerTimeformat');

      deltaStorageAccountId.setValidators([Validators.required]);
      deltaContainerName.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.pattern(/^(?!-+$)[a-z0-9][a-z0-9\- ]*$/), noWhitespaceValidator]);


      this.processConfigurationForm.get('column_name_list').clearValidators();
      this.processConfigurationForm.get('databaseName').clearValidators();
      this.processConfigurationForm.get('tableName').clearValidators();
      this.processConfigurationForm.get('db_column_name_list').clearValidators();
      this.processConfigurationForm.get('convert_datatypes_column_list').clearValidators();
      this.processConfigurationForm.get('convert_datatypes_column_list').clearValidators();
    }
  }

  setupCheckboxToggle(checkboxName: string, targetControlName: string) {
    const checkbox = this.processConfigurationForm.get(checkboxName);
    const targetControl = this.processConfigurationForm.get(targetControlName);

    if (!checkbox || !targetControl) return;

    // Initial state
    if (!checkbox.value) {
      targetControl.disable({ emitEvent: false });
    } else {
      targetControl.enable({ emitEvent: false });
    }

    // React to checkbox changes
    checkbox.valueChanges.subscribe(checked => {
      if (checked) {
        targetControl.enable({ emitEvent: false });
        //preselect first option
        switch (targetControlName) {
          case "landingLayerPrefix":
            targetControl.setValue('');
            break;
          case "landingLayerDateformat":
            targetControl.setValue(this.dateOnlyFormats[0].formatId);
            break;
          case "landingLayerTimeformat":
            targetControl.setValue(this.timeOnlyFormats[0].formatId);
            break;

        }
      } else {
        targetControl.disable({ emitEvent: false });
        targetControl.reset(); // optional: clear the ng-select
      }
    });
  }

  deltaStorageAccountIdChange() {
    const deltaStorageAccountId = this.processConfigurationForm.get('deltaStorageAccountId');
    const deltaContainerName = this.processConfigurationForm.get('deltaContainerName');
    const deltaSource = this.processConfigurationForm.get('deltaSource');
    const deltaJobId = this.processConfigurationForm.get('deltaJobId');
    const databaseName = this.processConfigurationForm.get('databaseName');
    const deltaTableName = this.processConfigurationForm.get('deltaTableName');
    const deltaServerNameId = this.processConfigurationForm.get('deltaServerNameId');

    if (this.configurationProcessType == DataSourceType.DataBricks) {


      deltaTableName.setValidators([Validators.required, Validators.pattern(/^(?!.*(__|--))[a-zA-Z0-9_-]*$/), Validators.maxLength(100)]); //Validators.pattern(/^[A-Z-A-z_][A-Za-z0-9_]*$/)
      deltaServerNameId.setValidators([Validators.required]);

      deltaStorageAccountId.setValidators([Validators.required]);
      //deltaContainerName.setValidators([Validators.required, Validators.maxLength(100), Validators.pattern(/^(?!.*--)[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/), noWhitespaceValidator]); // Validators.pattern(/^(?=.*[A-Za-z0-9])[A-Z-a-z0-9\s_-]+$/),
      deltaContainerName.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.maxLength(100), Validators.pattern(/^(?!-+$)[a-z0-9][a-z0-9\- ]*$/), noWhitespaceValidator]);
      deltaSource.setValidators([Validators.required, Validators.maxLength(100), Validators.pattern(/^(?!.*([ _-])\1)[a-zA-Z0-9_/ -]+$/), noWhitespaceValidator]); //Validators.pattern(/^(?=.*[A-Za-z0-9\/])[A-Z-a-z0-9\s\/_-]+$/),
      deltaJobId.setValidators([Validators.required, Validators.pattern(this.onlyNumbersPattern)]);
      databaseName?.clearValidators();
    } else {
      deltaTableName.clearValidators();
      deltaServerNameId.clearValidators();
      deltaStorageAccountId.clearValidators();
      deltaContainerName.clearValidators();
      deltaSource.clearValidators();
      deltaJobId.clearValidators();
      databaseName?.setValidators([Validators.required]);
    }
    deltaTableName?.updateValueAndValidity();
    deltaServerNameId?.updateValueAndValidity();
    deltaStorageAccountId?.updateValueAndValidity();
    deltaContainerName?.updateValueAndValidity();
    deltaSource?.updateValueAndValidity();
    deltaJobId?.updateValueAndValidity();
    databaseName?.updateValueAndValidity();
  }
  onProcessTypeChange(processType: string) {
    const additionalSettingsTab = document.getElementById(
      'addtional-setting-tab'
    ) as HTMLElement;
    // additionalSettingsTab.click();
    const serverLocationId =
      this.processConfigurationForm.get('serverLocationId');
    const baseFolderName = this.processConfigurationForm.get('baseFolderName');
    const sourceFolderLocation = this.processConfigurationForm.get(
      'sourceFolderLocation'
    );
    const blobStorageAccount =
      this.processConfigurationForm.get('blobStorageAccount');
    const blobContainerName =
      this.processConfigurationForm.get('blobContainerName');
    const blobSourcePath = this.processConfigurationForm.get('blobSourcePath');
    const scheduledId = this.processConfigurationForm.get('scheduledId');
    const scheduledDate = this.processConfigurationForm.get('scheduledDate');
    const scheduledTime = this.processConfigurationForm.get('scheduledTime');
    const scheduledEndDate =
      this.processConfigurationForm.get('scheduledEndDate');
    scheduledId.setValidators([Validators.required]);
    scheduledDate.setValidators([Validators.required]);
    scheduledTime.setValidators([Validators.required]);
    //this.processConfigurationForm.get('scheduledEndDate').setValue(this.minDate);
    if (processType && processType == '2') {
      serverLocationId.setValidators([Validators.required]);
      baseFolderName.setValidators([
        Validators.required,
        Validators.maxLength(100),
        noWhitespaceValidator,
        Validators.pattern(/^(?!_+$)(?!\.+$)(?!.*[\\/:*?"<>])[A-Za-z0-9_.]+$/),
      ]);
      //not allowed
      // test/_______/
      // test/__________/test/
      // test/___-_______/test/
      // test/___._______/test/
      // test/___-.-_______/test/
      sourceFolderLocation.setValidators([
        Validators.required,
        Validators.maxLength(100),
        noWhitespaceValidator,
        //Validators.pattern(/^(?![_.\\]+$)(?!\.+$)(?!.*[/:*?"<>])[a-zA-Z0-9_.\\]*$/),
        Validators.pattern(/^(?=(?:.*[a-zA-Z0-9]){2,})(?![_.\\]+$)(?!\.+$)(?!.*[/:*?"<>])(?!.*\\[-_.]+\\)[a-zA-Z0-9_.\\-\s]+$/)
      ]);

      blobStorageAccount?.clearValidators();

      blobContainerName?.clearValidators();
      blobSourcePath?.clearValidators();
      blobStorageAccount?.patchValue('');
      blobContainerName?.patchValue('');
      blobSourcePath?.patchValue('');
    } else if (processType && processType == '3') {
      blobStorageAccount.setValidators([Validators.required]);
      blobContainerName.setValidators([
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(63),
        noWhitespaceValidator,
        Validators.pattern(/^(?!.*--)[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/),
      ]);
      blobSourcePath.setValidators([
        Validators.required,
        Validators.maxLength(100),
        noWhitespaceValidator,
        Validators.pattern(/^(?=(?:.*[a-zA-Z0-9]){2,})(?![_.]+$)(?!\.+$)(?!.*[:*?"<>])(?!.*\/[-_.]+\/)[a-zA-Z0-9_.\/-\s]+$/)
      ]);
      serverLocationId?.clearValidators();
      baseFolderName?.clearValidators();
      sourceFolderLocation?.clearValidators();
      serverLocationId?.patchValue('');
      baseFolderName?.patchValue('');
      sourceFolderLocation?.patchValue('');
      scheduledEndDate?.clearValidators();
      scheduledEndDate?.patchValue(null);
    } else if (processType && isSharePointProcessType(processType)) {
      blobStorageAccount?.clearValidators();
      blobContainerName?.clearValidators();
      blobSourcePath?.clearValidators();
      serverLocationId?.clearValidators();
      baseFolderName?.clearValidators();
      sourceFolderLocation?.clearValidators();
      blobStorageAccount?.patchValue('');
      blobContainerName?.patchValue('');
      blobSourcePath?.patchValue('');
      serverLocationId?.patchValue('');
      baseFolderName?.patchValue('');
      sourceFolderLocation?.patchValue('');
      scheduledEndDate?.clearValidators();
      scheduledEndDate?.patchValue(null);
    } else {
      blobStorageAccount?.clearValidators();
      blobContainerName?.clearValidators();
      blobSourcePath?.clearValidators();
      blobStorageAccount?.patchValue('');
      blobContainerName?.patchValue('');
      blobSourcePath?.patchValue('');
      serverLocationId?.clearValidators();
      baseFolderName?.clearValidators();
      sourceFolderLocation?.clearValidators();
      serverLocationId?.patchValue('');
      baseFolderName?.patchValue('');
      sourceFolderLocation?.patchValue('');
      scheduledId?.clearValidators();
      scheduledDate?.clearValidators();
      scheduledTime?.clearValidators();
      scheduledEndDate?.clearValidators();
      scheduledId?.patchValue('');
      scheduledDate?.patchValue(null);
      scheduledTime?.patchValue('');
      scheduledEndDate?.patchValue(null);
    }
    serverLocationId?.updateValueAndValidity();
    baseFolderName?.updateValueAndValidity();
    sourceFolderLocation?.updateValueAndValidity();
    blobStorageAccount?.updateValueAndValidity();
    blobContainerName?.updateValueAndValidity();
    blobSourcePath?.updateValueAndValidity();
    scheduledId?.updateValueAndValidity();
    scheduledDate?.updateValueAndValidity();
    scheduledTime?.updateValueAndValidity();
    scheduledEndDate?.updateValueAndValidity();
  }
  resetScheduler() {
    this.updateSchedular = true;
    this.processConfigurationForm.get('scheduledId')?.setValue('');
    this.processConfigurationForm.get('hourFrequency')?.setValue('');
    this.processConfigurationForm.get('scheduledDate')?.setValue(null);
    this.processConfigurationForm.get('scheduledTime')?.setValue('');
    this.processConfigurationForm.get('scheduledEndDate')?.setValue(null);
    this.processConfigurationForm.get('scheduledEndTime')?.setValue('');
    this.getWeekDays.controls.forEach((c) => {
      c.reset();
      c.enable();
    });
    this.isReadonlySchedular = false;
    // this.processConfigurationForm.get('scheduledId').enable();
    // this.processConfigurationForm.get('hourFrequency').enable();
    // this.processConfigurationForm.get('weekDays').enable();
    // this.processConfigurationForm.get('scheduledDate').enable();
    // this.processConfigurationForm.get('scheduledTime').enable();
    // this.processConfigurationForm.get('scheduledEndDate').enable();
    // this.processConfigurationForm.get('scheduledEndTime').enable();
  }
  validMonth(val: number): string {
    return val < 10 ? '0' + val : val.toString();
  }

  onFromDateChange(date: Date | NgbDate) {
    if (date) {
      const selectedDate = date instanceof Date
        ? date
        : new Date(date.year, date.month - 1, date.day);
      this.schedulerEndMinDate = selectedDate;
      this.minToDate = date instanceof Date
        ? {
            year: date.getFullYear(),
            month: date.getMonth() + 1,
            day: date.getDate(),
          }
        : date;
      this.processConfigurationForm.get('scheduledEndDate')?.setValue(null);
      this.onDateChange(this.formatSchedulerCompareDate(selectedDate));
    }
  }
  get dayCheckboxes(): FormArray {
    return this.processConfigurationForm.get('weekDays') as FormArray;
  }
  oneCheckboxChecked(control: AbstractControl): ValidationErrors | null {
    const values = control.value || [];
    if (values.some((value) => value === true)) {
      return null;
    }
    return { oneCheckboxChecked: 'one checkbox must be selected' };
  }
  onSchedulerChange() {
    const sdlType = this.processConfigurationForm.get('scheduledId');
    const hourFrequency = this.processConfigurationForm.get('hourFrequency');
    //const weekDays = this.processConfigurationForm.get('weekDays');

    if (sdlType?.value == 2) {
      hourFrequency?.setValidators([Validators.required]);
      this.getWeekDays.setValidators(this.oneCheckboxChecked);
      this.getWeekDays.updateValueAndValidity();
    } else {
      hourFrequency?.clearValidators();
      hourFrequency?.patchValue('');
      this.getWeekDays.clearValidators();
      this.getWeekDays.updateValueAndValidity();
    }
    hourFrequency?.updateValueAndValidity();
    this.dayCheckboxes?.updateValueAndValidity();
  }
  onIgnoreDuplicateRow(val: any) {
    const key_column_list =
      this.processConfigurationForm.get('key_column_list');
    this.processConfigurationForm.get('key_column_list');
    if (val) {
      key_column_list?.setValidators([
        Validators.required,
        Validators.maxLength(500),
        Validators.pattern(/^[a-zA-Z0-9, _,á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+$/),
      ]);
    } else {
      key_column_list?.clearValidators();
      key_column_list?.patchValue('');
      key_column_list?.setValidators([
        Validators.maxLength(500),
        Validators.pattern(/^[a-zA-Z0-9, _,á,é,í,ó,ú,ü,ñ,Á,É,Í,Ñ,Ó,Ú,Ü]+$/),
      ]);

      this.recordHeaders.forEach(c => {
        c.ColumnKey = false;
      });
      this.processConfigurationForm.get('order_by_column_list_name_sort_dir').disable();
      this.processConfigurationForm.get('dedup').setValue("");
    }
    key_column_list?.updateValueAndValidity();



  }
  patchFormData() {
    try {
      if (this.paramsId && this.paramsId != '') {
        //this.processConfigurationForm.get('scheduledEndDate').setValue('');
        this.busyService.busy();
        this.configurationDetails = null;
        this.updateSchedular = false;

        //this.configService.getConfigurationDetails(this.paramsId)
        this.configService.getConfigurationDetailsV2(this.paramsId, this.tabName)
          .subscribe({
            next: (response: APIResponse<FileConfigurationDetails>) => {
              if (response) {
                if (response.responseCode === 200 && response.result) {
                  //console.log(response);
                  this.ruleSetPayLoad = null;
                  this.configurationDetails = response.result;
                  this.sharePointInitialApplicationId = response.result.sharePointApplicationId ?? null;
                  this.sharePointInitialApplicationSiteId = response.result.sharePointApplicationSiteId ?? null;
                  this.sharePointInitialLibraryName = response.result.sharePointLibraryName ?? null;
                  this.sharePointInitialFolderPath = response.result.sharePointFolderPath ?? null;
                  this.configurationProcessType = response.result.dataSource;
                  this.navigateService.configurationProcess = response.result.dataSource;

                  if (this.navigateService.configurationProcess === DataSourceType.LandingLayer) {
                    this.landingLayerControls();
                  }
                  //console.log(this.configurationProcessType);
                  //this.getStorageAccountDetails(); //this has already been called in ngOnit - removed by wbq 10/22/2025
                  this.deltaStorageAccountIdChange();
                  // this.getRegion([String(this.configurationDetails.regionId)]);
                  // this.getSubRegion(String(this.configurationDetails.regionId));
                  // this.getClient(this.configurationDetails.subRegionId);
                  this.dsRegion = [
                    {
                      id: this.configurationDetails.regionId,
                      name: this.configurationDetails.regionName,
                    },
                  ];
                  this.dsSubRegion = [
                    {
                      id: this.configurationDetails.subRegionId,
                      name: this.configurationDetails.subRegionName,
                    },
                  ];
                  this.dsClient = [
                    {
                      id: this.configurationDetails.clientId,
                      name: this.configurationDetails.clientName,
                    },
                  ];
                  // this.dsClient = [
                  //   {
                  //     id: this.configurationDetails.clientId,
                  //     name: this.configurationDetails.clientName,
                  //   },
                  // ];
                  this.regionName = this.configurationDetails.regionName;
                  this.subRegionName = this.configurationDetails.subRegionName;
                  this.clientName = this.configurationDetails.clientName;
                  this.processConfigurationForm.get('RegionId')?.enable();
                  this.processConfigurationForm.get('SubRegionId')?.enable();
                  this.processConfigurationForm.get('ClientId')?.enable();
                  let isSkip = false;
                  if (
                    this.configurationDetails?.fileConfigurationDetails[0]
                      ?.skipRows > 0 ||
                    this.configurationDetails?.fileConfigurationDetails[0]
                      ?.skipFooterRows > 0
                  ) {
                    isSkip = true;
                  }

                  const configurationTableMappingDetails = this.configurationDetails?.configurationTableMappingDetails?.[0];
                  this.processConfigurationForm.patchValue({
                    processName: this.configurationDetails.process_name,
                    processType: this.configurationDetails?.processTypeId,
                    description: this.configurationDetails.description,
                    delimiter:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.delimiter,
                    //flexCheckSkipEmptyLines:'',
                    flexCheckHasHeaders:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.isHeaderProvided,
                    flexCheckSkipEmptyLines:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.skipEmptyLines,
                    txtQuoteCharacter:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.quoteCharacter,
                    do_not_archive_file:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.doNotArchiveFile,
                    ignore_duplicate_rows:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.ignoreDuplicateRows,
                    // keepFirstRow: this.configurationDetails?.fileConfigurationDetails[0]
                    // ?.keepFirstRow == true ?
                    dedup:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.dedup,
                    //order_by_column_list_name: 'desc',
                    order_by_column_list_name_sort_dir:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.keepFirstRow == true
                        ? 'asc'
                        : 'desc',
                    isSkipRow: isSkip,
                    skipRows:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.skipRows,
                    skipFooterRows:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.skipFooterRows,
                    RegionId: this.configurationDetails?.regionId,
                    SubRegionId: this.configurationDetails?.subRegionId,
                    ClientId: this.configurationDetails?.clientId,
                    databaseName: this.configurationDetails?.databaseName,
                    databaseConfigurationId: configurationTableMappingDetails?.databaseConfigurationId,
                    serverLocationId: this.configurationDetails?.fileServerId,
                    baseFolderName: this.configurationDetails?.folderName,
                    sourceFolderLocation: this.configurationDetails?.sourcePath,
                    scheduledId: this.configurationDetails?.scheduleTypeId,
                    scheduledDate: this.getDate(
                      this.configurationDetails?.scheduleStartDate
                    ),
                    scheduledTime: this.configurationDetails?.scheduleStartTime,

                    scheduledEndDate: this.getDate(
                      this.configurationDetails?.scheduleEndDate
                    ),
                    scheduledEndTime: this.configurationDetails?.scheduleEndTime,
                    blobStorageAccount:
                      this.configurationDetails?.blobStorageAccountId,
                    blobContainerName:
                      this.configurationDetails?.blobStorageContainerName,
                    blobSourcePath: this.configurationDetails?.sourcePath,
                    search_string_in_file_name:
                      this.configurationDetails?.search_string_in_file_name,
                    key_column_list:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.keyColumnList,
                    column_name_list:
                      (this.configurationDetails?.fileConfigurationDetails[0]?.columnNameList),// ?? '').replace(/=[^,]*/g, ''),
                    convert_datatypes_column_list:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.convertDatatypesColumnList,
                    sender_communication_email:
                      this.configurationDetails?.sender_communication_email,
                    tableName: this.configurationDetails?.tableName,
                    drop_history_table: configurationTableMappingDetails?.dropHistoryTable,
                    drop_main_table: configurationTableMappingDetails?.dropMainTable,
                    is_validate_fileschema_with_target_table: configurationTableMappingDetails?.validateFileSchema,
                    hourFrequency:
                      this.configurationDetails?.customSchedulerDetails[0]
                        ?.frequencyHoursId,
                    spanish_to_english:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.spanishToEnglish,
                    roman_numerals_only:
                      this.configurationDetails?.fileConfigurationDetails[0]
                        ?.romanNumeralsOnly,
                    mergeData: configurationTableMappingDetails?.mergeData, //this.configurationDetails?.configurationTableMappingDetails[0].mergeData,
                    createHistoryTable: configurationTableMappingDetails?.createHistoryTable, //this.configurationDetails?.configurationTableMappingDetails[0].createHistoryTable,
                    deltaStorageAccountId: Number(this.configurationDetails?.deltaStorageAccountId ?? 0),
                    deltaContainerName: this.configurationDetails?.deltaContainerName ?? "",
                    deltaSource: this.configurationDetails?.deltaSource ?? "",
                    deltaTableName: this.configurationDetails?.configurationTableMappingDetails[0].tableName,
                    deltaServerNameId: this.configurationDetails?.configurationTableMappingDetails[0].databaseConfigurationId,
                    deltaJobId: this.configurationDetails?.configurationTableMappingDetails[0].deltaJobId,
                    RuleSetName: this.configurationDetails?.flpRuleSet[0]?.ruleSetName,
                    campaignName: this.configurationDetails?.campaignId
                  });
                  // const payLoad: PayLoad = {
                  //   ruleSets: this.configurationDetails?.flpRuleSet.map(rule => ({ ...rule, ruleSetNameId: ruleSetNameId })),  //,
                  //   created_by: sessionStorage.getItem('upn').split('@')[0],
                  //   username: sessionStorage.getItem('username'),
                  //   description: this.configurationDetails?.flpRuleSet[0].description
                  // };
                  // this.ruleSetPayLoad = this.configurationDetails?.flpRuleSet[0];


                  this.configurationDetails?.configurationSecurityGroupMappingList.forEach(sg => {
                    let tmpSG = this.securityGroups.find(x => x.id === sg.securityGroupId);
                    if (!tmpSG) {
                      this.securityGroups.push({ id: sg.securityGroupId, displayName: sg.securityGroupName });
                    }
                    this.selectedSecurityGroups.push({ securityGroupId: sg.securityGroupId, securityGroupName: sg.securityGroupName, userSelectedGroup: false });
                  });

                  this.processConfigurationForm.get('security_group').setValue(this.securityGroups);
                  if (this.configurationProcessType !== DataSourceType.LandingLayer) {
                    setTimeout(() => {
                      (document.getElementById('column_name_list_forDisplay') as HTMLInputElement).value = this.configurationDetails?.fileConfigurationDetails[0]?.columnNameList;
                    }, 0)


                    if (!this.processConfigurationForm.get('scheduledEndDate').value) {
                      this.processConfigurationForm.get('scheduledEndDate').setValue(null);
                    }

                    this.onIgnoreDuplicateRow(this.processConfigurationForm.get('ignore_duplicate_rows').value);


                    if (this.configurationDetails.processType) {
                      // this.processConfigurationForm.get('scheduledEndDate').setValue(this.getDate(
                      //   this.configurationDetails?.scheduleEndDate
                      // ));
                      this.onSchedulerChange();
                    }

                    //console.log(this.weekDays);
                    if (this.weekDays) {
                      this.weekDays?.map((x, i) => {
                        this.getWeekDays.at(i).disable();
                        if (
                          this.configurationDetails.customSchedulerDetails
                            ?.map((x) => x.weekDaysId)
                            .indexOf(x.id) !== -1
                        ) {

                          this.getWeekDays.at(i).patchValue(true);
                          //(this.getWeekDays.at(i) as FormGroup).get('isChecked')?.disable();

                        }
                      });
                    }
                  }

                  this.onProcessTypeChange(
                    this.configurationDetails?.processTypeId.toString()
                  );
                  if (this.configurationProcessType !== DataSourceType.LandingLayer) {
                    if (this.paramsId) {
                      this.processConfigurationForm
                        .get('db_column_name_list')
                        .setValue(
                          this.configurationDetails?.fileColumnMapping
                            ?.map((x) => x.dbColumn)
                            .join(',')
                        );

                      let columnHeaders = this.processConfigurationForm.get('column_name_list').value;
                      let key_column_list = this.processConfigurationForm.get('key_column_list').value;
                      let db_column_name_list = this.processConfigurationForm.get('db_column_name_list').value;
                      let dedup = this.configurationDetails?.fileConfigurationDetails[0]?.dedup;

                      this.recordHeaders = [];
                      //let convert_datatypes_column_list = this.processConfigurationForm.get('convert_datatypes_column_list').value;
                      columnHeaders.split(',').forEach((e, i) => {
                        let fileColumnMapping =
                          this.configurationDetails.fileColumnMapping.find(
                            (x) => x.fileColumn === ((response.result.fileConfigurationDetails[0].isHeaderProvided) ? e : e.split('=')[0])
                          );

                        this.recordHeaders.push({
                          index: i,
                          ColumnName: response.result.fileConfigurationDetails[0].isHeaderProvided ? e : e.split('=')[0],
                          DbColumnName: fileColumnMapping.dbColumn,
                          OgColumnName: response.result.fileConfigurationDetails[0].isHeaderProvided ? e : e.split('=')[0],
                          OgDbColumnName: fileColumnMapping.dbColumn,
                          DatatypeName: fileColumnMapping.dataType,
                          willInclude: true,
                          ColumnKey: false,
                          newColumn: false,
                          missingColumn: false,
                          invalidDataType: false,
                          willAddNewColumn: true,
                          invalidColumnName: false,
                          columnForDedeup: false,
                          dateTimeFormatId: fileColumnMapping.formatId,
                          isDuplicateColumn: false,
                          useMultipleSheets: true
                        });
                      });

                      // convert_datatypes_column_list.split(',').forEach(e => {
                      //   let foundRecordHeader = this.recordHeaders.find(x => x.ColumnName === e);
                      //     if(foundRecordHeader){
                      //       foundRecordHeader.DatatypeName = e.split('');
                      //     }
                      // });



                      if (key_column_list.length > 0) {
                        key_column_list.split(',').forEach((e, i) => {
                          let foundRecordHeader = this.recordHeaders.find((x) => x.ColumnName === e);
                          if (foundRecordHeader) {
                            foundRecordHeader.ColumnKey = true;
                          }
                        });

                        this.processConfigurationForm.get('order_by_column_list_name_sort_dir')?.enable();
                      }

                      // if (db_column_name_list.length > 0) {
                      //   db_column_name_list.split(',').forEach((e, i) => {
                      //     let foundRecordHeader = this.recordHeaders.find(x => x.ColumnName === e);
                      //     if (foundRecordHeader) {
                      //       foundRecordHeader.DbColumnName = e;
                      //     }
                      //   })
                      // }

                      if (dedup.length > 0) {
                        dedup.split(',').forEach((e) => {
                          let foundRecordHeader = this.recordHeaders.find(
                            (x) => x.ColumnName === e
                          );
                          if (foundRecordHeader) {
                            foundRecordHeader.columnForDedeup = true;
                          }
                        });
                      }
                    }

                    this.retriveValidationDetails();
                  } else {

                    const rawLandingLayer = this.configurationDetails.fileConfigurationDetails[0].landingLayerFileExtension.split(',');

                    //var landingLayer: string[] = Array.isArray(rawLandingLayer) ? rawLandingLayer : rawLandingLayer ? [rawLandingLayer] : [];
                    this.processConfigurationForm.get('landingLayerFileExtension').setValue((rawLandingLayer ?? []).map(x => Number(x)));
                    const raw = this.configurationDetails.fileConfigurationDetails[0].landingLayerRegex;
                    this.regexList = this.helperUtil.toRegexArray(raw);

                    if (this.configurationDetails.fileConfigurationDetails[0].landingLayerPrefix) {
                      this.processConfigurationForm.get('landingLayerPrefixCheckbox').setValue(true);
                    }

                    if (this.configurationDetails.fileConfigurationDetails[0].dateFormatId) {
                      this.processConfigurationForm.get('landingLayerDateformatCheckbox').setValue(true);
                    }

                    if (this.configurationDetails.fileConfigurationDetails[0].timeFormatId) {
                      this.processConfigurationForm.get('landingLayerTimeformatCheckBox').setValue(true);
                    }

                    this.processConfigurationForm.get('landingLayerPrefix').setValue(this.configurationDetails.fileConfigurationDetails[0].landingLayerPrefix);
                    this.processConfigurationForm.get('landingLayerDateformat').setValue(this.configurationDetails.fileConfigurationDetails[0].dateFormatId);
                    this.processConfigurationForm.get('landingLayerTimeformat').setValue(this.configurationDetails.fileConfigurationDetails[0].timeFormatId);




                    this.processConfigurationForm.get('landingLayerAcceptedPath').setValue(this.configurationDetails.configurationTableMappingDetails[0].landingLayerAcceptedPath);
                    this.processConfigurationForm.get('landingLayerRejectedPath').setValue(this.configurationDetails.configurationTableMappingDetails[0].landingLayerRejectedPath);
                  }
                } else {
                  //this.apiErrorMessage = response.responseMessage[0];
                }
              } else {
                //this.apiErrorMessage = 'Failed to get data!';
              }

              this.isReadonlyProcessDetails = true;


              this.isReadonlySchedular = true;
              if (this.configurationDetails?.campaignId) {
                if (this.fabService.fabReady$) {
                  this.showCampaignName = true;

                  setTimeout(() => {
                    //debugger;
                    this.processConfigurationForm.get('isExternalProject').setValue(true);
                    this.processConfigurationForm.get('isExternalProject').disable();
                    this.campaignNames = this.fabService.FABUserAccount.map(x => ({ campaignId: x.campaignId, campaignName: x.campaignName }));
                    this.processConfigurationForm.get('campaignName').setValue(this.configurationDetails.campaignId);
                  }, 500);

                }
              }
              this.retriveValidationDetails();

              this.busyService.idle();
            },
            error: (err) => {
              this.busyService.idle();
              console.log(err?.message);
            },
          });
      }
    } catch (error) {
      console.error(error);
    }
  }
  onProcessChange(processName: string) {
    let tableName =
      processName +
      '_' +
      new Date().getFullYear() +
      (new Date().getMonth() + 1).toString().padStart(2, '0') +
      new Date().getDate().toString().padStart(2, '0');
    this.processConfigurationForm.get('tableName').setValue(tableName);
  }
  onDatabaseNameChange(event: DIDatabaseNames) {

    let selValue = this.databaseNames.find((x) => x.id === event.id);
    this.processConfigurationForm.get('databaseConfigurationId').setValue(selValue.id);
    this.processConfigurationForm.get('databaseName').setValue(selValue.databaseName);
  }

  onDateChange(val: any): void {
    if (val && val != '<empty string>') {
      let startTime = this.processConfigurationForm.get('scheduledTime').value;
      this.hours = [];
      const today = new Date();
      let todayDate = `${today.getUTCFullYear()}-${String(
        today.getUTCMonth() + 1
      ).padStart(2, '0')}-${String(today.getUTCDate()).padStart(2, '0')}`;

      if (val == todayDate)
        for (let hour = today.getUTCHours() + 1; hour < 24; hour++) {
          this.hours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
        }
      else
        for (let hour = 0; hour < 24; hour++) {
          this.hours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
        }
      if (!this.hours.includes(startTime))
        this.processConfigurationForm.get('scheduledTime').setValue('');
      //this.checkDate();
    }
  }

  landingLayerAcceptedPath = '';
  landingLayerRejectedPath = '';
  getProcessName(selType: string) {
    const regionId = this.processConfigurationForm.get('RegionId')?.value;
    const subRegionId = this.processConfigurationForm.get('SubRegionId')?.value;
    const clientId = this.processConfigurationForm.get('ClientId')?.value;

    if (selType == 'R') {
      this.processConfigurationForm.get('SubRegionId').setValue('');
      this.processConfigurationForm.get('ClientId').setValue('');
      this.getSubRegion(regionId);

    }
    if (selType == 'S') {
      this.processConfigurationForm.get('ClientId').setValue('');
      this.getClient(regionId, subRegionId);

    }

    // const regionName = regionId
    //   ? this.DIRegions?.find((x) => x.id === regionId)?.name
    //   : '';
    const regionName = regionId
      ? this.dsRegion?.find((x) => x.id === regionId)?.name
      : '';
    this.regionName = regionName;
    // const subRegionName = subRegionId
    //   ? this.DISubRegions?.find((x) => x.id === subRegionId)?.name
    //   : '';
    const subRegionName = subRegionId
      ? this.dsSubRegion?.find((x) => x.id === subRegionId)?.name
      : '';
    this.subRegionName = subRegionName;
    // const clientName = clientId
    //   ? this.DIClientnames?.find((x) => x.id === clientId)?.name
    //   : '';
    const clientName = clientId
      ? this.dsClient?.find((x) => x.id === clientId)?.name
      : '';
    this.clientName = clientName;
    if (selType == 'C') {
      this.processName = `DI_${this.sliceChars(
        regionName,
        0,
        2
      )}${this.sliceChars(subRegionName, 0, 3)}${this.sliceChars(
        clientName,
        0,
        2
      )}`.toUpperCase();

      if (
        this.regionName !== '' &&
        this.subRegionName !== '' &&
        this.clientName !== ''
      ) {
        this.configService
          .checkProcessNameExists(this.processName, this.paramsId)
          .subscribe({
            next: (response) => {
              if (response) {
                if (response.responseCode === 200) {
                  //let num = +this.sliceChars(this.processName,10,70);
                  //this.processName = `${this.processName}${num + 1}`;
                  this.databaseNames = [];
                  this.processName = response.result;
                  this.processConfigurationForm
                    .get('processName')
                    ?.setValue(this.processName);
                  this.processConfigurationForm
                    .get('tableName')
                    ?.setValue(this.processName.replace('DI_', 'tbImport_'));
                  this.processConfigurationForm.get('deltaTableName')?.setValue(this.processName.replace('DI_', 'tbImport_'));
                  this.processConfigurationForm.get('RuleSetName')?.setValue(this.processName.replace('DI_', 'RSN_'));

                  if (this.configurationProcessType === DataSourceType.LandingLayer) {
                    const today = new Date();

                    //get todays year
                    const year = today.getFullYear();
                    const month = String(today.getMonth() + 1).padStart(2, '0');
                    const day = String(today.getDate()).padStart(2, '0');
                    //get todays month
                    //get todays date
                    this.landingLayerAcceptedPath = `landing\\${this.regionName}\\${this.subRegionName}\\${this.clientName}\\${this.processName}\\${year}\\${month}\\${day}`;
                    this.landingLayerRejectedPath = `reject\\${this.regionName}\\${this.subRegionName}\\${this.clientName}\\${this.processName}\\${year}\\${month}\\${day}`;
                    this.processConfigurationForm.get('landingLayerAcceptedPath').setValue(this.landingLayerAcceptedPath);
                    this.processConfigurationForm.get('landingLayerRejectedPath').setValue(this.landingLayerRejectedPath);
                  }
                }
              }
            },
            error: (error) => {
              console.error(error);
            },
          });
      }
      this.processConfigurationForm.get('processName')?.setValue(this.processName);
      this.processConfigurationForm.get('tableName')?.setValue(this.processName.replace('DI_', 'tbImport_'));
      this.processConfigurationForm.get('deltaTableName')?.setValue(this.processName.replace('DI_', 'tbImport_'));
    } else {
      this.processConfigurationForm.get('processName')?.setValue('');
    }
  }

  sliceChars(name: string, start: number, end: number): string {
    if (name) {
      const val = name.replace(/[^a-zA-Z0-9]/g, '');
      return val.slice(start, end);
    }
    return '';
  }
  onEndDateChange(val: any): void {
    if (val && val != '<empty string>') {
      let startTime =
        this.processConfigurationForm.get('scheduledEndTime').value;
      this.endHours = [];
      const today = new Date();
      let todayDate = `${today.getUTCFullYear()}-${String(
        today.getUTCMonth() + 1
      ).padStart(2, '0')}-${String(today.getUTCDate()).padStart(2, '0')}`;

      if (val == todayDate)
        for (let hour = today.getUTCHours() + 1; hour < 24; hour++) {
          this.endHours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
        }
      else
        for (let hour = 0; hour < 24; hour++) {
          this.endHours.push(hour < 10 ? `0${hour}:00` : `${hour}:00`);
        }
      if (!this.endHours.includes(startTime))
        this.processConfigurationForm.get('scheduledEndTime').setValue('');
      //this.checkDate();
    }
  }

  onSubmit(formValue: any) {
    this.isSubmitted = true;
    this.processConfigurationForm.markAllAsTouched();
    if (formValue.scheduledEndDate === '') {
      this.processConfigurationForm.get('scheduledEndDate').setValue(null);
    }
    if (isSharePointProcessType(formValue.processType) && !this.isSharePointSettingsTabValid(true)) {
      return;
    }
    if (!this.processConfigurationForm.valid) {
      const invalidFields = this.getInvalidFields(
        this.processConfigurationForm
      );
      //console.log('Invalid Fields:', invalidFields);
      if (this.paramsId && this.paramsId != '') {
        this.toastr.error(
          'Please fill in all required fields.',
          'Update Process Configuration'
        );
      } else {
        this.toastr.error(
          'Please fill in all required fields.',
          'New Process Configuration'
        );
      }
      return;
    }



    let sDate = '',
      eDate = null;
    if (formValue.scheduledDate) {
      sDate = this.formatSchedulerFormDate(formValue.scheduledDate) ?? '';
    }
    if (formValue.scheduledEndDate) {
      eDate = this.formatSchedulerFormDate(formValue.scheduledEndDate);
    }

    let db_file_columnName_list_array: string[] = [];

    this.recordHeaders.forEach((e, i) => {
      if (e.willInclude) {
        switch (e.DatatypeName.toLowerCase()) {
          case 'time':
          case 'date':
          case 'datetime':
            db_file_columnName_list_array.push(
              `${e.DbColumnName}#${e.ColumnName}=${e.DatatypeName}|${e.dateTimeFormatId}`
            );
            break;
          default:
            db_file_columnName_list_array.push(
              `${e.DbColumnName}#${e.ColumnName}=${e.DatatypeName}`
            );
            break;
        }
      }
    });

    this.db_file_columnName_list = db_file_columnName_list_array.join(',');
    var campaignId = '';
    var internalCampaignId = '';
    if (this.fabService.isFabUserValue) {
      const campaignNameRawValue = this.processConfigurationForm.get('campaignName')?.getRawValue();
      var fabAccount = this.fabService.FABUserAccount.find(x => x.campaignId === campaignNameRawValue);
      campaignId = fabAccount?.campaignId;
      internalCampaignId = fabAccount?.internalCampaignId;
    }
    const regionId = this.fabService.isFabUserValue ? this.processConfigurationForm.get('RegionId')?.getRawValue() : formValue.RegionId;
    const subRegionId = this.fabService.isFabUserValue ? this.processConfigurationForm.get('SubRegionId')?.getRawValue() : formValue.SubRegionId;
    const clientId = this.fabService.isFabUserValue ? this.processConfigurationForm.get('ClientId')?.getRawValue() : formValue.ClientId;
    let data: FileProcessConfig = {
      configurationId:
        this.paramsId == undefined || this.paramsId == ''
          ? null
          : this.paramsId,
      flpConfigurationId: '',
      processName: formValue.processName,
      locationTypeId: resolveLocationTypeId(formValue.processType),
      senderCommunicationEmail: formValue.sender_communication_email,
      createdBy: sessionStorage.getItem('upn')?.split('@')[0],
      userName: sessionStorage.getItem('username'),
      description: formValue.description,
      processTypeId: formValue.processType,
      regionId: regionId, //formValue.RegionId,
      subRegionId: subRegionId, //formValue.SubRegionId,
      clientId: clientId, //formValue.ClientId,
      fileType: 0,
      updateSchedular: this.updateSchedular,
      searchStringInFileName: formValue.search_string_in_file_name,
      serverLocationId:
        formValue.serverLocationId == '' ? 1 : formValue.serverLocationId,
      baseFolderName: formValue.baseFolderName.trim(),
      sourceFolderLocation: formValue.sourceFolderLocation.trim(),
      scheduledId: formValue.scheduledId == '' ? 1 : formValue.scheduledId,
      scheduleValue: '',
      scheduledDate: sDate,
      scheduledTime: formValue.scheduledTime,
      scheduledEndDate: eDate,
      scheduledEndTime: formValue.scheduledEndTime,
      blobStorageAccount:
        formValue.blobStorageAccount == '' ? 1 : formValue.blobStorageAccount,
      blobContainerName: formValue.blobContainerName.trim(),
      blobSourcePath: formValue.blobSourcePath.trim(),
      hourFrequency:
        formValue.hourFrequency == '' || formValue.hourFrequency === null
          ? 0
          : formValue.hourFrequency,
      weekDays: this.selectedDays(formValue.weekDays),
      region: this.regionName,
      subRegion: this.subRegionName,
      clientName: this.clientName,
      dataSource: this.navigateService.configurationProcess,
      deltaSource: formValue?.deltaSource,
      deltaStorageAccountId: String(formValue.deltaStorageAccountId),
      deltaContainerName: formValue.deltaContainerName.trim(),
      securityGroups: this.selectedSecurityGroups,
      campaignId: campaignId,
      internalCampaignId: internalCampaignId,
      ...(isSharePointProcessType(formValue.processType)
        ? sharePointFileProcessFields(this.sharePointTab?.selection() ?? this.sharePointCachedSelection)
        : {}),
      fileConfigurations: [
        {
          flpConfigurationId: '',
          delimiter: formValue.delimiter,
          quoteCharacter: formValue.txtQuoteCharacter,
          isHeaderProvided: formValue.flexCheckHasHeaders ?? false,
          //isHeaderProvided: true,
          skipRows: formValue.skipRows == '' ? 0 : formValue.skipRows,
          skipFooterRows:
            formValue.skipFooterRows == '' ? 0 : formValue.skipFooterRows,
          keyColumnList: formValue.key_column_list.toUpperCase(),
          columnNameList: formValue.column_name_list.toUpperCase(),
          convertDatatypesColumnList:
            formValue.convert_datatypes_column_list.toUpperCase(), //'',//need to check
          dedup: formValue.dedup?.toUpperCase() ?? '',
          ignoreDuplicateRows: formValue.ignore_duplicate_rows ?? false,
          doNotArchiveFile: formValue.do_not_archive_file ?? false,
          keepFirstRow:
            formValue.order_by_column_list_name_sort_dir === 'asc'
              ? true
              : false,
          db_file_column_name_list: this.db_file_columnName_list,
          spanishToEnglish: formValue.spanish_to_english,
          romanNumeralsOnly: formValue.roman_numerals_only,
          skipEmptyLines: formValue.flexCheckSkipEmptyLines,
          tabName: this.tabName,
          landingLayerFileExtension: formValue.landingLayerFileExtension,
          landingLayerRegex: this.regexList,
          landingLayerPrefix: formValue.landingLayerPrefix,
          dateFormatId: formValue.landingLayerDateformat,
          timeFormatId: formValue.landingLayerTimeformat
        },
      ],
      configurationTableMappings: [
        {
          flpConfigurationId: '',
          tableName: formValue.tableName,
          databaseConfigurationId: formValue.databaseConfigurationId,
          dropMainTable: formValue.drop_main_table ?? false,
          validateFileSchema:
            formValue.is_validate_fileschema_with_target_table ?? false,
          dropHistoryTable: formValue.drop_history_table ?? false,
          mergeData: formValue.mergeData,
          createHistoryTable: formValue.createHistoryTable,
          deltaJobId: ' ',
          tabName: this.tabName,
          deltaSource: formValue?.deltaSource,
          landingLayerAcceptedPath: formValue?.landingLayerAcceptedPath,
          landingLayerRejectedPath: formValue?.landingLayerRejectedPath
        },
      ],
    };
    //this.configurationProcessType
    // if(this.navigateService.configurationProcess === DataSourceType.DataBricks){
    if (this.configurationProcessType === DataSourceType.DataBricks) {
      data.configurationTableMappings[0].tableName = formValue.deltaTableName;
      data.configurationTableMappings[0].databaseConfigurationId = formValue.deltaServerNameId;
      data.configurationTableMappings[0].deltaJobId = formValue.deltaJobId;
      data.deltaSource = formValue.deltaSource;
    } else if (this.navigateService.configurationProcess === DataSourceType.LandingLayer) {
      // data.fileConfigurations[0].dateFormatId = this.processConfigurationForm.get('landingLayerDateformat').getRawValue();
      // data.fileConfigurations[0].timeFormatId = this.processConfigurationForm.get('landingLayerTimeformat').getRawValue();
      data.configurationTableMappings[0].databaseConfigurationId = 8; //default to 8
      data.configurationTableMappings[0].landingLayerAcceptedPath = formValue.landingLayerAcceptedPath;
      data.configurationTableMappings[0].landingLayerRejectedPath = formValue.landingLayerRejectedPath;
    }

    var apiVersion = this.configurationProcessType === DataSourceType.LandingLayer ? "4.1" : "1.0";
    this.configService.InsertFlpConfiguration(data, apiVersion).subscribe({
      next: (response: APIResponse<any | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              const result = response.result;
              let flpConfigurationId = response.result;
              if (this.paramsId && this.paramsId != '') {
                flpConfigurationId = this.paramsId;
                this.toastr.success(
                  'Process has been modified successfully!',
                  'Update Process Configuration'
                );
              } else {

                this.toastr.success(
                  'New process has been added/configured successfully!',
                  'New Process Configuration'
                );
              }

              if (this.ruleSetPayLoad?.ruleSets.length > 0) {
                //add more modification
                this.ruleSetPayLoad.ruleSets.forEach(rule => {
                  rule.isGlobal = false;
                  rule.ruleSetType = 1;
                });

                this.dataInsiderService.insertRuleSets(this.ruleSetPayLoad, flpConfigurationId, this.tabName).subscribe({
                  next: (response: APIResponse<boolean>) => {
                  },
                  error: error => {
                    console.log(error);
                    this.toastr.error(ToastrMessages.SomethingWentWrong);
                  }
                });
              }
              this.router.navigate(['/process-config-list']);
            } else {
              this.toastr.error(response.responseMessage[0]);
            }
        } else {
          this.toastr.error(response.responseMessage[0]);
        }
      },
      error: (error) => {
        this.toastr.error(
          'An error occurred while configuring/adding the new process!',
          'New Process Configuration'
        );
        console.log(error);
      },
    });
  }
  populateForm() {
    if (this.formValues) {
      if (this.formValues.flpConfigurationId) {
        this.processConfigurationForm.disable();
        // this.processConfigurationForm.get('processName').disable();
        this.processConfigurationForm.get('delimiter').enable();
        // this.processConfigurationForm.get('description').disable();
        // this.processConfigurationForm.get('flexCheckHasHeaders').disable();
      }
      this.processConfigurationForm
        .get('processName')
        ?.setValue(this.formValues.processName);
      this.processConfigurationForm
        .get('processType')
        ?.setValue(this.formValues.processType);
      this.processConfigurationForm
        .get('description')
        ?.setValue(this.formValues.description);

      this.delimiter = this.formValues.delimiter;
      if (this.delimiter === '\\t') {
        this.disableDelimiter = true;
      }
      this.processConfigurationForm
        .get('delimiter')
        ?.setValue(this.formValues.delimiter);
      this.processConfigurationForm
        .get('flexCheckSkipEmptyLines')
        .setValue(this.formValues.flexCheckSkipEmptyLines);
      this.processConfigurationForm
        .get('flexCheckHasHeaders')
        .setValue(this.formValues.flexCheckHasHeaders);
      this.processConfigurationForm
        .get('txtQuoteCharacter')
        .setValue(this.formValues.txtQuoteCharacter);
      this.processConfigurationForm
        .get('is_active')
        .setValue(this.formValues.is_active);
      this.processConfigurationForm
        .get('do_not_archive_file')
        .setValue(this.formValues.do_not_archive_file);
      this.processConfigurationForm
        .get('ignore_duplicate_rows')
        .setValue(this.formValues.ignore_duplicate_rows);
      this.processConfigurationForm
        .get('order_by_column_list_for_dedup')
        .setValue(this.formValues.flexCheckOrderByColumnListForDedup);
      if (this.formValues.ignore_duplicate_rows) {
        this.formValues.csv_column_name_list = this.keyColumnsSelected;
        this.formValues.keep_first_row =
          this.formValues.order_by_column_list_name_sort_dir === 'asc'
            ? true
            : false;
      }

      this.processConfigurationForm
        .get('order_by_column_list_name')
        .setValue(this.formValues.order_by_column_list_name);
      this.processConfigurationForm
        .get('order_by_column_list_name_sort_dir')
        .setValue(
          this.formValues.order_by_column_list_name_sort_dir === ''
            ? 'desc'
            : this.formValues.order_by_column_list_name_sort_dir
        );

      this.processConfigurationForm
        .get('RegionId')
        .setValue(this.formValues.RegionId);
      this.processConfigurationForm
        .get('SubRegionId')
        .setValue(this.formValues.SubRegionId);
      this.processConfigurationForm
        .get('ClientId')
        .setValue(this.formValues.ClientId);

      this.processConfigurationForm
        .get('tableName')
        .setValue(this.formValues.tableName);
      this.processConfigurationForm
        .get('databaseConfigurationId')
        .setValue(this.formValues.databaseConfigurationId);
      this.processConfigurationForm
        .get('drop_history_table')
        .setValue(this.formValues.drop_history_table);
      this.processConfigurationForm
        .get('drop_main_table')
        .setValue(this.formValues.drop_main_table);
      this.processConfigurationForm
        .get('is_validate_fileschema_with_target_table')
        .setValue(this.formValues.is_validate_fileschema_with_target_table);

      this.processConfigurationForm
        .get('serverLocationId')
        .setValue(this.formValues.serverLocationId);
      this.processConfigurationForm
        .get('baseFolderName')
        .setValue(this.formValues.baseFolderName);
      this.processConfigurationForm
        .get('sourceFolderLocation')
        .setValue(this.formValues.sourceFolderLocation);

      this.processConfigurationForm
        .get('scheduledId')
        .setValue(this.formValues.scheduledId);
      this.processConfigurationForm
        .get('scheduledDate')
        .setValue(this.formValues.scheduledDate);
      this.processConfigurationForm
        .get('scheduledTime')
        .setValue(this.formValues.scheduledTime);
      this.processConfigurationForm
        .get('blobStorageAccount')
        .setValue(this.formValues.blobStorageAccount);
      this.processConfigurationForm
        .get('blobContainerName')
        .setValue(this.formValues.blobContainerName);
      this.processConfigurationForm
        .get('blobSourcePath')
        .setValue(this.formValues.blobSourcePath);
      this.processConfigurationForm
        .get('search_string_in_file_name')
        .setValue(this.formValues.search_string_in_file_name);
      this.processConfigurationForm
        .get('key_column_list')
        .setValue(this.formValues.key_column_list);
      this.processConfigurationForm
        .get('column_name_list')
        .setValue(this.formValues.column_name_list);
      this.processConfigurationForm
        .get('sender_communication_email')
        .setValue(this.formValues.sender_communication_email);

      this.processConfigurationForm.markAllAsTouched();
      this.processConfigurationForm.updateValueAndValidity();
    }
  }

  getAllDIRegions() {
    this.configService.getAllDIRegions().subscribe({
      next: (response: APIResponse<DIRegions[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DIRegions = response.result;
        }
      },
    });
  }

  getAllDISubRegions() {
    this.configService.getAllDISubRegions().subscribe({
      next: (response: APIResponse<DISubRegions[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DISubRegions = response.result;
        }
      },
    });
  }

  getAllDIClientnames() {
    this.configService.getAllDIClientnames().subscribe({
      next: (response: APIResponse<DIClientNames[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DIClientnames = response.result;
        }
      },
    });
  }
  onDelimiterChanged(event: any) {
    if (event.target.innerText === 'Other') {
      this.processConfigurationForm.get('delimiter')?.setValue('');
      this.delimiterInput?.nativeElement.focus();
    } else {
      let val = event.target.innerText;
      switch (event.target.innerText) {
        case 'comma (,)':
          val = ',';
          break;
        case 'tab (\\t)':
          val = '\\t';
          break;
        case 'pipe (|)':
          val = '|';
          break;
        case 'semicolon (;)':
          val = ';';
          break;
        default:
          break;
      }
      this.processConfigurationForm.get('delimiter')?.setValue(val);
      this.paramChangedForFilePreview = true;
      if (this.filePreview) {
        this.filePreviewValues.delimiter = val;
        this.filePreviewValues.paramChanged = this.paramChangedForFilePreview;
        this.filePreviewComponent.updateFilePreview();
      }
      //this.formValues.delimiter = val;
    }
    //this.previewToParent.emit(this.formValues);
  }
  paramChangedForFilePreview: boolean = false;
  onHasHeadersChange(event: any) {
    this.processConfigurationForm
      .get('flexCheckHasHeaders')
      .setValue(event.target.checked);
    //this.formValues.flexCheckHasHeaders = event.target.checked;
    this.paramChangedForFilePreview = true;
    if (this.filePreview) {
      this.filePreviewValues.hasHeaders = event.target.checked;
      this.filePreviewValues.paramChanged = this.paramChangedForFilePreview;
      this.filePreviewComponent.updateFilePreview();
    }
  }

  onOrderByColumnListForDedup(event: any) {
    event.target.checked
      ? this.processConfigurationForm.get('order_by_column_list_name').enable()
      : this.processConfigurationForm
        .get('order_by_column_list_name')
        .disable();
  }
  onLocationSource(): void {
    const src = this.processConfigurationForm.get(
      'sourceFolderLocation'
    )?.value;
    // if (src && !src.endsWith('\\')) {
    //   this.processConfigurationForm
    //     .get('sourceFolderLocation')
    //     .setValue(`${src}\\`);
    // }
    if (src) {
      this.processConfigurationForm.get('sourceFolderLocation').setValue(cleanSourceLocation(src));
    }
  }

  onFoderNameChange(event: any): void {
    //event.target.value = event.target.value.trim();
    this.processConfigurationForm.get('baseFolderName').setValue(event.target.value.trim());
  }

  onDeltaContainerNameChange(event: any): void {
    this.processConfigurationForm.get('deltaContainerName').setValue(event.target.value.trim());
  }

  onBlobContainerNameChange(event: any): void {
    this.processConfigurationForm.get('blobContainerName').setValue(event.target.value.trim());
  }
  onCloudSource(): void {
    //if (this.navigateService.configurationProcess === DataSourceType.Default) {
    const blobSourcePath = this.processConfigurationForm.get('blobSourcePath')?.value;
    if (blobSourcePath) { //&& !blobSourcePath.endsWith('/')
      this.processConfigurationForm.get('blobSourcePath').setValue(cleanBlobSourceLocation(blobSourcePath));
      //this.processConfigurationForm.get('blobSourcePath').setValue(`${blobSourcePath}/`);
    }
    //} else if (this.navigateService.configurationProcess === DataSourceType.DataBricks) {
    const deltaSource = this.processConfigurationForm.get('deltaSource')?.value;
    if (deltaSource) { //&& !deltaSource.endsWith('/')
      //this.processConfigurationForm.get('deltaSource').setValue(`${deltaSource}/`);
      this.processConfigurationForm.get('deltaSource').setValue(cleanBlobSourceLocation(deltaSource));
    }
    //}

  }
  // clientTabIsValid : boolean = false;
  onTabChanged(tabName: any, btnNextClicked: boolean) {

    const notificationSettingsTab = document.getElementById(
      'notification-settings-tab'
    ) as HTMLElement;

    this.modifySettingsClose(null);

    this.activeTab = tabName;
    if (tabName === this.sharePointSettingsTabId) {
      this.sharePointCachedSelection = this.sharePointTab?.selection() ?? this.sharePointCachedSelection;
    }
    this.databaseDestinationSettingsError = '';
    let error = false;
    let regionId = this.processConfigurationForm.get('RegionId')?.value;
    let subRegionId = this.processConfigurationForm.get('SubRegionId')?.value;
    let clientNameId = this.processConfigurationForm.get('ClientId')?.value;

    if (tabName === 'client-tab') {
      if (!regionId || !subRegionId || !clientNameId) {
        // this.clientTabIsValid = false;
        this.toastr.error(
          'Please complete Client Settings.',
          ModalTitles.TPDataIngestion
        );
        return;
      }

      if (btnNextClicked) {
        // this.clientTabIsValid = true;
        const processTab = document.getElementById(
          'process-tab'
        ) as HTMLElement;
        processTab.click();
        return;
      }
    }

    if (
      tabName === 'addtional-setting-tab' ||
      tabName === 'database-settings-tab' ||
      tabName == 'location-settings-tab' ||
      tabName == 'blob-settings-tab' ||
      tabName == SHAREPOINT_SETTINGS_TAB_ID ||
      tabName == 'schedular-settings-tab' ||
      tabName == 'notification-settings-tab' ||
      tabName == 'process-tab'
    ) {
      if (!regionId || !subRegionId || !clientNameId) {
        this.activeTab = 'client-tab';
        if (tabName != 'client-tab') {
          const clientTab = document.getElementById(
            'client-tab'
          ) as HTMLElement;
          clientTab.click();
          this.toastr.error(
            'Please complete Client Settings.',
            ModalTitles.TPDataIngestion
          );
        }
        this.processConfigurationForm.get('RegionId').markAsTouched();
        this.processConfigurationForm.get('SubRegionId').markAsTouched();
        this.processConfigurationForm.get('ClientId').markAsTouched();
        return;
      } else {
        if (tabName === 'process-tab' && this.databaseNames.length === 0 || (this.paramsId && this.paramsId != '')) {
          if (this.configurationProcessType === DataSourceType.Default) {
            this.clientSettingsValuesValid = true;
            this.isDatabaseNamesLoading = true;
            this.configService
              .getDIDatabaseNames(regionId, subRegionId, clientNameId, this.configurationProcessType)
              .subscribe({
                next: (res: APIResponse<DIDatabaseNames[]>) => {
                  //console.log(res);

                  if (res.responseCode === 200) {
                    this.databaseNames = [];
                    res.result.forEach(x => {
                      if (x.defaultDB === true) {
                        this.databaseNames.push({ id: x.id, databaseName: x.databaseName, defaultDB: x.defaultDB, databaseServer: x.databaseServer, groupBy: 'Default' })
                      } else {
                        this.databaseNames.push({ id: x.id, databaseName: x.databaseName, defaultDB: x.defaultDB, databaseServer: x.databaseServer, groupBy: 'Exclusive' })
                      }
                    });

                    this.databaseNames.sort((a, b) => {
                      if (a.groupBy === 'Default' && b.groupBy !== 'Default') return -1;
                      if (a.groupBy !== 'Default' && b.groupBy === 'Default') return -1;
                      return 0;
                    });
                    //this.databaseNames = res.result;
                    if (res.result.length >= 1) {




                      //this.databaseNameId = this.databaseNames[0].id;
                      if (this.paramsId && this.paramsId !== '') {
                        if (this.processConfigurationForm.get('databaseConfigurationId').touched === false) {
                          let foundDb = this.databaseNames.find(x => x.id === this.configurationDetails.configurationTableMappingDetails[0].databaseConfigurationId);
                          this.processConfigurationForm.get('databaseName')?.setValue(foundDb.databaseName);
                          this.processConfigurationForm.get('databaseConfigurationId').setValue(foundDb.id);
                        }
                      } else {
                        if (this.processConfigurationForm.get('databaseConfigurationId').touched === false) {
                          this.processConfigurationForm.get('databaseName')
                            ?.setValue(this.databaseNames.filter(x => x.groupBy === 'Default')[0].databaseName);
                          this.processConfigurationForm.get('databaseConfigurationId')
                            .setValue(this.databaseNames.filter(x => x.groupBy === 'Default')[0].id);
                        }
                      }
                    }
                  }
                  this.isDatabaseNamesLoading = false;
                },
                error: error => {
                  console.log('Something went wrong: ' + error);
                  this.isDatabaseNamesLoading = false;
                }
              });
          } else if (this.configurationProcessType === DataSourceType.DataBricks) {
            this.clientSettingsValuesValid = true;

            this.configService
              .getDIDatabaseNames(regionId, subRegionId, clientNameId, this.configurationProcessType)
              .subscribe({
                next: (res: APIResponse<DIDatabaseNames[]>) => {
                  //console.log(res);
                  if (res.responseCode === 200) {
                    this.databaseNames = [];
                    res.result.forEach(x => {
                      if (x.defaultDB === true) {
                        this.databaseNames.push({ id: x.id, databaseName: x.databaseName, defaultDB: x.defaultDB, databaseServer: x.databaseServer, groupBy: 'Default' })
                      } else {
                        this.databaseNames.push({ id: x.id, databaseName: x.databaseName, defaultDB: x.defaultDB, databaseServer: x.databaseServer, groupBy: 'Exclusive' })
                      }
                    });

                    this.databaseNames.sort((a, b) => {
                      if (a.groupBy === 'Default' && b.groupBy !== 'Default') return -1;
                      if (a.groupBy !== 'Default' && b.groupBy === 'Default') return -1;
                      return 0;
                    });
                    // this.databaseNames = res.result;
                    if (res.result.length >= 1) {
                      //this.databaseNameId = this.databaseNames[0].id;
                      if (this.paramsId && this.paramsId !== '') {
                        if (this.processConfigurationForm.get('deltaServerNameId').touched === false) {
                          let foundDb = this.databaseNames.find(x => x.id === this.configurationDetails.configurationTableMappingDetails[0].databaseConfigurationId);
                          this.processConfigurationForm.get('databaseName')?.setValue(foundDb.databaseName);
                          this.processConfigurationForm.get('deltaServerNameId').setValue(foundDb.id);
                        }
                      } else {
                        if (this.processConfigurationForm.get('deltaServerNameId').touched === false) {
                          let foundDb = this.databaseNames.find(x => x.defaultDB);
                          let dbName = '';
                          let dbNameId = 0;
                          if (foundDb) {
                            dbName = foundDb.databaseName;
                            dbNameId = foundDb.id
                          } else {
                            dbName = this.databaseNames[0].databaseName;
                            dbNameId = this.databaseNames[0].id;
                          }
                          this.processConfigurationForm.get('databaseName')?.setValue(dbName);
                          this.processConfigurationForm.get('deltaServerNameId').setValue(dbNameId);
                        }
                      }
                    }
                  }
                },
              });
          }
        }


        //if (this.processConfigurationForm.get('processType')?.touched) {
        if (tabName === 'process-tab') {
          if (this.processConfigurationForm.get('processType')?.value !== '' &&
            this.processConfigurationForm.get('security_group')?.value.length !== 0
            && btnNextClicked) {
            const addtionalSettingTab = document.getElementById(
              'addtional-setting-tab'
            ) as HTMLElement;
            addtionalSettingTab.click();
            return;
          } else {
            if (btnNextClicked) {
              this.processConfigurationForm.get('processType')?.markAsTouched();
              this.processConfigurationForm.get('security_group')?.markAsTouched();
              this.toastr.error(
                'Please complete Additional Settings.',
                ModalTitles.TPDataIngestion
              );
              return;
            }
          }
          //}
        }

        if (tabName === 'addtional-setting-tab' && btnNextClicked) {
          if (this.isAdditionalSettingsTabValid()) {
            const clientTab = document.getElementById(
              'addtional-setting-tab'
            ) as HTMLElement;
            clientTab.click();
            this.toastr.error('Please complete the additional settings');
            return;
          } else {
            const databaseSettingsTab = document.getElementById(
              'database-settings-tab'
            ) as HTMLElement;
            databaseSettingsTab.click();
          }
        }
      }
    }
    if (
      // tabName === 'client-tab' ||
      tabName === 'database-settings-tab' ||
      tabName == 'location-settings-tab' ||
      tabName == 'blob-settings-tab' ||
      tabName == SHAREPOINT_SETTINGS_TAB_ID ||
      tabName == 'schedular-settings-tab' ||
      tabName == 'notification-settings-tab'
    ) {
      //this.getStorageAccountDetails(); //this has already been called in ngOnit - removed by wbq 10/22/2025
      if (this.isAdditionalSettingsTabValid()) {
        //if (error) {
        const clientTab = document.getElementById(
          'addtional-setting-tab'
        ) as HTMLElement;
        clientTab.click();
        this.toastr.error('Please complete the additional settings');
        return;
      }
      if (tabName === 'database-settings-tab') {
        if (this.processConfigurationForm.get('deltaContainerName')?.hasError('required')
          && (this.processConfigurationForm.get('deltaContainerName')?.touched || this.processConfigurationForm.get('deltaContainerName')?.dirty)) {
          this.databaseDestinationSettingsError = ModalMessages.NoContainerName;
        }
      }
      if (tabName === 'blob-settings-tab') {
        if (this.processConfigurationForm.get('blobContainerName')?.hasError('required')
          && (this.processConfigurationForm.get('blobContainerName')?.touched || this.processConfigurationForm.get('blobContainerName')?.dirty)) {
          this.databaseDestinationSettingsError = ModalMessages.NoContainerName;
        }
      }

      if (tabName === 'database-settings-tab' && btnNextClicked) {
        if (!this.isDatabaseSettingsTabValid()) {
          return;
        } else {
          if (this.processConfigurationForm.get('processType')?.value === 2) {
            const locationSettingsTab = document.getElementById(
              'location-settings-tab'
            ) as HTMLElement;
            locationSettingsTab.click();
            return;
          } else if (this.processConfigurationForm.get('processType')?.value === 3) {
            const blobSettingsTab = document.getElementById(
              'blob-settings-tab'
            ) as HTMLElement;
            blobSettingsTab.click();
            return;
          } else if (isSharePointProcessType(this.processConfigurationForm.get('processType')?.value)) {
            activateSharePointSettingsTab();
            return;
          } else {

            notificationSettingsTab.click();
            return;
          }
        }
      }
    }

    if (
      tabName == 'location-settings-tab' ||
      tabName == 'blob-settings-tab' ||
      tabName == SHAREPOINT_SETTINGS_TAB_ID ||
      tabName == 'schedular-settings-tab' ||
      tabName == 'notification-settings-tab'
    ) {
      this.isDatabaseSettingsTabValid();

      if ((tabName === 'location-settings-tab' || tabName === 'blob-settings-tab' || tabName === SHAREPOINT_SETTINGS_TAB_ID) && btnNextClicked) {
        if (!this.isSharedLocationSettingsTabValid()) {
          return;
        } else {

          //this.getWeekDayName();
          const schedularSettingsTab = document.getElementById(
            'schedular-settings-tab'
          ) as HTMLElement;
          schedularSettingsTab.click();
          return;
        }
      }
    }

    if (
      tabName == 'schedular-settings-tab' ||
      tabName == 'notification-settings-tab'
    ) {
      this.isSharedLocationSettingsTabValid();

      if (tabName === 'schedular-settings-tab' && btnNextClicked) {
        if (!this.isSchedularSettingsTabValid()) {
          return;
        } else {
          notificationSettingsTab.click();
        }
      }
    }
    if (
      tabName == 'schedular-settings-tab' ||
      tabName == 'notification-settings-tab'
    ) {
      if (this.processConfigurationForm.get('processType')?.value == '3') {
        if (
          !this.processConfigurationForm.get('blobStorageAccount')?.value ||
          !this.processConfigurationForm.get('blobContainerName')?.value ||
          !this.processConfigurationForm.get('blobSourcePath')?.value
        ) {
          const clientTab = document.getElementById(
            'blob-settings-tab'
          ) as HTMLElement;
          clientTab.click();
          this.toastr.error('Please complete the blob storage settings');
          this.processConfigurationForm
            .get('blobStorageAccount')
            .markAsTouched();
          this.processConfigurationForm
            .get('blobContainerName')
            .markAsTouched();
          this.processConfigurationForm.get('blobSourcePath').markAsTouched();
          return;
        }
      }
      if (isSharePointProcessType(this.processConfigurationForm.get('processType')?.value)) {
        if (!this.isSharePointSettingsTabValid()) {
          activateSharePointSettingsTab();
          return;
        }
      }
    }
    if (tabName == 'notification-settings-tab') {
      this.isSchedularSettingsTabValid();

      const ctrl = this.processConfigurationForm.controls;

      for (const name in ctrl) {
        if (ctrl[name].invalid) {
          //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
          console.log(name + ' is invalid');
          return;
          //break;
        }
      }
    }


  }

  isAdditionalSettingsTabValid(): boolean {
    let error = false;
    if (this.configurationProcessType === DataSourceType.LandingLayer) {


      var ctrl = this.processConfigurationForm.get('landingLayerFileExtension');

      var isEmpty =
        ctrl?.value == null ||
        (Array.isArray(ctrl.value) && ctrl.value.length === 0);

      if (isEmpty) {
        ctrl?.markAsTouched();
        ctrl?.markAsDirty();
        ctrl?.updateValueAndValidity({ onlySelf: true });

        error = true;
      }

      error = this.validateNamingConvention('landingLayerPrefix', 'landingLayerPrefixCheckbox') || error;
      error = this.validateNamingConvention('landingLayerDateformat', 'landingLayerDateformatCheckbox') || error;
      error = this.validateNamingConvention('landingLayerTimeformat', 'landingLayerTimeformatCheckBox') || error;

      return error;
    }
    if (
      this.processConfigurationForm.get('ignore_duplicate_rows')?.value == true
    ) {
      if (
        !this.processConfigurationForm.get('key_column_list')?.value ||
        this.processConfigurationForm.get('key_column_list')?.value == ''
      ) {
        error = true;
        this.processConfigurationForm.get('key_column_list').markAsTouched();
      }
    }
    if (
      !this.processConfigurationForm.get('column_name_list')?.value ||
      this.processConfigurationForm.get('column_name_list')?.value == ''
    ) {
      error = true;
      this.processConfigurationForm.get('column_name_list').markAsTouched();
    }

    if (
      !this.processConfigurationForm.get('db_column_name_list')?.value ||
      this.processConfigurationForm.get('db_column_name_list')?.value == ''
    ) {
      error = true;
      this.processConfigurationForm.get('db_column_name_list').markAsTouched();
    }

    if (
      !this.processConfigurationForm.get('convert_datatypes_column_list')?.value ||
      this.processConfigurationForm.get('convert_datatypes_column_list')?.value == ''
    ) {
      error = true;
      this.processConfigurationForm
        .get('convert_datatypes_column_list')
        .markAsTouched();
    }

    return error;
  }

  private validateNamingConvention(
    controlName: string,
    checkboxName: string
  ): boolean {
    const control = this.processConfigurationForm.get(controlName);
    const checkbox = this.processConfigurationForm.get(checkboxName);

    if (!checkbox || !control) return false; // safety check

    if (checkbox.value === true && (control.value === '' || control.value == null)) {
      control.markAsTouched();
      return true; // error found
    }

    return false; // no error
  }

  isDatabaseSettingsTabValid(): boolean {
    const databaseSettingsTab = document.getElementById(
      'database-settings-tab'
    ) as HTMLElement;

    if (this.configurationProcessType === DataSourceType.Default) {
      if (
        !this.processConfigurationForm.get('databaseConfigurationId')?.value ||
        !this.processConfigurationForm.get('tableName')?.value
      ) {
        this.processConfigurationForm
          .get('databaseConfigurationId')
          .markAsTouched();
        this.processConfigurationForm.get('tableName').markAsTouched();
        this.toastr.error('Please complete the database settings');

        databaseSettingsTab.click();

        return false;
      }


      // if (
      //   this.recordHeaders.filter(x => x.willInclude === false).length > 0 &&
      //   this.processConfigurationForm.get('is_validate_fileschema_with_target_table').value === false
      // ) {
      //   this.toastr.error('An excluded column is found! Please check the Validate Schema in Database Settings');
      //   clientTab.click();
      // }


    } else if (this.configurationProcessType === this.DataSourceTypes.DataBricks) {
      if (
        !this.processConfigurationForm.get('deltaStorageAccountId')?.value ||
        !this.processConfigurationForm.get('deltaContainerName')?.value ||
        this.processConfigurationForm.get('deltaContainerName')?.hasError('pattern') ||
        !this.processConfigurationForm.get('deltaSource')?.value ||
        this.processConfigurationForm.get('deltaSource')?.hasError('pattern') ||
        this.processConfigurationForm.get('deltaJobId')?.hasError('pattern') ||
        !this.processConfigurationForm.get('deltaTableName')?.value ||
        this.processConfigurationForm.get('deltaTableName')?.hasError('pattern') ||
        !this.processConfigurationForm.get('deltaServerNameId')?.value ||
        !this.processConfigurationForm.get('deltaJobId')?.value
      ) {
        this.processConfigurationForm.get('deltaStorageAccountId').markAsTouched();
        this.processConfigurationForm.get('deltaContainerName').markAsTouched();
        this.processConfigurationForm.get('deltaSource').markAsTouched();
        this.processConfigurationForm.get('deltaTableName').markAsTouched();
        this.processConfigurationForm.get('deltaServerNameId').markAsTouched();
        this.processConfigurationForm.get('deltaJobId').markAsTouched();
        this.toastr.error('Please complete the Destination settings');

        databaseSettingsTab.click();

        return false;
      }


    }

    if (this.configurationProcessType === DataSourceType.LandingLayer) {
      if (!this.processConfigurationForm.get('deltaStorageAccountId')?.value ||
        !this.processConfigurationForm.get('deltaContainerName')?.value ||
        this.processConfigurationForm.get('deltaContainerName')?.hasError('pattern') ||
        !this.processConfigurationForm.get('landingLayerAcceptedPath')?.value ||
        !this.processConfigurationForm.get('landingLayerRejectedPath')?.value) {
        this.processConfigurationForm.get('deltaStorageAccountId').markAsTouched();
        this.processConfigurationForm.get('deltaContainerName').markAsTouched();
        this.processConfigurationForm.get('landingLayerAcceptedPath').markAsTouched();
        this.processConfigurationForm.get('landingLayerRejectedPath').markAsTouched();
        this.toastr.error('Please complete the Destination settings');
        databaseSettingsTab.click();
        return false;
      }
    }

    return true;
  }

  isSharedLocationSettingsTabValid(): boolean {
    if (this.processConfigurationForm.get('processType')?.value == '2') {
      if (
        !this.processConfigurationForm.get('serverLocationId')?.value ||
        !this.processConfigurationForm.get('baseFolderName')?.value ||
        !this.processConfigurationForm.get('sourceFolderLocation')?.value
      ) {
        const clientTab = document.getElementById(
          'location-settings-tab'
        ) as HTMLElement;
        clientTab.click();
        this.toastr.error('Please complete the shared location settings');
        this.processConfigurationForm.get('serverLocationId').markAsTouched();
        this.processConfigurationForm.get('baseFolderName').markAsTouched();
        this.processConfigurationForm
          .get('sourceFolderLocation')
          .markAsTouched();
        return false;
      }
    }

    if (isSharePointProcessType(this.processConfigurationForm.get('processType')?.value)) {
      if (!this.isSharePointSettingsTabValid(true)) {
        activateSharePointSettingsTab();
        return false;
      }
    }

    return true;
  }

  // #region Sharepoint Workspace - AY
  isSharePointSettingsTabValid(showToast = false): boolean {
    if (!isSharePointProcessType(this.processConfigurationForm.get('processType')?.value)) {
      return true;
    }
    const live = this.sharePointTab?.selection() ?? null;
    if (live?.sharePointApplicationId && live?.sharePointLibraryName) {
      this.sharePointCachedSelection = live;
      return true;
    }
    if (this.sharePointCachedSelection?.sharePointApplicationId && this.sharePointCachedSelection?.sharePointLibraryName) {
      return true;
    }
    if (showToast) {
      this.sharePointTab?.validateSelection(true);
    }
    return false;
  }

  onSharePointSelectionChange(selection: ProcessConfigSharePointSelection | null): void {
    this.sharePointCachedSelection = selection;
  }
  // #endregion

  isSchedularSettingsTabValid(): boolean {
    if (this.processConfigurationForm.get('processType')?.value != '1') {
      if (
        !this.processConfigurationForm.get('scheduledId')?.value ||
        !this.processConfigurationForm.get('scheduledDate')?.value ||
        !this.processConfigurationForm.get('scheduledTime')?.value
      ) {
        const clientTab = document.getElementById(
          'schedular-settings-tab'
        ) as HTMLElement;
        this.toastr.error('Please complete the schedular settings');
        this.processConfigurationForm.get('scheduledId').markAsTouched();
        this.processConfigurationForm.get('scheduledDate').markAsTouched();
        this.processConfigurationForm.get('scheduledTime').markAsTouched();
        clientTab.click();
        return false;
      }


      if (this.processConfigurationForm.get('scheduledId')?.value === 2) {
        if (!this.processConfigurationForm.get('hourFrequency')?.value) {
          this.processConfigurationForm.get('hourFrequency')?.markAsTouched();
          return false;
        }
        //if (this.weekDays.length === 0) {
        let hasWeekDaysSelected = false;
        let counter = 0;
        this.processConfigurationForm.get('weekDays')?.value.forEach(element => {
          if (!element) {
            counter++;
          }
        });
        if (counter === this.processConfigurationForm.get('weekDays')?.value.length) {
          this.processConfigurationForm.get('weekDays')?.markAllAsTouched();
          return false;
        }
        //}
      }
      return true;
    }

    return true;
  }

  validateFormControl = (controlName: string) => {
    return (
      this.processConfigurationForm.get(controlName)?.invalid &&
      (this.processConfigurationForm.get(controlName)?.touched ||
        this.processConfigurationForm.get(controlName)?.dirty)
    );
  };
  hasError = (controlName: string, errorName: string) => {
    return this.processConfigurationForm.get(controlName)?.hasError(errorName);
  };

  ngOnDestroy(): void {
    if (this.keyColumnsSubscription) this.keyColumnsSubscription.unsubscribe();
  }
  selectedDays(days: any) {
    if (days) {
      return days
        .map((checked: any, i: number) => (checked ? this.weekDays[i].id : null))
        .filter((val: any) => val !== null);
    } else {
      return [];
    }
  }

  getAllInvalidCtrls() {
    const ctrl = this.processConfigurationForm.controls;
    const invalid = [];
    for (const name in ctrl) {
      if (ctrl[name].invalid) {
        invalid.push(name);
        //break;
      }
    }

    console.log(invalid);
  }
  validateProcessName(): AsyncValidatorFn {
    return (control: AbstractControl) => {
      return control.valueChanges.pipe(
        debounceTime(1000),
        take(1),
        switchMap(() => {
          return this.configService
            .checkProcessNameExists(control.value, this.paramsId)
            .pipe(
              map((result) => {
                return result.result ? { processNameExist: true } : null;
              }),
              finalize(() => control.markAsTouched())
            );
        })
      );
      // return this.configService
      //   .checkProcessNameExists(control.value, this.paramsId)
      //   .pipe(
      //     map((result) => {
      //       return result.result ? { processNameExist: true } : null;
      //     }),
      //     finalize(() => control.markAsTouched())
      //   );
    };
  }

  validateWhitespace(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const isWhitespace = control.value.trim().length === 0;
      return isWhitespace ? { whitespace: true } : null;
    };
  }
  getDate(date: any) {
    try {
      if (date) {
        const [year, month, day] = date.split('-');
        return new Date(
          parseInt(year, 10),
          parseInt(month, 10) - 1,
          parseInt(day.split(' ')[0].trim(), 10)
        );
      }
      return null;
    } catch {
      return null;
    }
  }

  formatSchedulerFormDate(
    value: Date | { year: number; month: number; day: number } | null | ''
  ): string | null {
    if (!value) {
      return null;
    }
    if (value instanceof Date) {
      const year = value.getFullYear();
      const month = String(value.getMonth() + 1).padStart(2, '0');
      const day = String(value.getDate()).padStart(2, '0');
      return `${year}-${month}-${day}`;
    }
    if (typeof value === 'object' && 'year' in value) {
      const month = String(value.month).padStart(2, '0');
      const day = String(value.day).padStart(2, '0');
      return `${value.year}-${month}-${day}`;
    }
    return null;
  }

  formatSchedulerCompareDate(date: Date): string {
    return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(2, '0')}-${String(date.getUTCDate()).padStart(2, '0')}`;
  }

  onSchedulerEndDateSelect(date: Date) {
    if (date) {
      this.onEndDateChange(this.formatSchedulerCompareDate(date));
    }
  }
  get getWeekDays() {
    return this.processConfigurationForm.get('weekDays') as FormArray;
  }

  showFilePreview() {
    //recordHeaders - header with properties
    //recordsArrayForDisplay - records used to help validating data types
    //headersRow - actual file headers


    this.uiValidation = false;

    this.filePreviewValues = {
      paramChanged: this.paramChangedForFilePreview,
      file: this.file,
      delimiter: this.processConfigurationForm.get('delimiter').value,
      hasHeaders: this.processConfigurationForm.get('flexCheckHasHeaders')
        .value,
      flexCheckSkipEmptyLines: this.processConfigurationForm.get(
        'flexCheckSkipEmptyLines'
      ).value,
      txtQuoteCharacter:
        this.processConfigurationForm.get('txtQuoteCharacter').value,
      skip_header_rows: this.processConfigurationForm.get('skipRows').value,
      skip_footer_rows:
        this.processConfigurationForm.get('skipFooterRows').value,
      fileName: this.fileName,
      recordHeaders: this.recordHeaders,
      recordsArrayForDisplay: this.recordsArrayForDisplay,
      headersRow: this.headersRow,
      spanish_to_english:
        this.processConfigurationForm.get('spanish_to_english').value,
      roman_numerals_only: this.processConfigurationForm.get(
        'roman_numerals_only'
      ).value,
    };

    this.filePreview = true;

    // this.bsModalRef = this.modalService.showFilePreview(
    //   this.processConfigurationForm.get('delimiter').value,
    //   this.processConfigurationForm.get('flexCheckHasHeaders').value,
    //   this.processConfigurationForm.get('txtQuoteCharacter').value,
    //   this.processConfigurationForm.get('skipRows').value,
    //   this.processConfigurationForm.get('skipFooterRows').value,
    //   this.fileName,
    //   this.recordHeaders,

    // );

    // this.bsModalRef.content?.outputEmitfilePreviewValues.subscribe((output: FilePreviewValues) => {
    //   this.fileName = output.fileName,
    //     this.recordHeaders = output.recordHeaders,
    //     this.processConfigurationForm.get('key_column_list').setValue(output.keyColumnNamesSelected);
    //   this.processConfigurationForm.get('column_name_list').setValue(output.columnNamesSelected);
    //   this.processConfigurationForm.get('dedup').setValue(output.columnsForDedupSelected);
    // });
  }

  resetColumns() {
    this.sampleFileIsProvided = !this.sampleFileIsProvided;

    this.file = null;
    this.recordHeaders = [];
    this.fileName = '';
    this.processConfigurationForm.get('key_column_list').setValue('');
    this.processConfigurationForm.get('column_name_list').setValue('');
    this.processConfigurationForm.get('convert_datatypes_column_list').setValue('');
    this.processConfigurationForm.get('db_column_name_list').setValue('');
    this.processConfigurationForm.get('dedup').setValue('');
    (document.getElementById('column_name_list_forDisplay') as HTMLInputElement).value = '';
    this.modifySettingsClose(null);
    this.alertUIValidationReset();
  }

  sampleFileIsProvided: boolean = false;
  updateForm(event: { filePreviewValues: FilePreviewValues, payLoad: PayLoad }) {
    if (event.filePreviewValues.recordHeaders.length > 0) {
      this.nameOfParamChange = '';
      this.sampleFileIsProvided = true;
      this.recordHeaders = event.filePreviewValues.recordHeaders;
      this.fileName = event.filePreviewValues.fileName;

      let headersWithDataType: string[] = [];
      this.recordHeaders.forEach((header, i) => {
        if (header.willInclude) {
          switch (header.DatatypeName) {
            case 'date':
            case 'time':
            case 'datetime':
              headersWithDataType.push(
                `${header.ColumnName}=${header.DatatypeName}|${header.dateTimeFormatId}`
              );
              break;
            default:
              headersWithDataType.push(
                `${header.ColumnName}=${header.DatatypeName}`
              );
              break;
          }
        }
      });
      let columnNamesSelected = headersWithDataType.join(',');
      this.disableDelimiter = (this.fileName?.split('.').pop() === FileType.MSExcel1 || this.fileName?.split('.').pop() === FileType.MSExcel2 || this.fileName?.split('.').pop() === FileType.MSExcel3)
      this.processConfigurationForm.get('delimiter').setValue(event.filePreviewValues.delimiter);
      let keyColumnNamesSelected = this.recordHeaders
        .filter((x) => x.ColumnKey === true && x.willInclude === true)
        .map((x) => x.ColumnName)
        .join(',');


      this.processConfigurationForm.get('ignore_duplicate_rows').setValue(keyColumnNamesSelected.length > 0);
      this.onIgnoreDuplicateRow(keyColumnNamesSelected);
      if (keyColumnNamesSelected.length > 0) {
        this.processConfigurationForm.get('order_by_column_list_name_sort_dir').enable();
      } else {
        this.processConfigurationForm.get('order_by_column_list_name_sort_dir').disable();
      }

      let columnsForDedupSelected = this.recordHeaders
        .filter((x) => x.columnForDedeup === true && x.willInclude === true)
        .map((x) => x.ColumnName)
        .join(',');
      let columnNameList = this.recordHeaders
        .filter((x) => x.willInclude === true)
        .map((x) => x.ColumnName)
        .join(',');
      let dbColumnNamesList = this.recordHeaders
        .filter((x) => x.willInclude === true)
        .map((x) => x.DbColumnName)
        .join(',');

      this.file = event.filePreviewValues.file;
      this.recordsArrayForDisplay = event.filePreviewValues.recordsArrayForDisplay;
      this.headersRow = event.filePreviewValues.headersRow;
      if (
        this.file?.name.split('.').pop() === FileType.MSExcel1 ||
        this.file?.name.split('.').pop() === FileType.MSExcel2 ||
        this.file?.name.split('.').pop() === FileType.MSExcel3
      ) {
        this.processConfigurationForm.get('delimiter').setValue('\\t');
      } else {
        this.processConfigurationForm.get('delimiter').setValue(event.filePreviewValues.delimiter);
      }
      this.paramChangedForFilePreview = false;
      this.processConfigurationForm
        .get('key_column_list')
        .setValue(keyColumnNamesSelected);
      this.processConfigurationForm.get('column_name_list').setValue(columnNameList);
      if (columnNameList.trim().length > 0) {
        (document.getElementById('column_name_list_forDisplay') as HTMLInputElement).value = columnNameList;
      }
      this.processConfigurationForm
        .get('db_column_name_list')
        .setValue(dbColumnNamesList);
      this.processConfigurationForm
        .get('convert_datatypes_column_list')
        .setValue(columnNamesSelected.toUpperCase());
      this.processConfigurationForm
        .get('dedup')
        .setValue(columnsForDedupSelected);

      let db_file_columnName_list_array: string[] = [];

      this.recordHeaders.forEach((e, i) => {
        switch (e.DatatypeName.toLowerCase()) {
          case 'time':
          case 'date':
          case 'datetime':
            db_file_columnName_list_array.push(
              `${e.DbColumnName}#${e.ColumnName}=${e.DatatypeName}|${e.dateTimeFormatId}`
            );
            break;
          default:
            db_file_columnName_list_array.push(
              `${e.DbColumnName}#${e.ColumnName}=${e.DatatypeName}`
            );
            break;
        }
      });

      this.db_file_columnName_list = db_file_columnName_list_array.join(',');
    }

    if (event.payLoad) {
      this.ruleSetPayLoad = event.payLoad;
    }
  }

  getInvalidFields(formGroup: FormGroup | FormArray): string[] {
    const invalidFields: string[] = [];

    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);

      if (control instanceof FormGroup || control instanceof FormArray) {
        // Recursively handle nested groups or arrays
        invalidFields.push(...this.getInvalidFields(control));
      } else if (control?.invalid) {
        invalidFields.push(key);
      }
    });

    return invalidFields;
  }
  // async onDBColumnNamesChange(event: any) {
  //   if (
  //     event.target.value.length > 0 &&
  //     event.target.value.split(',').length > 0
  //   ) {
  //     let seen = new Set();
  //     let renamedArr = [];
  //     var listOfColumnNames = event.target.value.split(',');
  //     let convertToSpanish =
  //       this.processConfigurationForm.get('spanish_to_english').value;
  //     let romanNumeralsOnly = this.processConfigurationForm.get(
  //       'roman_numerals_only'
  //     ).value;
  //     for (let i = 0; i < listOfColumnNames.length; i++) {
  //       let tempStr = cleanColumnName(listOfColumnNames[i].toUpperCase()); //this.helperUtil.cleanColumnName(listOfColumnNames[i].toUpperCase());

  //       // if (convertToSpanish === true) {
  //       //   tempStr = await this.convertToEnglishOnlyCharacters(
  //       //     'spanish',
  //       //     tempStr
  //       //   );
  //       // }

  //       if (convertToSpanish) {

  //       }

  //       if (romanNumeralsOnly === true) {
  //         tempStr = convertToRoman(tempStr, false); //this.helperUtil.convertToRoman(tempStr, false);
  //       }
  //       if (seen.has(tempStr)) {
  //         let counter = 1;
  //         let newName = counter + '_' + counter;
  //         while (seen.has(newName)) {
  //           counter++;
  //           newName = tempStr + '_' + counter;
  //         }

  //         renamedArr.push(newName);
  //         seen.add(newName);
  //       } else {
  //         renamedArr.push(tempStr);
  //         seen.add(tempStr);
  //       }
  //     }

  //     this.processConfigurationForm
  //       .get('db_column_name_list')
  //       .setValue(renamedArr.join(','));
  //   }
  // }
  onColumnNamesChange(event: any) {
    if (event.target.value.length === 0) {
      this.processConfigurationForm.get('column_name_list').setValue('');
      this.processConfigurationForm.get('db_column_name_list').setValue('');
      return;
    }
    this.recordHeaders = [];
    var dbColumnNames: string[] = [];
    const columnNames = event.target.value.split(',').map((name: string) => name.trim()).filter((name: string) => name !== '');

    columnNames.forEach((cleanName: string, i: number) => {
      cleanName = cleanColumnName(cleanName); //this.helperUtil.cleanColumnName(name);
      this.recordHeaders.push({
        index: i,
        ColumnName: cleanName,
        DbColumnName: cleanName,
        OgColumnName: cleanName,
        OgDbColumnName: cleanName,
        DatatypeName: 'string',
        willInclude: true,
        ColumnKey: false,
        newColumn: false,
        missingColumn: false,
        invalidDataType: false,
        willAddNewColumn: true,
        invalidColumnName: false,
        columnForDedeup: false,
        dateTimeFormatId: 0,
        isDuplicateColumn: false,
        useMultipleSheets: true
      });

      dbColumnNames.push(`${cleanName}=string`)
    });
    this.processConfigurationForm.get('column_name_list').setValue(this.recordHeaders.map(x => x.ColumnName).join(','));
    event.target.value = this.recordHeaders.map(x => x.ColumnName).join(',');
    this.processConfigurationForm.get('db_column_name_list').setValue(this.recordHeaders.map(x => x.DbColumnName).join(','));
    this.processConfigurationForm.get('convert_datatypes_column_list').setValue(dbColumnNames.join(',').toUpperCase());

  }
  // onColumnNamesChange(event: any) {
  //   if (
  //     event.target.value.length > 0 &&
  //     event.target.value.split(',').length > 0
  //   ) {
  //     var listOfColumnNames = event.target.value.split(',');
  //     let results = [];
  //     let seen = new Set();
  //     let renamedArr = [];
  //     var currentElement = '';
  //     for (let i = 0; i < listOfColumnNames.length; i++) {
  //       const match = listOfColumnNames[i].match(/=/g);
  //       if (match && match.length == 2) {
  //         //this.toastr.error('Invalid Column Name. Please correct');
  //         return;
  //       } else if (match && match.length == 1) {
  //         let tempStr = listOfColumnNames[i].split('=');
  //         let isValidDataType;
  //         let isDateTime = tempStr[1].includes('|'); // .split('|');

  //         if (isDateTime) {
  //           let dateTimeKeyValue = tempStr[1].split('|');
  //           isValidDataType = this.dateTimeNames.find(
  //             (x) =>
  //               x.dataTypeName === dateTimeKeyValue[0] &&
  //               x.formatId === +dateTimeKeyValue[1]
  //           );
  //         } else {
  //           isValidDataType = this.datatypeNames.find(
  //             (x) => x.datatypeName === tempStr[1].trim().toLowerCase()
  //           );
  //         }

  //         currentElement = cleanColumnName(tempStr[0]) + '=' + tempStr[1].trim().toLowerCase(); //this.helperUtil.cleanColumnName(tempStr[0]) +'=' + tempStr[1].trim().toLowerCase();
  //         if (isValidDataType) {
  //           this.processConfigurationForm.controls[
  //             'column_name_list'
  //           ].setErrors({ incorrectDataType: false });
  //         } else {
  //           this.processConfigurationForm.controls[
  //             'column_name_list'
  //           ].setErrors({ incorrectDataType: true });
  //           return;
  //         }
  //       } else {
  //         currentElement = cleanColumnName(listOfColumnNames[i].trim()); //this.helperUtil.cleanColumnName(listOfColumnNames[i].trim());
  //       }

  //       if (seen.has(currentElement)) {
  //         //find unique name for the duplicate
  //         let counter = 1;
  //         let newName = currentElement + '_' + counter;
  //         while (seen.has(newName)) {
  //           counter++;
  //           newName = currentElement + '_' + counter;
  //         }

  //         renamedArr.push(newName);
  //         seen.add(newName);
  //       } else {
  //         renamedArr.push(currentElement);
  //         seen.add(currentElement);
  //       }
  //     }

  //     //console.log(renamedArr);
  //     this.processConfigurationForm
  //       .get('column_name_list')
  //       .setValue(renamedArr.join(','));
  //   }
  // }

  onColumnNamesWithDataTypeChange(event: any) {
    if (
      event.target.value.length > 0 &&
      event.target.value.split(',').length > 0
    ) {
      var listOfColumnNames = event.target.value.split(',');
      let results = [];
      let seen = new Set();
      let renamedArr = [];
      var currentElement = '';
      let tempStr = '';

      for (let i = 0; i < listOfColumnNames.length; i++) {
        const match = listOfColumnNames[i].match(/=/g);
        if (match && match.length == 2) {
          //this.toastr.error('Invalid Column Name. Please correct');
          return;
        } else if (match && match.length == 1) {
          tempStr = listOfColumnNames[i].split('=');
          let isValidDataType;
          let isDateTime = tempStr[1].includes('|'); // .split('|');

          if (isDateTime) {
            let dateTimeKeyValue = tempStr[1].split('|');
            isValidDataType = this.dateTimeNames.find(
              (x) =>
                x.dataTypeName === dateTimeKeyValue[0].trim().toLowerCase() &&
                x.formatId === +dateTimeKeyValue[1]
            );
          } else {
            isValidDataType = this.datatypeNames.find(
              (x) => x.datatypeName === tempStr[1].trim().toLowerCase()
            );


          }

          currentElement = cleanColumnName(tempStr[0]); //this.helperUtil.cleanColumnName(tempStr[0]) +'=' + tempStr[1].trim().toLowerCase();
          if (isValidDataType) {
            this.processConfigurationForm.controls[
              'convert_datatypes_column_list'
            ].setErrors({ incorrectDataType: false });

            this.recordHeaders.find(x => x.index === i).DatatypeName = tempStr[1].trim().toUpperCase();
          } else {
            this.processConfigurationForm.controls[
              'convert_datatypes_column_list'
            ].setErrors({ incorrectDataType: true });
            return;
          }
        } else {
          currentElement = cleanColumnName(listOfColumnNames[i].trim()); //this.helperUtil.cleanColumnName(listOfColumnNames[i].trim());
        }

        if (seen.has(currentElement)) {
          //find unique name for the duplicate
          let counter = 1;
          let newName = currentElement + '_' + counter;
          while (seen.has(newName)) {
            counter++;
            newName = currentElement + '_' + counter;
          }

          renamedArr.push(newName + '=' + tempStr[1].trim());
          seen.add(newName);
          this.recordHeaders.find(x => x.index === i).DbColumnName = newName.trim().toUpperCase();
        } else {
          renamedArr.push(currentElement + '=' + tempStr[1].trim());
          seen.add(currentElement);
          this.recordHeaders.find(x => x.index === i).DbColumnName = currentElement;
        }
      }

      //console.log(renamedArr);
      this.processConfigurationForm
        .get('convert_datatypes_column_list')
        .setValue(renamedArr.join(','));
      this.processConfigurationForm.get('db_column_name_list').setValue([...seen].join(','));
    }
  }

  onKeyColumnListChange(event: any) {
    if (event.target.value.length > 0) {
      this.processConfigurationForm.get('ignore_duplicate_rows').setValue(true);
    }
  }

  onDedupChange(event: any) {
    if (event.target.value.length > 0) {
      this.processConfigurationForm
        .get('dedup')
        .setValue(cleanColumnName(event.target.value)); //this.helperUtil.cleanColumnName(event.target.value));
    }
  }

  // modifySettingsOpen() {
  //   this.modifySettings = true;
  // }
  modifySettingsClose(event: string | null) {
    if (event) {

      this.processConfigurationForm.get(event)?.setValue(false);

    }
    this.filePreview = false;
  }

  onConvertRomanNumerals(event: any) {

    if (this.recordHeaders.length > 0 && this.filePreview === false) {
      this.showFilePreview();
    }

    this.paramChangedForFilePreview = true;
    this.nameOfParamChange = 'roman_numerals_only';
    if (this.paramsId && this.paramsId != '') {
      //if no file is present; you're editing an exising process
      this.recordHeaders = [];
      this.configurationDetails?.fileColumnMapping.forEach((c, i) => {
        this.recordHeaders.push({
          index: i,
          ColumnName: c.fileColumn,
          DbColumnName: c.dbColumn,
          OgColumnName: c.fileColumn,
          OgDbColumnName: c.dbColumn,
          DatatypeName: c.dataType,
          willInclude: true,
          ColumnKey: false,
          newColumn: false,
          missingColumn: false,
          invalidDataType: false,
          willAddNewColumn: true,
          invalidColumnName: false,
          columnForDedeup: false,
          dateTimeFormatId: 0,
          isDuplicateColumn: false,
          useMultipleSheets: true
        });
      });
      this.filePreviewValues.recordHeaders = this.recordHeaders;
    }
    if (this.filePreview) {
      this.filePreviewValues.roman_numerals_only = event.target.checked;
      this.filePreviewValues.paramChanged = this.paramChangedForFilePreview;
      this.filePreviewComponent?.updateFilePreview();
    }

  }

  async onConvertSpanishToEnglish(event: any) {
    //this.formValues.spanish_to_english = event.target.checked;
    // console.log(this.filePreview);
    if (this.recordHeaders.length > 0 && this.filePreview === false) {
      this.showFilePreview();
    }

    this.paramChangedForFilePreview = true;

    this.nameOfParamChange = 'spanish_to_english';

    if (this.paramsId && this.paramsId != '') {
      this.recordHeaders = [];

      this.configurationDetails?.fileColumnMapping.forEach((c, i) => {
        this.recordHeaders.push({
          index: i,
          ColumnName: c.fileColumn,
          DbColumnName: c.dbColumn,
          OgColumnName: c.fileColumn,
          OgDbColumnName: c.dbColumn,
          DatatypeName: c.dataType,
          willInclude: true,
          ColumnKey: false,
          newColumn: false,
          missingColumn: false,
          invalidDataType: false,
          willAddNewColumn: true,
          invalidColumnName: false,
          columnForDedeup: false,
          dateTimeFormatId: 0,
          isDuplicateColumn: false,
          useMultipleSheets: true
        });
      });
      this.filePreviewValues.recordHeaders = this.recordHeaders;
    }

    if (this.filePreview) {
      this.filePreviewValues.spanish_to_english = event.target.checked;
      this.filePreviewValues.paramChanged = this.paramChangedForFilePreview;
      this.filePreviewComponent?.updateFilePreview();
    } else {
      let column_name_list = this.processConfigurationForm.get('column_name_list').value;
      this.processConfigurationForm
        .get('column_name_list')
        .setValue(
          await this.convertToEnglishOnlyCharacters('spanish', column_name_list)
        );

      let db_column_name_list = this.processConfigurationForm.get('db_column_name_list').value;
      this.processConfigurationForm
        .get('db_column_name_list')
        .setValue(
          await this.convertToEnglishOnlyCharacters(
            'spanish',
            db_column_name_list
          )
        );

      let convert_datatypes_column_list = this.processConfigurationForm.get('convert_datatypes_column_list').value;
      this.processConfigurationForm
        .get('convert_datatypes_column_list')
        .setValue(
          await this.convertToEnglishOnlyCharacters(
            'spanish',
            convert_datatypes_column_list
          )
        );

      let key_column_list =
        this.processConfigurationForm.get('key_column_list').value;
      this.processConfigurationForm
        .get('key_column_list')
        .setValue(
          await this.convertToEnglishOnlyCharacters('spanish', key_column_list)
        );

      let dedup = this.processConfigurationForm.get('dedup').value;
      this.processConfigurationForm
        .get('dedup')
        .setValue(await this.convertToEnglishOnlyCharacters('spanish', dedup));
    }
  }
  showEnglishConversionOnProcess: boolean = false;

  //TODO check if this is needed and be replaced
  async convertToEnglishOnlyCharacters(
    language: string,
    wordToBeConverted: string
  ) {
    //if (this.helperUtil.checkForAccents(wordToBeConverted)) {
    this.showEnglishConversionOnProcess = true;
    const result = await lastValueFrom(
      this.configService.convertToEnglishOnlyCharacters(
        language,
        wordToBeConverted
      )
    );

    if (result) {
      if (result.responseCode === 200) {
        if (result.result) {
          this.showEnglishConversionOnProcess = false;
          return result.result;
        }
      }
    }

    //}

    return wordToBeConverted;
  }

  getAllEnglishCharactersOnly() {
    this.configService.getAllEnglishCharactersOnly('spanish').subscribe({
      next: (response: APIResponse<EnglishOnlyCharacters[]>) => {
        if (response.responseCode === 200) {
          if (response.result) {
            // //console.log(response.result);
            this.englishOnlyCharacters = response.result;
          }
        }
      },
    });
  }

  onMergeData(event: any) {
    if (!event.target.checked) {
      this.processConfigurationForm.get('createHistoryTable').setValue(false);
    }
  }

  compareWithFn(item1: any, item2: any): boolean {
    return item1 && item2 ? item1 === item2 : item1 === item2;
  }

  onDisableDelimiter(event: string) {
    //this.disableDelimiter =  ( event=== FileType.MSExcel1 || event === FileType.MSExcel2 || event === FileType.MSExcel3)
  }
  uiValidation: boolean = false;
  excelFileRule: ExcelRule[] = [];
  onUIValidationRule(event: any) {
    // const offcanvasElement = document.getElementById('offcanvasWithBothOptions');
    // if (offcanvasElement) {
    //   const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement);
    //   offcanvas.show();

    // }

    //check that the column names is not null then proceed
    if (this.recordHeaders.length === 0) {
      this.toastr.error(ToastrMessages.ColumnNamesNotProvided);
      return;
    }

    if (this.ruleSetPayLoad?.ruleSets?.length > 0) {
      this.excelFileRule = this.ruleSetPayLoad.ruleSets.map(rule => ({ ...rule, isIrrelevant: false }));
      this.uiValidation = true;
      return;
    }

    //retrieve the flpRuleSets and pass it to the child
    if (!this.rulesetUpdated) {
      this.retriveValidationDetails(true);
    } else {
      this.uiValidation = true;
    }

  }
  rulesetUpdated: boolean = false;
  retriveValidationDetails(displayUIValidation: boolean = false) {
    let ruleSetNameId = this.configurationDetails?.flpRuleSet[0]?.ruleSetNameId;
    if (ruleSetNameId) {
      this.dataInsiderService.getRuleSetByRuleSetNameId(ruleSetNameId).subscribe({
        next: (response: APIResponse<ExcelRule[]>) => {
          if (response?.responseCode === 200) {
            let excelFileRule: ExcelRule[] = response.result;
            //use the ruleSetNameId
            const payLoad: PayLoad = {
              ruleSets: excelFileRule.map(rule => ({ ...rule, ruleSetNameId: ruleSetNameId })),  //,
              created_by: sessionStorage.getItem('upn').split('@')[0],
              username: sessionStorage.getItem('username'),
              description: excelFileRule[0].description,
              ruleSetName: excelFileRule[0]?.ruleSetName
            };

            this.ruleSetPayLoad = payLoad;

            if (displayUIValidation) this.uiValidation = true;
          }
        },
        error: error => {
          this.toastr.error(ToastrMessages.SomethingWentWrong);
          console.log(error);
        }
      });
    } else {
      if (displayUIValidation) this.uiValidation = true;
    }
  }

  ruleSetPayLoad: PayLoad;
  onUIValidationClose(event: { showUIValidation: boolean, saveCancel: string, payLoad: PayLoad } | null) {
    if (!event) {
      return;
    }

    if (event.saveCancel === 'save') {
      //need to close
      this.ruleSetPayLoad = event.payLoad;
      this.processConfigurationForm.get('RuleSetName')?.setValue(event.payLoad.ruleSets[0].ruleSetName);
      this.uiValidation = false;
      //this.alertUIValidationReset();
      return;
    }

    if (event.saveCancel === 'cancel') {
      if (event.payLoad != null) {
        const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
        modalRef.componentInstance.title = 'UI Validation';
        modalRef.componentInstance.message = `Closing this window will discard everything. Please click on ${this.configurationDetails?.flpRuleSet[0]?.ruleSetNameId ? 'Update' : 'Save'}`;
        modalRef.result.then((result) => {
          if (result) {
            this.uiValidation = false;
          }
        });
      }
      else {
        this.uiValidation = false;
      }
    }


  }

  showHideUIValidation(willShow: boolean) {
    this.uiValidation = willShow;
  }

  alertUIValidationReset() {

    if (this.ruleSetPayLoad) {
      this.ruleSetPayLoad.ruleSets = [];
    }

    //clear the ruleSetNameId
    if (this.configurationDetails?.flpRuleSet[0]?.ruleSetNameId) {
      this.configurationDetails.flpRuleSet[0].ruleSetNameId = '';
    }
  }

  campaignNames: CampaignNames[] = [];
  showCampaignName: boolean = false;
  onExternalProjectChange(event: any) {

    const ctrlCampaignName = this.processConfigurationForm.get('campaignName');
    const ctrlSubRegion = this.processConfigurationForm.get('SubRegionId');
    const ctrlClient = this.processConfigurationForm.get('ClientId');
    const ctrlRegionId = this.processConfigurationForm.get('RegionId');
    this.getProcessName('X');
    ctrlCampaignName.disable();
    ctrlSubRegion.reset();
    ctrlClient.reset();
    ctrlSubRegion.disable();
    ctrlClient.disable();
    ctrlRegionId.reset();
    ctrlRegionId.disable();
    this.showCampaignName = event.target.checked;
    if (this.showCampaignName) {
      if (this.dsRegion.length === 0) {
        ctrlCampaignName.disable();
      } else {
        ctrlCampaignName.enable();
      }
      ctrlCampaignName.setValidators([Validators.required]);

      ctrlCampaignName.updateValueAndValidity();

      this.fabService.fabReady$.pipe(
        take(1)
      ).subscribe({
        next: () => {
          this.campaignNames = this.fabService.FABUserAccount.map(x => ({ campaignId: x.campaignId, campaignName: x.campaignName }));
        }
      });
    } else {
      ctrlRegionId.enable();
      ctrlCampaignName.clearValidators();
      ctrlCampaignName.setValue(null);
      this.dsRegion = [...this.dsREgionTemp];
      return;
    }
  }
  //this.dsRegionTemp : DDL
  onCampaignNameChange(event: any) {
    const ctrlRegionId = this.processConfigurationForm.get('RegionId');
    const ctrlSubRegion = this.processConfigurationForm.get('SubRegionId');
    const ctrlClient = this.processConfigurationForm.get('ClientId');

    //lets' reset
    ctrlSubRegion.reset();
    ctrlClient.reset();
    ctrlSubRegion.disable();
    ctrlClient.disable();
    this.dsRegion = [...this.dsREgionTemp];
    this.getProcessName('X');
    ctrlRegionId.setValue('');
    if (event) {
      this.fabService.fabReady$.pipe(
        take(1)
      ).subscribe({
        next: () => {

          if (Array.isArray(this.dsRegion) && this.dsRegion.length > 0) {
            if (this.fabService.isFabUserValue) {
              const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === event.campaignId).map(x => x.regionId);
              //ctrlRegionId.enable();
              this.dsRegion = this.dsRegion
                .filter((r) => allowedIds.includes(r.id))
                .map(r => ({ id: r.id, name: r.name }));


              ctrlRegionId.setValue(this.dsRegion[0]?.id || '', { emitEvent: true });
              //workaround, event not firing
              this.getProcessName('R');

            }


          }
        }
      });
    } else {

    }
  }




  isFileExtensionLoading: boolean = false;

  getFileExtensions() {
    this.isFileExtensionLoading = true;
    this.configService.getFileExtensionNames().subscribe({
      next: (response: APIResponse<FileNameExtension[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.isFileExtensionLoading = false;
            this.fileNameExtensions = response.result;
          }
        }
      },
      error: error => {
        console.log(error);
        this.isFileExtensionLoading = false;
      }
    });
  }




  addOrUpdateRegex() {

    const raw = this.processConfigurationForm.get('landingLayerRegex')?.value ?? '';
    const value = raw.trim();
    if (!value) return;

    // Prevent duplicates (unless editing the same exact item)
    const existingIndex = this.regexList.findIndex(r => r === value);
    const isDuplicate =
      existingIndex !== -1 && existingIndex !== this.editingRegexIndex;

    if (isDuplicate) {
      // You can replace this with a snack-bar / toast
      this.toastr.error('That expression is already in the list.');
      return;
    }

    const isRegexValid = this.helperUtil.compilePattern(value);

    if (!isRegexValid.ok) {
      this.toastr.error(`Invalid Regex: ${isRegexValid.error}`);
      return;
    } else {
      if (this.editingRegexIndex !== null) {
        this.regexList[this.editingRegexIndex] = value;
      } else {
        this.regexList.push(value);
      }

    }

    this.clearEditor();

  }

  /** Load a list item back into the textbox for editing */
  selectForEdit(index: number): void {
    this.editingRegexIndex = index;
    this.processConfigurationForm.patchValue({ landingLayerRegex: this.regexList[index] });
  }
  /** Remove a specific regex from the list */
  removeRegex(index: number, event?: MouseEvent): void {
    // Don’t trigger the row click (edit) when clicking the delete icon
    event?.stopPropagation();

    this.regexList.splice(index, 1);

    // If we deleted the one we were editing, reset editor
    if (this.editingRegexIndex === index) {
      this.clearEditor();
    } else if (this.editingRegexIndex !== null && index < this.editingRegexIndex) {
      // Adjust editing index if we removed an earlier item
      this.editingRegexIndex -= 1;
    }
  }

  /** Cancel editing and clear the input */
  clearEditor(): void {
    this.editingRegexIndex = null;
    this.processConfigurationForm.get('landingLayerRegex').setValue('');
  }

  trackByRegexListIndex(i: number) {
    return i;
  }



  resetLandingPath(control: string) {
    if (!control || !control.trim()) return;

    if (control === 'landingLayerAcceptedPath') {
      this.processConfigurationForm.get(control)?.setValue(this.landingLayerAcceptedPath);
      return;
    }

    if (control === 'landingLayerRejectedPath') {
      this.processConfigurationForm.get(control)?.setValue(this.landingLayerRejectedPath);
      return;
    }
  }

  openCustomRegex() {
    const ref = this.modalService.showCustomRegexBuilder();
    const comp = ref.content as RegexBuilderComponent;  // <-- returned regex
    comp.onClose?.subscribe((result) => {

      if (result.action === 'save') {

        if (comp) {
          //add only if comp.result is not empty and not already in the list
          if (comp.result && !this.regexList.some(r => r.regex === comp.result)) {

            const regex = comp.result?.trim();
            const description = comp.generatedDescription?.trim();

            const newItem = {
              regex,
              description
            };
            this.regexList.push(newItem);               // add to array
            //this.validateFileNamesWithRegex.emit(this.regexList); // emit to parent
          } else {
            // You can replace this with a snack-bar / toast
            this.toastr.error('That expression is emtpy or already in the list.');
          }
        }
      }
    });
  }

  onStorageAccountChange(storageAccountId: number, controlName: string): void {
    this.databaseDestinationSettingsError = '';

    const control = this.processConfigurationForm.get(controlName);
    const selectedAccount = (
      controlName === 'blobContainerName'
        ? this.blobStorageAccount
        : this.storageAccount
    ).find(acc => acc.storageAccountId === storageAccountId);

    if (!control) {
      return;
    }

    if (selectedAccount?.containerName) {
      control.setValue(selectedAccount.containerName);
    } else {
      control.setValue('');

      this.databaseDestinationSettingsError = ModalMessages.NoContainerName;
    }

    // Trigger validation
    control.markAsTouched();
    control.markAsDirty();
    control.updateValueAndValidity();
  }


}

function validateCheckbox(min: 1) {
  const validator: ValidatorFn = (formArray: AbstractControl) => {
    if (formArray instanceof FormArray) {
      const totSelected = formArray.controls
        .map((control) => control.value)
        .reduce((prev, next) => (next ? prev + next : prev), 0);
      return totSelected >= min ? null : { required: true };
    }
    throw new Error('check control type');
  };
  return validator;
}
