import {
  AfterViewChecked,
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnInit,
  viewChild,
  ViewChild,
} from '@angular/core';
import { AbstractControl, FormArray, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { Helper, noWhitespaceValidator } from '../core/utils/helper';
import { DiParserService } from '../core/services/di-parser.service';
import { ModalServiceService } from '../core/services/modal-service.service';
import { ConfigurationService } from '../core/services/configuration.service';
import { APIResponse } from '../core/models/apiResponse';
import { DatatypeName, DateTimeFormats } from '../core/models/datatypeNames';
import { ToastrService } from 'ngx-toastr';
import {
  DataSourceType,
  FileType,
  ModalMessages,
  RuleTypeNames,
  SubRuleTypes,
  ToastrMessages,
} from '../shared/enum';
import {
  AdditionalSettings,
  FileColumnMapping,
  RegexItem
} from '../core/models/additionalSettings';
import { ColumnNameDatatypeName, SheetValidationData } from '../core/models/columnNameDatatypeName';
import { ProcessNames } from '../core/models/processNames';
import { async, BehaviorSubject, debounceTime, distinctUntilChanged, lastValueFrom, Observable, Subject, switchMap } from 'rxjs';
import { FileSettings, LocalFileDataSourceType, LocalFileDataSourceTypev4_1 } from '../core/models/localFileDataSourceType';
import { FlpConvertParquetRequestDto } from '../core/models/FlpConvertParquetRequestDto';
import {
  FileConfiguration,
  FilePreviewNotification,
  KeyValuePair,
  ProcessSettings,
} from '../core/models/fileConfiguration';
import { Router } from '@angular/router';
import { BusyService } from '../core/services/busy.service';
import { EnglishOnlyCharacters } from '../core/models/englistOnlyCharacters';
import {
  convertToRoman,
  cleanColumnName,
} from '../core/services/di-parser.service';
import { NavigateService } from '../core/services/navigate.service';
import { ProcessConfigService } from '../core/services/process-config.service';

import { ProcessConfigurationComponent } from '../process-configuration/process-configuration.component';
import { SecurityGroup } from '../core/models/userDetails';
import { DatabaseSettings } from '../core/models/databaseSettings';
import { OnlineConfigResponse } from '../core/models/OnlineConfigResponse';
import { add, isSameMinute } from 'date-fns';
import { invalid } from 'moment';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { runPostSignalSetFn } from '@angular/core/primitives/signals';
import { ExcelRule, RuleSetNames } from '../core/models/DataInsider';

import { BlobServiceClient } from '@azure/storage-blob';
import JSZip from 'jszip';
import { ConvertToXLSXDto } from '../core/models/processConfigurationlist';
import { LogUploadedFileRequest } from '../core/models/fileProcessConfig';
import { ModalService } from '../core/services/confirm-modal.service';
import { trigger, transition, style, animate } from '@angular/animations';
import { LandingLayerInsertConfigurationRequest, SelectedFiles } from '../core/models/LandingLayer/landingLayer';
import { match } from 'assert';
import { error } from 'console';
import { LandingLayerService } from '../core/services/LandingLayer/landingLayer.service';
import { LandingLayerConfiguration } from '../core/models/LandingLayer/landingLayer';
// #region Sharepoint Workspace - AY
import { MODULE_BRANDING, SP_INTEGRATION } from '../sharepoint/core/sharepoint.messages';
// #endregion
type AOA = any[][];
type Order = 'asc' | 'desc';

// interface KeyValuePair {
//   sheetName: string;
//   workBook: AOA
// }
@Component({
  selector: 'app-file-upload',
  templateUrl: './file-upload.component.html',
  styleUrl: './file-upload.component.css',
  standalone: false,
  animations: [
    trigger('slideIn', [
      transition(':enter', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate('300ms ease-out', style({ transform: 'translateX(0)', opacity: 1 }))
      ]),
      transition(':leave', [
        animate('300ms ease-in', style({ transform: 'translateX(100%)', opacity: 0 }))
      ])
    ])
  ]
})
export class FileUploadComponent implements OnInit, AfterViewInit, AfterViewChecked {
  @ViewChild('formFile') formFile: ElementRef;
  @ViewChild(ProcessConfigurationComponent) childComponent!: ProcessConfigurationComponent
  fileUploadForm: FormGroup = new FormGroup({});
  get clientInfoForm(): FormGroup {
    return this.fileUploadForm.get('clientInfo') as FormGroup;
  }
  file: any | null = null;
  fileName: string = '';
  page: number = 1;
  fileSize: string | null = '';
  fileType: string = FileType.CommaSeparatedValues; //default
  upn: string = '';
  datatypeNames: DatatypeName[] = [];
  dateTimeNames: DateTimeFormats[] = [];
  dateTimeNamesValues: DateTimeFormats[] = [];
  selectTimeOptions: DateTimeFormats[] = [];
  config: AdditionalSettings;
  ruleSet: ExcelRule[] = [];
  ruleSetNames: RuleSetNames[] = [];
  columnDatatype: ColumnNameDatatypeName[] = [];
  multiSheetConfigs: { [sheetName: string]: AdditionalSettings } = {};
  columnDatatypePerSheet: { [sheetName: string]: ColumnNameDatatypeName[] } = {};
  //previousValidateSheetColumnData: { [sheetName: string]: ValidateSheetColumnData[] } = {};
  previousValidateSheetColumnData: { [sheetName: string]: SheetValidationData } = {};
  recordsArrayForDisplayPerSheet: { [sheetName: string]: any[] } = {};
  processNames: ProcessNames[] = null;
  data: LocalFileDataSourceType;
  dataxlsx: AOA = [];
  disableDelimiter = false;
  processNamePattern = "^[\\w\-\\s]+$"; //alphanumeric but does not start with numbers
  // wksheets: { key: string, value: AOA }[] = [
  //   // {key: 'first', value: 'one'},
  //   // {key: 'second', value: 'two'}
  // ];

  wksheets: KeyValuePair[] = [];
  multiSheetForms: { [sheetName: string]: FormGroup } = {};
  selectedWorkSheetName: string = '';

  headersRow: string[] | undefined = [];
  headersRowForDisplayingRecords: string[] | undefined = [];
  headersRow2: ColumnNameDatatypeName[] = [];
  recordsArray: any[] = [];
  recordsArrayForDisplay: any[] = [];
  arrayWithError: any[] = [];
  modifySettings = true;

  rowCountForDisplay: number = 0;
  noOfDuplicates = 0;
  unorderd: any[];
  keyColumnNameSelected: string[] = [];
  file_columns_mapping: FileColumnMapping[] = [];
  columnsForDedupSelected: string[] = [];
  keyColumnIndexSelected: string[] = [];
  columnsForDedupIndexSelected: string[] = [];
  showDatabasePreview = false;
  flexCheckHasHeaders = true;
  hasErrors: boolean = true;
  parseComplete = false;
  isValidDataType = true;
  InvalidDataSource = false;
  NewColumnFoundOnSource = false;
  MissingColumnFoundOnSource = false;
  preview = false;
  isNewProcess = false;
  processConfigurationFormHasError = true;
  parsingTime: number | null = null;
  configSubject: Subject<AdditionalSettings> =
    new Subject<AdditionalSettings>();
  keyColumnsSubject: Subject<string> = new Subject<string>();

  dedupColumnsSubject: Subject<string> = new Subject<string>();
  activeTab = 'attachFile-tab';
  fileSubmissionCompleted = false;
  newColumnVSTrue = false;
  row1AreHeaders = false;
  englishOnlyCharacters: EnglishOnlyCharacters[] = [];
  dataSource: number = 1;
  isDragOver = false;
  isFileUploaded = false;
  // #region Sharepoint Workspace - AY
  showSharepoint = false;
  readonly spFileFirst = SP_INTEGRATION.fileFirst;
  readonly spWorkspaceNavTitle = MODULE_BRANDING.browseNavTitle;
  // #endregion
  selectedTab = 0;
  originalColumnHeadersPerSheet: { [sheetName: string]: string[] } = {};

  mappingType = 'sheetName';
  useSheetName = false;
  useSheetIndex = false;
  useMultipleSheets = true;
  FilePreviewNotification: FilePreviewNotification[] = [];
  // Example dummy values for processSetting and fileSettings
  processSettings: ProcessSettings;
  databaseSettings: DatabaseSettings;
  fileSettings: FileSettings[] = [
    // ...fill with actual properties as per your model
  ];
  ignoreSheet = false; //default value for ignoreSheet
  isSelectedExistingProcess = false;
  hasValidatedExcelConfig = false;
  filContainMissingColumn = false;
  showDateTimeOptions = false;
  selectedDateTimeFormat: string = '';
  selectedDateTimeFormatId: number = 0;
  startValidateExistingExcelConfigurationToFile: any;
  selectedProcesSheetNames: string[] = [];
  fileNotProcessed = false;
  newSheetInfoMessage: string = '';
  missingSheetInfoMessage: string = '';
  notFoundSheetMessage: string = '';
  firstSheetSelectedIndex = 0;
  excelUploadedErrorMessages: string[] = [];
  excelUploadMultiSheet = false;
  selectedProcessHasTabName = false;
  validationRule = false;

  DataSourceTypes = DataSourceType;
  //showProcessConfig = true;
  constructor(
    private fb: FormBuilder,
    private helperUtil: Helper,
    private myParser: DiParserService,
    private configService: ConfigurationService,
    private toastr: ToastrService,
    private modalService: ModalServiceService,
    private router: Router,
    private busyService: BusyService,
    private navigateService: NavigateService,
    private processService: ProcessConfigService,
    private confirmModalService: NgbModal,
    private confirmModalService2: ModalService,
    private landingLayerService: LandingLayerService

  ) //private dsService: DataSliceService
  {
    // this.processNames$ = this.processNameSearchTerm$.pipe(
    //   debounceTime(300),
    //   //distinctUntilChanged(),
    //   switchMap(term=>this.fetchProcessNames(term))
    // );
  }
  ngAfterViewChecked(): void {
    if (this.dataSource !== this.DataSourceTypes.LandingLayer) {
      setTimeout(() => {
        if (this.config) {
          if (this.config.flpConfigurationId !== '') {
            if (Object.keys(this.multiSheetConfigs).length > 0) {
              this.childComponent?.processConfigurationForm?.disable();
            }
          }
        }
      }, 0);
    }
  }
  ngDoCheck(): void {
    // if (this.config.flpConfigurationId !== '' && this.parseComplete) {
    //   let configurationKeyColumns: string[] = this.config.key_columns.split(','); //existing list of key columns in db
    //   let convert_datatypes_column_list = this.config.convert_datatypes_column_list.split(',');
    //   //re-assign the data types in the headers
    //   this.columnDatatype.forEach((c, i) => {
    //     c.ColumnKey = configurationKeyColumns.find(x => x === c.ColumnName) ? true : false;
    //     convert_datatypes_column_list.forEach(dt => {
    //       if (dt.split('=')[0] === c.ColumnName) {
    //         //validate 20 column data
    //         this.validateDataOnDataType(i,dt.split('=')[1], c.ColumnName );
    //         c.DatatypeName = dt.split('=')[1];
    //       }
    //     });
    //   })
    // }
  }

  fetchProcessNames(term: string = ''): Observable<ProcessNames[]> {

    return this.configService.getProcessNamesByLoginIdByTerm(this.navigateService.dataSource, term);

  }

  ngAfterViewInit(): void {

  }

  ngOnInit(): void {
    this.dataSource = this.navigateService.dataSource;
    this.initializeForm();
    this.getAllDateTimeFormats();
    this.getProcessNamesByLoginId();
    if (this.dataSource !== this.DataSourceTypes.LandingLayer) {
      this.getDataColumns();



      this.getAllEnglishCharactersOnly();
    } else {
      //get total number of files that can be uploaded
      this.getLandingLayerUploadConfiguration();
    }
  }

  initializeForm() {


    this.fileUploadForm = this.fb.group({
      clientInfo: this.fb.group({

        RegionId: ['', Validators.required],
        SubRegionId: ['', Validators.required],
        ClientId: ['', Validators.required],
        processName: ['', Validators.required],
        description: ['', Validators.required],
        security_group: [[], Validators.required]
      }),
      formFile: [{ value: '', disabled: true }, Validators.required],
      configuration: [null, Validators.required],
    });


  }

  onChangeFile(event: any) {
    // this.fileUploadForm.get('formFile')?.enable();
    // this.fileUploadForm.get('formFile')?.setValue('');
    // this.fileUploadForm.get('configuration').setValue('');
    // this.resetForm();
    // this.formFile.nativeElement.click();
    this.isFileUploaded = false;
    this.resetForm();
    event.preventDefault();

  }

  // #region Sharepoint Workspace - AY
  toggleFileSource(source: 'local' | 'sharepoint'): void {
    this.showSharepoint = source === 'sharepoint';
    if (source === 'local') {
      this.onChangeFile({ preventDefault: () => {} });
    }
  }

  onSharepointFileSelected(file: File): void {
    this.showSharepoint = false;
    setTimeout(() => {
      this.onFileChange({ target: { files: [file] } });
    }, 0);
  }

  landingLayerConfiguration: LandingLayerConfiguration;
  getLandingLayerUploadConfiguration() {
    this.landingLayerService.getLandingLayerUploadConfiguration().subscribe({
      next: (response: APIResponse<LandingLayerConfiguration>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              //console.log(response.result);
              this.landingLayerConfiguration = response.result;
            }
          }
        }
      },
      error: (error) => {
        this.toastr.error("Can't get landing layer upload configuration");
      },
    });
  }
  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;
    if (this.dataSource !== DataSourceType.LandingLayer) {
      if (event.dataTransfer && event.dataTransfer.files.length > 0) {
        const file = event.dataTransfer.files[0];
        // You can call your file change handler here
        this.onFileChange({ target: { files: [file] } });
      }
    } else {
      const dt = event.dataTransfer;
      if (!dt) return;

      let files: File[] = [];

      if (dt.items && dt.items.length > 0) {
        //filter to files only and ignore directories
        files = Array.from(dt.items)
          .filter(item => item.kind === 'file')
          .map(item => item.getAsFile())
          .filter((f): f is File => !!f);
      } else if (dt.files && dt.files.length > 0) {
        files = Array.from(dt.files);
      }

      if (files.length > 0) {
        this.onFileChange({ target: { files: files } });
        dt.clearData();
      }
    }
  }


  getDataColumns() {
    this.configService.getAllDataTypeNames().subscribe({
      next: (response: APIResponse<DatatypeName[] | null>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              this.datatypeNames = response.result;
              if (this.dataSource === DataSourceType.DataBricks) {
                this.datatypeNames = this.datatypeNames.filter(
                  (x) =>
                    x.datatypeName !== 'date' &&
                    x.datatypeName !== 'datetime' &&
                    x.datatypeName !== 'time'
                );
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
    var showDefault = this.dataSource === DataSourceType.LandingLayer;
    this.configService.getAllDataTimeFormats(showDefault).subscribe({
      next: (response: APIResponse<DateTimeFormats[] | null>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              this.dateTimeNames = response.result;
            }
          }
        }
      },
      error: (error) => {
        this.toastr.error("Can't find all date time formats");
      },
    });
  }
  defaultSelectedProcessName: ProcessNames;
  isProcessNameLoading: boolean = false;
  getProcessNamesByLoginId() {
    this.isProcessNameLoading = true;
    this.configService
      .getProcessNamesByLoginId(this.navigateService.dataSource)
      .subscribe({
        next: (response: APIResponse<ProcessNames[]>) => {
          if (response.responseCode === 200) {
            if (response.result) this.processNames = response.result;
            this.processNames.unshift({
              flpConfigurationId: '',
              processNames: 'New',
              description: 'New Process',
            });
            this.fileUploadForm.get('configuration').setValue('');
          }
          this.isProcessNameLoading = false;
        },
        error: (_) => {
          this.isProcessNameLoading = false;
        },
      });
  }

  getAllEnglishCharactersOnly() {
    this.configService.getAllEnglishCharactersOnly('spanish').subscribe({
      next: (response: APIResponse<EnglishOnlyCharacters[]>) => {
        if (response.responseCode === 200) {
          if (response.result) {
            ////console.log(response.result);
            this.englishOnlyCharacters = response.result;
          }
        }
      },
    });
  }

  randomColor: string = '';
  getRandomColor(): string {
    const letters = '0123456789ABCDEF';
    let color = '#';
    for (let i = 0; i < 6; i++) {
      color += letters[Math.floor(Math.random() * 16)];
    }
    return color;
  }


  selectTabByIndexNumber() {
    const tabs = document.querySelectorAll<HTMLElement>('[id^="sheet-tab-"]');
    // Clear active/selected from all

    tabs.forEach(tab => {
      const isSelectTabByIndex = tab.id === `sheet-tab-${this.firstSheetSelectedIndex}`;;
      tab.setAttribute('aria-selected', isSelectTabByIndex ? 'true' : 'false');
      tab.classList.toggle('active', isSelectTabByIndex);
    });
  }


  onTabChanged(tabName: string, event: any) {
    //if (tabName === 'databaseSchema-tab' || tabName === 'fileSubmission-tab') {
    if (this.wksheets.length > 1) {
      // const sheetNames = this.submittedSheets.map(s => s.sheetName);
      // const tabs = document.querySelectorAll<HTMLElement>('[id^="sheet-tab-"]');
      // // Clear active/selected from all

      // tabs.forEach(tab => {
      //   const isFirstTab = tab.id === 'sheet-tab-0';
      //   tab.setAttribute('aria-selected', isFirstTab ? 'true' : 'false');
      //   tab.classList.toggle('active', isFirstTab);
      // });
      this.displayRecordByWorkSheetName(this.wksheets.filter(x => !x.ignoreSheet)[0].sheetName);
    }
    if (this.activeTab === 'attachFile-tab') {
      if (tabName === 'fileSubmission-tab') {
        if (
          this.config?.flpConfigurationId !== '' ||
          this.InvalidDataSource ||
          this.NewColumnFoundOnSource
        ) {
        }
        if (
          !this.fileSubmissionCompleted &&
          (this.hasErrors || this.processConfigurationFormHasError)
        ) {
          //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
          this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
          // const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
          // attachFileTab.click();
          // this.activeTab = 'attachFile-tab';
          // return;
        }
        const attachFileTab = document.getElementById(
          'attachFile-tab'
        ) as HTMLElement;
        attachFileTab.click();
        this.activeTab = 'attachFile-tab';
        return;
      }
      if (tabName === 'databaseSchema-tab') {
        if (
          this.config?.flpConfigurationId !== '' &&
          this.InvalidDataSource &&
          this.NewColumnFoundOnSource
        ) {
        }
        if (this.hasErrors || this.processConfigurationFormHasError) {
          //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
          this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
          const attachFileTab = document.getElementById(
            'attachFile-tab'
          ) as HTMLElement;
          attachFileTab.click();
          this.activeTab = 'attachFile-tab';
          return;
        }

        const databaseSchemaTab = document.getElementById(
          'databaseSchema-tab'
        ) as HTMLElement;
        databaseSchemaTab.click();
        this.activeTab = 'databaseSchema-tab';
        return;
      }
    }

    if (this.activeTab === 'databaseSchema-tab') {
      if (
        tabName === 'fileSubmission-tab' ||
        this.columnDatatype.filter((x) => x.invalidColumnName === true).length >
        0
      ) {
        if (!this.fileSubmissionCompleted) {
          //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
          const attachFileTab = document.getElementById(
            this.activeTab
          ) as HTMLElement;
          attachFileTab.click();
          this.activeTab = 'databaseSchema-tab';
          return;
        }
      }
    }

    if (this.activeTab === 'fileSubmission-tab') {
      if (tabName === 'databaseSchema-tab') {
        const attachFileTab = document.getElementById(
          'attachFile-tab'
        ) as HTMLElement;
        attachFileTab.click();
        this.activeTab = 'attachFile-tab';
        return;
      }
    }

    // if (this.fileSubmissionCompleted && tabName === 'databaseSchema-tab') {
    //   this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
    //   const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
    //   attachFileTab.click();
    //   this.activeTab = 'attachFile-tab';
    //   return;
    // }
    // else if ((this.hasErrors || this.processConfigurationFormHasError) && !this.fileSubmissionCompleted) {
    //   this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
    //   const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
    //   attachFileTab.click();
    //   this.activeTab = 'attachFile-tab';
    //   return;
    // }
    // else if (!this.fileSubmissionCompleted) {
    //   const currTab = document.getElementById(tabName) as HTMLElement;
    //   currTab.click();
    //   return;
    // }
    //}
    this.activeTab = tabName;
  }

  get submittedSheets() {
    return this.wksheets.filter(x => x.ignoreSheet === false && x.newSheet === false && x.missingSheet === false);
  }

  onFileChange(event: any) {
    if (this.dataSource === DataSourceType.LandingLayer) {
      this.displayFileNames(event);
      return;
    }
    const validExtensions = ['csv', 'txt', 'xls', 'xlsx', 'xlsb'];

    this.resetForm();
    if (event.target.files.length > 0) {
      const file = event.target.files[0] as File;

      this.fileSize = file.size + ' Bytes';
      this.file = file;
      this.fileName = this.file.name.substring(0, this.file.name.indexOf('.'));
      this.fileUploadForm.get('formFile')?.setValue(this.file.name);
      this.fileType = this.file.name.split('.').pop();

      let fileSizeForAll = event.target.files[0].size > 300 * 1024 * 1024; //(250 * 1024 * 1024);
      let fileSizeForXLS = event.target.files[0].size > 100 * 1024 * 1024; //(250 * 1024 * 1024);
      let fileSizeForXLSB = event.target.files[0].size > 30 * 1024 * 1024; //(250 * 1024 * 1024);

      switch (this.fileType) {
        case FileType.MSExcel1:
          if (fileSizeForXLS) {
            this.fileUploadForm.get('formFile').setErrors({ fileIsTooLargeForXLS: true });
            return;
          }
          break;
        case FileType.MSExcel3:
          if (fileSizeForXLSB) {
            this.fileUploadForm.get('formFile').setErrors({ fileIsTooLargeForXLSB: true });
            return;
          }
          break;
        default:
          if (fileSizeForAll) {
            this.fileUploadForm.get('formFile').setErrors({ fileIsTooLarge: true });
            return;
          }
      }

      //log the file here
      let logUploadedFilePayload: LogUploadedFileRequest = {
        fileName: this.file.name,
        fileSize: file.size,
        uploadedBy: sessionStorage.getItem('username'),
        uploadedDateTime: new Date().toUTCString()
      };

      this.configService.logUploadedFile(logUploadedFilePayload).subscribe({
        next: () => { },
        error: (err) => {
          console.log(err)
        }
      });

      this.fileUploadForm.get('formFile').setErrors(null);
      // if (fileSizeForAll) {
      //   this.fileUploadForm.get('formFile').setErrors({ fileIsTooLarge: true });
      //   return;
      // } else {
      //   this.fileUploadForm.get('formFile').setErrors(null);
      // }

      //this.configService.testBackendParser(file).subscribe();



      let fileIsValid =
        validExtensions.indexOf(this.file.name.split('.').pop()) > -1;
      this.fileUploadForm.get('formFile').setErrors(null);
      if (!fileIsValid) {
        this.fileUploadForm.get('formFile').setErrors({ fileIsValid: true });
        this.fileUploadForm.get('formFile').markAsTouched();
        this.file = null;
        this.toastr.error(ToastrMessages.InvalidFileExtension);
        return;
      }
      //TODO: temporary workaround
      setTimeout(() => {
        this.isNewProcess = true;
      }, 500);
      //this.config = this.resetAdditionalSettings();
      //this.fileType = validExtensions[validExtensions.indexOf(this.file.name.split('.').pop())];
      ////console.log(this.fileType);
      this.prepopForm();
      //this.configSubject.next(this.config);
      this.onPreview(this.config);

    }
  }



  resetForm() {
    this.notFoundSheetMessage = '';
    this.fileSubmissionCompleted = false;
    this.preview = false;
    this.isNewProcess = false;
    this.hasErrors = true;
    this.NewColumnFoundOnSource = false;
    this.InvalidDataSource = false;
    this.processConfigurationFormHasError = true;
    this.MissingColumnFoundOnSource = false;
    this.totalRowCountEXL = 0;
    this.file = null;
    this.fileUploadForm.get('formFile')?.setValue('');
    this.fileUploadForm.get('configuration')?.setValue(this.processNames?.length > 0 ? this.processNames[0] : null);
    this.fileUploadForm.get('clientInfo')?.reset();
    this.headersRow = [];
    this.recordsArray = [];
    this.recordsArrayForDisplay = [];
    this.arrayWithError = [];

    this.fileSize = null;
    this.columnDatatype = [];
    this.parsingTime = null;

    this.wksheets = [];
    this.dataxlsx = [];
    this.keyColumnNameSelected = [];
    this.columnsForDedupSelected = [];

    this.columnsForDedupIndexSelected = [];
    this.keyColumnIndexSelected = [];

    this.noOfDuplicates = 0;
    this.selectedDateTimeFormat = '';
    this.showDateTimeOptions = false;

    this.config = this.resetAdditionalSettings();
    //this.getProcessNamesByLoginId(); //moved after submitting
    this.modifySettingsClose();
    this.FilePreviewNotification = [];
    this.fileSubmissionCompleted = false;
    this.fileUploadForm.get('clientInfo')?.reset();
    this.columnsForDedupSelected = [];
    this.multiSheetForms = {};
    this.multiSheetConfigs = {};
    this.columnDatatypePerSheet = {};
    this.previousValidateSheetColumnData = {};
    this.recordsArrayForDisplayPerSheet = {};
    this.excelUploadedErrorMessages = [];
    this.selectedWorkSheetName = '';
    this.isSelectedExistingProcess = false;
    this.hasValidatedExcelConfig = false;
    this.filContainMissingColumn = false;
    this.startValidateExistingExcelConfigurationToFile = null;
    this.selectedProcesSheetNames = [];
    this.fileNotProcessed = false;
    this.newSheetInfoMessage = '';
    this.missingSheetInfoMessage = '';
    this.firstSheetSelectedIndex = 0;
    this.excelUploadMultiSheet = false;
    this.selectedProcessHasTabName = false;
    this.isSelectedExistingProcess = false;
    //this.showProcessConfig = true;

    this.ruleSet = [];
  }

  modifySettingsClose() {
    this.modifySettings = false;
  }


  resetFormByChangeMapping(selectedExistingProcess: boolean = false) {

    this.fileSubmissionCompleted = false;
    this.fileUploadForm.get('clientInfo')?.reset();
    if (!selectedExistingProcess) {
      this.fileUploadForm.get('configuration').setValue(null);
    }
    this.headersRow = [];
    this.recordsArray = [];
    this.recordsArrayForDisplay = [];
    this.arrayWithError = [];
    this.columnDatatype = [];
    this.parsingTime = null;
    this.wksheets = [];
    this.dataxlsx = [];
    this.keyColumnNameSelected = [];
    this.columnsForDedupSelected = [];
    this.columnsForDedupIndexSelected = [];
    this.keyColumnIndexSelected = [];
    this.noOfDuplicates = 0;
    this.selectedDateTimeFormat = '';
    this.showDateTimeOptions = false;
    this.config = this.resetAdditionalSettings();
    this.FilePreviewNotification = [];
    this.multiSheetForms = {};
    this.multiSheetConfigs = {};
    this.columnDatatypePerSheet = {};
    this.previousValidateSheetColumnData = {};
    this.recordsArrayForDisplayPerSheet = {};
    this.excelUploadedErrorMessages = [];
  }

  prepopForm() {
    //let generatedProcessAndTableName = this.fileName + '_' + new Date().getFullYear() + (new Date().getMonth() + 1).toString().padStart(2, "0") + new Date().getDate().toString().padStart(2, "0");
    //this.config.processName = generatedProcessAndTableName;
    //this.config.tableName = generatedProcessAndTableName;
    this.config.fileType = this.fileType;
    this.config.order_by_column_list_name_sort_dir = 'desc';
    if (
      this.fileType === FileType.MSExcel1 ||
      this.fileType === FileType.MSExcel2 ||
      this.fileType === FileType.MSExcel3
    ) {
      this.config.delimiter = '\\t';
    }
  }

  resetAdditionalSettings(): AdditionalSettings {
    return {
      flpConfigurationId: '',
      processName: '',
      description: '',
      delimiter: ',', //TODO: to autodetect remove ,
      key_columns: '',
      flexCheckHasHeaders: true,
      flexCheckSkipEmptyLines: true, //todo not yet saved
      flexCheckEscapeCharacter: '"', //todo not yet saved
      txtQuoteCharacter: '"',
      txtEscapeCharacter: '"', //todo not yet saved
      txtEncoding: 'UTF-8',
      flexCheckOrderByColumnListForDedup: false,
      order_by_column_list_name: '',
      order_by_column_list_name_sort_dir: 'desc',
      is_active: true,
      do_not_archive_file: false,
      spanish_to_english: false,
      roman_numerals_only: false,
      ignore_duplicate_rows: false,
      csv_column_name_list: '',
      keep_first_row: false,
      tableName: '',// this.fileName,
      databaseName: '', //todo: get assigned default database for user
      validate_fileschema: false,
      drop_history_table: false,
      drop_main_table: false,
      order_by_column_list_for_dedup: '',
      RegionId: '', //todo: get assigned default region
      SubRegionId: '', //todo: get assigned default subregion
      ClientId: '', //todo: get assigned default clientname
      databaseNames: [],
      databaseNameId: 0,
      fileType: FileType.CommaSeparatedValues, //default,
      databaseConfigurationId: '',
      convert_datatypes_column_list: '',
      column_name_list: '',
      skip_footer_rows: 0,
      skip_header_rows: 0,
      sender_communication_email: sessionStorage.getItem('emailID'),
      region: '',
      subRegion: '',
      clientName: '',
      file_column_mapping: [],
      mergeData: false,
      createHistoryTable: false,
      deltaJobId: '',
      deltaTableName: '',
      deltaServerNameId: 0,
      deltaStorageAccountId: '',
      deltaContainerName: '',
      deltaSource: '',
      sourcePath: '',
      dataSource: this.dataSource,
      securityGroups: [],
      frmSubmitted: false,
      ignoreSheet: false, //default value for ignoreSheet 
      newSheet: false, //default value for newSheet
      missingSheet: false,
      ruleSet: [],
      ruleSetNameId: null,
      ruleSetName: '',
      ruleType: '',
      subRuleType: '',
      patternType: '',
      ruleColumnName: '',
      isCombinationRule: false,
      requiredRuleDescription: '',
      uniqueRuleDescription: '',
      formatType: '',
      valueType: '',
      conditionType: '',
      aiPrompt: '',
      fromValue: 0,
      toValue: 0,
      spName: '',
      campaignId: '',
      internalCampaignId: '',

      //landing layer form values
      landingLayerFileExtension: [],
      landingLayerRegex: [],
      landingLayerPrefix: '',
      landingLayerDateformat: 0,
      landingLayerTimeformat: 0,
      landingLayerAcceptedPath: '',
      landingLayerRejectedPath: ''
    }
  }

  updateConfigOnly([additionalSettingsVal, columnData, ruleSet, ruleSetNames, formGroup]: [AdditionalSettings, ColumnNameDatatypeName[], ExcelRule[], RuleSetNames[], FormGroup]) {
    if (additionalSettingsVal) {
      this.config = additionalSettingsVal;

      if (ruleSet === null || ruleSet.length === 0) {
        this.config.ruleSet = [];
      } else {
        this.ruleSet = ruleSet;
      }

      if (ruleSetNames) { this.ruleSetNames = ruleSetNames; }
      if (this.selectedWorkSheetName) {
        this.multiSheetConfigs[this.selectedWorkSheetName] = { ...this.config };
        if (this.childComponent?.excelFileRule) {
          this.childComponent.excelFileRule = this.config.ruleSet;
        }
        this.columnDatatypePerSheet[this.selectedWorkSheetName] = columnData; //JSON.parse(JSON.stringify(this.columnDatatype));
        this.multiSheetForms[this.selectedWorkSheetName] = formGroup;
        //this.multiSheetForms[this.selectedWorkSheetName] = this.createProcessConfigForm(this.multiSheetConfigs[this.selectedWorkSheetName]);
        // this.childComponent.ruleSetNames = this.ruleSetNames;

        let processName_temp = '';

        //get first the sheet with processName; reason is what if user configured the 2nd, 3rd, 4th... sheet first
        for (const sheetName of Object.keys(this.multiSheetConfigs)) {
          const sheetConfig = this.multiSheetConfigs[sheetName];
          if (sheetConfig.processName.trim().length !== 0) {
            processName_temp = sheetConfig.processName;
          }
        }

        //set the tableName accdgly
        if (processName_temp) {
          for (const sheetName of Object.keys(this.multiSheetConfigs)) {
            const sheetConfig = this.multiSheetConfigs[sheetName];
            if (sheetConfig.processName.trim().length === 0) {
              if (sheetConfig.tableName === this.fileName) { //default fileName, change it to processName + sheetName
                sheetConfig.tableName = (processName_temp + '_' + cleanColumnName(sheetName)).replace('DI_', 'tbImport_');
                sheetConfig.deltaTableName = sheetConfig.tableName;
              }
            }
          }
        }
      }
    }
  }

  // this.keyColumnsSubject.next(''); //clear the list items
  createProcessConfigForm(formValues: AdditionalSettings): FormGroup {
    // this.dedupColumnsSubject.next(''); //clear the list items
    // console.log('createProcessConfigForm', formValues);

    const disableIgnore = formValues.csv_column_name_list.trim().length === 0 ? true : false;
    const form = this.fb.group({
      // Client tab
      // RegionId: [formValues.RegionId],
      // SubRegionId: [formValues.SubRegionId],
      // ClientId: [formValues.ClientId],
      // security_group: [formValues.securityGroups],
      // Additional Settings tab
      // delimiter:[formValues.delimiter,{ value:true, disabled: this.disableDelimiter }, Validators.required],// [formValues.delimiter,{disabled: this.disableDelimiter},Validators.required],
      // delimiter:[formValues.delimiter],
      delimiter: [
        { value: formValues.delimiter, disabled: this.disableDelimiter },
        {
          validators: [Validators.required],
          // asyncValidators: [this.yourAsyncValidator] // (optional) add if needed
        }
      ],
      txtQuoteCharacter: [formValues.txtQuoteCharacter],
      flexCheckHasHeaders: [formValues.flexCheckHasHeaders],
      flexCheckSkipEmptyLines: [formValues.flexCheckSkipEmptyLines],
      order_by_column_list_for_dedup: [formValues.order_by_column_list_for_dedup],
      order_by_column_list_name: [formValues.order_by_column_list_name],
      //default to 'desc' when the value is null, undefined, or an empty string ('')      
      order_by_column_list_name_sort_dir: [{ value: formValues.order_by_column_list_name_sort_dir?.trim() || 'desc', disabled: !formValues.ignore_duplicate_rows }],
      ignore_duplicate_rows: [{ value: formValues.ignore_duplicate_rows, disabled: !formValues.ignore_duplicate_rows }],
      keyColumnNameSelected: [formValues.csv_column_name_list],

      isSkipRow: [formValues.keep_first_row],
      // skip_header_rows: [formValues.skip_header_rows,{ value: '0', disabled: true }, [Validators.required, Validators.pattern("^\\d{1,2}$")]],
      // skip_footer_rows: [formValues.skip_footer_rows, { value: '0', disabled: true }, [Validators.required, Validators.pattern("^\\d{1,2}$")]],
      skip_header_rows: [
        { value: formValues.skip_header_rows, disabled: !formValues.keep_first_row },
        {
          validators: [
            Validators.required,
            Validators.pattern(/^\d{1,2}$/)
          ]
        }
      ],
      skip_footer_rows: [
        { value: formValues.skip_footer_rows, disabled: !formValues.keep_first_row },
        {
          validators: [
            Validators.required,
            Validators.pattern(/^\d{1,2}$/)
          ]
        }
      ],

      is_active: [formValues.is_active],
      do_not_archive_file: [formValues.do_not_archive_file],
      spanish_to_english: [formValues.spanish_to_english],
      roman_numerals_only: [formValues.roman_numerals_only],
      // Database/Databricks tab
      databaseConfigurationId: [+formValues.databaseConfigurationId],
      databaseName: [formValues.databaseName, Validators.required],
      databaseNameId: [formValues.databaseNameId],
      tableName: [formValues.tableName, [Validators.required, Validators.pattern(this.processNamePattern)]],
      deltaStorageAccountId: [formValues.deltaStorageAccountId, this.dataSource === DataSourceType.DataBricks ? [Validators.required] : []],
      deltaContainerName: [formValues.deltaContainerName, this.dataSource === DataSourceType.DataBricks ? [Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.pattern(/^(?!-+$)[a-z0-9][a-z0-9\- ]*$/), noWhitespaceValidator] : []],
      deltaSource: [formValues.deltaSource, this.dataSource === DataSourceType.DataBricks ? [Validators.required, Validators.maxLength(100), Validators.pattern(/^(?!.*([ _-])\1)[a-zA-Z0-9_/ -]+$/), noWhitespaceValidator] : []],
      deltaTableName: [formValues.deltaTableName, this.dataSource === DataSourceType.DataBricks ? [Validators.required, Validators.pattern(/^(?!.*(__|--))[a-zA-Z0-9_-]*$/), Validators.maxLength(100)] : []],
      deltaServerNameId: [formValues.deltaServerNameId, this.dataSource === DataSourceType.DataBricks ? [Validators.required] : []],
      deltaJobId: [formValues.deltaJobId, this.dataSource === DataSourceType.DataBricks ? [Validators.required, Validators.pattern(/^[0-9]+$/)] : []],
      // Row
      drop_main_table: [formValues.drop_main_table],
      is_validate_fileschema_with_target_table: [formValues.validate_fileschema],
      drop_history_table: [formValues.drop_history_table],
      mergeData: [formValues.mergeData],
      createHistoryTable: [formValues.createHistoryTable],
      frmSubmitted: [formValues.frmSubmitted],
      ignoreSheet: [formValues.ignoreSheet],
      newSheet: [formValues.newSheet],
      missingSheet: [formValues.missingSheet]
      // ... other controls ...
      // clientInfo: this.fb.group({
      //   RegionId: ['', Validators.required],
      //   SubRegionId: ['', Validators.required],
      //   ClientId: ['', Validators.required],
      //   security_group: [[], Validators.required]
      // })
      // Base setting
      // processName: [formValues.processName],
      // description: [formValues.description],
      // Add any other controls you use in your form!

      //rules     
      , ruleSetNameId: [formValues.ruleSetNameId ?? null],
      ruleSetNames: [null],
      ruleSetName: [formValues.ruleSetName],
      ruleSetDescription: '',
      ruleType: [null],
      subRuleType: '',
      patternType: '',
      ruleColumnName: '',
      ruleColumnName2: '',
      isCombinationRule: false,
      requiredRuleDescription: '',
      uniqueRuleDescription: '',
      formatType: '',
      valueType: '',
      conditionType: '',
      aiPrompt: '',
      fromValue: 0,
      toValue: 0,
      spName: [null],
      isAllowNullOrEmptySpaces: false
    });

    // if(formValues?.deltaContainerName){
    //   form.get('deltaContainerName').markAsTouched();
    // }

    this.markControlsWithValuesAsTouched(form);

    return form;




  }

  markControlsWithValuesAsTouched(control: AbstractControl): void {
    // If the control is a FormGroup, iterate through its child controls.
    if (control instanceof FormGroup) {
      Object.keys(control.controls).forEach(key => {
        const childControl = control.get(key);
        if (childControl) {
          this.markControlsWithValuesAsTouched(childControl);
        }
      });
      // If the control is a FormArray, iterate through its child controls.
    } else if (control instanceof FormArray) {
      control.controls.forEach(childControl => {
        this.markControlsWithValuesAsTouched(childControl);
      });
      // If the control is a FormControl and has a non-falsy value, mark it as touched.
    } else if (control instanceof FormControl) {
      // Check for a non-falsy value. This includes non-empty strings,
      // numbers, booleans (if true), etc.
      if (control.value) {
        control.markAsTouched();
      }
    }
  }


  originalRowCount: number = 0;
  onPreview = async (event: AdditionalSettings | null): Promise<boolean> => {

    let isSameOrder: boolean = true;
    this.busyService.busy();
    this.originalRowCount = 0;
    //console.log(`onPreview start:${performance.now()}`);
    this.parseComplete = false;
    this.hasErrors = false;
    this.isValidDataType = true;
    this.noOfDuplicates = 0;
    this.keyColumnIndexSelected = [];
    this.keyColumnNameSelected = [];
    this.recordsArrayForDisplayPerSheet = {};

    let delimiter: string = ',';
    let skipEmptyLines = true;
    let quoteChar: string = '"';

    const startTime = performance.now();
    this.keyColumnsSubject.next(''); //clear the list items
    this.dedupColumnsSubject.next('');

    if (event) {

      this.config = event;
      //this.config.ignore_duplicate_rows = false;

      if ((this.fileType === FileType.MSExcel1) || (this.fileType === FileType.MSExcel2) || (this.fileType === FileType.MSExcel3)) {

        if (!this.isSelectedExistingProcess && this.multiSheetConfigs[this.selectedWorkSheetName]) {
          //this.config = this.multiSheetConfigs[this.selectedWorkSheetName];
          this.multiSheetConfigs[this.selectedWorkSheetName].flexCheckHasHeaders = event.flexCheckHasHeaders;
          this.multiSheetConfigs[this.selectedWorkSheetName].txtQuoteCharacter = event.txtQuoteCharacter;
          this.multiSheetConfigs[this.selectedWorkSheetName].flexCheckSkipEmptyLines = event.flexCheckSkipEmptyLines;
          this.multiSheetConfigs[this.selectedWorkSheetName].delimiter = event.delimiter;
          this.multiSheetConfigs[this.selectedWorkSheetName].ignoreSheet = event.ignoreSheet;
        }
        //  delimiter = event.delimiter;
        // this.flexCheckHasHeaders = event.flexCheckHasHeaders;
        skipEmptyLines = event.flexCheckSkipEmptyLines;
        quoteChar = event.txtQuoteCharacter;

      } else {
        delimiter = event.delimiter;
        this.flexCheckHasHeaders = event.flexCheckHasHeaders;
        skipEmptyLines = event.flexCheckSkipEmptyLines;
        quoteChar = event.txtQuoteCharacter;
      }

      this.configSubject.next(this.config);
    }

    switch (this.fileType) {
      case FileType.CommaSeparatedValues:
      case FileType.TextFiles:
        //if (this.fileType !== FileType.CommaSeparatedValues) this.fileType = FileType.TextFiles;

        const parsedCSVTXT = await this.myParser.asyncParseCSVTXT(this.file, {
          delimiter: delimiter,
          hasHeader: this.flexCheckHasHeaders,
          skipEmptyLines: skipEmptyLines,
          quoteCharacter: quoteChar,
          worker: true,
          useOnlyRomanNumerals: this.config.roman_numerals_only,
          useOnlyEnglishLetters: this.config.spanish_to_english,
          englishOnlyCharacters: this.englishOnlyCharacters,
        });

        if (parsedCSVTXT) {
          this.hasErrors = false;
          const endTime = performance.now();
          this.parsingTime = endTime - startTime;

          //if (this.columnDatatype.length === 0)
          let tempColumnDataType = [...this.columnDatatype]; //store in a temporary variable
          //we reset the columnDataType because if english to spanish/show roman numerals is either true, 
          //there can be instances that the keyColumn header label was changed (ex. Customer12 -> CustomerXII)
          //in cases like this, we need to reset the key/dedup columns
          this.columnDatatype = [];


          this.recordsArray = parsedCSVTXT.data;
          this.originalRowCount = this.recordsArray.length;
          this.recordsArrayForDisplay = parsedCSVTXT.data;
          this.rowCountForDisplay = parsedCSVTXT.data.length;
          //this.headersRow = this.helperUtil.detectAndFixSameColumnName(result.meta.fields);
          //this.headersRowForDisplayingRecords = result.meta.fields;
          this.headersRow = parsedCSVTXT.meta.fields;

          var dataToSliced = [...parsedCSVTXT.data];

          if (this.config.flexCheckHasHeaders) {
            if (this.config.skip_header_rows > 0) {
              if (
                parsedCSVTXT.meta.fields &&
                this.config.skip_header_rows === 1
              ) {
                //row1 were headers
                dataToSliced = parsedCSVTXT.data.slice(
                  this.config.skip_header_rows
                ); //dataToSliced.slice(this.config.skip_header_rows);
              } else {
                //-1 because result.data does not contain the row1, row1 is the result.meta.fields
                dataToSliced = [
                  ...parsedCSVTXT.data.slice(this.config.skip_header_rows),
                ]; //dataToSliced.slice(this.config.skip_header_rows);
              }

              this.recordsArrayForDisplay = [...dataToSliced];
            }
          } else {
            this.recordsArrayForDisplay = [...dataToSliced];

            if (this.config.skip_header_rows > 0) {
              dataToSliced = dataToSliced.slice(
                this.config.skip_header_rows
              );
              this.recordsArrayForDisplay = [...dataToSliced];
            }
          }

          if (this.config.skip_footer_rows > 0) {
            // var slicedData = [...this.recordsArrayForDisplay];
            dataToSliced = dataToSliced
              .reverse()
              .slice(+this.config.skip_footer_rows);
            this.recordsArrayForDisplay = [...dataToSliced.reverse()];
            // this.dataxlsx = [...this.dataxlsx.slice(0, +this.config.skip_footer_rows + 1)];
          }

          this.recordsArray = this.recordsArrayForDisplay;
          this.rowCountForDisplay = this.recordsArray.length;
          if (parsedCSVTXT.meta.fields === undefined) {
            if (this.config.flpConfigurationId === '') {
              //incase the user uncheck the header provided in additional settings
              this.headersRow = this.createCustomHeaderRow(
                this.recordsArrayForDisplay
              );
              this.headersRow?.forEach((header, index) => {
                let cleanHeaderName = header;
                //this.helperUtil.cleanColumnName(header);
                this.columnDatatype.push({
                  index: index,
                  ColumnName: cleanHeaderName,
                  DbColumnName: cleanHeaderName,
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
              });

              //lets compare old and new
              const columnNameFromColumnDataType = this.columnDatatype.map(col => col.ColumnName);
              isSameOrder = tempColumnDataType.map(col => col.ColumnName).length > 0 && tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);
              if (isSameOrder) {
                this.columnDatatype = tempColumnDataType;
                let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
                this.keyColumnsSubject.next(keyColumnsList);
                this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
                let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
                this.dedupColumnsSubject.next(dedupColumnList);

              } else {
                this.childComponent?.alertUIValidationReset();
              }
            } else {
              if (this.config.flexCheckHasHeaders === false) {
                this.headersRow = this.createCustomHeaderRow(
                  this.recordsArrayForDisplay
                );
                this.headersRow?.forEach((header, index) => {
                  let cleanHeaderName = header; //this.helperUtil.cleanColumnName(header);
                  this.columnDatatype.push({
                    index: index,
                    ColumnName: cleanHeaderName,
                    DbColumnName: cleanHeaderName,
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
                });
                this.childComponent?.alertUIValidationReset();
              }

            }

          } else {

            if (this.columnDatatype.length === 0) {
              if (+this.config.skip_header_rows > 0) {
                if (this.config.flexCheckHasHeaders) {
                  //use row1 as the headers

                  //let row1 = this.recordsArrayForDisplay.slice(0, this.config.skip_header_rows);
                  let row1: any;
                  if (
                    parsedCSVTXT.meta.fields && this.config.skip_header_rows === 1
                  ) {
                    row1 = parsedCSVTXT.data.slice(0, this.config.skip_header_rows); //dataToSliced.slice(0, this.config.skip_header_rows) ;
                  } else {
                    row1 = parsedCSVTXT.data.slice(
                      this.config.skip_header_rows - 1,
                      this.config.skip_header_rows
                    ); //dataToSliced.slice(0, this.config.skip_header_rows-1) ;//
                  }


                  for (var i = 0; i < parsedCSVTXT.meta.fields.length; i++) {
                    let columnName = cleanColumnName(row1[0][parsedCSVTXT.meta.fields[i]]); // this.helperUtil.cleanColumnName(row1[0][result.meta.fields[i]]);
                    if (columnName.trim() === '') columnName = `COL${i}`;
                    this.columnDatatype.push({
                      index: i,
                      ColumnName: columnName,
                      DbColumnName: columnName,
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
                  }


                  if (this.config.spanish_to_english) {
                    this.convertToEnglishCharacters(this.columnDatatype);
                  }
                  if (this.config.roman_numerals_only) {
                    this.displayOnlyRomanNumerals(this.columnDatatype);
                  }
                  this.helperUtil.findDuplicateColumnAndGenerateNew(this.columnDatatype);

                  const columnNameFromColumnDataType = this.columnDatatype.map(col => col.ColumnName);
                  isSameOrder = tempColumnDataType.map(col => col.ColumnName).length > 0 && tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);

                  if (!isSameOrder) {
                    this.childComponent?.alertUIValidationReset();
                  } else {
                    this.columnDatatype = tempColumnDataType;
                    let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
                    this.keyColumnsSubject.next(keyColumnsList);
                    this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
                    let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
                    this.dedupColumnsSubject.next(dedupColumnList);
                  }

                  if (this.recordsArrayForDisplay.length > 20) {
                    this.recordsArrayForDisplay = [...this.recordsArrayForDisplay.slice(0, 20),];
                  }
                } else {
                }
              } else {

                let tempNewColumnDataType: ColumnNameDatatypeName[] = this.createColumnDataFromMetaHeaders(parsedCSVTXT.meta.fields);

                //consider comparing the tempColumnDataType (old) and the tempNewColumnDataType.
                const columnNameFromColumnDataType = tempNewColumnDataType.map(col => col.ColumnName);
                isSameOrder = tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);

                if (!isSameOrder) {
                  this.columnDatatype = tempNewColumnDataType; //get all the new columnDataType
                  //since we have a new set of columnDataType, let's update the keycolumn, keycolumnselectted dedup to blank
                  this.config.ignore_duplicate_rows = false;
                  this.config.key_columns = '';
                  this.config.order_by_column_list_name_sort_dir = 'desc';
                  this.childComponent?.alertUIValidationReset();
                } else {
                  if (tempColumnDataType.length > 0) {
                    //retrieve the old columnDataType
                    this.columnDatatype = tempColumnDataType;
                    //set the keycolumn and dedup in additional settings

                    let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
                    this.keyColumnsSubject.next(keyColumnsList);
                    this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
                    let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
                    this.dedupColumnsSubject.next(dedupColumnList);

                  } else {

                    //create new columnDataType
                    this.columnDatatype = this.createColumnDataFromMetaHeaders(this.headersRow);
                  }
                }

              }
            }
          }

          this.hasErrors = false;
          if (parsedCSVTXT.errors.length > 0) {

          } else {
            //TODO: create error message
            //this.sharedService.showNotification(true, "Data Source Valid", "Correct data source to proceed.");
          }

          //find duplicate columns
          //this.findDuplicateColumnAndGenerateNew();

          if (this.config.flexCheckSkipEmptyLines) {
            for (let i = this.recordsArrayForDisplay.length - 1; i >= 0; i--) {
              let numOfEmptyRows = 0;

              this.headersRow.forEach((d, j) => {
                if (this.flexCheckHasHeaders) {
                  if (this.recordsArrayForDisplay[i][d]?.trim().length === 0
                    || this.recordsArrayForDisplay[i][d] === undefined) {
                    numOfEmptyRows++;
                  }
                } else {
                  if (this.recordsArrayForDisplay[i][j]?.trim().length === 0
                    || this.recordsArrayForDisplay[i][j] === undefined) {
                    numOfEmptyRows++;
                  }
                }
              });

              if (numOfEmptyRows === this.columnDatatype.length) {
                //cleanRecordsArrayForDisplay.splice(i,1);
                this.recordsArrayForDisplay.splice(i, 1);
              }
            }
          }
          this.parseComplete = true;
          this.preview = true;
          this.isFileUploaded = true;

          if (this.config.flpConfigurationId !== '') {

            this.keyColumnsSubject.next(this.config.key_columns);
            this.dedupColumnsSubject.next(
              this.config.order_by_column_list_for_dedup
            );

            this.startValidateExistingConfigurationToFile = setTimeout(
              (_) => {
                this.validateExistingConfigurationToFile();
                this.busyService.idle();
              },
              500
            );

          } else {

            //apply sorting and dedup

            setTimeout(() => {
              this.columnDatatype.forEach((c, i) => {
                //if (c.missingColumn === false || c.newColumn === false) {
                if (c.columnForDedeup) {
                  this.sortAndRemove(c.index, true, false, true);
                } else if (c.ColumnKey) {
                  this.sortAndRemove(c.index, true, true, false);
                }
                //}
                if (!c.willInclude) {
                  this.toggleExcludedClass(i);
                }

              });

              this.busyService.idle();
            }, 0);


            this.modifySettingsOpen();
          }


        }

        {
          // this.myParser
          //   .parseCSVTXT(this.file, {
          //     delimiter: delimiter,
          //     hasHeader: this.flexCheckHasHeaders,
          //     skipEmptyLines: skipEmptyLines,
          //     quoteCharacter: quoteChar,
          //     worker: true,
          //     useOnlyRomanNumerals: this.config.roman_numerals_only,
          //     useOnlyEnglishLetters: this.config.spanish_to_english,
          //     englishOnlyCharacters: this.englishOnlyCharacters,
          //   })
          //   .subscribe({
          //     next: (result) => {
          //       //console.log(result);
          //       this.hasErrors = false;
          //       const endTime = performance.now();
          //       this.parsingTime = endTime - startTime;

          //       //if (this.columnDatatype.length === 0)
          //       let tempColumnDataType = [...this.columnDatatype]; //store in a temporary variable
          //       //we reset the columnDataType because if english to spanish/show roman numerals is either true, 
          //       //there can be instances that the keyColumn header label was changed (ex. Customer12 -> CustomerXII)
          //       //in cases like this, we need to reset the key/dedup columns
          //       this.columnDatatype = [];


          //       this.recordsArray = result.data;
          //       this.originalRowCount = this.recordsArray.length;
          //       this.recordsArrayForDisplay = result.data;
          //       this.rowCountForDisplay = result.data.length;
          //       //this.headersRow = this.helperUtil.detectAndFixSameColumnName(result.meta.fields);
          //       //this.headersRowForDisplayingRecords = result.meta.fields;
          //       this.headersRow = result.meta.fields;

          //       var dataToSliced = [...result.data];

          //       if (this.config.flexCheckHasHeaders) {
          //         if (this.config.skip_header_rows > 0) {
          //           if (
          //             result.meta.fields &&
          //             this.config.skip_header_rows === 1
          //           ) {
          //             //row1 were headers
          //             dataToSliced = result.data.slice(
          //               this.config.skip_header_rows
          //             ); //dataToSliced.slice(this.config.skip_header_rows);
          //           } else {
          //             //-1 because result.data does not contain the row1, row1 is the result.meta.fields
          //             dataToSliced = [
          //               ...result.data.slice(this.config.skip_header_rows),
          //             ]; //dataToSliced.slice(this.config.skip_header_rows);
          //           }

          //           this.recordsArrayForDisplay = [...dataToSliced];
          //         }
          //       } else {
          //         this.recordsArrayForDisplay = [...dataToSliced];

          //         if (this.config.skip_header_rows > 0) {
          //           dataToSliced = dataToSliced.slice(
          //             this.config.skip_header_rows
          //           );
          //           this.recordsArrayForDisplay = [...dataToSliced];
          //         }
          //       }

          //       if (this.config.skip_footer_rows > 0) {
          //         // var slicedData = [...this.recordsArrayForDisplay];
          //         dataToSliced = dataToSliced
          //           .reverse()
          //           .slice(+this.config.skip_footer_rows);
          //         this.recordsArrayForDisplay = [...dataToSliced.reverse()];
          //         // this.dataxlsx = [...this.dataxlsx.slice(0, +this.config.skip_footer_rows + 1)];
          //       }

          //       this.recordsArray = this.recordsArrayForDisplay;
          //       this.rowCountForDisplay = this.recordsArray.length;
          //       if (result.meta.fields === undefined) {
          //         if (this.config.flpConfigurationId === '') {
          //           //incase the user uncheck the header provided in additional settings
          //           this.headersRow = this.createCustomHeaderRow(
          //             this.recordsArrayForDisplay
          //           );
          //           this.headersRow?.forEach((header, index) => {
          //             let cleanHeaderName = header;
          //             //this.helperUtil.cleanColumnName(header);
          //             this.columnDatatype.push({
          //               index: index,
          //               ColumnName: cleanHeaderName,
          //               DbColumnName: cleanHeaderName,
          //               DatatypeName: 'string',
          //               willInclude: true,
          //               ColumnKey: false,
          //               newColumn: false,
          //               missingColumn: false,
          //               invalidDataType: false,
          //               willAddNewColumn: true,
          //               invalidColumnName: false,
          //               columnForDedeup: false,
          //               dateTimeFormatId: 0,
          //               isDuplicateColumn: false,
          //               useMultipleSheets: true
          //             });
          //           });

          //           //lets compare old and new
          //           const columnNameFromColumnDataType = this.columnDatatype.map(col => col.ColumnName);
          //           isSameOrder = tempColumnDataType.map(col => col.ColumnName).length > 0 && tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);
          //           if (isSameOrder) {
          //             this.columnDatatype = tempColumnDataType;
          //             let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
          //             this.keyColumnsSubject.next(keyColumnsList);
          //             this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
          //             let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
          //             this.dedupColumnsSubject.next(dedupColumnList);

          //           } else {
          //             this.childComponent?.alertUIValidationReset();
          //           }
          //         } else {
          //           if (this.config.flexCheckHasHeaders === false) {
          //             this.headersRow = this.createCustomHeaderRow(
          //               this.recordsArrayForDisplay
          //             );
          //             this.headersRow?.forEach((header, index) => {
          //               let cleanHeaderName = header; //this.helperUtil.cleanColumnName(header);
          //               this.columnDatatype.push({
          //                 index: index,
          //                 ColumnName: cleanHeaderName,
          //                 DbColumnName: cleanHeaderName,
          //                 DatatypeName: 'string',
          //                 willInclude: true,
          //                 ColumnKey: false,
          //                 newColumn: false,
          //                 missingColumn: false,
          //                 invalidDataType: false,
          //                 willAddNewColumn: true,
          //                 invalidColumnName: false,
          //                 columnForDedeup: false,
          //                 dateTimeFormatId: 0,
          //                 isDuplicateColumn: false,
          //                 useMultipleSheets: true
          //               });
          //             });
          //             this.childComponent?.alertUIValidationReset();
          //           }

          //           //let's check if row1 equates to db column headers
          //           ////console.log(this.config.csv_column_name_list);

          //           //get the row1
          //           // if (result.data.length > 1) {
          //           //   let row1: any[] = result.data[0];
          //           //   this.row1AreHeaders = false;
          //           //   row1.forEach((c, i) => {
          //           //     let foundColumn = this.config.csv_column_name_list.split(',').find(existingColumnName => existingColumnName.split('=')[0] === c.replace('\r', ''));
          //           //     let columnDatatype = this.config.convert_datatypes_column_list.split(',').find(existingColumn => existingColumn.split('=')[0] === c.replace('\r',''));
          //           //     let columnKeys = this.config.key_columns.split(',').find(existingColumn => existingColumn === c.replace('\r',''));

          //           //     if (foundColumn) {
          //           //       this.row1AreHeaders = true;
          //           //       this.columnDatatype.push({
          //           //         index: +foundColumn.split('=')[1], ColumnName: foundColumn.split('=')[0], DatatypeName: columnDatatype.split('=')[1], willInclude: true,
          //           //         ColumnKey: columnKeys ? true : false, newColumn: false, missingColumn: false
          //           //       });
          //           //     } else {

          //           //       // this.columnDatatype.push({
          //           //       //   index: +foundColumn.split('=')[1], ColumnName: foundColumn.split('=')[0], DatatypeName: columnDatatype.split('=')[1], willInclude: true,
          //           //       //   ColumnKey: columnKeys ? true : false, newColumn: false, missingColumn: false
          //           //       // });
          //           //     }

          //           //     if(this.row1AreHeaders){
          //           //       //how would we know that row1 on the file has headers
          //           //       //if there's a header that is found from database we need to add new columns as newcolumn or missing column
          //           //     }

          //           //   });

          //           //   this.recordsArray = result.data.splice(1);
          //           //   this.recordsArrayForDisplay = this.recordsArray;
          //           //   this.rowCountForDisplay = this.recordsArrayForDisplay.length;
          //           // }
          //         }

          //       } else {

          //         if (this.columnDatatype.length === 0) {
          //           if (+this.config.skip_header_rows > 0) {
          //             if (this.config.flexCheckHasHeaders) {
          //               //use row1 as the headers

          //               //let row1 = this.recordsArrayForDisplay.slice(0, this.config.skip_header_rows);
          //               let row1: any;
          //               if (
          //                 result.meta.fields && this.config.skip_header_rows === 1
          //               ) {
          //                 row1 = result.data.slice(0, this.config.skip_header_rows); //dataToSliced.slice(0, this.config.skip_header_rows) ;
          //               } else {
          //                 row1 = result.data.slice(
          //                   this.config.skip_header_rows - 1,
          //                   this.config.skip_header_rows
          //                 ); //dataToSliced.slice(0, this.config.skip_header_rows-1) ;//
          //               }


          //               for (var i = 0; i < result.meta.fields.length; i++) {
          //                 let columnName = cleanColumnName(row1[0][result.meta.fields[i]]); // this.helperUtil.cleanColumnName(row1[0][result.meta.fields[i]]);
          //                 if (columnName.trim() === '') columnName = `COL${i}`;
          //                 this.columnDatatype.push({
          //                   index: i,
          //                   ColumnName: columnName,
          //                   DbColumnName: columnName,
          //                   DatatypeName: 'string',
          //                   willInclude: true,
          //                   ColumnKey: false,
          //                   newColumn: false,
          //                   missingColumn: false,
          //                   invalidDataType: false,
          //                   willAddNewColumn: true,
          //                   invalidColumnName: false,
          //                   columnForDedeup: false,
          //                   dateTimeFormatId: 0,
          //                   isDuplicateColumn: false,
          //                   useMultipleSheets: true
          //                 });
          //               }


          //               if (this.config.spanish_to_english) {
          //                 this.convertToEnglishCharacters(this.columnDatatype);
          //               }
          //               if (this.config.roman_numerals_only) {
          //                 this.displayOnlyRomanNumerals(this.columnDatatype);
          //               }
          //               this.helperUtil.findDuplicateColumnAndGenerateNew(this.columnDatatype);

          //               const columnNameFromColumnDataType = this.columnDatatype.map(col => col.ColumnName);
          //               const isSameOrder = tempColumnDataType.map(col => col.ColumnName).length > 0 && tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);

          //               if (!isSameOrder) {

          //               } else {
          //                 this.columnDatatype = tempColumnDataType;
          //                 let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
          //                 this.keyColumnsSubject.next(keyColumnsList);
          //                 this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
          //                 let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
          //                 this.dedupColumnsSubject.next(dedupColumnList);
          //               }

          //               if (this.recordsArrayForDisplay.length > 20) {
          //                 this.recordsArrayForDisplay = [...this.recordsArrayForDisplay.slice(0, 20),];
          //               }
          //             } else {
          //             }
          //           } else {

          //             let tempNewColumnDataType: ColumnNameDatatypeName[] = this.createColumnDataFromMetaHeaders(result.meta.fields);

          //             //consider comparing the tempColumnDataType (old) and the tempNewColumnDataType.
          //             const columnNameFromColumnDataType = tempNewColumnDataType.map(col => col.ColumnName);
          //             const isSameOrder = tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);


          //             // tempNewColumnDataType.forEach((newCol , index) => {
          //             //   const oldCol = tempColumnDataType[index];
          //             //   if(oldCol && newCol.ColumnName !== oldCol.ColumnName){
          //             //     newCol.ColumnKey = false;
          //             //     newCol.columnForDedeup  = false;
          //             //     newCol.DatatypeName  = 'string';
          //             //     newCol.dateTimeFormatId  = 0;

          //             //     this.config.ignore_duplicate_rows = false;
          //             //   this.config.key_columns = '';
          //             //   this.config.order_by_column_list_name_sort_dir = 'desc';

          //             //   }
          //             // });
          //             if (!isSameOrder) {
          //               this.columnDatatype = tempNewColumnDataType; //get all the new columnDataType
          //               //since we have a new set of columnDataType, let's update the keycolumn, keycolumnselectted dedup to blank
          //               this.config.ignore_duplicate_rows = false;
          //               this.config.key_columns = '';
          //               this.config.order_by_column_list_name_sort_dir = 'desc';
          //               this.childComponent?.alertUIValidationReset();
          //             } else {
          //               if (tempColumnDataType.length > 0) {
          //                 //retrieve the old columnDataType
          //                 this.columnDatatype = tempColumnDataType;
          //                 //set the keycolumn and dedup in additional settings

          //                 let keyColumnsList = this.columnDatatype.filter(x => x.ColumnKey).map(x => x.ColumnName).join(', ');
          //                 this.keyColumnsSubject.next(keyColumnsList);
          //                 this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
          //                 let dedupColumnList = this.columnDatatype.filter(x => x.columnForDedeup).map(x => x.ColumnName).join(', ');
          //                 this.dedupColumnsSubject.next(dedupColumnList);

          //               } else {

          //                 //create new columnDataType
          //                 this.columnDatatype = this.createColumnDataFromMetaHeaders(this.headersRow);
          //               }
          //             }

          //             // this.headersRow?.forEach((header, index) => {
          //             //   let cleanHeaderName = header; //cleanColumnName(header); //this.helperUtil.cleanColumnName(header);
          //             //   this.columnDatatype.push({
          //             //     index: index,
          //             //     ColumnName: cleanHeaderName,
          //             //     DbColumnName: cleanHeaderName,
          //             //     DatatypeName: 'string',
          //             //     willInclude: true,
          //             //     ColumnKey: false,
          //             //     newColumn: false,
          //             //     missingColumn: false,
          //             //     invalidDataType: false,
          //             //     willAddNewColumn: true,
          //             //     invalidColumnName: false,
          //             //     columnForDedeup: false,
          //             //     dateTimeFormatId: 0,
          //             //     isDuplicateColumn: false,
          //             //     useMultipleSheets: true
          //             //   });
          //             // });
          //           }
          //         }
          //       }

          //       this.hasErrors = false;
          //       if (result.errors.length > 0) {
          //         //this.toastr.error(ToastrMessages.InvalidDataSource);
          //         ////this.hasErrors = true;
          //         //this.InvalidDataSource = true;
          //         //this.parseComplete = true;
          //         ////this.arrayWithError = result.errors;
          //         //return;
          //       } else {
          //         //TODO: create error message
          //         //this.sharedService.showNotification(true, "Data Source Valid", "Correct data source to proceed.");
          //       }

          //       //find duplicate columns
          //       //this.findDuplicateColumnAndGenerateNew();

          //       if (this.config.flexCheckSkipEmptyLines) {
          //         for (let i = this.recordsArrayForDisplay.length - 1; i >= 0; i--) {
          //           let numOfEmptyRows = 0;

          //           this.headersRow.forEach((d, j) => {
          //             if (this.flexCheckHasHeaders) {
          //               if (this.recordsArrayForDisplay[i][d]?.trim().length === 0
          //                 || this.recordsArrayForDisplay[i][d] === undefined) {
          //                 numOfEmptyRows++;
          //               }
          //             } else {
          //               if (this.recordsArrayForDisplay[i][j]?.trim().length === 0
          //                 || this.recordsArrayForDisplay[i][j] === undefined) {
          //                 numOfEmptyRows++;
          //               }
          //             }
          //           });

          //           if (numOfEmptyRows === this.columnDatatype.length) {
          //             //cleanRecordsArrayForDisplay.splice(i,1);
          //             this.recordsArrayForDisplay.splice(i, 1);
          //           }
          //         }
          //       }
          //       this.parseComplete = true;
          //       this.preview = true;
          //       this.isFileUploaded = true;

          //       //this.displayOnlyEnglishCharacters();

          //       // if(!this.config.spanish_to_english && this.config.roman_numerals_only){
          //       //   this.displayOnlyRomanNumerals(this.config.roman_numerals_only);
          //       // }

          //       //if (this.fileUploadForm.get('configuration')?.value !== 'new') {
          //       if (this.config.flpConfigurationId !== '') {
          //         //this.processConfigurationFormHasError =

          //         // //reflect correct data types from database
          //         // var column_list = this.config.convert_datatypes_column_list.split(',');
          //         // this.columnDatatype.forEach(header => {
          //         //   column_list.forEach(c => {
          //         //     if (c.split('=')[0] === header.ColumnName) {
          //         //       header.DatatypeName = c.split('=')[1];
          //         //     }
          //         //   });
          //         // });

          //         this.keyColumnsSubject.next(this.config.key_columns);
          //         this.dedupColumnsSubject.next(
          //           this.config.order_by_column_list_for_dedup
          //         );

          //         this.startValidateExistingConfigurationToFile = setTimeout(
          //           (_) => {
          //             this.validateExistingConfigurationToFile();
          //           },
          //           500
          //         );

          //       } else {

          //         //apply sorting and dedup

          //         setTimeout(() => {
          //           this.columnDatatype.forEach((c, i) => {
          //             //if (c.missingColumn === false || c.newColumn === false) {
          //             if (c.columnForDedeup) {
          //               this.sortAndRemove(c.index, true, false, true);
          //             } else if (c.ColumnKey) {
          //               this.sortAndRemove(c.index, true, true, false);
          //             }
          //             //}
          //             if (!c.willInclude) {
          //               this.toggleExcludedClass(i);
          //             }

          //           });
          //         }, 0);


          //         this.modifySettingsOpen();
          //       }

          //       this.busyService.idle();
          //     },
          //     error: (error) => {
          //       this.isFileUploaded = false;
          //       //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
          //       this.toastr.error(ModalMessages.SomethingWentWrong);
          //     },
          //   });
        }
        break;
      case FileType.MSExcel1:
      case FileType.MSExcel2:
      case FileType.MSExcel3:

        //try {
        const parsedExcel = await this.myParser.asyncParseExcel(
          this.file,
          this.flexCheckHasHeaders ? 1 : 0,
          this.config.skip_header_rows,
          this.config.flexCheckSkipEmptyLines,
          this.mappingType
        );

        if (parsedExcel) {
          this.wksheets = parsedExcel.filter(wk => wk.sheetName && wk.sheetName.trim().length > 0);
          const endTime = performance.now();

          const totalSeconds = Math.floor((endTime - startTime) / 1000);
          const minutes = Math.floor(totalSeconds / 60);
          const seconds = totalSeconds % 60;
          const totalTime = `${minutes}:${seconds.toString().padStart(2, '0')}`;

          this.parsingTime = endTime - startTime;
          this.disableDelimiter = true;
          this.hasErrors = false;
          this.columnDatatype = [];
          this.hasErrors = false;
          let newSheets = [];
          let missingSheet = [];

          // const hasValidSheetName = Object.keys(this.multiSheetConfigs).some(
          //   sheetName => sheetName.trim() !== ''
          // );


          if (this.excelUploadMultiSheet === false && this.isSelectedExistingProcess && this.selectedProcessHasTabName === false) {
            this.hasErrors = false;
            const btnNext = document.getElementById(
              'btnNext'
            ) as HTMLButtonElement;

            if (parsedExcel[0].workBook.length == 1) {
              if (this.config.skip_header_rows === 0) {
                this.rowCountForDisplay = 0;
                this.toastr.error(ModalMessages.NoRowsToIngest);
                this.busyService.idle();

                btnNext.disabled = true;
                return null;
              }
            }


            this.wksheets = [this.wksheets[0]]; //only process the first sheet

            this.multiSheetConfigs[this.wksheets[0].sheetName] = this.config;
            this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
            this.busyService.idle();

            this.parseComplete = true;
            this.preview = true;
            this.isFileUploaded = true;
            btnNext.disabled = false;
          } else {
            // After rebuilding, restore ignoreSheet flags
            // Correct way to restore ignoreSheet flags
            this.wksheets.forEach(wk => {
              //reset the ignore duplicate                 
              if (this.multiSheetConfigs[wk.sheetName] && this.multiSheetConfigs[wk.sheetName].ignoreSheet) {
                // this.multiSheetConfigs[wk.sheetName].ignoreSheet = true;
                wk.ignoreSheet = true;
                wk.selected = false;
              }
            });

            this.selectedProcesSheetNames = this.isSelectedExistingProcess ? Object.keys(this.multiSheetConfigs) : [];

            const btnNext = document.getElementById(
              'btnNext'
            ) as HTMLButtonElement;

            if (parsedExcel[0].workBook.length == 1) {
              if (this.config.skip_header_rows === 0) {
                this.rowCountForDisplay = 0;
                this.toastr.error(ModalMessages.NoRowsToIngest);
                this.busyService.idle();

                btnNext.disabled = true;
                return null;
              }
              //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.ExcelWorksheetMoreThanOne);
              //this.toastr.error(ModalMessages.ExcelWorksheetMoreThanOne);
              //return;
            }
            // Initialize per-sheet configs and columns
            this.wksheets.forEach(wk => {
              //TODO; Add to condition -To existing sheet these values are alredy filled               

              if (!this.multiSheetConfigs[wk.sheetName]) {
                this.multiSheetConfigs[wk.sheetName] = this.resetAdditionalSettings();
              }
              if (!this.columnDatatypePerSheet[wk.sheetName]) {
                this.columnDatatypePerSheet[wk.sheetName] = [];

                this.previousValidateSheetColumnData[wk.sheetName] = {
                  data: [],
                  validateSchema: false
                };

              }
              //this.displayRecordByWorkSheetName(wk.sheetName);
              //this.displayRecordByWorkSheetName(wk.sheetName, false);
            });

            //TODO: check if the file has more than 1 worksheet
            //Check if the New sheet is present in the wksheets list    

            //removed this so it will show error messages when no sheets && this.selectedProcesSheetNames.length > 0
            if (this.isSelectedExistingProcess && this.wksheets.length > 0) {

              // btnNext.disabled = true;
              //The sheetNames are not present in the uploaded file
              newSheets = this.wksheets
                .map(wk => wk.sheetName)
                .filter(sheetName => !this.selectedProcesSheetNames.includes(sheetName));


              const newSheetNames = this.wksheets.map(wk => wk.sheetName);

              const existSheetFromWkSheet = this.selectedProcesSheetNames.filter(
                sheetName => newSheetNames.includes(sheetName)
              );
              // if (existSheetFromWkSheet.length > 0) {
              //   this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);
              // }


              this.firstSheetSelectedIndex = this.wksheets.findIndex(wk => wk.sheetName === existSheetFromWkSheet[0]);
              const missingSheets = this.selectedProcesSheetNames.filter(
                sheetName => !newSheetNames.includes(sheetName)
              );


              if ((existSheetFromWkSheet.length > 0) && (newSheets.length > 0 || missingSheets.length > 0)) {
                const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
                modalRef.componentInstance.title = 'Uploaded File Alert.';
                let componentInstanceMessage = `The uploaded file contains `;
                if (newSheets.length > 0) {
                  componentInstanceMessage += `New sheets:<b><i> ${newSheets.join(', ')} </i></b>`;
                }

                if (newSheets.length > 0 && missingSheets.length > 0) {
                  componentInstanceMessage += ` and `;
                }
                if (missingSheets.length > 0) {
                  componentInstanceMessage += `Missing sheets:<b><i> ${missingSheets.join(', ')} </b></i>`;
                }
                componentInstanceMessage += ` These sheets will not be processed. Do you want to continue?`;
                modalRef.componentInstance.message = componentInstanceMessage;//this.sanitizer.bypassSecurityTrustHtml(componentInstanceMessage)
                modalRef.result.then((result) => {
                  if (result === true) {
                    this.newSheetInfoMessage = newSheets.length > 0 ? `New sheets <b><i> ${newSheets.join(', ')} </b></i> will not be processed` : ``;
                    this.missingSheetInfoMessage = missingSheets.length > 0 ? `Missing sheets <b><i> ${missingSheets.join(', ')} </b></i> will not be processed` : ``;
                    newSheets.forEach(sheetName => {
                      this.multiSheetConfigs[sheetName].frmSubmitted = true;
                      this.multiSheetConfigs[sheetName].newSheet = true;
                      this.multiSheetForms[sheetName] = this.createProcessConfigForm(this.multiSheetConfigs[sheetName]);
                      this.multiSheetForms[sheetName].get('frmSubmitted')?.setValue(true);
                      this.multiSheetForms[sheetName].get('newSheet')?.setValue(true);

                      // Find the worksheet object by sheetName
                      const wkSheet = this.wksheets.find(wk => wk.sheetName === sheetName);
                      if (wkSheet) {
                        wkSheet.newSheet = true;
                      }
                    });
                    missingSheets.forEach(sheetName => {
                      // Find the worksheet object by sheetName
                      this.multiSheetConfigs[sheetName].missingSheet = true;
                      this.multiSheetForms[sheetName].get('missingSheet')?.setValue(true);
                    });
                    this.fileNotProcessed = false;
                    if (existSheetFromWkSheet.length > 0) {
                      this.selectTabByIndexNumber();
                      // this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);
                      //#region fix for inc036901515
                      //reverse the loop 
                      [...this.wksheets].reverse().forEach(wk => {
                        this.displayRecordByWorkSheetName(wk.sheetName);
                      });
                      //#endregion
                    }

                    this.setExcelUploadedFileMessages();

                  }
                }, (reason) => {
                  //this.selectedOption = 'No';
                  this.newSheetInfoMessage = newSheets.length > 0 ? `New sheets <b><i> ${newSheets.join(', ')} </b></i> will not be processed` : ``;
                  this.missingSheetInfoMessage = missingSheets.length > 0 ? `Missing sheets <b><i> ${missingSheets.join(', ')} <b><i> will not be processed` : ``;

                  newSheets.forEach(sheetName => {
                    this.multiSheetConfigs[sheetName].frmSubmitted = false;
                    this.multiSheetConfigs[sheetName].newSheet = true;
                    this.multiSheetForms[sheetName] = this.createProcessConfigForm(this.multiSheetConfigs[sheetName]);
                    this.multiSheetForms[sheetName].get('frmSubmitted')?.setValue(true);
                    this.multiSheetForms[sheetName].get('newSheet')?.setValue(true);
                    const wkSheet = this.wksheets.find(wk => wk.sheetName === sheetName);
                    if (wkSheet) {
                      wkSheet.newSheet = true;
                    }
                  });
                  missingSheets.forEach(sheetName => {
                    // Find the worksheet object by sheetName
                    this.multiSheetConfigs[sheetName].missingSheet = true;
                    this.multiSheetForms[sheetName].get('missingSheet')?.setValue(true);
                  });
                  this.fileNotProcessed = true;
                  if (existSheetFromWkSheet.length > 0) {

                    this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);

                  } else {
                    this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
                  }
                  // this.callDisplayRecordByWorkSheetName(res); 
                  this.setExcelUploadedFileMessages();
                });
              }
              else {

                //this.notFoundSheetMessage = (existSheetFromWkSheet.length !== this.wksheets.length) ? `Not found any sheet.` : '';
                this.notFoundSheetMessage = '';
                if ((existSheetFromWkSheet.length !== this.wksheets.length)) {
                  this.notFoundSheetMessage = `No valid sheet has been found. Unable to proceed.`;
                  btnNext.disabled = true;

                  this.wksheets.forEach(wk => { wk.ignoreSheet = true }); //lets disable all sheets
                }

                //reverse the loop 
                [...this.wksheets].reverse().forEach(wk => {
                  this.displayRecordByWorkSheetName(wk.sheetName);
                });

                //removed this added line because we reverse the loop at the previous line of code
                //this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);

                this.fileNotProcessed = false;
                this.setExcelUploadedFileMessages();

              }

            } else {
              //check if there's selectedprocessnames
              if (this.selectedWorkSheetName) {
                this.displayRecordByWorkSheetName(this.selectedWorkSheetName);
              } else {
                this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
              }

            }

          }


          this.busyService.idle();


          this.parseComplete = true;
          this.preview = true;
          this.isFileUploaded = true;
        }

        {
          // this.myParser
          //   .parseExcel(
          //     this.file,
          //     this.flexCheckHasHeaders ? 1 : 0,
          //     this.config.skip_header_rows,
          //     this.config.flexCheckSkipEmptyLines,
          //     this.mappingType)
          //   .subscribe({
          //     next: async (res: KeyValuePair[]) => {
          //       this.wksheets = res.filter(wk => wk.sheetName && wk.sheetName.trim().length > 0);
          //       const endTime = performance.now();

          //       const totalSeconds = Math.floor((endTime - startTime) / 1000);
          //       const minutes = Math.floor(totalSeconds / 60);
          //       const seconds = totalSeconds % 60;
          //       const totalTime = `${minutes}:${seconds.toString().padStart(2, '0')}`;

          //       this.parsingTime = endTime - startTime;
          //       this.disableDelimiter = true;
          //       this.hasErrors = false;
          //       this.columnDatatype = [];
          //       this.hasErrors = false;
          //       let newSheets = [];
          //       let missingSheet = [];

          //       // const hasValidSheetName = Object.keys(this.multiSheetConfigs).some(
          //       //   sheetName => sheetName.trim() !== ''
          //       // );


          //       if (this.excelUploadMultiSheet === false && this.isSelectedExistingProcess && this.selectedProcessHasTabName === false) {
          //         this.hasErrors = false;
          //         const btnNext = document.getElementById(
          //           'btnNext'
          //         ) as HTMLButtonElement;

          //         if (res[0].workBook.length == 1) {
          //           if (this.config.skip_header_rows === 0) {
          //             this.rowCountForDisplay = 0;
          //             this.toastr.error(ModalMessages.NoRowsToIngest);
          //             this.busyService.idle();

          //             btnNext.disabled = true;
          //             return;
          //           }
          //         }


          //         this.wksheets = [this.wksheets[0]]; //only process the first sheet

          //         this.multiSheetConfigs[this.wksheets[0].sheetName] = this.config;
          //         this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
          //         this.busyService.idle();

          //         this.parseComplete = true;
          //         this.preview = true;
          //         this.isFileUploaded = true;
          //         btnNext.disabled = false;
          //       } else {
          //         // After rebuilding, restore ignoreSheet flags
          //         // Correct way to restore ignoreSheet flags
          //         this.wksheets.forEach(wk => {
          //           //reset the ignore duplicate                 
          //           if (this.multiSheetConfigs[wk.sheetName] && this.multiSheetConfigs[wk.sheetName].ignoreSheet) {
          //             // this.multiSheetConfigs[wk.sheetName].ignoreSheet = true;
          //             wk.ignoreSheet = true;
          //             wk.selected = false;
          //           }
          //         });

          //         this.selectedProcesSheetNames = this.isSelectedExistingProcess ? Object.keys(this.multiSheetConfigs) : [];

          //         const btnNext = document.getElementById(
          //           'btnNext'
          //         ) as HTMLButtonElement;

          //         if (res[0].workBook.length == 1) {
          //           if (this.config.skip_header_rows === 0) {
          //             this.rowCountForDisplay = 0;
          //             this.toastr.error(ModalMessages.NoRowsToIngest);
          //             this.busyService.idle();

          //             btnNext.disabled = true;
          //             return;
          //           }
          //           //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.ExcelWorksheetMoreThanOne);
          //           //this.toastr.error(ModalMessages.ExcelWorksheetMoreThanOne);
          //           //return;
          //         }
          //         // Initialize per-sheet configs and columns
          //         this.wksheets.forEach(wk => {
          //           //TODO; Add to condition -To existing sheet these values are alredy filled               

          //           if (!this.multiSheetConfigs[wk.sheetName]) {
          //             this.multiSheetConfigs[wk.sheetName] = this.resetAdditionalSettings();
          //           }
          //           if (!this.columnDatatypePerSheet[wk.sheetName]) {
          //             this.columnDatatypePerSheet[wk.sheetName] = [];

          //             this.previousValidateSheetColumnData[wk.sheetName] = {
          //               data: [],
          //               validateSchema: false
          //             };

          //           }
          //           //this.displayRecordByWorkSheetName(wk.sheetName);
          //           //this.displayRecordByWorkSheetName(wk.sheetName, false);
          //         });

          //         //TODO: check if the file has more than 1 worksheet
          //         //Check if the New sheet is present in the wksheets list    

          //         //removed this so it will show error messages when no sheets && this.selectedProcesSheetNames.length > 0
          //         if (this.isSelectedExistingProcess && this.wksheets.length > 0) {

          //           // btnNext.disabled = true;
          //           //The sheetNames are not present in the uploaded file
          //           newSheets = this.wksheets
          //             .map(wk => wk.sheetName)
          //             .filter(sheetName => !this.selectedProcesSheetNames.includes(sheetName));


          //           const newSheetNames = this.wksheets.map(wk => wk.sheetName);

          //           const existSheetFromWkSheet = this.selectedProcesSheetNames.filter(
          //             sheetName => newSheetNames.includes(sheetName)
          //           );
          //           // if (existSheetFromWkSheet.length > 0) {
          //           //   this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);
          //           // }


          //           this.firstSheetSelectedIndex = this.wksheets.findIndex(wk => wk.sheetName === existSheetFromWkSheet[0]);
          //           const missingSheets = this.selectedProcesSheetNames.filter(
          //             sheetName => !newSheetNames.includes(sheetName)
          //           );


          //           if ((existSheetFromWkSheet.length > 0) && (newSheets.length > 0 || missingSheets.length > 0)) {
          //             const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
          //             modalRef.componentInstance.title = 'Uploaded File Alert.';
          //             let componentInstanceMessage = `The uploaded file contains `;
          //             if (newSheets.length > 0) {
          //               componentInstanceMessage += `New sheets:<b><i> ${newSheets.join(', ')} </i></b>`;
          //             }

          //             if (newSheets.length > 0 && missingSheets.length > 0) {
          //               componentInstanceMessage += ` and `;
          //             }
          //             if (missingSheets.length > 0) {
          //               componentInstanceMessage += `Missing sheets:<b><i> ${missingSheets.join(', ')} </b></i>`;
          //             }
          //             componentInstanceMessage += ` These sheets will not be processed. Do you want to continue?`;
          //             modalRef.componentInstance.message = componentInstanceMessage;//this.sanitizer.bypassSecurityTrustHtml(componentInstanceMessage)
          //             modalRef.result.then((result) => {
          //               if (result === true) {
          //                 this.newSheetInfoMessage = newSheets.length > 0 ? `New sheets <b><i> ${newSheets.join(', ')} </b></i> will not be processed` : ``;
          //                 this.missingSheetInfoMessage = missingSheets.length > 0 ? `Missing sheets <b><i> ${missingSheets.join(', ')} </b></i> will not be processed` : ``;
          //                 newSheets.forEach(sheetName => {
          //                   this.multiSheetConfigs[sheetName].frmSubmitted = true;
          //                   this.multiSheetConfigs[sheetName].newSheet = true;
          //                   this.multiSheetForms[sheetName] = this.createProcessConfigForm(this.multiSheetConfigs[sheetName]);
          //                   this.multiSheetForms[sheetName].get('frmSubmitted')?.setValue(true);
          //                   this.multiSheetForms[sheetName].get('newSheet')?.setValue(true);

          //                   // Find the worksheet object by sheetName
          //                   const wkSheet = this.wksheets.find(wk => wk.sheetName === sheetName);
          //                   if (wkSheet) {
          //                     wkSheet.newSheet = true;
          //                   }
          //                 });
          //                 missingSheets.forEach(sheetName => {
          //                   // Find the worksheet object by sheetName
          //                   this.multiSheetConfigs[sheetName].missingSheet = true;
          //                   this.multiSheetForms[sheetName].get('missingSheet')?.setValue(true);
          //                 });
          //                 this.fileNotProcessed = false;
          //                 if (existSheetFromWkSheet.length > 0) {
          //                   this.selectTabByIndexNumber();
          //                   this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);
          //                 }

          //                 this.setExcelUploadedFileMessages();

          //               }
          //             }, (reason) => {
          //               //this.selectedOption = 'No';
          //               this.newSheetInfoMessage = newSheets.length > 0 ? `New sheets <b><i> ${newSheets.join(', ')} </b></i> will not be processed` : ``;
          //               this.missingSheetInfoMessage = missingSheets.length > 0 ? `Missing sheets <b><i> ${missingSheets.join(', ')} <b><i> will not be processed` : ``;

          //               newSheets.forEach(sheetName => {
          //                 this.multiSheetConfigs[sheetName].frmSubmitted = false;
          //                 this.multiSheetConfigs[sheetName].newSheet = true;
          //                 this.multiSheetForms[sheetName] = this.createProcessConfigForm(this.multiSheetConfigs[sheetName]);
          //                 this.multiSheetForms[sheetName].get('frmSubmitted')?.setValue(true);
          //                 this.multiSheetForms[sheetName].get('newSheet')?.setValue(true);
          //                 const wkSheet = this.wksheets.find(wk => wk.sheetName === sheetName);
          //                 if (wkSheet) {
          //                   wkSheet.newSheet = true;
          //                 }
          //               });
          //               missingSheets.forEach(sheetName => {
          //                 // Find the worksheet object by sheetName
          //                 this.multiSheetConfigs[sheetName].missingSheet = true;
          //                 this.multiSheetForms[sheetName].get('missingSheet')?.setValue(true);
          //               });
          //               this.fileNotProcessed = true;
          //               if (existSheetFromWkSheet.length > 0) {

          //                 this.displayRecordByWorkSheetName(existSheetFromWkSheet[0]);

          //               } else {
          //                 this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
          //               }
          //               // this.callDisplayRecordByWorkSheetName(res); 
          //               this.setExcelUploadedFileMessages();
          //             });
          //           }
          //           else {

          //             //this.notFoundSheetMessage = (existSheetFromWkSheet.length !== this.wksheets.length) ? `Not found any sheet.` : '';
          //             this.notFoundSheetMessage = '';
          //             if ((existSheetFromWkSheet.length !== this.wksheets.length)) {
          //               this.notFoundSheetMessage = `No valid sheet has been found. Unable to proceed.`;
          //               btnNext.disabled = true;

          //               this.wksheets.forEach(wk => { wk.ignoreSheet = true }); //lets disable all sheets
          //             }

          //             //reverse the loop 
          //             [...this.wksheets].reverse().forEach(wk => {
          //               this.displayRecordByWorkSheetName(wk.sheetName);
          //             });

          //             //removed this added line because we reverse the loop at the previous line of code
          //             //this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);

          //             this.fileNotProcessed = false;
          //             this.setExcelUploadedFileMessages();

          //           }

          //         } else {
          //           //check if there's selectedprocessnames
          //           if (this.selectedWorkSheetName) {
          //             this.displayRecordByWorkSheetName(this.selectedWorkSheetName);
          //           } else {
          //             this.displayRecordByWorkSheetName(this.wksheets[0].sheetName);
          //           }

          //         }

          //       }


          //       this.busyService.idle();


          //       this.parseComplete = true;
          //       this.preview = true;
          //       this.isFileUploaded = true;
          //       // btnNext.disabled = false;
          //     },
          //     error: (_) => {
          //       this.isFileUploaded = false;
          //       //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
          //       this.toastr.error(ModalMessages.SomethingWentWrong);
          //     },
          //   });
        }
      // }
      // catch (error) {
      //   this.busyService.idle();
      //   this.toastr.error('Oops, something went wrong!<br> File maybe corrupted.', '', { enableHtml: true });
      //   return isSameOrder;
      // }


    }

    return isSameOrder;

    //if (!this.processConfigurationFormHasError) this.modifySettingsClose();
  }


  setExcelUploadedFileMessages() {
    if (this.isSelectedExistingProcess) {
      this.excelUploadedErrorMessages = [
        this.newSheetInfoMessage?.trim(),
        this.missingSheetInfoMessage?.trim(),
        this.notFoundSheetMessage?.trim()
      ].filter(msg => msg !== '');
    }
  }

  createCustomHeaderRow(records: any[]): string[] | undefined {
    const headersRow = [];
    if (records) {
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        for (var i = 0; i < Object.keys(records[0]).length; i++) {
          //this.headersRow.push(`COL${i}`);
          headersRow.push(`COL${i}`);
        }
      } else {
        for (var i = 0; i < records[0].length; i++) {
          //this.headersRow.push(`COL${i}`);
          headersRow.push(`COL${i}`);
        }
      }
      return headersRow;
    }
    return undefined;
  }

  totalRowCountEXL: number = 0;
  displayRecordByWorkSheetName(tabName: string, openModal: boolean = true) {

    // 1. Save current sheet's state before switching

    //Recreate the object for each tab to avoid reference issues

    this.noOfDuplicates = 0;
    this.keyColumnIndexSelected = [];
    this.keyColumnNameSelected = [];

    let delimiter: string = ',';
    let skipEmptyLines = true;
    let quoteChar: string = '"';
    let isSameOrder: boolean = true;

    const startTime = performance.now();

    // this.keyColumnsSubject.next(''); //clear the list items
    // this.dedupColumnsSubject.next('');

    if (this.config.flpConfigurationId) {
      if (tabName) {
        this.config = this.multiSheetConfigs[tabName];
        this.config.frmSubmitted = true;
        this.columnDatatype = [];
        // this.columnDatatypePerSheet[tabName] = JSON.parse(JSON.stringify(this.columnDatatype));
        this.multiSheetForms[tabName] = this.createProcessConfigForm(this.multiSheetConfigs[tabName]);
      }
    } else {
      if (tabName) {
        this.config = this.multiSheetConfigs[tabName];
        this.columnDatatype = this.columnDatatypePerSheet[tabName]; //[];// this.columnDatatypePerSheet[tabName] || [];
        //  this.columnDatatypePerSheet[tabName] = JSON.parse(JSON.stringify(this.columnDatatype));
        //if (!this.multiSheetForms[tabName]) {
          this.multiSheetForms[tabName] = this.createProcessConfigForm(this.multiSheetConfigs[tabName]);
        //}
      }
    }

    if (this.multiSheetForms[tabName]) {
      this.multiSheetConfigs[tabName] = {
        ...this.multiSheetConfigs[tabName],
        ...this.multiSheetForms[tabName].value
      };
    }

    this.selectedWorkSheetName = tabName;

    // Restore state for new sheet (or initialize if missing)
    this.config = this.multiSheetConfigs[tabName]
      ? { ...this.multiSheetConfigs[tabName] }
      : this.resetAdditionalSettings();
    //End- 
    // starting old code
    // if (!this.columnDatatypePerSheet[tabName]) {
    //   this.columnDatatypePerSheet[tabName] = [];
    // }

    this.dataxlsx = [];
    this.recordsArray = [];
    this.recordsArrayForDisplay = [];
    //this.columnDatatype = [];
    this.totalRowCountEXL = this.wksheets
      .filter((x) => x.sheetName === tabName)[0].maxRowCount;
    this.dataxlsx = this.wksheets
      .filter((x) => x.sheetName === tabName)
      .map((y) => y.workBook)[0];
    this.recordsArray = this.wksheets
      .filter((x) => x.sheetName === tabName)
      .map((y) => y.workBook)[0];
    this.recordsArrayForDisplay = this.wksheets
      .filter((x) => x.sheetName === tabName)
      .map((y) => y.workBook)[0];
    var dataToSliced = [...this.recordsArrayForDisplay];
    const sheetHeaders = this.dataxlsx[0]; // assuming first row is header
    //this.originalColumnHeadersPerSheet[tabName] = [...sheetHeaders];
    //validate skipped header and footer row count before any logic
    if (
      this.config.skip_header_rows >=
      (this.config.flexCheckHasHeaders
        ? this.dataxlsx.length - 1
        : this.dataxlsx.length)
    ) {
      //this.toastr.info(ModalMessages.SkipHeaderRowsMoreThanTotalRowcount);
      //this.config.skip_header_rows = 0;
      //return;
    }

    this.recordsArray = this.wksheets
      .filter((x) => x.sheetName === tabName)
      .map((y) => y.workBook)[0];
    this.originalRowCount = this.wksheets.filter((x) => x.sheetName === tabName)[0].maxRowCount;
    this.recordsArrayForDisplay = this.wksheets
      .filter((x) => x.sheetName === tabName)
      .map((y) => y.workBook)[0];
    var dataToSliced = [...this.recordsArrayForDisplay];

    if (this.config.skip_header_rows > 0) {
      dataToSliced = dataToSliced.slice(this.config.skip_header_rows);
      this.recordsArrayForDisplay = dataToSliced;
      //this.dataxlsx = [...this.dataxlsx.slice(this.config.skip_header_rows)];
      this.dataxlsx = [...dataToSliced];
      this.totalRowCountEXL =
        this.totalRowCountEXL - this.config.skip_header_rows;
    }



    if (this.config.skip_footer_rows > 0) {

      // var slicedData = [...this.recordsArrayForDisplay];
      dataToSliced = dataToSliced
        .reverse()
        .slice(+this.config.skip_footer_rows);
      this.recordsArrayForDisplay = dataToSliced;
      //this.dataxlsx = [...this.dataxlsx.reverse().slice(+this.config.skip_footer_rows + 1).reverse()];
      this.dataxlsx = [...dataToSliced.reverse()];
      this.totalRowCountEXL =
        this.totalRowCountEXL - this.config.skip_footer_rows;
    }

    this.recordsArray = this.dataxlsx;
    this.flexCheckHasHeaders = this.config.flexCheckHasHeaders;
    if (this.flexCheckHasHeaders) {
      var temp = [...this.recordsArrayForDisplay.slice(0, 1)]
      this.rowCountForDisplay = this.recordsArrayForDisplay.length - 1; //this.recordsArray.length - 1;
      this.recordsArrayForDisplay = [...this.recordsArrayForDisplay.slice(1, 21)]; //remove row 1
      //check if row 1 is sa same with the columnDataType
      //extract columnName values from object2,

      let tempColumnDataType: ColumnNameDatatypeName[] = this.createColumnDataType([...this.dataxlsx.slice(0, 1)]);
      // if (this.config.spanish_to_english) {
      //   this.convertToEnglishCharacters(tempColumnDataType);
      // }
      // if (this.config.roman_numerals_only) {
      //   this.displayOnlyRomanNumerals(tempColumnDataType);
      // }
      // this.helperUtil.findDuplicateColumnAndGenerateNew(tempColumnDataType);

      const columnNameFromColumnDataType = this.columnDatatype.map(col => col.ColumnName);
      // const storeColumnDataType = this.columnDatatype; //to retain the data modeling like keycolumn/datatypes/exclude
      // const isSameOrder = tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);


      isSameOrder = this.columnDataTypeHasChanges(tempColumnDataType, columnNameFromColumnDataType);
      if (!isSameOrder && columnNameFromColumnDataType.length > 0) {
        this.columnDatatype = []; //lets reset

        //since we reset the columnDataType, let's update the keycolumn, keycolumnselectted dedup to blank
        this.config.ignore_duplicate_rows = false;
        this.config.key_columns = '';
        this.config.order_by_column_list_name_sort_dir = 'desc';
        this.multiSheetForms[tabName].get('order_by_column_list_name_sort_dir')?.disable();
        this.childComponent?.alertUIValidationReset();
        this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[tabName]]);
      }
      if (this.columnDatatype.length === 0) {
        ////console.log(this.dataxlsx.slice(0,1));
        var headerCount = 0;
        var duplicateNames = [];

        this.columnDatatype = this.createColumnDataType(this.dataxlsx.slice(0, 1));


      } else {
      }

      ////console.log(this.columnDatatype);
    } else {
      this.rowCountForDisplay = this.recordsArray.length;
      this.totalRowCountEXL = this.recordsArray.length;

      //store original columnDataType      
      let tempColumnDataType: ColumnNameDatatypeName[] = this.columnDatatype;
      //create COL0-COL1-COLnth
      let tempNewColumnDataType: ColumnNameDatatypeName[] = this.createCustomHeaderRow2(this.dataxlsx.slice(0, 1));

      // if (this.config.spanish_to_english) {
      //   this.convertToEnglishCharacters(tempColumnDataType);
      // }
      // if (this.config.roman_numerals_only) {
      //   this.displayOnlyRomanNumerals(tempColumnDataType);
      // }
      // this.helperUtil.findDuplicateColumnAndGenerateNew(tempColumnDataType);
      const columnNameFromColumnDataType = tempNewColumnDataType.map(col => col.ColumnName);
      //const isSameOrder = tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);

      isSameOrder = this.columnDataTypeHasChanges(tempColumnDataType, columnNameFromColumnDataType);

      if (!isSameOrder) {
        this.columnDatatype = tempNewColumnDataType;

        this.config.ignore_duplicate_rows = false;
        this.config.key_columns = '';
        this.config.order_by_column_list_name_sort_dir = 'desc';
        this.multiSheetForms[tabName].get('order_by_column_list_name_sort_dir')?.disable();
        this.multiSheetForms[tabName].get('ignore_duplicate_rows')?.disable();
        this.childComponent?.alertUIValidationReset();
        this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent.excelFileRule, this.childComponent.ruleSetNames, this.multiSheetForms[tabName]]);
      } else {
        this.columnDatatype = tempColumnDataType;
      }
      // this.columnDatatype = this.createCustomHeaderRow2(
      //   this.dataxlsx.slice(0, 1)
      // );
      //this.columnDatatype = this.headersRow2;


    }



    // 4. Restore form
    if (!this.multiSheetForms[tabName]) {
      this.multiSheetForms[tabName] = this.createProcessConfigForm(this.config);

    } else {

      this.multiSheetForms[tabName].patchValue(this.config);
      if (this.config.flpConfigurationId) {
        if (this.dataSource === DataSourceType.DataBricks && this.config.sourcePath) {
          this.multiSheetForms[tabName].get('deltaSource')?.setValue(this.config.sourcePath);
        }

      }

    }


    if (this.childComponent) {
      //always reset to client settings in modify process configuration

      //this.childComponent.activeTab = 'client-tab';      

      let processName = this.fileUploadForm.value.clientInfo?.processName;
      //let processName1 = this.fileUploadForm.value.clientInfo?.updateTableName(processName, tabName,this.multiSheetForms[tabName]);
      if (this.config.frmSubmitted) {
        var tableName = this.dataSource === DataSourceType.DataBricks ? this.config.deltaTableName : this.config.tableName;
        this.childComponent.setUpdatedTableName(processName, tableName, tabName, this.multiSheetForms[tabName]);
      } else {

        //setup default
        this.childComponent.updateTableName(processName, tabName, this.multiSheetForms[tabName]);
      }

      //other items to update
      this.childComponent.databaseDestinationSettingsError = '';
      this.childComponent.excelFileRule = this.config.ruleSet;
      this.childComponent.validationSection = false;
      this.childComponent.ruleSetNames = this.ruleSetNames;
      //this.multiSheetForms[tabName].disable({ emitEvent: false });
      //  const currentSheet = tabName?.trim();
      //         if (currentSheet) {
      //            this.config.tableName += `_${currentSheet}`;
      //         }  
    }

    // Ensure the config is initialized after by clicking tab directly
    if (this.config) {
      this.flexCheckHasHeaders = this.config.flexCheckHasHeaders;

      let columnForDedupList: string = this.columnDatatype
        .filter((x) => x.columnForDedeup)
        .map((x) => x.ColumnName)
        .join(', ');
      this.dedupColumnsSubject.next(columnForDedupList);
      let keyColumnsList: string = this.columnDatatype
        .filter((x) => x.ColumnKey)
        .map((x) => x.ColumnName)
        .join(', ');
      this.keyColumnsSubject.next(keyColumnsList);
      if (keyColumnsList) {
        this.columnDatatype.forEach((c) => {
          c.useMultipleSheets = false; //reset to true
        });
      } else {
        this.multiSheetForms[tabName].get('ignore_duplicate_rows')?.setValue(false);
        this.multiSheetForms[tabName].get('ignore_duplicate_rows')?.disable();

        // if (!this.config.order_by_column_list_name_sort_dir) {
        this.multiSheetForms[tabName].get('order_by_column_list_name_sort_dir')?.setValue('desc');
        this.multiSheetForms[tabName].get('order_by_column_list_name_sort_dir')?.disable();
        this.config.order_by_column_list_name_sort_dir = 'desc'
      }
    }


    //End multisheet logic    
    if (this.config.spanish_to_english) {
      this.convertToEnglishCharacters(this.columnDatatype);
    }

    //find duplicate columns
    this.helperUtil.findDuplicateColumnAndGenerateNew(this.columnDatatype);

    if (this.config.roman_numerals_only) {
      this.displayOnlyRomanNumerals(this.columnDatatype);
    }

    if (tabName) {
      this.columnDatatypePerSheet[tabName] = JSON.parse(JSON.stringify(this.columnDatatype));
    }
    //apply sorting and dedup
    //  this.noOfDuplicates =0;
    if (this.config.flpConfigurationId != '') {
      if (this.config.ignoreSheet) {
        this.wksheets.filter(wk => wk.sheetName === tabName).forEach(wk => {
          wk.selected = false;
          wk.ignoreSheet = true;
        });
      }

      this.startValidateExistingExcelConfigurationToFile = setTimeout((_) => {
        this.validateExistingExcelConfigurationToFile(tabName);
        this.childComponent.processConfigurationForm.disable();
      }, 500);
    } else {
      setTimeout(() => {
        this.columnDatatype.forEach((c, i) => {
          if (c.columnForDedeup) {
            this.sortAndRemoveForExcel(c.index, true, false, true);
          } else if (c.ColumnKey) {
            this.sortAndRemoveForExcel(c.index, true, true, false);
          }

          // if(!c.willInclude){
          //   this.toggleExcludedClass(i);
          // }
        });
      }, 0);

    }


    if (this.recordsArrayForDisplay) {
      this.recordsArrayForDisplayPerSheet[tabName] = JSON.parse(JSON.stringify(this.recordsArrayForDisplay));
    }

    if (this.multiSheetConfigs[this.selectedWorkSheetName].ignoreSheet === false && this.notFoundSheetMessage === '') {
      if (openModal) {
        this.modifySettingsOpen();
      } else {
        this.modifySettingsClose();
      }
    } else {
      this.modifySettingsClose();
    }

    return isSameOrder;




  }

  createColumnDataType(records: any[]): ColumnNameDatatypeName[] {
    let tempColumnDataType: ColumnNameDatatypeName[] = [];
    records.slice(0, 1).forEach((x, i) => {
      x.forEach((d: string, j) => {
        //let's skip empty headers
        var headerName =
          d.trim().length === 0 ? `COL${j}` : cleanColumnName(d.trim()); //this.helperUtil.cleanColumnName(d.trim());
        //if (headerName.length > 0) {

        tempColumnDataType.push({
          index: j,
          ColumnName: headerName,
          DbColumnName: headerName,
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
      });
    });

    return tempColumnDataType;
  }

  createColumnDataFromMetaHeaders(metaHeaders: string[]): ColumnNameDatatypeName[] {
    let tempColumnDataType: ColumnNameDatatypeName[] = [];
    metaHeaders?.forEach((header, index) => {
      let cleanHeaderName = header; //cleanColumnName(header); //this.helperUtil.cleanColumnName(header);
      tempColumnDataType.push({
        index: index,
        ColumnName: cleanHeaderName,
        DbColumnName: cleanHeaderName,
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
    });

    return tempColumnDataType;
  }

  createCustomHeaderRow2(records: any[]): ColumnNameDatatypeName[] {
    const headersRow: ColumnNameDatatypeName[] = [];
    if (records) {
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        for (var i = 0; i < Object.keys(records[0].filter((x) => x)).length; i++) {
          //this.headersRow.push(`COL${i}`);

          headersRow.push({
            index: i,
            ColumnName: `COL${i}`,
            DbColumnName: `COL${i}`,
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
        }
      } else {
        for (var i = 0; i < records.length; i++) {
          //this.headersRow.push(`COL${i}`);
          headersRow.push({
            index: i,
            ColumnName: `COL${i}`,
            DbColumnName: `COL${i}`,
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
        }
      }
      return headersRow;
    }
    return headersRow;
  }

  remainCountForExcel: number = 0;
  sortAndRemoveForExcel(
    index: number | null,
    isNewKey: boolean | null,
    isColumnKey: boolean,
    isColumnDedup: boolean
  ) {



    if (isNewKey) {
      this.busyService.busy();
    }

    this.remainCountForExcel = 0;
    if (index !== null && isNewKey !== null) {
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        var ColumnName = this.columnDatatype.find((x) => x.index === index).ColumnName;
        if (isColumnKey) {

          if (isNewKey) {
            if (!this.keyColumnNameSelected.includes(ColumnName)) {
              this.keyColumnNameSelected.push(ColumnName)
            }
          } else {
            const existingIndex = this.keyColumnNameSelected.indexOf(ColumnName);
            if (existingIndex > -1) {
              this.keyColumnNameSelected.splice(existingIndex, 1);
            }
          }
        } else if (isColumnDedup) {
          if (isNewKey) {
            if (this.columnsForDedupSelected.includes(ColumnName)) {
              this.columnsForDedupSelected.push(ColumnName);
            }
          } else {
            const existingIndex = this.columnsForDedupSelected.indexOf(ColumnName);
            if (existingIndex > -1) {
              this.columnsForDedupSelected.splice(existingIndex, 1);
            }
          }

        }

        if (this.wksheets && this.wksheets.length > 0) {
          this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
        }
      }

      //column index
      if (isColumnKey) {
        if (isNewKey) {
          if (!this.keyColumnIndexSelected.includes(index.toString())) {
            this.keyColumnIndexSelected.push(index.toString())
          }
        } else {
          const existingIndex = this.keyColumnIndexSelected.indexOf(index.toString());
          if (existingIndex > -1) {
            this.keyColumnIndexSelected.splice(existingIndex, 1);
          }
        }


      } else if (isColumnDedup) {
        if (isNewKey) {
          if (!this.columnsForDedupIndexSelected.includes(index.toString())) {
            this.columnsForDedupIndexSelected.push(index.toString())
          }
        } else {
          const existingIndex = this.columnsForDedupIndexSelected.indexOf(index.toString());
          if (existingIndex > -1) {
            this.columnsForDedupIndexSelected.splice(existingIndex, 1);
          }
        }

      }
    }

    //sort then remove then store
    if (this.config.ignore_duplicate_rows === false) {
      this.noOfDuplicates = 0;
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        if (this.config.flexCheckHasHeaders) {
          this.recordsArrayForDisplay = [...this.recordsArray].slice(1, 21);
        } else {
          this.recordsArrayForDisplay = [...this.recordsArray];
        }
      }

      //remove all key column and dedup column
      this.columnDatatype.forEach((i) => {
        i.ColumnKey = false;
        i.columnForDedeup = false;
      });

      this.AddOrUpdateNotificationList(this.selectedWorkSheetName, this.noOfDuplicates,
        this.recordsArrayForDisplay, this.InvalidDataSource, this.NewColumnFoundOnSource,
        this.MissingColumnFoundOnSource, this.config.validate_fileschema);

      this.busyService.idle();
      return;
    }
    this.unorderd = [...this.recordsArray]; //.slice(0,20);

    if (
      this.fileType === FileType.MSExcel1 ||
      this.fileType === FileType.MSExcel2 || this.fileType === FileType.MSExcel3
    ) {

      if (this.keyColumnIndexSelected.length > 0) {
        // let orderedList = this.sortManyRecords(this.flexCheckHasHeaders ? this.unorderd.splice(1) : this.unorderd, isColumnKey ? this.keyColumnIndexSelected : this.columnsForDedupIndexSelected);
        // this.recordsArrayForDisplay = this.removeDups(orderedList,isColumnKey ? this.keyColumnNameSelected : this.columnsForDedupSelected);
        // this.noOfDuplicates = (this.flexCheckHasHeaders ? (this.recordsArray.length - 1) : this.recordsArray.length) - this.recordsArrayForDisplay.length;

        // let orderedList = this.sortManyRecords(
        //   this.flexCheckHasHeaders ? this.unorderd.splice(1) : this.unorderd,
        //   this.keyColumnIndexSelected
        // );


        // Build the input rows (without header, if present) and inject __origIndex
        const inputRows = (this.flexCheckHasHeaders ? this.unorderd.slice(1) : this.unorderd)
          .map((r: any, i: number) => ({ ...r, __origIndex: i }));

        // Sort using your existing function — your comparator can now use a['__origIndex']
        let orderedList = this.sortManyRecords(inputRows, this.keyColumnIndexSelected);


        this.recordsArrayForDisplay = this.removeDups(
          orderedList,
          this.keyColumnNameSelected
        );

        if (this.columnDatatype.find((x) => x.columnForDedeup)) {
          this.recordsArrayForDisplay = this.partitionBy(
            orderedList,
            this.keyColumnIndexSelected,
            this.columnDatatype.find((x) => x.columnForDedeup).ColumnName
          );
        }

        this.noOfDuplicates =
          (this.flexCheckHasHeaders
            ? this.recordsArray.length - 1
            : this.recordsArray.length) - this.recordsArrayForDisplay.length;

        this.remainCountForExcel = this.recordsArrayForDisplay.length;
        if (this.recordsArrayForDisplay.length >= 20) {
          this.recordsArrayForDisplay = this.recordsArrayForDisplay.slice(0, 20);
        }
      }
    }

    this.AddOrUpdateNotificationList(this.selectedWorkSheetName, this.noOfDuplicates,
      this.recordsArrayForDisplay, this.InvalidDataSource, this.NewColumnFoundOnSource,
      this.MissingColumnFoundOnSource, this.config.validate_fileschema);



    this.busyService.idle();
    //this.recordsArrayForDisplay = this.recordsArray;
  }

  modifySettingsOpen() {
    this.modifySettings = true;
  }


  validateExistingExcelConfigurationToFile(tabName: string) {

    this.config = this.multiSheetConfigs[tabName];
    this.noOfDuplicates = 0;
    this.InvalidDataSource = false;
    this.NewColumnFoundOnSource = false;
    this.MissingColumnFoundOnSource = false;
    this.selectedWorkSheetName = tabName;
    if (this.config.spanish_to_english && this.showEnglishConversionOnProcess) {
      return;
    }

    //clearTimeout(this.startValidateExistingExcelConfigurationToFile);
    //const btnNext = document.getElementById('btnNext') as HTMLButtonElement;
    //btnNext.disabled = true;
    //this.isNewProcess = false
    this.isSelectedExistingProcess = true;
    var myObject: { file: string; db: string }[] = [];
    var fileDbColumns: FileColumnMapping[] = [
      ...this.config.file_column_mapping,
    ];
    let configurationKeyColumns: string[] = this.config.key_columns.split(','); //existing list of key columns in db
    let configurationColumnNames = this.config.file_column_mapping.map((x) =>
      x.fileColumn.toUpperCase()
    ); //this.config.csv_column_name_list.split(','); // existing list of column names in db
    let configurationDbColumnNames = this.config.file_column_mapping.map((x) =>
      x.dbColumn.toUpperCase()
    );
    this.columnDatatype = [];
    this.columnDatatype = this.columnDatatypePerSheet[tabName] || [];
    let fileCurrentColumnNames = this.columnDatatype.map((x) => x.ColumnName); //current file columns

    let convert_datatypes_column_list = this.config.file_column_mapping.map(
      (x) => x.dataType
    ); // this.config.convert_datatypes_column_list.split(',');
    let configurationDedupColumns: string[] =
      this.config.order_by_column_list_for_dedup.split(',');

    if (this.config.spanish_to_english) {
      //this.displayOnlyEnglishCharacters();
      fileCurrentColumnNames = this.columnDatatype.map((x) => x.ColumnName);
    }

    this.config.file_column_mapping.forEach((x) => {
      myObject.push({ file: x.fileColumn, db: x.dbColumn });
    });

    let diff: string[] = []; //new columns in file
    let seen = new Set();
    fileCurrentColumnNames.forEach((c, i) => {
      if (this.config.flexCheckHasHeaders) {
        let foundColumn = fileDbColumns.findIndex((x) => x.fileColumn === c); //configurationColumnNames.indexOf(c);
        let foundColumnInDbColumn = fileDbColumns.findIndex(
          (x) => x.dbColumn === c
        ); //configurationDbColumnNames.indexOf(c);

        //if (foundColumn < 0) diff.push(c);
        if (foundColumn < 0 && foundColumnInDbColumn < 0) {
          diff.push(`${c}-${i}`); //added the index of the not found column
        }

        if (foundColumn >= 0) {
          //remove from the list
          fileDbColumns.splice(foundColumn, 1);
        }
      } else {
        //to get the index
        //let configurationColumnNames2 = configurationColumnNames.map(x => `COL${x.substr(x.lastIndexOf('=') + 1)}`);

        let foundColumn = configurationColumnNames.indexOf(c);
        //if (foundColumn < 0) diff.push(c);
        if (foundColumn < 0) diff.push(`${c}-${i}`); //added the index of the not found column
      }
    });


    //compare fileColumn (in db) with file headers
    let existingCols: string[] = []; //new columns in file
    configurationColumnNames = this.config.file_column_mapping.map((x) =>
      x.fileColumn.toUpperCase()
    );
    if (!this.config.flexCheckHasHeaders) {
      //regenerate column headers with col0-colnth
      //let configurationColumnNames2 = configurationColumnNames.map(x => `COL${x.substr(x.lastIndexOf('=') + 1)}`);

      configurationColumnNames.forEach((c, i) => {
        let foundColumn = fileCurrentColumnNames.indexOf(c);
        if (foundColumn < 0) existingCols.push(c);
      });
    } else {
      let columnNameIsInFile: number = 0;
      let columnNameIsInDbColumn: number = 0;
      //database fileColumn vs actual file headers
      // configurationColumnNames.forEach((c, i) => {

      //   let foundColumn = fileCurrentColumnNames.indexOf(c);

      //   if (foundColumn < 0) {
      //     //check if fileColumn are in actual file column

      //     existingCols.push(c);
      //   }

      // });

      this.config.file_column_mapping.forEach((o, i) => {
        let foundColumn = fileCurrentColumnNames.indexOf(
          o.fileColumn.toUpperCase()
        );
        let foundColumn2 = fileCurrentColumnNames.indexOf(
          o.dbColumn.toUpperCase()
        );

        if (foundColumn < 0 && foundColumn2 < 0) {
          existingCols.push(o.dbColumn);
        }
      });
    }




    if (diff.length > 0 || existingCols.length > 0) {
      if (existingCols.length > 0) {
        this.MissingColumnFoundOnSource = true;

        if (this.config.validate_fileschema) {
          this.InvalidDataSource = true;
          if (diff.length > 0) this.NewColumnFoundOnSource = true;
        }
        if (this.config.flexCheckHasHeaders) {
          //this.config.convert_datatypes_column_list.split(',').forEach((c, i) => {
          //convert_datatypes_column_list.forEach((c, i) => {
          this.config.file_column_mapping.forEach((c, i) => {
            //let foundColumn = fileCurrentColumnNames.indexOf(c.split('=')[0]); //found column in the file
            let foundColumn =
              fileCurrentColumnNames.indexOf(c.fileColumn) < 0 &&
              fileCurrentColumnNames.indexOf(c.dbColumn) < 0;
            if (foundColumn) {
              //if column is not found in file
              //add the column to the headersnjn
              this.columnDatatype.push({
                index: this.columnDatatype.length + 1,
                ColumnKey: false,
                DatatypeName: c.dataType, // c.split('=')[1],
                newColumn: true,
                ColumnName: c.fileColumn, // c.split('=')[0],
                DbColumnName: c.dbColumn, // c.split('=')[0],
                willInclude: false,
                missingColumn: true,
                invalidDataType: false,
                willAddNewColumn: true,
                invalidColumnName: false,
                columnForDedeup: false,
                dateTimeFormatId: 0,
                isDuplicateColumn: false,
                useMultipleSheets: true
              });
            }
          });
        } else {
          //regenerate column headers with col0-colnth
          let configurationColumnNames2 = configurationColumnNames.map(
            (x) => `COL${x.substr(x.lastIndexOf('=') + 1)}`
          );

          configurationColumnNames2.forEach((c, i) => {
            let foundColmn = fileCurrentColumnNames.indexOf(c);
            let columndatatype = '';
            let columnName = c;
            let dataType = 'string';
            let willInclude = false;
            let missingColumn = true;
            if (foundColmn >= 0) {
              //find the correct header name from database using index
              columndatatype = configurationColumnNames.find(
                (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
              );
              columnName = columndatatype.split('=')[0];
              dataType = columndatatype.split('=')[1];
              willInclude = true;
              missingColumn = false;

              let existingColumn = this.columnDatatype.find(
                (x) => x.ColumnName === c
              );
              existingColumn.ColumnName = columnName;
              existingColumn.willInclude = true;
              existingColumn.missingColumn = false;
            } else {
              this.columnDatatype.push({
                index: this.columnDatatype.length + 1,
                ColumnKey: false,
                DatatypeName: dataType,
                newColumn: true,
                ColumnName: columnName,
                DbColumnName: columnName,
                willInclude: willInclude,
                missingColumn: missingColumn,
                invalidDataType: false,
                willAddNewColumn: true,
                invalidColumnName: false,
                columnForDedeup: false,
                dateTimeFormatId: 0,
                isDuplicateColumn: false,
                useMultipleSheets: true
              });
            }
          });
        }
      }
      if (diff.length > 0) {
        this.columnDatatype.forEach((c, i) => {
          //find the database column name and rename the headers using the index
          if (!this.config.flexCheckHasHeaders) {
            let columnName = configurationColumnNames.find(
              (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
            );

            if (columnName) {
              c.ColumnName = columnName.split('=')[0];
            }
          }
          if (
            diff.find(
              (d) => d.split('-')[0] === c.ColumnName && +d.split('-')[1] === i
            )
          ) {
            if (this.config.validate_fileschema === false) {
              c.willInclude = true;
            } else {
              c.willInclude = false;
            }

            //do check again if the column name is already taken
            let currentDiff = diff.find(
              (y) => y.split('-')[0] === c.ColumnName
            );
            let duplicatColumnName = this.columnDatatype.find(
              (x) =>
                x.index !== +currentDiff.split('-')[1] &&
                x.ColumnName === currentDiff.split('-')[0]
            );
            if (duplicatColumnName) {
              if (diff.length > 0) {
                duplicatColumnName.ColumnName = this.generateRandomColumnName(
                  c.ColumnName,
                  diff,
                  i
                );
              }
            }

            this.NewColumnFoundOnSource = true;
            c.newColumn = true;
          }

          //c.ColumnKey = configurationKeyColumns.find(x => x === c.ColumnName) ? true : false;
        });

        this.newColumnVSTrue = false;

        if (
          this.columnDatatype.find((x) => x.missingColumn) &&
          this.config.validate_fileschema
        ) {
          this.newColumnVSTrue = true;
        }
      }

      //return true
    }
    if (!this.config.flexCheckHasHeaders) {
      let configurationColumnNames2 = configurationColumnNames.map(
        (x) => `COL${x.substr(x.lastIndexOf('=') + 1)}`
      );

      this.columnDatatype.forEach((c, i) => {
        let columnName = configurationColumnNames.find(
          (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
        );

        if (columnName) {
          c.ColumnName = columnName.split('=')[0];
        }
      });
    }

    //re-assign the data types in the headers
    this.columnDatatype.forEach((c, i) => {
      c.ColumnKey = configurationKeyColumns.find((x) => x === c.ColumnName)
        ? true
        : false;
      c.columnForDedeup = configurationDedupColumns.find(
        (x) => x === c.ColumnName
      )
        ? true
        : false;

      c.DbColumnName = this.config.file_column_mapping.find(
        (x) => x.fileColumn === c.ColumnName || x.dbColumn === c.ColumnName
      )?.dbColumn;
      //if dbColumnName is undefined, means this is a new column and needs to be added in the list
      if (c.DbColumnName === undefined) {
        //lets check first if column name is unique, else, increment

        c.DbColumnName = c.ColumnName;
      }

      //let's check dbColumnNames if they are unique

      if (seen.has(c.DbColumnName)) {
        //find unique name for the duplicate
        let counter = 1;
        let newName = c.ColumnName + '_' + counter;
        while (seen.has(newName)) {
          counter++;
          newName = c.ColumnName + '_' + counter;
        }

        c.DbColumnName = newName;
        seen.add(newName);
      } else {
        seen.add(c.DbColumnName);
      }
      this.keyColumnIndexSelected = [];
      this.keyColumnNameSelected = [];
      this.config.file_column_mapping.forEach((fcm, i) => {
        if (
          fcm.fileColumn.toLowerCase() === c.ColumnName.toLowerCase() ||
          fcm.dbColumn.toLowerCase() === c.ColumnName.toLowerCase()
        ) {
          if (fcm.formatId > 0) {
            c.DatatypeName = fcm.dataType;
            c.dateTimeFormatId = fcm.formatId;
            this.selectedDateTimeFormat = this.dateTimeNames.find(
              (x) => x.formatId === fcm.formatId
            ).format;
            this.selectedDateTimeFormatId = fcm.formatId;
          } else {
            c.DatatypeName = fcm.dataType;
          }
          //validate only if column is not missing or 
          if (c.missingColumn === false || c.newColumn === false) {
            //console.log('validating excel data type', i, fcm.dataType, c.ColumnName, tabName);
            this.validateDataOnDataType(i, fcm.dataType, c.ColumnName);
          }
        }
      });
    });

    //check if there's duplicate dbcolumn name

    //re-assign the dedups

    this.isValidDataType = !(
      this.columnDatatype.filter((x) => x.invalidDataType === true).length > 0
    );

    //return false;

    //apply sorting and dedup
    this.columnDatatype.forEach((c, i) => {
      //if (c.missingColumn === false || c.newColumn === false) {
      if (c.columnForDedeup) {
        this.sortAndRemoveForExcel(c.index, true, false, true);
      } else if (c.ColumnKey) {
        this.sortAndRemoveForExcel(c.index, true, true, false);
      }
      //}
    });

    if (tabName) {
      //this.config = this.multiSheetConfigs[this.selectedWorkSheetName];

      //this.previousValidateSheetColumnData[tabName] = JSON.parse(JSON.stringify(this.columnDatatype));

      this.previousValidateSheetColumnData[tabName] = {
        data: JSON.parse(JSON.stringify(this.columnDatatype)),
        validateSchema: this.config.validate_fileschema
      };


      //this.previousValidateSheetColumnData[tabName].values[0].validate_fileschema = this.config.validate_fileschema;
      //this.multiSheetForms[this.selectedWorkSheetName] = this.createProcessConfigForm(this.multiSheetConfigs[this.selectedWorkSheetName]);       
    }

    this.AddOrUpdateNotificationList(tabName, this.noOfDuplicates, this.recordsArrayForDisplay, this.InvalidDataSource,
      this.NewColumnFoundOnSource, this.MissingColumnFoundOnSource, this.config.validate_fileschema);
    // btnNext.disabled = false;     
  }

  generateRandomColumnName(
    columnName: string,
    diff: string[],
    i: number
  ): string {
    let columnNameIndexToCheck = diff.find(
      (y) => y.split('-')[0] === columnName
    ); //
    var newColumnName = '';
    if (!this.config.flexCheckHasHeaders) {
      newColumnName = `COL${i + 1}`;
    } else {
      newColumnName = `${columnName}${i + 1}`;
    }
    if (columnNameIndexToCheck) {
      let foundColumn = this.columnDatatype.find(
        (x) => x.index === +columnNameIndexToCheck.split('-')[1]
      );

      if (foundColumn) {
        if (this.columnDatatype.find((x) => x.ColumnName === newColumnName))
          newColumnName = this.generateRandomColumnName(
            newColumnName,
            diff,
            i + 1
          );
        else return newColumnName;
      }
    }

    return newColumnName;
  }



  displayTabDetailsBySheetName(tabName: string) {
    // 1. Save current sheet's state before switching

    // Restore state for new sheet (or initialize if missing)
    this.config = this.multiSheetConfigs[tabName];
    this.columnDatatype = this.columnDatatypePerSheet[tabName] || [];
    this.modifySettingsClose();

  }



  showEnglishConversionOnProcess: boolean;
  englishConversionCount: number = 0;
  refresh: any;
  //use to convert the whole headers
  displayOnlyEnglishCharacters() {
    if (this.config.spanish_to_english) {
      //this.englishConversionCount = 0;
      var tempVal: string = '';
      this.columnDatatype.forEach((c) => {
        //if (this.helperUtil.checkForAccents(c.ColumnName)) {
        this.englishConversionCount++;
        this.busyService.busy();
        tempVal = '';
        this.configService
          .convertToEnglishOnlyCharacters('spanish', c.ColumnName)
          .subscribe({
            next: (response: APIResponse<string>) => {
              if (response) {
                if (response.responseCode === 200) {
                  this.busyService.idle();
                  if (response.result) {
                    if (this.config.roman_numerals_only) {
                      tempVal = convertToRoman(response.result, false); //this.helperUtil.convertToRoman(response.result,false);
                    } else {
                      tempVal = response.result;
                    }
                    c.ColumnName = tempVal;
                    c.DbColumnName = tempVal;
                    this.englishConversionCount--;
                    if (this.englishConversionCount <= 0) {
                      clearTimeout(this.refresh);
                      this.showEnglishConversionOnProcess = false;
                    }
                  }
                }
              }
            },
          });
        //}
      });
      if (this.englishConversionCount > 0) {
        this.refresh = setInterval((_) => {
          this.showEnglishConversionOnProcess = true;
        }, 500);
      }
    }
  }

  convertToEnglishCharacters(columnNames: ColumnNameDatatypeName[]) {
    let convertedWord: string = '';
    let hasNewName = false;
    columnNames.forEach((c, i) => {
      convertedWord = c.ColumnName;
      hasNewName = false;
      Array.from(convertedWord).forEach((char) => {
        var equivalent = this.englishOnlyCharacters.find(
          (c) => c.charToConvert === char
        );
        if (equivalent) {
          hasNewName = true;
          convertedWord = convertedWord.replace(
            char,
            equivalent.englishEquivalent
          );
        }
      });
      if (hasNewName) {
      }
      c.ColumnName = convertedWord;
      c.DbColumnName = convertedWord;
    });
  }

  //used to convert per word
  async convertToEnglishOnlyCharacters(
    language: string,
    wordToBeConverted: string
  ) {
    const result = await lastValueFrom(
      this.configService.convertToEnglishOnlyCharacters(
        language,
        wordToBeConverted
      )
    );

    if (result) {
      if (result.responseCode === 200) {
        if (result.result) {
          return result.result;
        }
      }
    }
    return wordToBeConverted;
  }

  async convertToEnglishOnlyCharactersPerWord(wordToBeConverted: string) {
    Array.from(wordToBeConverted).forEach((char) => {
      var equivalent = this.englishOnlyCharacters.find(
        (c) => c.charToConvert === char
      );
      if (equivalent) {
        wordToBeConverted = wordToBeConverted.replace(
          char,
          equivalent.englishEquivalent
        );
      }
    });
    return wordToBeConverted;
  }

  onDisplayOnlyRomanNumerals(event: any) {
    this.config.roman_numerals_only = event;
    this.displayOnlyRomanNumerals(this.columnDatatype);
  }

  // displayOnlyRomanNumerals(event: any) {
  //   this.config.roman_numerals_only = event;
  displayOnlyRomanNumerals(columnNames: ColumnNameDatatypeName[]) {
    if (this.config.roman_numerals_only) {
      columnNames.forEach((c) => {
        c.ColumnName = convertToRoman(c.ColumnName, c.isDuplicateColumn); //this.helperUtil.convertToRoman(c.ColumnName,c.isDuplicateColumn);
        c.DbColumnName = convertToRoman(c.DbColumnName, c.isDuplicateColumn); //this.helperUtil.convertToRoman(c.DbColumnName,c.isDuplicateColumn);
      });
    }
  }

  onExcludeColumn(event: any, columnName: string) {

    //event.preventDefault();
    let foundValue = this.columnDatatype.find(
      (item) => item.ColumnName === columnName
    );
    const input = event.target as HTMLInputElement;
    const previousChecked = !!foundValue?.willInclude;

    var columnHasRule;

    var tempRuleSet;
    if (this.wksheets.length > 0) {
      const configAdditionalSetting = this.multiSheetConfigs[this.selectedWorkSheetName];
      tempRuleSet = configAdditionalSetting.ruleSet;
    } else {
      tempRuleSet = this.ruleSet;
    }

    const escapedColumnName = this.helperUtil.escapeRegExp(foundValue.ColumnName);
    columnHasRule = tempRuleSet?.find(x => {
      if (x.ruleColumnName.includes(foundValue.ColumnName) || x.ruleColumnName2 === foundValue.ColumnName)
        return x;

      const re = new RegExp(`(^|[^@])@${escapedColumnName}\\b`, 'i');
      if (re.test(x.prompt)) return x;

      return null;
    });

    if (columnHasRule) {
      //this.toastr.warning(`Column ${foundValue.ColumnName} is part of a rule set, if excluded, the rule will be removed.`);

      this.confirmModalService2
        .confirm('Validations', `Column ${foundValue.ColumnName} is part of a rule set.<br> If excluded, the rule will be removed. Would you like to proceed?`)
        .then((confirmed) => {
          if (confirmed === true) {
            //remove rule in child
            this.childComponent.excelFileRule = this.childComponent?.excelFileRule.filter(x => x.id !== columnHasRule.id);
            //remove from parent as well
            this.ruleSet = this.ruleSet.filter(x => !x.ruleColumnName.includes(foundValue.ColumnName));

            this.subOnExcludeColumn(foundValue);
          } else {
            foundValue.willInclude = previousChecked;
            input.checked = !previousChecked;
            return;
          }
        });

      return;

    }

    this.subOnExcludeColumn(foundValue);

  }

  subOnExcludeColumn(foundValue: ColumnNameDatatypeName) {
    var previousColumnKeyVal: boolean = false;
    if (foundValue) {
      foundValue.willInclude = !foundValue.willInclude;
      previousColumnKeyVal = foundValue.ColumnKey;
      if (!foundValue.willInclude) {
        foundValue.ColumnKey = false;
        foundValue.columnForDedeup = false;
        this.onGetSelectedDatatypes('string', foundValue.ColumnName, foundValue.index);
      }
      // this.toggleExcludedClass(foundValue.index);

      let keyColumnsList: string = this.columnDatatype
        .filter((x) => x.ColumnKey)
        .map((x) => x.ColumnName)
        .join(', ');


      this.keyColumnsSubject.next(keyColumnsList);

      if (keyColumnsList.length > 0) {
        //enable all dedup
        for (let i = 0; i < this.columnDatatype.length; i++) {
          //if (i !== foundValue.index) {
          if (this.columnDatatype[i].willInclude) {
            const elem = document.getElementById(
              'checkKeyColumnForDedup' + (i + 1)
            ) as HTMLElement;
            elem.removeAttribute('disabled');

          }
        }
        this.config.ignore_duplicate_rows = true;
        this.config.key_columns = keyColumnsList;
      } else {
        for (let i = 0; i < this.columnDatatype.length; i++) {
          const elem = document.getElementById(
            'checkKeyColumnForDedup' + (i + 1)
          ) as HTMLElement;
          elem.setAttribute('disabled', 'true');

        }

        this.columnDatatype.forEach(c => {
          c.columnForDedeup = false;
        });

        this.dedupColumnsSubject.next('');

        this.config.ignore_duplicate_rows = false;
        this.config.key_columns = '';
      }

      //no longer needed; disabled is implemented on html
      // setTimeout(() => {
      //   this.toggleExcludedClass(foundValue.index);
      // }, 500);

      if (previousColumnKeyVal) {
        if (this.fileType === FileType.CommaSeparatedValues || this.fileType === FileType.TextFiles) {
          this.sortAndRemove(foundValue.index, previousColumnKeyVal ? false : true, true, false);
        } else {
          this.sortAndRemoveForExcel(foundValue.index, previousColumnKeyVal ? false : true, true, false);
        }
      }



      if (this.wksheets && this.wksheets.length > 0) {
        this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
      }

    }
  }

  toggleExcludedClass(foundIndex: number) {
    const tableRows = document.querySelectorAll('#preview tbody > tr');
    tableRows.forEach((tr, index) => {
      const tds = tr.querySelectorAll('td');
      tds.forEach((td, i) => {
        if (i === foundIndex + 1) td.classList.toggle('excluded');
      });
    });
  }

  onExcelColumnKeyListChecked(event: any, columnName: string, index: number) {
    let foundValue = this.columnDatatype.find(
      (item) => item.ColumnName === columnName
    );

    //this.config = this.multiSheetConfigs[this.selectedWorkSheetName] || this.resetAdditionalSettings();
    //const keyId = document.getElementById(`key_${columnName}`) as HTMLElement;
    if (foundValue) {
      // this.randomColor = this.getRandomColor();

      // keyId.style.color = this.randomColor;
      // keyId.removeAttribute('display');
      foundValue.ColumnKey = event.target.checked; //!foundValue.ColumnKey;
      let keyColumnsList: string = this.columnDatatype
        .filter((x) => x.ColumnKey)
        .map((x) => x.ColumnName)
        .join(', ');

      this.keyColumnsSubject.next(keyColumnsList);
      if (keyColumnsList.length > 0) {
        //enable all dedup
        for (let i = 0; i < this.columnDatatype.length; i++) {
          //if (i !== foundValue.index) {
          if (this.columnDatatype[i].willInclude) {
            const elem = document.getElementById(
              'checkKeyColumnForDedup' + (i + 1)
            ) as HTMLElement;
            elem.removeAttribute('disabled');

          }
        }
        this.config.ignore_duplicate_rows = true;
        this.config.key_columns = keyColumnsList;
      } else {
        for (let i = 0; i < this.columnDatatype.length; i++) {
          const elem = document.getElementById(
            'checkKeyColumnForDedup' + (i + 1)
          ) as HTMLElement;
          elem.setAttribute('disabled', 'true');

        }

        this.columnDatatype.forEach(c => {
          c.columnForDedeup = false;
        });

        this.dedupColumnsSubject.next('');

        this.config.ignore_duplicate_rows = false;
        this.config.key_columns = '';

      }
      if (keyColumnsList.length === 0) this.noOfDuplicates = 0;

      this.sortAndRemoveForExcel(foundValue.index, event.target.checked, true, false);
      if (this.wksheets && this.wksheets.length > 0) {
        this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
      }
      // if (event.target.checked) {
      //   //this.sortRecords(this.recordsArray, foundValue.index);

      // }
      // else
      //   this.recordsArrayForDisplay = this.recordsArray;

      //trigger data types selection      
      //this.onGetSelectedDatatypes(this.tmpSelectedDatatype, foundValue.ColumnName, index);
    }

    //this.AddOrUpdateNotificationList(this.selectedWorkSheetName,
    // keyId.style.display = "none";
  }

  onColumnKeyListChecked(event: any, columnName: string) {

    let keyColumnsList: string = '';
    let foundValue = this.columnDatatype.find(
      (item) => item.ColumnName === columnName
    );
    //const keyId = document.getElementById(`key_${columnName}`) as HTMLElement;
    if (foundValue) {
      // this.randomColor = this.getRandomColor();

      // keyId.style.color = this.randomColor;
      // keyId.removeAttribute('display');
      foundValue.ColumnKey = !foundValue.ColumnKey;
      keyColumnsList = this.columnDatatype
        .filter((x) => x.ColumnKey)
        .map((x) => x.ColumnName)
        .join(', ');

      this.keyColumnsSubject.next(keyColumnsList);
      if (keyColumnsList.length > 0) {
        //enable all dedup
        for (let i = 0; i < this.columnDatatype.length; i++) {
          //if (i !== foundValue.index) {
          if (this.columnDatatype[i].willInclude) {
            const elem = document.getElementById(
              'checkKeyColumnForDedup' + (i + 1)
            ) as HTMLElement;
            elem.removeAttribute('disabled');
          }

        }
      }

      //fix for when keyColumnList is removed, the recordForDisplay is not reset
      this.config.ignore_duplicate_rows = keyColumnsList.length > 0 ? true : false;
      this.config.key_columns = keyColumnsList;
    } else {
      for (let i = 0; i < this.columnDatatype.length; i++) {
        const elem = document.getElementById(
          'checkKeyColumnForDedup' + (i + 1)
        ) as HTMLElement;
        elem.setAttribute('disabled', 'true');
      }

      this.columnDatatype.forEach((c) => {
        c.columnForDedeup = false;
      });

      this.dedupColumnsSubject.next('');

      this.config.ignore_duplicate_rows = false;
      this.config.key_columns = '';
    }
    if (keyColumnsList.length === 0) this.noOfDuplicates = 0;

    this.sortAndRemove(foundValue.index, event.target.checked, true, false);

    if (keyColumnsList.length === 0) {
      this.noOfDuplicates = 0;
    }

    // this.sortAndRemove(foundValue.index, event.target.checked, true, false);
    // if (event.target.checked) {
    //   //this.sortRecords(this.recordsArray, foundValue.index);

    // }
    // else
    //   this.recordsArrayForDisplay = this.recordsArray;
  }
  // keyId.style.display = "none";


  onColumnForDedupListChecked(event: any, columnName: string) {
    let foundValue = this.columnDatatype.find(
      (item) => item.ColumnName === columnName
    );
    if (foundValue) {
      //let isValid = this.validateDataOnDataType(foundValue.index, foundValue.DatatypeName, foundValue.ColumnName);

      foundValue.columnForDedeup = event.target.checked; //!foundValue.columnForDedeup;
      let columnForDedupList: string = this.columnDatatype
        .filter((x) => x.columnForDedeup)
        .map((x) => x.ColumnName)
        .join(', ');
      this.dedupColumnsSubject.next(columnForDedupList);
      if (
        columnForDedupList.length > 0 ||
        this.columnDatatype.filter((x) => x.ColumnKey).length > 0
      ) {
        this.config.ignore_duplicate_rows = true;
      } else {
        this.config.ignore_duplicate_rows = false;
      }

      this.sortAndRemove(foundValue.index, event.target.checked, false, true);
    }
  }


  onExcelColumnForDedupListChecked(event: any, columnName: string) {
    let foundValue = this.columnDatatype.find(
      (item) => item.ColumnName === columnName
    );
    if (foundValue) {
      //let isValid = this.validateDataOnDataType(foundValue.index, foundValue.DatatypeName, foundValue.ColumnName);

      foundValue.columnForDedeup = !foundValue.columnForDedeup;
      let columnForDedupList: string = this.columnDatatype
        .filter((x) => x.columnForDedeup)
        .map((x) => x.ColumnName)
        .join(', ');
      this.dedupColumnsSubject.next(columnForDedupList);
      if (
        columnForDedupList.length > 0 ||
        this.columnDatatype.filter((x) => x.ColumnKey).length > 0
      ) {
        this.config.ignore_duplicate_rows = true;
      } else {
        this.config.ignore_duplicate_rows = false;
      }

      this.sortAndRemoveForExcel(foundValue.index, event.target.checked, false, true);
      //Update config only if we change the column for dedup
      if (this.wksheets && this.wksheets.length > 0) {
        this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
      }
    }
  }

  seen: any[] = [];
  onColumnNameChange(event: any, index: number) {
    let column = this.columnDatatype.find((x) => x.index === index);
    column.ColumnName = cleanColumnName(event.target.value); //this.helperUtil.cleanColumnName(event.target.value);

    if (
      this.columnDatatype.find(
        (item) => item.index !== index && item.ColumnName === event.target.value
      )
    ) {
      column.invalidColumnName = true;
      this.seen.push(column);
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.UniqueColumnName);
      this.toastr.error(ModalMessages.UniqueColumnName);
    } else {
      //column.invalidColumnName = false;

      //this.columnDatatype.find(item => item.index === index).ColumnName = event.target.value;
      column.invalidColumnName = false;
      //check if the column changed was invalid column before removing anything from seen
      const existingColumn = this.seen.find(
        (x) => x.index === index && x.invalidColumnName === false
      );
      if (existingColumn) {
        this.seen.splice(
          this.seen.findIndex((x) => x.index === index),
          1
        );
      }
    }

    const duplicates = [];
    this.columnDatatype.forEach((item, i) => {
      const value = item.ColumnName;

      const found = this.seen.find(
        (x) => x.ColumnName === value && x.index !== i
      );
      if (found) {
        found.invalidColumnName = true;
        duplicates.push(item);
      } else {
        this.columnDatatype.find(
          (x) => x.ColumnName === item.ColumnName
        ).invalidColumnName = false;
      }
      // if (this.seen[value]) {
      //   duplicates.push(item);
      //   item.invalidColumnName = true;
      // } else {
      //   //this.seen[value] = true;
      //   item.invalidColumnName = false;
      // }
    });

    if (duplicates.length === 0) this.seen = [];
  }

  async onDBColumnNameChange(event: any, index: number) {

    let column = this.columnDatatype.find((x) => x.index === index);
    const input = event.target as HTMLInputElement;

    //check first if it has a rule
    var columnHasRule;
    var tempRuleSet;
    if (this.wksheets.length > 0) {
      const configAdditionalSetting = this.multiSheetConfigs[this.selectedWorkSheetName];
      tempRuleSet = configAdditionalSetting.ruleSet;
    } else {
      tempRuleSet = this.ruleSet;
    }
    columnHasRule = tempRuleSet?.find(x => {
      if (x.ruleColumnName.includes(column.ColumnName) || x.ruleColumnName2 === column.ColumnName)
        return x;

      const re = new RegExp(`(^|[^@])@${column.ColumnName}\\b`, 'i');
      if (re.test(x.prompt)) return x;

      return null;
    });

    if (columnHasRule) {
      this.confirmModalService2
        .confirm('Validations', `Column ${column.ColumnName} is part of a rule set.<br> If changed, the rule will be removed. Would you like to proceed?`)
        .then((confirmed) => {
          if (confirmed === true) {
            //remove rule in child
            this.childComponent.excelFileRule = this.childComponent?.excelFileRule.filter(x => x.id !== columnHasRule.id);
            //remove from parent as well
            this.ruleSet = this.ruleSet.filter(x => !x.ruleColumnName.includes(column.ColumnName));


          } else {

            input.value = column.DbColumnName;
            return;
          }
        });

      return;
    }





    let columnName = cleanColumnName(event.target.value); //this.helperUtil.cleanColumnName(event.target.value);
    column.DbColumnName = columnName;

    if (event.target.value === '' || columnName === '') {
      column.invalidColumnName = true;
      this.toastr.error(ModalMessages.UniqueColumnName);
      return;
    }

    if (this.config.spanish_to_english) {
      //&& this.helperUtil.checkForAccents(columnName)
      column.DbColumnName = await this.convertToEnglishOnlyCharactersPerWord(
        columnName
      );
    }

    if (this.config.roman_numerals_only) {
      column.DbColumnName = convertToRoman(columnName, false); //this.helperUtil.convertToRoman(columnName, false);
    }

    if (
      this.columnDatatype.find(
        (item) =>
          item.index !== index && item.DbColumnName === column.DbColumnName
      )
    ) {
      column.invalidColumnName = true;
      column.isDuplicateColumn = true;
      this.seen.push(column);
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.UniqueColumnName);
      this.toastr.error(ModalMessages.UniqueColumnName);
    } else {
      //column.invalidColumnName = false;

      //this.columnDatatype.find(item => item.index === index).ColumnName = event.target.value;
      column.invalidColumnName = false;
      column.isDuplicateColumn = false;
      //check if the column changed was invalid column before removing anything from seen
      const existingColumn = this.seen.find(
        (x) => x.index === index && x.invalidColumnName === false
      );
      if (existingColumn) {
        this.seen.splice(
          this.seen.findIndex((x) => x.index === index),
          1
        );
      }
    }

    const duplicates = [];
    this.columnDatatype.forEach((item, i) => {
      const value = item.DbColumnName;

      const found = this.seen.find(
        (x) => x.DbColumnName === value && x.index !== i
      );
      if (found) {
        found.invalidColumnName = true;
        duplicates.push(item);
      } else {
        this.columnDatatype.find(
          (x) => x.DbColumnName === item.DbColumnName
        ).invalidColumnName = false;
      }
    });

    if (duplicates.length === 0) this.seen = [];

    if (this.wksheets.length > 0) {
      this.columnDatatypePerSheet[this.selectedWorkSheetName] = this.columnDatatype;
      //this.updateConfigOnly([this.config, this.columnDatatype]);
    }
  }

  findDateTimeValues(value: string) {
    return this.dateTimeNames.filter((x) => x.dataTypeName === value);
  }

  findDateTimeFormatForDisplay(value: number) {
    if (value > 0)
      return this.dateTimeNames.find((x) => x.formatId === value).format;
    return '';
  }

  onAddNewColumn(event: any, index: number) {
    let foundValue = this.columnDatatype.find((item) => item.index === index);
    if (foundValue) {
      foundValue.willAddNewColumn = event.target.checked;
    }
  }
  tmpSelectedDatatype: string = '';
  onGetSelectedDatatypes(value: string, headerName: string, index: number) {
    this.tmpSelectedDatatype = '';
    let foundValue = this.columnDatatype.find((item) => item.index === index);
    if (foundValue) {
      if (value !== 'exclude') {
        this.tmpSelectedDatatype = value;
        foundValue.DatatypeName = value;

        this.showDateTimeOptions = false;
        this.selectedDateTimeFormat = '';
        this.selectedDateTimeFormatId = 0;
        // const elemDateTimeOptions = document.getElementById(
        //   `showDateTimeOptions${index + 1}`
        // ) as HTMLElement;
        // const elemDateTimeOptionsForDBSchema = document.getElementById(
        //   `showDBDateTimeOptions${index + 1}`
        // ) as HTMLElement;
        // elemDateTimeOptions.style.display = 'none';
        // if (elemDateTimeOptionsForDBSchema)
        //   elemDateTimeOptionsForDBSchema.style.display = 'none';
        this.dateTimeNamesValues = [];
        switch (value) {
          case 'date':
          case 'time':
          case 'datetime':
            this.showDateTimeOptions = true;
            this.dateTimeNames.forEach((c, i) => {
              if (c.dataTypeName === value) {
                this.selectedDateTimeFormat = this.dateTimeNames.filter(
                  (x) => x.dataTypeName === value
                )[0].format;
                this.selectedDateTimeFormatId = this.dateTimeNames.filter(
                  (x) => x.dataTypeName === value
                )[0].formatId;

                this.dateTimeNamesValues.push({
                  formatId: c.formatId,
                  format: c.format,
                  dataTypeName: c.dataTypeName,
                });
              }
            });
            break;
          default:
            foundValue.dateTimeFormatId = 0;
            break;
        }
        if (this.showDateTimeOptions) {
          // elemDateTimeOptions.style.display = 'block';
          // if (elemDateTimeOptionsForDBSchema)
          //   elemDateTimeOptionsForDBSchema.style.display = 'block';
          foundValue.dateTimeFormatId = this.selectedDateTimeFormatId;
        }
        let isValid = this.validateDataOnDataType(foundValue.index, foundValue.DatatypeName, headerName);
        if (isValid) {
          if (foundValue.columnForDedeup) {
            this.sortAndRemove(foundValue.index, true, foundValue.ColumnKey, true);
          }
          ////console.log(event.target.value);

          //lets re-implement columnkey, because validateDataOnDataType() uses the original data on file
          this.columnDatatype.forEach(col => {
            if (col.ColumnKey) {
              this.onExcelColumnKeyListChecked({ target: { checked: col.ColumnKey } }, col.ColumnName, col.index)
            }
          });
        }



      } else {
        foundValue.willInclude = !foundValue.willInclude;

        const tableRows = document.querySelectorAll('#preview tbody > tr');
        tableRows.forEach((tr, index) => {
          const tds = tr.querySelectorAll('td');
          tds.forEach((td, i) => {
            if (i == foundValue.index) td.classList.toggle('excluded');
          });
        });
      }
    }
  }

  onValidateDateTimeFormat(event: any, headerName: string, index: number) {
    let columnDataType = this.columnDatatype.find((x) => x.index === index);
    this.selectedDateTimeFormatId = event.target.value;
    this.selectedDateTimeFormat = this.dateTimeNames.find(
      (x) => x.formatId === +event.target.value
    ).format;
    columnDataType.dateTimeFormatId = Number(this.selectedDateTimeFormatId);
    this.validateDataOnDataType(
      columnDataType.index,
      columnDataType.DatatypeName,
      columnDataType.ColumnName
    );
    if (
      this.fileType === FileType.MSExcel1 ||
      this.fileType === FileType.MSExcel2 ||
      this.fileType === FileType.MSExcel3
    ) {
      //lets re-implement columnkey, because validateDataOnDataType() uses the original data on file
      this.columnDatatype.forEach(col => {
        if (col.ColumnKey) {
          this.onExcelColumnKeyListChecked({ target: { checked: col.ColumnKey } }, col.ColumnName, col.index);
        }
      });
    } else {
      //lets re-implement columnkey, because validateDataOnDataType() uses the original data on file
      this.columnDatatype.forEach(col => {
        if (headerName === col.ColumnName) {
          if (col.ColumnKey) {
            this.onColumnKeyListChecked({ target: { checked: false } }, col.ColumnName);
          }
          if (col.columnForDedeup) {
            this.sortAndRemove(col.index, true, false, true);
          }
        }

      });
    }
  }

  validateDataOnDataType(index: number, dataTypeName: string, headerName: string): boolean {
    let currentColumn = this.columnDatatype.find(
      (x) => x.index === index && x.missingColumn === false
    ); //only available column needs validation
    if (currentColumn) {
      currentColumn.invalidDataType = false;

      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        let data: any[] = [];
        //if (this.flexCheckHasHeaders) {
        //  data = this.recordsArrayForDisplay.slice(1, 20).map((x) => x[index]);
        //} else {
        if (this.wksheets && this.wksheets.length > 0) {
          this.recordsArrayForDisplay = this.recordsArrayForDisplayPerSheet[this.selectedWorkSheetName];
        }
        data = this.recordsArrayForDisplay.slice(0, 20).map((x) => x[index]);
        //console.log('tabName', this.selectedWorkSheetName);
        //console.log('data', data);
        //console.log('recordsArrayForDisplayPerSheet:', this.recordsArrayForDisplayPerSheet);

        //}

        this.isValidDataType = this.helperUtil.identifyDataType(
          data,
          dataTypeName,
          this.selectedDateTimeFormat.trim().length > 0
            ? this.selectedDateTimeFormat
            : null,
          currentColumn.ColumnKey
        );
        // const spanHeader = document.getElementById(headerName) as HTMLElement;
        // if (spanHeader) spanHeader.removeAttribute("style");

        //let spanHeader2 = document.getElementById('txt' + headerName) as HTMLElement;
        //if (spanHeader2) spanHeader2.removeAttribute("style");

        if (this.isValidDataType === false) {
          let currentColumn = this.columnDatatype.find(
            (x) => x.index === index
          );

          this.columnDatatype.find((x) => x.index === index).invalidDataType = true;
          this.columnDatatype.find((x) => x.index === index).DatatypeName = dataTypeName;
          //if(spanHeader) spanHeader.setAttribute("style", "color:red");
          //if (spanHeader2) { spanHeader2.setAttribute("style", "color:red"); }

          //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataType);
          this.toastr.error(ToastrMessages.InvalidDataType);


          if (this.wksheets && this.wksheets.length > 0 && !this.isSelectedExistingProcess) {
            // this.config.frmSubmitted = false;
            this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
          }
          return false;
        } else {
          if (this.wksheets && this.wksheets.length > 0 && !this.isSelectedExistingProcess) {
            // this.config.frmSubmitted = false;
            this.updateConfigOnly([this.config, this.columnDatatype, this.childComponent?.excelFileRule ?? [], this.childComponent?.ruleSetNames ?? [], this.multiSheetForms[this.selectedWorkSheetName]]);
          }
        }
      } else if (
        this.fileType === FileType.CommaSeparatedValues ||
        this.fileType === FileType.TextFiles
      ) {
        var slicedData: any[] = [];
        if (this.flexCheckHasHeaders)
          slicedData = this.recordsArrayForDisplay.map(
            (x) => x[this.headersRow[index]]
          );
        //.slice(1, 20)
        else slicedData = this.recordsArrayForDisplay.map((x) => x[index]); //.slice(0, 20)

        //let data: any[] = this.recordsArray.slice(0, 20).map(x => x[foundValue.index]);

        this.isValidDataType = this.helperUtil.identifyDataType(
          slicedData,
          dataTypeName,
          this.selectedDateTimeFormat.trim().length > 0
            ? this.selectedDateTimeFormat
            : null,
          currentColumn.ColumnKey
        );

        // const spanHeader = document.getElementById(headerName) as HTMLElement;
        // if(spanHeader) spanHeader.removeAttribute("style");

        // let spanHeader2 = document.getElementById('txt' + headerName) as HTMLElement;
        // if (spanHeader2) spanHeader2.removeAttribute("style");

        if (this.isValidDataType === false) {
          this.columnDatatype.find((x) => x.index === index).invalidDataType =
            true;

          //if(spanHeader) spanHeader.setAttribute("style", "color:red");
          //if (spanHeader2) { spanHeader2.setAttribute("style", "color:red"); }
          //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataType);
          this.toastr.error(ModalMessages.InvalidDataType);

          return false;
        }
      }
    }

    return true;
  }




  onMappingTypeChange(event: any) {

    this.resetFormByChangeMapping();
    this.onConfigurationChange({ flpConfigurationId: '', processNames: 'New', description: 'New Process' });
    this.modifySettingsClose();
    event.preventDefault();
  }


  // get sheetFormEntries(): [string, FormGroup][] {
  //   return this.multiSheetForms ? Object.entries(this.multiSheetForms) : [];
  // }
  hasMissingColumns(): boolean {
    return this.getSheetValidationEntries().some(
      entry => entry.hasMissingColumn
    );
  }


  getSheetValidationEntries(): { sheetName: string; hasMissingColumn: boolean }[] {
    let sheetValidationList = Object.entries(this.previousValidateSheetColumnData).map(([sheetName, sheetData]) => ({
      sheetName,
      hasMissingColumn: sheetData.data.some(col => col.missingColumn === true) && sheetData.validateSchema === true
    }));
    //this.isSelectedExistingProcess = sheetValidationList.length > 0 ? true:false;
    return sheetValidationList;
  }



  isAllSheetsSubmitted(): boolean {

    if (this.navigateService.dataSource === this.DataSourceTypes.LandingLayer) {
      if (this.config?.frmSubmitted && this.selectedFiles.length > 0) {
        return true;
      }
      return false;
    }
    if (this.fileType == FileType.MSExcel1 || this.fileType == FileType.MSExcel2 || this.fileType == FileType.MSExcel3) {
      if (!this.multiSheetForms) return false;

      //if all wksheets are unselected return false
      //now check if there were any worksheet selected
      const noOfActiveSheets = this.wksheets.filter(wk => !wk.ignoreSheet).length;
      if (noOfActiveSheets === 0) {
        return false;
      }

      //New Process
      //Find config 
      if (this.wksheets.length > 0 && this.isSelectedExistingProcess === true) {
        if (this.config.frmSubmitted === true && this.config.newSheet === true) {
          return true;
        }
      }



      if (this.config.flpConfigurationId != '') {

        let buttonDisable = true;
        if (this.fileNotProcessed) {
          return false;
        }
        //If any sheet has missing columns, enable the button
        for (const [sheetName, sheetData] of Object.entries(this.previousValidateSheetColumnData)) {
          const hasMissingColumn = sheetData.data.some(col => col.missingColumn) && sheetData.validateSchema == true;
          if (hasMissingColumn) {
            buttonDisable = false;
            this.filContainMissingColumn = true;
            break; // No need to check further sheets
          }
        }
        return buttonDisable;

      } else {
        const validateDataOnDataType = !Object.values(this.columnDatatypePerSheet)
          .some(sheetData => sheetData.some(col => col.invalidDataType));
        if (!validateDataOnDataType) {
          return false;
        }
        let isAllSheetAreIgnoredCount = Object.values(this.multiSheetForms).filter(form =>
          (form.get('ignoreSheet')?.value === true)
        ).length;

        if (isAllSheetAreIgnoredCount !== this.wksheets.length) {
          let frmSubmittedCount = Object.values(this.multiSheetForms).filter(form =>
            (form.get('frmSubmitted')?.value === true) || (form.get('ignoreSheet')?.value === true)
          ).length;
          return (frmSubmittedCount === this.wksheets.length);
        } else {
          return false;
        }

      }
    } else {
      //CSV & txt

      if (!this.isValidDataType ||
        this.columnDatatype.filter((x) => x.invalidDataType === true).length >
        0 ||
        this.columnDatatype.filter((x) => x.invalidColumnName === true).length > 0) {
        return false;
      }
      return this.config?.frmSubmitted;
    }

  }

  onSubmitAllSheets() {
    Object.keys(this.multiSheetConfigs).forEach(tabName => {

    });
  }

  validateClientSettings(config: AdditionalSettings): boolean {
    // Example: check required fields
    if (!config.RegionId || !config.SubRegionId || !config.ClientId) {
      return false;
    }
    // Add more validation as needed
    return true;
  }

  validateDatabaseSchemaSettings() {
    var hasErrors = this.columnDatatype.find(x => x.DbColumnName.length > 255);
    if (hasErrors) {
      return true;
    }

    return false;
  }


  getTabClasses(wk: any): any {

    let columnData = this.columnDatatypePerSheet[wk.sheetName] || [];
    const invalidDataType = columnData.some(col => col.invalidDataType);// Exclude columns with 'ignore' datatype
    let validateTab = this.validateTab(wk.sheetName);
    let isValid = (validateTab == true && invalidDataType == false);

    return {
      'tabValidate': isValid ? false : true,// ((this.validateTab(wk.sheetName)==false) || validateDataOnDataType),
      'excluded': (wk.ignoreSheet === true || wk.newSheet === true)
    };
  }

  validateTab(tabName: string): boolean {
    return Object.entries(this.multiSheetForms)
      .filter(([sheetName, form]) =>
        sheetName === tabName && form.get('frmSubmitted')?.value === true
      ).length > 0;
  }


  onShowDatabaseSchema() {

    if (this.wksheets.length == 0) {
      if (this.processConfigurationFormHasError) {
        //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
        this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
        this.modifySettingsOpen();
        return;
      } else if (
        !this.isValidDataType ||
        this.columnDatatype.filter((x) => x.invalidDataType === true).length >
        0 ||
        this.columnDatatype.filter((x) => x.invalidColumnName === true).length > 0
      ) {
        //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
        this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
        return;
      } else if (this.InvalidDataSource) {
        //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataSourceNewColumn);
        return;
      }

    } else {
      //Button already would be enable when all submitted sheet (No error occurred)              
    }

    if (this.processConfigurationFormHasError) {
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      this.modifySettingsOpen();
      return;
    }
    else if ([this.DataSourceTypes.DataBricks, this.DataSourceTypes.Default].includes(this.dataSource)) {
      if (
        !this.isValidDataType ||
        this.columnDatatype.filter((x) => x.invalidDataType === true).length > 0
        || this.columnDatatype.filter((x) => x.invalidColumnName === true).length > 0
      ) {
        //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
        this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
        return;
      } else if (this.InvalidDataSource) {
        //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataSourceNewColumn);
        return;
      }
    }

    if (this.dataSource === this.DataSourceTypes.LandingLayer) {
      if (this.selectedFiles.length === 0) {
        this.toastr.error(ModalMessages.NoSelectedFiles);
        return;
      }
      if (this.selectedFiles.length > this.landingLayerConfiguration?.noOfAllowedFilesToUpload) {
        this.toastr.error(`Total file count exceeds the limit of ${this.landingLayerConfiguration.noOfAllowedFilesToUpload} files. Please remove some files and try again.`);
        return;
      }

      if (this.totalFileSizeInMb > this.landingLayerConfiguration?.totalFileSize) {
        this.toastr.error(`Total file size exceeds the limit of ${this.landingLayerConfiguration.totalFileSize} MB. Please remove some files and try again.`);
        return;
      }
    }



    // else if (this.InvalidDataSource) {
    //   return;
    // }

    if (this.activeTab === 'attachFile-tab') {

      if (this.wksheets.length > 0) {
        //check validation before next screen
        if (!this.isAllSheetsSubmitted()) {
          this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
          return;
        }
        this.hasErrors = false;
        this.processConfigurationFormHasError = false;
        if (this.rowCountForDisplay < 1) {
          this.toastr.error(ModalMessages.NoRowsToIngest);
          const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
          //clientTab.classList.add("active");
          attachFileTab.click();
          return;
        }
        this.activeTab = 'databaseSchema-tab';

        const databaseSchemaTab = document.getElementById('databaseSchema-tab') as HTMLElement;
        databaseSchemaTab.click();
        if (this.wksheets.length > 1) {
          const sheetNames = this.submittedSheets.map(s => s.sheetName);
          this.config = this.multiSheetConfigs[sheetNames[0]];
          this.columnDatatype = this.columnDatatypePerSheet[sheetNames[0]];
          this.selectedWorkSheetName = sheetNames[0];
        }
        this.modifySettingsClose();

      } else {
        // if (this.hasErrors || this.processConfigurationFormHasError) {
        //   //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
        //   this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
        //   const attachFileTab = document.getElementById(
        //     'attachFile-tab'
        //   ) as HTMLElement;
        //   //clientTab.classList.add("active");
        //   attachFileTab.click();
        //   return;
        // }
        if (this.dataSource !== this.DataSourceTypes.LandingLayer) {
          if (!this.isAllSheetsSubmitted()) {
            this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
            return;
          }

          if (this.rowCountForDisplay < 1) {
            this.toastr.error(ModalMessages.NoRowsToIngest);
            const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
            //clientTab.classList.add("active");
            attachFileTab.click();
            return;
          }
        } else {
          if (this.selectedFiles.length === 0) {
            this.toastr.error(ModalMessages.NoSelectedFiles);
            const attachFileTab = document.getElementById('attachFile-tab') as HTMLElement;
            attachFileTab.click();
            return;
          }
        }
        this.activeTab = 'databaseSchema-tab';

        const databaseSchemaTab = document.getElementById(
          'databaseSchema-tab'
        ) as HTMLElement;
        databaseSchemaTab.click();

      }

    }
    else if (this.activeTab === 'databaseSchema-tab') {

      let securityGroups: SecurityGroup[] = [];

      if ([this.DataSourceTypes.Default, this.DataSourceTypes.DataBricks].includes(this.dataSource)) {
        if (this.validateDatabaseSchemaSettings()) {
          this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
          return;
        }

        if (this.fileType == FileType.MSExcel1 || this.fileType == FileType.MSExcel2 || this.fileType == FileType.MSExcel3) {
          //(this.fileUploadForm.value.clientInfo?.security_group || []).forEach((sg: any) => {
          (this.multiSheetConfigs[this.selectedWorkSheetName].securityGroups || []).forEach((sg: SecurityGroup) => {
            if (!sg || !sg.securityGroupId || !sg.securityGroupName) {
              this.toastr.error(ModalMessages.SecurityGroupDoesNotExist);
              return;
            }

            securityGroups.push({
              securityGroupId: sg.securityGroupId,
              securityGroupName: sg.securityGroupName,
              userSelectedGroup: false
            });
          });

          if (securityGroups.length == 0) {
            (this.fileUploadForm.value.clientInfo?.security_group || []).forEach((sg: any) => {
              if (!sg || !sg.id || !sg.displayName) {
                this.toastr.error(ModalMessages.SecurityGroupDoesNotExist);
                return;
              }

              securityGroups.push({
                securityGroupId: sg.id,
                securityGroupName: sg.displayName,
                userSelectedGroup: false
              });
            });
          }

        } else {

          //We ge the securioty group form form if in case csv & text file user has changed any value
          let securityGroupFromForm = null;
          if (this.config.flpConfigurationId) {
            securityGroupFromForm = this.fileUploadForm.getRawValue();
          } else {
            securityGroupFromForm = this.fileUploadForm.value;
          }

          //fix for csv/txt where SG can not be binded to securityGroupFromForm?.clientInfo?.security_group
          this.config.securityGroups.forEach((sg: SecurityGroup) => {
            if (!sg) {
              this.toastr.error(ModalMessages.SecurityGroupDoesNotExist);
              return;
            }
            securityGroups.push({
              securityGroupId: sg.securityGroupId,
              securityGroupName: sg.securityGroupName,
              userSelectedGroup: false
            });
          })

        }
      } else {
        this.config.securityGroups.forEach((sg: SecurityGroup) => {
          if (!sg) {
            this.toastr.error(ModalMessages.SecurityGroupDoesNotExist);
            return;
          }
          securityGroups.push({
            securityGroupId: sg.securityGroupId,
            securityGroupName: sg.securityGroupName,
            userSelectedGroup: false
          });
        })
      }

      if (securityGroups.length == 0) {
        this.toastr.error(ModalMessages.SecurityGroupDoesNotExist);
        return;
      }

      // If you want all values, including disabled:
      let processSettingFormValue = this.fileUploadForm.getRawValue();//
      // if (this.config.flpConfigurationId) {
      //   processSettingFormValue = this.fileUploadForm.getRawValue();
      // } else {
      //   processSettingFormValue = this.fileUploadForm.value;
      // }



      //Creating model  
      this.processSettings = {
        flpConfigurationId: this.config.flpConfigurationId,
        processName: processSettingFormValue.clientInfo?.processName,
        description: processSettingFormValue.clientInfo?.description,
        RegionId: String(processSettingFormValue.clientInfo?.RegionId),
        SubRegionId: String(processSettingFormValue.clientInfo?.SubRegionId),
        ClientId: String(processSettingFormValue.clientInfo?.ClientId),
        fileType: this.fileType,
        region: this.childComponent.regionName,
        subRegion: this.childComponent.subRegionName,
        clientName: this.childComponent.clientName,
        securityGroups: securityGroups,
        sender_communication_email: this.config.sender_communication_email,
        dataSource: this.config.dataSource,
        multisheet: (this.wksheets.length > 1),
        sheetReferenceByIndex: this.mappingType === 'sheetIndex' ? true : false,
        sourcePath: null
      };

      this.fileSettings = [];
      // this.fileSettings = Object.keys(this.multiSheetConfigs).map(sheetName => ({
      //     sheetName: sheetName,
      //     ignoreSheet: false,
      //     additionalSettings: this.multiSheetConfigs[sheetName],
      //     columnNameDatatypeNames: this.columnDatatypePerSheet[sheetName] || [],
      //     // ...add other FileSettings fields as needed
      //   }));
      // 2. Create fileSettings array from multiSheetConfigs


      if ([this.DataSourceTypes.Default, this.DataSourceTypes.DataBricks].includes(this.dataSource)) {
        if (this.wksheets.length > 0) {
          Object.keys(this.multiSheetConfigs).forEach(tabName => {
            if (!tabName) {
              return;
            }
            const configAdditionalSetting = this.multiSheetConfigs[tabName];
            const tabRuleSet = this.multiSheetConfigs[tabName].ruleSet;
            const igSheet = this.checkIgnoreSheet(tabName);
            if (configAdditionalSetting.newSheet === true || configAdditionalSetting.missingSheet === true) {
              return; //skip new sheet

            }
            if (this.dataSource === DataSourceType.DataBricks) {
              configAdditionalSetting.dataSource = this.dataSource;
              configAdditionalSetting.databaseConfigurationId = String(this.config.deltaServerNameId);
              configAdditionalSetting.tableName = this.config.deltaTableName;
            }
            else if (this.dataSource === DataSourceType.LandingLayer) {
              configAdditionalSetting.databaseConfigurationId = "8";
            } else {
              //todo
              configAdditionalSetting.deltaServerNameId = null;
            }

            if (!configAdditionalSetting.flexCheckHasHeaders) {
              let customHeaders: string[] = []; //new columns in file
              //we add =<index> for API to identify the column index mapping


              this.columnDatatypePerSheet[tabName].forEach((c, i) => {
                if (c.willInclude && c.willAddNewColumn)
                  customHeaders.push(`${c.ColumnName}=${i}`); //c.willInclude &&
              });

              configAdditionalSetting.column_name_list = customHeaders.join(',');
              configAdditionalSetting.order_by_column_list_for_dedup =
                this.columnDatatypePerSheet[tabName]
                  .filter((x) => x.columnForDedeup)
                  .map((c) => c.ColumnName)
                  .join(',');
            } else {
              configAdditionalSetting.column_name_list = this.columnDatatypePerSheet[tabName]
                .filter((x) => x.willInclude === true && x.willAddNewColumn)
                .map((c) => c.ColumnName)
                .join(','); //x.willInclude === true &&

              configAdditionalSetting.order_by_column_list_for_dedup =
                this.columnDatatypePerSheet[tabName]
                  .filter((x) => x.columnForDedeup)
                  .map((c) => c.ColumnName)
                  .join(',');
            }

            this.databaseSettings = {
              tableName: configAdditionalSetting.tableName,
              drop_history_table: configAdditionalSetting.drop_history_table,
              drop_main_table: configAdditionalSetting.drop_main_table,
              //databaseConfigurationId:String(configAdditionalSetting.databaseConfigurationId),          
              databaseConfigurationId: configAdditionalSetting.ignoreSheet === false ? String(configAdditionalSetting.databaseConfigurationId)
                : null,
              validate_fileschema: configAdditionalSetting.validate_fileschema,
              databaseName: configAdditionalSetting.databaseName,
              mergeData: configAdditionalSetting.mergeData,
              createHistoryTable: configAdditionalSetting.createHistoryTable,
              deltaStorageAccountId: String(configAdditionalSetting.deltaStorageAccountId),
              deltaContainerName: configAdditionalSetting.deltaContainerName,
              deltaSource: configAdditionalSetting.deltaSource,
              deltaJobId: String(configAdditionalSetting.deltaJobId),
              deltaTableName: configAdditionalSetting.deltaTableName,
              deltaServerNameId: configAdditionalSetting.deltaServerNameId,
              datalakeStorageAccountPath: null,
              landingLayerAcceptedPath: configAdditionalSetting?.landingLayerAcceptedPath ?? '',
              landingLayerRejectedPath: configAdditionalSetting?.landingLayerRejectedPath ?? ''
            }

            if (this.isSelectedExistingProcess === true && this.excelUploadMultiSheet === false && this.selectedProcessHasTabName === false && this.wksheets.length == 1) {
              tabName = null;
            }

            for (let i = 0; i < tabRuleSet.length; i++) {
              if (tabRuleSet[i].subRuleId === 9) {
                const col1 = tabRuleSet[i].ruleColumnName;

                tabRuleSet[i].ruleColumnName = Array.isArray(col1)
                  ? col1.map(String)
                  : [String(col1)];

                // this.ruleSet[i].ruleColumnName2 = Array.isArray(col2)
                //   ? col2.map(String)
                //   : [String(col2)];
              }
            }


            const fileSettingObj: FileSettings = {
              tabName: tabName,
              ignoreSheet: igSheet,
              additionalSettings: configAdditionalSetting,
              databaseSettings: this.databaseSettings,
              columnNameDatatypeNames: this.wksheets.length == 1 ? this.columnDatatype.filter(
                (x) => x.willInclude === true && x.willAddNewColumn
              ) : this.columnDatatypePerSheet[tabName].filter(
                (x) => x.willInclude === true && x.willAddNewColumn
              ),
              ruleSet: tabRuleSet
              // ...add other properties as per your FileSettings interface
            };
            this.fileSettings.push(fileSettingObj);
          });

        } else {

          //for csv and txt only        
          const configAdditionalSetting = this.config;
          // const igSheet= this.checkIgnoreSheet(tabName);
          if (this.dataSource === DataSourceType.DataBricks) {
            configAdditionalSetting.dataSource = this.dataSource;
            configAdditionalSetting.databaseConfigurationId = String(this.config.deltaServerNameId);
            configAdditionalSetting.tableName = this.config.deltaTableName;
          }

          if (!configAdditionalSetting.flexCheckHasHeaders) {
            let customHeaders: string[] = []; //new columns in file
            //we add =<index> for API to identify the column index mapping
            this.columnDatatype.forEach((c, i) => {
              if (c.willInclude && c.willAddNewColumn)
                customHeaders.push(`${c.ColumnName}=${i}`); //c.willInclude &&
            });

            configAdditionalSetting.column_name_list = customHeaders.join(',');
            configAdditionalSetting.order_by_column_list_for_dedup =
              this.columnDatatype.filter((x) => x.columnForDedeup).map((c) => c.ColumnName).join(',');
          } else {
            configAdditionalSetting.column_name_list =
              this.columnDatatype.filter((x) => x.willInclude === true && x.willAddNewColumn).map((c) => c.ColumnName).join(',');

            configAdditionalSetting.order_by_column_list_for_dedup =
              this.columnDatatype.filter((x) => x.columnForDedeup).map((c) => c.ColumnName).join(',');
          }

          this.databaseSettings = {
            tableName: configAdditionalSetting.tableName,
            drop_history_table: configAdditionalSetting.drop_history_table,
            drop_main_table: configAdditionalSetting.drop_main_table,
            databaseConfigurationId: String(configAdditionalSetting.databaseConfigurationId),
            validate_fileschema: configAdditionalSetting.validate_fileschema,
            databaseName: configAdditionalSetting.databaseName,
            mergeData: configAdditionalSetting.mergeData,
            createHistoryTable: configAdditionalSetting.createHistoryTable,
            deltaStorageAccountId: String(configAdditionalSetting.deltaStorageAccountId),
            deltaContainerName: configAdditionalSetting.deltaContainerName,
            deltaSource: configAdditionalSetting.deltaSource,
            deltaJobId: String(configAdditionalSetting.deltaJobId),
            deltaTableName: configAdditionalSetting.deltaTableName,
            deltaServerNameId: configAdditionalSetting.deltaServerNameId,
            datalakeStorageAccountPath: null,
            landingLayerAcceptedPath: configAdditionalSetting?.landingLayerAcceptedPath ?? '',
            landingLayerRejectedPath: configAdditionalSetting?.landingLayerRejectedPath ?? ''
          }

          for (let i = 0; i < this.ruleSet.length; i++) {
            if (this.ruleSet[i].subRuleId === SubRuleTypes.Comparison || this.ruleSet[i].ruleTypeId === RuleTypeNames.BEValidation) {
              const col1 = this.ruleSet[i].ruleColumnName;
              this.ruleSet[i].ruleColumnName = Array.isArray(col1)
                ? col1.map(String)
                : [String(col1)];

              // this.ruleSet[i].ruleColumnName2 = Array.isArray(col2)
              //   ? col2.map(String)
              //   : [String(col2)];
            }
          }

          const fileSettingObj: FileSettings = {
            tabName: null,
            ignoreSheet: false,
            additionalSettings: configAdditionalSetting,
            databaseSettings: this.databaseSettings,
            columnNameDatatypeNames: this.columnDatatype.filter(
              (x) => x.willInclude === true && x.willAddNewColumn
            ),
            ruleSet: this.ruleSet
            // ...add other properties as per your FileSettings interface
          };
          this.fileSettings.push(fileSettingObj);
        }
      } else {
        const configAdditionalSetting = this.config;

        this.databaseSettings = {
          tableName: configAdditionalSetting.tableName,
          drop_history_table: configAdditionalSetting.drop_history_table,
          drop_main_table: configAdditionalSetting.drop_main_table,
          //databaseConfigurationId:String(configAdditionalSetting.databaseConfigurationId),          
          databaseConfigurationId: configAdditionalSetting.ignoreSheet === false ? String(configAdditionalSetting.databaseConfigurationId)
            : '0',
          validate_fileschema: configAdditionalSetting.validate_fileschema,
          databaseName: configAdditionalSetting.databaseName,
          mergeData: configAdditionalSetting.mergeData,
          createHistoryTable: configAdditionalSetting.createHistoryTable,
          deltaStorageAccountId: String(configAdditionalSetting.deltaStorageAccountId),
          deltaContainerName: configAdditionalSetting.deltaContainerName,
          deltaSource: configAdditionalSetting.deltaSource,
          deltaJobId: String(configAdditionalSetting.deltaJobId),
          deltaTableName: configAdditionalSetting.deltaTableName,
          deltaServerNameId: configAdditionalSetting.deltaServerNameId,
          datalakeStorageAccountPath: null,
          landingLayerAcceptedPath: configAdditionalSetting.landingLayerAcceptedPath,
          landingLayerRejectedPath: configAdditionalSetting.landingLayerRejectedPath
        }

        const fileSettingObj: FileSettings = {
          tabName: null,
          ignoreSheet: false,
          additionalSettings: configAdditionalSetting,
          databaseSettings: this.databaseSettings,
          columnNameDatatypeNames: [],
          ruleSet: []
          // ...add other properties as per your FileSettings interface
        };
        this.fileSettings.push(fileSettingObj);
        // debugger;
        //return;
      }

      const model: LocalFileDataSourceTypev4_1 = {
        fileInput: this.file,
        processSettings: this.processSettings,
        fileSettings: this.fileSettings
      };

      // 4. Submit or process the model
      // Example: this.yourService.submitModel(model).subscribe(...)

      // this.data = {
      //   fileInput: this.file,
      //   configuration: this.fileUploadForm.value,
      //   columnNameDatatypeNames: this.columnDatatype.filter(
      //     (x) => x.willInclude === true && x.willAddNewColumn
      //   ), //x.willInclude === true &&
      //   additionalSettings: this.config,
      // };
      if (this.dataSource === DataSourceType.LandingLayer) {
        this.landingLayerService.insertConfigLandingLayer(this.selectedFiles, model)
          .subscribe({
            next: (response: APIResponse<FlpConvertParquetRequestDto>) => {
              console.log(response);
              if (response.result) {
                this.fileSubmissionCompleted = true;

                //create the payload
                var payload: LandingLayerInsertConfigurationRequest = {
                  flpConfigurationId: response.result.flpConfigurationId,
                  processName: response.result.processName,
                  files: this.selectedFiles.map(f => f.File),
                  userName: sessionStorage.getItem('username'),
                  loggedInUser: sessionStorage.getItem('upn').split('@')[0],
                  uploadFileId: response.result.blobClients?.uploadedId
                }
                this.landingLayerService.moveFileToLandingLayer(payload).subscribe({
                  next: (resp) => {
                    console.log(resp);
                  },
                  error: (error) => {
                    console.log('error' + error);
                    this.toastr.error('MoveFileToLandingLayer ' + ModalMessages.SomethingWentWrong);
                    //return;
                  },
                });
                // this.configService
                //   .flpCSVtoParquet(response.result, apiEndPoint, isDataBricks, apiVersion)
                //   .subscribe({
                //     next: (_) => { },
                //     error: (error) => {
                //       console.log('error' + error);
                //       //if an error was encountered, import will update the status; do nothing
                //       //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
                //     },
                //   });


                const fileSubmissionTab = document.getElementById(
                  'fileSubmission-tab'
                ) as HTMLElement;
                fileSubmissionTab.click();

                setTimeout(() => {
                  //this.router.navigate(['/file-processing-status']);
                  this.router.navigate([
                    '/file-processing-status',
                    response.result.blobClients?.uploadedId,
                  ]);
                }, 3000);
              }
            },
            error: (error) => {
              this.toastr.error(ModalMessages.SomethingWentWrong);

            },
          })
      } else {
        this.configService
          .insertNewConfigurationV4_1(this.file, model)
          .subscribe({
            next: (response: APIResponse<FlpConvertParquetRequestDto>) => {
              //console.log('getting response');
              console.log(response);
              if (response) {
                if (response.responseCode === 200) {
                  if (response.responseMessage[0] === 'Invalid File.') {
                    //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.InvalidFile);
                    this.toastr.error(ModalMessages.InvalidFile);
                    return;
                  }

                  if (
                    response.responseMessage[0] ===
                    'Additional settings not provided.'
                  ) {
                    //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.AdditionalSettingsNotProvided);
                    this.toastr.error(
                      ModalMessages.AdditionalSettingsNotProvided
                    );
                    return;
                  }

                  if (response.result) {
                    let apiVersion = this.dataSource === DataSourceType.DataBricks ? '4.0' : '3.0';
                    let apiEndPoint = 'ProcessCsvFile';
                    if (
                      this.file.name.split('.').pop() === FileType.MSExcel1 ||
                      this.file.name.split('.').pop() === FileType.MSExcel2 ||
                      this.file.name.split('.').pop() === FileType.MSExcel3
                    ) {
                      apiEndPoint = 'ProcessExcelFile';
                      if (this.isSelectedExistingProcess === true && this.excelUploadMultiSheet === false && this.selectedProcessHasTabName === false && this.wksheets.length == 1) {
                        let apiVersion = this.dataSource === DataSourceType.DataBricks ? '4.0' : '3.0';
                      } else {
                        apiVersion = '4.1';
                      }


                    } else if (
                      this.file.name.split('.').pop() === FileType.TextFiles
                    ) {
                      apiEndPoint = 'ProcessTxtFile';
                    }

                    this.resetForm();
                    this.getProcessNamesByLoginId();
                    this.showDatabasePreview = false;
                    this.preview = false;
                    this.isNewProcess = false;
                    //this.config = ;
                    //this.resetAdditionalSettings(); already been called in this.resetForm();

                    //TODO: uncomment after testing\
                    const isDataBricks = (this.dataSource == DataSourceType.DataBricks);
                    this.configService
                      .flpCSVtoParquet(response.result, apiEndPoint, isDataBricks, apiVersion)
                      .subscribe({
                        next: (_) => { },
                        error: (error) => {
                          console.log('error' + error);
                          //if an error was encountered, import will update the status; do nothing
                          //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
                        },
                      });

                    //this.formFile.nativeElement.value = ''; //clear the previously selected file in input-type
                    this.activeTab = 'fileSubmission-tab';
                    this.fileSubmissionCompleted = true;
                    const fileSubmissionTab = document.getElementById(
                      'fileSubmission-tab'
                    ) as HTMLElement;
                    fileSubmissionTab.click();

                    setTimeout(() => {
                      //this.router.navigate(['/file-processing-status']);
                      this.router.navigate([
                        '/file-processing-status',
                        response.result.blobClients?.uploadedId,
                      ]);
                    }, 3000);
                  }
                  else {
                    //update the [commit_FlpConfigProcessStatus] flpProcessStatusId=3
                    //console.log('2:' + response);
                    this.toastr.error(ModalMessages.SomethingWentWrong);
                  }

                  //this.submitted = false;
                } else {
                  //update the [commit_FlpConfigProcessStatus] flpProcessStatusId=3
                  //console.log('2:' + response);
                  this.toastr.error(ModalMessages.SomethingWentWrong);
                }
              }
            },
            error: (error) => {
              //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
              this.toastr.error(ModalMessages.SomethingWentWrong);
              //update
            },
          });
      }
    }

    //
  }


  onShowDatabaseSchemaForMultiSheet() {
    this.processConfigurationFormHasError = false;
    if (this.processConfigurationFormHasError) {
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      this.modifySettingsOpen();
      return;
    } else if (
      !this.isValidDataType ||
      this.columnDatatype.filter((x) => x.invalidDataType === true).length >
      0 ||
      this.columnDatatype.filter((x) => x.invalidColumnName === true).length > 0
    ) {
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      return;
    } else if (this.InvalidDataSource) {
      //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataSourceNewColumn);
      return;
    }

    //additional checker
    //if there's an excluded column; check whether the validate schema is checked, if not, don't proceed and show error
    // if(this.columnDatatype.filter(x => x.willInclude === false).length > 0 && this.config.validate_fileschema === false){
    //   let strMsgTemp = 'Database'
    //   if(this.dataSource === DataSourceType.DataBricks) strMsgTemp = 'Databricks';
    //   this.toastr.error(`An excluded column is found! Please check the Validate Schema in ${strMsgTemp} Settings`);
    //   return;
    // }

    // else if (this.InvalidDataSource) {
    //   return;
    // }

    if (this.activeTab === 'attachFile-tab') {
      if (this.hasErrors || this.processConfigurationFormHasError) {
        //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.CantProceedPleaseCorrectOtherDetails);
        this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
        const attachFileTab = document.getElementById(
          'attachFile-tab'
        ) as HTMLElement;
        //clientTab.classList.add("active");
        attachFileTab.click();
        return;
      }
      if (this.rowCountForDisplay < 1) {
        this.toastr.error(ModalMessages.NoRowsToIngest);
        const attachFileTab = document.getElementById(
          'attachFile-tab'
        ) as HTMLElement;
        //clientTab.classList.add("active");
        attachFileTab.click();
        return;
      }
      this.activeTab = 'databaseSchema-tab';

      const databaseSchemaTab = document.getElementById(
        'databaseSchema-tab'
      ) as HTMLElement;
      databaseSchemaTab.click();
    } else if (this.activeTab === 'databaseSchema-tab') {
      this.data = {
        fileInput: this.file,
        configuration: this.fileUploadForm.value,
        columnNameDatatypeNames: this.columnDatatype.filter(
          (x) => x.willInclude === true && x.willAddNewColumn
        ), //x.willInclude === true &&
        additionalSettings: this.config,
      };

      if (this.dataSource === DataSourceType.DataBricks) {
        this.data.additionalSettings.dataSource = this.dataSource;
        this.data.additionalSettings.databaseConfigurationId = String(
          this.config.deltaServerNameId
        );
        this.data.additionalSettings.tableName = this.config.deltaTableName;
      }

      if (!this.config.flexCheckHasHeaders) {
        let customHeaders: string[] = []; //new columns in file
        //we add =<index> for API to identify the column index mapping
        this.columnDatatype.forEach((c, i) => {
          if (c.willInclude && c.willAddNewColumn)
            customHeaders.push(`${c.ColumnName}=${i}`); //c.willInclude &&
        });

        this.data.additionalSettings.column_name_list = customHeaders.join(',');
        this.data.additionalSettings.order_by_column_list_for_dedup =
          this.columnDatatype
            .filter((x) => x.columnForDedeup)
            .map((c) => c.ColumnName)
            .join(',');
      } else {
        this.data.additionalSettings.column_name_list = this.columnDatatype
          .filter((x) => x.willInclude === true && x.willAddNewColumn)
          .map((c) => c.ColumnName)
          .join(','); //x.willInclude === true &&

        this.data.additionalSettings.order_by_column_list_for_dedup =
          this.columnDatatype
            .filter((x) => x.columnForDedeup)
            .map((c) => c.ColumnName)
            .join(',');
      }

      // //prepare ColumnNameDatatypeName
      // this.columnDatatype.forEach((c, i) => {
      //   c.ColumnName = `${c.ColumnName}=${c.DatatypeName}`;
      //   switch (c.DatatypeName.toLowerCase()) {
      //     case "date":
      //     case "time:":
      //     case "datetime":
      //       c.ColumnName = `${c.ColumnName}|${c.dateTimeFormatId}`
      //       break;
      //   }
      // });
      this.configService
        .insertNewConfiguration(this.file, this.data)
        .subscribe({
          next: (response: APIResponse<FlpConvertParquetRequestDto>) => {
            //console.log('getting response');
            //console.log(response);
            if (response) {
              if (response.responseCode === 200) {
                if (response.responseMessage[0] === 'Invalid File.') {
                  //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.InvalidFile);
                  this.toastr.error(ModalMessages.InvalidFile);
                  return;
                }

                if (
                  response.responseMessage[0] ===
                  'Additional settings not provided.'
                ) {
                  //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.AdditionalSettingsNotProvided);
                  this.toastr.error(
                    ModalMessages.AdditionalSettingsNotProvided
                  );
                  return;
                }

                if (response.result) {
                  let apiVersion = '3.0';
                  let apiEndPoint = 'ProcessCsvFile';
                  if (
                    this.file.name.split('.').pop() === FileType.MSExcel1 ||
                    this.file.name.split('.').pop() === FileType.MSExcel2 ||
                    this.file.name.split('.').pop() === FileType.MSExcel3
                  ) {
                    apiEndPoint = 'ProcessExcelFile';
                    apiVersion = '4.1';
                  } else if (
                    this.file.name.split('.').pop() === FileType.TextFiles
                  ) {
                    apiEndPoint = 'ProcessTxtFile';
                  }

                  this.resetForm();
                  this.getProcessNamesByLoginId();
                  this.showDatabasePreview = false;
                  this.preview = false;
                  this.isNewProcess = false;
                  //this.config = ;
                  //this.resetAdditionalSettings(); already been called in this.resetForm();

                  //TODO: uncomment after testing\
                  // debugger;
                  const isDataBricks =
                    (this.dataSource ==
                      DataSourceType.DataBricks);
                  this.configService
                    .flpCSVtoParquet(response.result, apiEndPoint, isDataBricks, apiVersion)
                    .subscribe({
                      next: (_) => { },
                      error: (error) => {
                        console.log('error' + error);
                        //if an error was encountered, import will update the status; do nothing
                        //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
                      },
                    });

                  //this.formFile.nativeElement.value = ''; //clear the previously selected file in input-type
                  this.activeTab = 'fileSubmission-tab';
                  this.fileSubmissionCompleted = true;
                  const fileSubmissionTab = document.getElementById(
                    'fileSubmission-tab'
                  ) as HTMLElement;
                  fileSubmissionTab.click();

                  setTimeout(() => {
                    //this.router.navigate(['/file-processing-status']);
                    this.router.navigate([
                      '/file-processing-status',
                      response.result.blobClients?.uploadedId,
                    ]);
                  }, 3000);
                }

                //this.submitted = false;
              } else {
                //update the [commit_FlpConfigProcessStatus] flpProcessStatusId=3
                //console.log('2:' + response);
              }
            }
          },
          error: (error) => {
            //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
            this.toastr.error(ModalMessages.SomethingWentWrong);
            //update
          },
        });
    }

    //
  }


  onConfigurationChange(event: any) {

    this.isNewProcess = false;
    if (!this.file && this.dataSource !== DataSourceType.LandingLayer) return;
    this.resetFormByChangeMapping(true);
    const btnNext = document.getElementById('btnNext') as HTMLButtonElement;
    btnNext.disabled = true;

    this.NewColumnFoundOnSource = false;
    this.notFoundSheetMessage = '';
    if (event.processNames === 'New') {
      this.config = this.resetAdditionalSettings();
      this.InvalidDataSource = false;

      this.processConfigurationFormHasError = true;
      this.MissingColumnFoundOnSource = false;
      this.isSelectedExistingProcess = false;

      if (this.dataSource === DataSourceType.LandingLayer) {
        //display it again after 500ms
        setTimeout(() => {
          this.totalFileSizeInMb = 0;

          this.isNewProcess = true;
          this.isFileUploaded = true;
          //reset input so the same file can be selected again
          this.selectedFiles = [];
        }, 500);
      }

      this.prepopForm();
      //TODO: temporary workaround
      if (this.dataSource !== DataSourceType.LandingLayer) {
        setTimeout(() => {
          this.isNewProcess = true;
          this.onPreview(null);
        }, 500);
      }
    } else {
      this.fileUploadForm
        .get('configuration')
        .setValue(event.flpConfigurationId);
      this.busyService.busy();
      this.configService
        .getConfigurationWithIdV4_1(event.flpConfigurationId)
        .subscribe({
          next: (response: APIResponse<OnlineConfigResponse>) => {
            //  next: (response) => {
            this.isNewProcess = false;
            var _fileType = this.fileType;
            if (response.responseCode === 200) {
              if (response.result !== null) {
                let fileSettings = response.result.fileSettings;
                let processSettings = response.result.processSettings;
                this.isSelectedExistingProcess = true;
                // let databaseSettings = response.result.
                this.excelUploadMultiSheet = processSettings.multisheet;
                this.selectedProcessHasTabName = fileSettings.some(fs => fs.tabName && fs.tabName.trim() != '');
                for (const fileSetting of fileSettings) {
                  let tabName = fileSetting.tabName;
                  let additionalSettings = fileSetting?.additionalSettings;
                  var columnNameList = additionalSettings.order_by_column_list_for_dedup
                    ? additionalSettings.order_by_column_list_for_dedup
                    : '';
                  let databaseSettings = fileSetting.databaseSettings;
                  let flpRuleSets = fileSetting.ruleSets;
                  var settingsFromExistingFileConfiguration: AdditionalSettings = {
                    key_columns: additionalSettings.key_column_list,
                    flpConfigurationId: processSettings.flpConfigurationId,
                    ClientId: processSettings.clientId,
                    clientName: processSettings.clientName,
                    delimiter: additionalSettings.delimiter,
                    txtQuoteCharacter: additionalSettings.quote_character,
                    flexCheckHasHeaders: additionalSettings.is_header_provided,
                    flexCheckSkipEmptyLines: additionalSettings.skip_empty_lines,
                    flexCheckEscapeCharacter: '"',//TODO not yet saved
                    txtEncoding: '', //TODO
                    flexCheckOrderByColumnListForDedup: additionalSettings.order_by_column_list_for_dedup
                      ? true
                      : false,
                    order_by_column_list_name:
                      columnNameList ??
                      additionalSettings.order_by_column_list_for_dedup.split(
                        ' '
                      )[0],
                    order_by_column_list_name_sort_dir:
                      columnNameList ?? additionalSettings?.keep_first_row ? 'asc' : 'desc',
                    is_active: true,
                    do_not_archive_file: additionalSettings.do_not_archive_file,
                    spanish_to_english: additionalSettings.spanish_to_english,
                    roman_numerals_only: additionalSettings.roman_numerals_only,
                    ignore_duplicate_rows: additionalSettings.ignore_duplicate_rows,
                    csv_column_name_list: additionalSettings.column_name_list,
                    tableName: databaseSettings.table_name,
                    databaseName: databaseSettings.database_name,
                    validate_fileschema: databaseSettings.validate_fileschema,
                    drop_history_table: databaseSettings.drop_history_table,
                    drop_main_table: databaseSettings.drop_main_table,
                    order_by_column_list_for_dedup: additionalSettings.order_by_column_list_for_dedup,
                    keep_first_row: additionalSettings.keep_first_row,
                    databaseNames: [], //TODO: no need to pull, will be retrieved on page-load
                    databaseNameId: 0, //TODO
                    databaseConfigurationId: databaseSettings.databaseConfigurationId, //todo get from
                    RegionId: processSettings.regionId,
                    SubRegionId: processSettings.subRegionId,
                    fileType: '',
                    convert_datatypes_column_list:
                      additionalSettings.convert_datatypes_column_list,
                    column_name_list: '',
                    skip_footer_rows: additionalSettings.skip_footer_rows,
                    skip_header_rows: additionalSettings.skip_rows,
                    sender_communication_email:
                      sessionStorage.getItem('emailID'),
                    region: processSettings.region,
                    subRegion: processSettings.subRegion,
                    file_column_mapping: fileSetting.fileColumnMapping,
                    mergeData: databaseSettings.mergeData,
                    createHistoryTable: databaseSettings.createHistoryTable,
                    dataSource: +processSettings.dataSource,
                    sourcePath: (processSettings.dataSource === DataSourceType.DataBricks) ? databaseSettings.datalakeStorageAccountPath : processSettings.sourcePath,
                    deltaTableName: databaseSettings.table_name,
                    deltaServerNameId: +databaseSettings.databaseConfigurationId,
                    deltaJobId: databaseSettings.deltaJobId,
                    deltaStorageAccountId: databaseSettings.deltaStorageAccountId,
                    deltaContainerName: databaseSettings.deltaContainerName,
                    deltaSource: databaseSettings.datalakeStorageAccountPath,
                    securityGroups: processSettings.securityGroups,
                    frmSubmitted: true,
                    processName: processSettings.process_name,
                    description: processSettings.description,
                    txtEscapeCharacter: '', //TODO not yet saved
                    ignoreSheet: fileSetting.ignoreSheet,
                    newSheet: false,
                    missingSheet: false,
                    ruleSet: flpRuleSets,
                    ruleSetNameId: flpRuleSets[0]?.ruleSetNameId,
                    ruleSetName: flpRuleSets[0]?.ruleSetName,
                    ruleType: '',
                    subRuleType: null,
                    patternType: null,
                    ruleColumnName: '',
                    isCombinationRule: false,
                    requiredRuleDescription: '',
                    uniqueRuleDescription: '',
                    formatType: '',
                    valueType: '',
                    conditionType: null,
                    aiPrompt: '',
                    fromValue: 0,
                    toValue: 0,
                    spName: '', //todo,
                    campaignId: processSettings?.campaignId,
                    internalCampaignId: processSettings?.internalCampaignId


                    //landling layer values
                    ,
                    landingLayerFileExtension: fileSetting.additionalSettings?.landingLayerFileExtension.split(',').map(ext => parseInt(ext.trim(), 10)),
                    landingLayerRegex: [],
                    landingLayerPrefix: fileSetting.additionalSettings?.landingLayerPrefix,
                    landingLayerDateformat: fileSetting.additionalSettings?.landingLayerDateFormatId,
                    landingLayerTimeformat: fileSetting.additionalSettings?.landingLayerTimeFormatId,
                    landingLayerAcceptedPath: databaseSettings.landingLayerAcceptedPath,
                    landingLayerRejectedPath: databaseSettings.landingLayerRejectedPath
                  };
                  if (fileSetting.additionalSettings?.regex) {
                    settingsFromExistingFileConfiguration.landingLayerRegex = this.helperUtil.toRegexArray(fileSetting.additionalSettings?.regex);
                  }
                  this.config = settingsFromExistingFileConfiguration;
                  this.isNewProcess = true;

                  this.processConfigurationFormHasError = false;
                  this.InvalidDataSource = false;

                  this.keyColumnsSubject.next(
                    settingsFromExistingFileConfiguration.key_columns
                  );
                  this.dedupColumnsSubject.next(
                    settingsFromExistingFileConfiguration.order_by_column_list_for_dedup
                  );
                  const notification: FilePreviewNotification = {
                    sheetName: tabName,
                    noOfDuplicates: this.noOfDuplicates,
                    totalCountForDisplay: this.totalRowCountEXL - this.noOfDuplicates, // this.recordsArrayForDisplay.length 
                    InvalidDataSource: false,
                    NewColumnFoundOnSource: false,
                    MissingColumnFoundOnSource: false,
                    validate_fileschema: settingsFromExistingFileConfiguration.validate_fileschema,
                  };
                  this.FilePreviewNotification.push(notification);
                  if (tabName) {
                    this.multiSheetConfigs[tabName] = { ...this.config };
                    this.columnDatatypePerSheet[tabName] = [];// JSON.parse(JSON.stringify(this.columnDatatype));

                    this.multiSheetForms[tabName] = this.createProcessConfigForm(this.multiSheetConfigs[tabName]);
                  } else {
                    //this.config = { ...this.config };
                    //this.columnDatatype = JSON.parse(JSON.stringify(this.columnDatatype))
                  }
                }

                this.mappingType = processSettings.sheetReferenceByIndex === true ? 'sheetIndex' : 'sheetName'; //default mapping type
              } else {
                btnNext.disabled = true;
                this.busyService.idle();
                this.toastr.error('Unable to proceed!<br>Invalid process name for the file', '', { enableHtml: true });

                setTimeout(() => {
                  this.fileUploadForm.get('configuration').setValue({ flpConfigurationId: '', processNames: 'New', description: 'New Process' });
                  this.onConfigurationChange({ flpConfigurationId: '', processNames: 'New', description: 'New Process' });
                }, 1000);
                return;
              }

              // this.config = this.multiSheetConfigs[0];

              if (this.dataSource !== DataSourceType.LandingLayer) this.onPreview(this.config);
              this.busyService.idle();
            }
          },

          error: (_) => {
            //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.SomethingWentWrong);
            this.toastr.error(ModalMessages.SomethingWentWrong);
            this.busyService.idle();
          },
        });
    }

    //btnNext.disabled = false;
  }
  startValidateExistingConfigurationToFile: any;
  async validateExistingConfigurationToFile() {
    if (this.config.spanish_to_english && this.showEnglishConversionOnProcess) {
      return;
    }

    clearTimeout(this.startValidateExistingConfigurationToFile);

    const btnNext = document.getElementById('btnNext') as HTMLButtonElement;
    btnNext.disabled = true;

    var myObject: { file: string; db: string }[] = [];
    var fileDbColumns: FileColumnMapping[] = [...this.config.file_column_mapping,];
    let configurationKeyColumns: string[] = this.config.key_columns.split(','); //existing list of key columns in db
    let configurationColumnNames = this.config.file_column_mapping.map((x) =>
      x.fileColumn.toUpperCase()
    ); //this.config.csv_column_name_list.split(','); // existing list of column names in db
    let configurationDbColumnNames = this.config.file_column_mapping.map((x) =>
      x.dbColumn.toUpperCase()
    );
    let fileCurrentColumnNames = this.columnDatatype.map((x) => x.ColumnName); //current file columns
    let convert_datatypes_column_list = this.config.file_column_mapping.map(
      (x) => x.dataType
    ); // this.config.convert_datatypes_column_list.split(',');
    let configurationDedupColumns: string[] = this.config.order_by_column_list_for_dedup.split(',');

    if (this.config.spanish_to_english) {
      //this.displayOnlyEnglishCharacters();
      fileCurrentColumnNames = this.columnDatatype.map((x) => x.ColumnName);
    }

    this.config.file_column_mapping.forEach((x) => {
      myObject.push({ file: x.fileColumn, db: x.dbColumn });
    });

    let diff: string[] = []; //new columns in file
    let seen = new Set();
    fileCurrentColumnNames.forEach((c, i) => {
      if (this.config.flexCheckHasHeaders) {
        let foundColumn = fileDbColumns.findIndex((x) => x.fileColumn === c); //configurationColumnNames.indexOf(c);
        let foundColumnInDbColumn = fileDbColumns.findIndex(
          (x) => x.dbColumn === c
        ); //configurationDbColumnNames.indexOf(c);

        //if (foundColumn < 0) diff.push(c);
        if (foundColumn < 0 && foundColumnInDbColumn < 0) {
          diff.push(`${c}-${i}`); //added the index of the not found column
        }

        if (foundColumn >= 0) {
          //remove from the list
          fileDbColumns.splice(foundColumn, 1);
        }
      } else {
        //to get the index
        //let configurationColumnNames2 = configurationColumnNames.map(x => `COL${x.substr(x.lastIndexOf('=') + 1)}`);

        let foundColumn = configurationColumnNames.indexOf(c);
        //if (foundColumn < 0) diff.push(c);
        if (foundColumn < 0) diff.push(`${c}-${i}`); //added the index of the not found column
      }
    });

    ////console.log(diff);

    //compare fileColumn (in db) with file headers
    let existingCols: string[] = []; //new columns in file
    configurationColumnNames = this.config.file_column_mapping.map((x) =>
      x.fileColumn.toUpperCase()
    );
    if (!this.config.flexCheckHasHeaders) {
      //regenerate column headers with col0-colnth
      //let configurationColumnNames2 = configurationColumnNames.map(x => `COL${x.substr(x.lastIndexOf('=') + 1)}`);

      configurationColumnNames.forEach((c, i) => {
        let foundColumn = fileCurrentColumnNames.indexOf(c);
        if (foundColumn < 0) existingCols.push(c);
      });
    } else {
      let columnNameIsInFile: number = 0;
      let columnNameIsInDbColumn: number = 0;
      //database fileColumn vs actual file headers
      // configurationColumnNames.forEach((c, i) => {

      //   let foundColumn = fileCurrentColumnNames.indexOf(c);

      //   if (foundColumn < 0) {
      //     //check if fileColumn are in actual file column

      //     existingCols.push(c);
      //   }

      // });

      this.config.file_column_mapping.forEach((o, i) => {
        let foundColumn = fileCurrentColumnNames.indexOf(
          o.fileColumn.toUpperCase()
        );
        let foundColumn2 = fileCurrentColumnNames.indexOf(
          o.dbColumn.toUpperCase()
        );

        if (foundColumn < 0 && foundColumn2 < 0) {
          existingCols.push(o.dbColumn);
        }
      });
    }


    this.InvalidDataSource = false;
    this.MissingColumnFoundOnSource = false;
    if (diff.length > 0 || existingCols.length > 0) {
      if (existingCols.length > 0) {
        this.MissingColumnFoundOnSource = true;

        if (this.config.validate_fileschema) {
          this.InvalidDataSource = true;
          if (diff.length > 0) this.NewColumnFoundOnSource = true;
        }
        if (this.config.flexCheckHasHeaders) {
          //this.config.convert_datatypes_column_list.split(',').forEach((c, i) => {
          //convert_datatypes_column_list.forEach((c, i) => {
          this.config.file_column_mapping.forEach((c, i) => {
            //let foundColumn = fileCurrentColumnNames.indexOf(c.split('=')[0]); //found column in the file
            let foundColumn =
              fileCurrentColumnNames.indexOf(c.fileColumn) < 0 &&
              fileCurrentColumnNames.indexOf(c.dbColumn) < 0;
            if (foundColumn) {
              //if column is not found in file
              //add the column to the headers
              this.columnDatatype.push({
                index: this.columnDatatype.length + 1,
                ColumnKey: false,
                DatatypeName: c.dataType, // c.split('=')[1],
                newColumn: true,
                ColumnName: c.fileColumn, // c.split('=')[0],
                DbColumnName: c.dbColumn, // c.split('=')[0],
                willInclude: false,
                missingColumn: true,
                invalidDataType: false,
                willAddNewColumn: true,
                invalidColumnName: false,
                columnForDedeup: false,
                dateTimeFormatId: 0,
                isDuplicateColumn: false,
                useMultipleSheets: true
              });
            }
          });
        } else {
          //regenerate column headers with col0-colnth
          let configurationColumnNames2 = configurationColumnNames.map(
            (x) => `COL${x.substr(x.lastIndexOf('=') + 1)}`
          );

          configurationColumnNames2.forEach((c, i) => {
            let foundColmn = fileCurrentColumnNames.indexOf(c);
            let columndatatype = '';
            let columnName = c;
            let dataType = 'string';
            let willInclude = false;
            let missingColumn = true;
            if (foundColmn >= 0) {
              //find the correct header name from database using index
              columndatatype = configurationColumnNames.find(
                (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
              );
              columnName = columndatatype.split('=')[0];
              dataType = columndatatype.split('=')[1];
              willInclude = true;
              missingColumn = false;

              let existingColumn = this.columnDatatype.find(
                (x) => x.ColumnName === c
              );
              existingColumn.ColumnName = columnName;
              existingColumn.willInclude = true;
              existingColumn.missingColumn = false;
            } else {
              this.columnDatatype.push({
                index: this.columnDatatype.length + 1,
                ColumnKey: false,
                DatatypeName: dataType,
                newColumn: true,
                ColumnName: columnName,
                DbColumnName: columnName,
                willInclude: willInclude,
                missingColumn: missingColumn,
                invalidDataType: false,
                willAddNewColumn: true,
                invalidColumnName: false,
                columnForDedeup: false,
                dateTimeFormatId: 0,
                isDuplicateColumn: false,
                useMultipleSheets: true
              });
            }
          });
        }
      }


      if (diff.length > 0) {
        this.columnDatatype.forEach((c, i) => {
          //find the database column name and rename the headers using the index
          if (!this.config.flexCheckHasHeaders) {
            let columnName = configurationColumnNames.find(
              (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
            );

            if (columnName) {
              c.ColumnName = columnName.split('=')[0];
            }
          }
          if (
            diff.find(
              (d) => d.split('-')[0] === c.ColumnName && +d.split('-')[1] === i
            )
          ) {
            if (this.config.validate_fileschema === false) {
              c.willInclude = true;
            } else {
              c.willInclude = false;
            }

            //do check again if the column name is already taken
            let currentDiff = diff.find(
              (y) => y.split('-')[0] === c.ColumnName
            );
            let duplicatColumnName = this.columnDatatype.find(
              (x) =>
                x.index !== +currentDiff.split('-')[1] &&
                x.ColumnName === currentDiff.split('-')[0]
            );
            if (duplicatColumnName) {
              if (diff.length > 0) {
                duplicatColumnName.ColumnName = this.generateRandomColumnName(
                  c.ColumnName,
                  diff,
                  i
                );
              }
            }

            this.NewColumnFoundOnSource = true;
            c.newColumn = true;
          }

          //c.ColumnKey = configurationKeyColumns.find(x => x === c.ColumnName) ? true : false;
        });

        this.newColumnVSTrue = false;

        if (
          this.columnDatatype.find((x) => x.missingColumn) &&
          this.config.validate_fileschema
        ) {
          this.newColumnVSTrue = true;
        }
      }

      //return true
    }
    if (!this.config.flexCheckHasHeaders) {
      let configurationColumnNames2 = configurationColumnNames.map(
        (x) => `COL${x.substr(x.lastIndexOf('=') + 1)}`
      );

      this.columnDatatype.forEach((c, i) => {
        let columnName = configurationColumnNames.find(
          (x) => x.substring(x.lastIndexOf('=') + 1) === i.toString()
        );

        if (columnName) {
          c.ColumnName = columnName.split('=')[0];
        }
      });
    }

    //re-assign the data types in the headers
    this.columnDatatype.forEach((c, i) => {
      c.ColumnKey = configurationKeyColumns.find((x) => x === c.ColumnName)
        ? true
        : false;
      c.columnForDedeup = configurationDedupColumns.find(
        (x) => x === c.ColumnName
      )
        ? true
        : false;

      c.DbColumnName = this.config.file_column_mapping.find(
        (x) => x.fileColumn === c.ColumnName || x.dbColumn === c.ColumnName
      )?.dbColumn;
      //if dbColumnName is undefined, means this is a new column and needs to be added in the list
      if (c.DbColumnName === undefined) {
        //lets check first if column name is unique, else, increment

        c.DbColumnName = c.ColumnName;
      }

      //let's check dbColumnNames if they are unique

      if (seen.has(c.DbColumnName)) {
        //find unique name for the duplicate
        let counter = 1;
        let newName = c.ColumnName + '_' + counter;
        while (seen.has(newName)) {
          counter++;
          newName = c.ColumnName + '_' + counter;
        }

        c.DbColumnName = newName;
        seen.add(newName);
      } else {
        seen.add(c.DbColumnName);
      }

      this.config.file_column_mapping.forEach((fcm, i) => {
        if (
          fcm.fileColumn.toLowerCase() === c.ColumnName.toLowerCase() ||
          fcm.dbColumn.toLowerCase() === c.ColumnName.toLowerCase()
        ) {
          if (fcm.formatId > 0) {
            c.DatatypeName = fcm.dataType;
            c.dateTimeFormatId = fcm.formatId;
            this.selectedDateTimeFormat = this.dateTimeNames.find(
              (x) => x.formatId === fcm.formatId
            ).format;
            this.selectedDateTimeFormatId = fcm.formatId;
          } else {
            c.DatatypeName = fcm.dataType;
          }
          //validate only if column is not missing or
          if (c.missingColumn === false || c.newColumn === false) {
            this.validateDataOnDataType(i, fcm.dataType, c.ColumnName);
          }
        }
      });
    });

    //check if there's duplicate dbcolumn name

    //re-assign the dedups

    this.isValidDataType = !(
      this.columnDatatype.filter((x) => x.invalidDataType === true).length > 0
    );

    //return false;

    //apply sorting and dedup
    this.columnDatatype.forEach((c, i) => {
      //if (c.missingColumn === false || c.newColumn === false) {
      if (c.ColumnKey) {
        this.sortAndRemove(c.index, true, true, false);
      } else if (c.columnForDedeup) {
        this.sortAndRemove(c.index, true, false, true);
      }
      //}
    });

    btnNext.disabled = false;
  }


  AddOrUpdateNotificationList(sheetName: string, noOfDuplicates: number = 0, recordsArrayForDisplay: any[] = [],
    invalidDataSource: boolean = false, newColumnFoundOnSource: boolean = false,
    missingColumnFoundOnSource: boolean = false, validate_fileschema: boolean = false) {
    const currentTabIndex = this.FilePreviewNotification.findIndex(
      n => n.sheetName === this.selectedWorkSheetName);

    if (currentTabIndex !== -1) {
      // Update existing notification in place
      this.FilePreviewNotification[currentTabIndex].noOfDuplicates = noOfDuplicates;
      this.FilePreviewNotification[currentTabIndex].totalCountForDisplay = this.totalRowCountEXL - this.noOfDuplicates, // this.recordsArrayForDisplay.length 
        this.FilePreviewNotification[currentTabIndex].InvalidDataSource = invalidDataSource;
      this.FilePreviewNotification[currentTabIndex].NewColumnFoundOnSource = newColumnFoundOnSource;
      this.FilePreviewNotification[currentTabIndex].MissingColumnFoundOnSource = missingColumnFoundOnSource;
      this.FilePreviewNotification[currentTabIndex].validate_fileschema = validate_fileschema;
      // When updating notification after deduplication or key column change:
    } else {
      // Add new notification for this sheet
      const notification: FilePreviewNotification = {
        sheetName: sheetName,
        noOfDuplicates: noOfDuplicates,
        totalCountForDisplay: this.totalRowCountEXL - this.noOfDuplicates, // recordsArrayForDisplay.length 
        InvalidDataSource: invalidDataSource,
        NewColumnFoundOnSource: newColumnFoundOnSource,
        MissingColumnFoundOnSource: missingColumnFoundOnSource,
        validate_fileschema: validate_fileschema
      };
      this.FilePreviewNotification.push(notification);
    }
  }


  sortAndRemove(
    index: number | null,
    isNewKey: boolean | null,
    isColumnKey: boolean,
    isColumnDedup: boolean
  ) {


    if (isNewKey) {
      this.busyService.busy();
    }

    this.remainCountForExcel = 0;
    if (index !== null && isNewKey !== null) {

      if (isColumnKey) {
        if (isNewKey) {
          if (!this.keyColumnNameSelected.includes(this.headersRow[index])) {
            this.keyColumnNameSelected.push(this.headersRow[index]);
          }
        } else {
          const existingIndex = this.keyColumnNameSelected.indexOf(this.headersRow[index]);
          if (existingIndex > -1) {
            this.keyColumnNameSelected.splice(existingIndex, 1);
          }
        }
        // isNewKey
        //   ? this.keyColumnNameSelected.push(this.headersRow[index])
        //   : this.keyColumnNameSelected.splice(
        //     this.keyColumnNameSelected.indexOf(this.headersRow[index]),
        //     1
        //   );
      } else if (isColumnDedup) {
        if (isNewKey) {
          if (
            this.columnsForDedupSelected.findIndex(
              (x) => x === this.headersRow[index]
            ) < 0
          ) {
            this.columnsForDedupSelected.push(this.headersRow[index]);
          }
        } else {
          this.columnsForDedupSelected.splice(
            this.columnsForDedupSelected.indexOf(this.headersRow[index]),
            1
          );
        }
        //isNewKey ? this.columnsForDedupSelected.push(this.headersRow[index]) : this.columnsForDedupSelected.splice(this.columnsForDedupSelected.indexOf(this.headersRow[index]), 1);
      }

      //column index
      if (isColumnKey) {
        if (isNewKey) {
          if (!this.keyColumnIndexSelected.includes(index.toString())) {
            this.keyColumnIndexSelected.push(index.toString());
          }
        } else {
          const existingIndex = this.keyColumnNameSelected.indexOf(index.toString());
          if (existingIndex > -1) {
            this.keyColumnNameSelected.splice(existingIndex, 1);
          }
        }
        // isNewKey
        //   ? this.keyColumnIndexSelected.push(index.toString())
        //   : this.keyColumnIndexSelected.splice(
        //     this.keyColumnIndexSelected.indexOf(index.toString()),
        //     1
        //   );
      } else if (isColumnDedup) {
        if (isNewKey) {
          // Only push if the index isn't already there
          if (!this.columnsForDedupIndexSelected.includes(index.toString())) {
            this.columnsForDedupIndexSelected.push(index.toString());
          }
        } else {
          const existingIndex = this.columnsForDedupIndexSelected.indexOf(index.toString());
          if (existingIndex > -1) {
            this.columnsForDedupIndexSelected.splice(existingIndex, 1);
          }
        }
        // isNewKey
        //   ? this.columnsForDedupIndexSelected.push(index.toString())
        //   : this.columnsForDedupIndexSelected.splice(
        //     this.columnsForDedupIndexSelected.indexOf(index.toString()),
        //     1
        //   );
      }
    }

    //sort then remove then store
    if (this.config.ignore_duplicate_rows === false) {
      this.noOfDuplicates = 0;

      this.recordsArrayForDisplay = [...this.recordsArray];


      //remove all key column and dedup column
      this.columnDatatype.forEach((i) => {
        i.ColumnKey = false;
        i.columnForDedeup = false;
      });
      for (let i = 0; i < this.columnDatatype.length; i++) {
        const elem = document.getElementById(
          'checkKeyColumnForDedup' + (i + 1)
        ) as HTMLElement;
        elem.setAttribute('disabled', 'true');
      }

      return;
    }
    this.unorderd = [...this.recordsArray]; //.slice(0,20);


    if (
      this.keyColumnIndexSelected.length > 0 ||
      this.columnsForDedupIndexSelected.length > 0
    ) {
      let orderedList = this.sortManyRecords(
        this.unorderd,
        this.keyColumnIndexSelected
      ).map((r: any, i: number) => ({ ...r, __origIndex: i }));;

      //let orderedList2 = this.sortManyRecords(orderedList, listOfKeyIndex.push(index))
      //this.recordsArrayForDisplay = this.removeDups(this.unorderd, this.keysSelected);

      this.recordsArrayForDisplay = this.removeDups(
        orderedList,
        this.keyColumnNameSelected
      );

      if (this.columnDatatype.find((x) => x.columnForDedeup)) {
        var dedupColumn = this.columnDatatype.find((x) => x.columnForDedeup).ColumnName;
        if (this.config.flexCheckHasHeaders) {
          this.recordsArrayForDisplay = this.partitionBy(
            orderedList,
            this.keyColumnNameSelected,
            dedupColumn //this.columnDatatype.find((x) => x.columnForDedeup).ColumnName
          );
        } else {
          this.recordsArrayForDisplay = this.partitionBy(
            orderedList,
            this.keyColumnIndexSelected,
            dedupColumn //this.columnDatatype.find((x) => x.columnForDedeup).ColumnName
          );
        }

        //this.recordsArrayForDisplay = this.sortManyRecords(this.recordsArrayForDisplay, this.keyColumnIndexSelected);
      }

      this.noOfDuplicates =
        this.recordsArray.length - this.recordsArrayForDisplay.length;
    }


    // this.columnDatatype.forEach(c => {
    //   if (!c.willInclude) {
    //     this.toggleExcludedClass(c.index);
    //     this.toggleExcludedClass(c.index);
    //   }
    // })


    this.busyService.idle();

    //this.recordsArrayForDisplay = this.recordsArray;
  }

  createKeyForPartition_old(item, keys) {
    return keys.map((key) => item[key]).join('|');
  }

  createKeyForPartition(item: Record<string, any>, keys: (string | number)[]) {

    if (
      this.fileType === FileType.CommaSeparatedValues ||
      this.fileType === FileType.TextFiles
    ) {
      return keys.map((key) => item[key]).join('|');
    }

    return keys.map((k) => {
      // 1) Direct hit
      if (k in item) return String(item[k as any]);

      // 2) Case-insensitive name match
      const keyUpper = String(k).toUpperCase();
      const hitByCase = Object.keys(item).find(p => p.toUpperCase() === keyUpper);
      if (hitByCase) return String(item[hitByCase]);

      // 3) If numeric index is passed, try mapping index -> ColumnName and use that
      const idx = Number(k);
      if (!isNaN(idx) && Array.isArray(this.columnDatatype)) {
        const col = this.columnDatatype.find((c: any) => c.index === idx);
        if (col?.ColumnName) {
          const n = col.ColumnName;
          if (n in item) return String(item[n]);
          const nUpper = String(n).toUpperCase();
          const hitUpper = Object.keys(item).find(p => p.toUpperCase() === nUpper);
          if (hitUpper) return String(item[hitUpper]);
        }
      }

      // 4) Last resort: empty
      console.warn('[createKeyForPartition] key not found', { k, itemKeys: Object.keys(item) });
      return '';
    }).join('|');
  }
  // Minimal helper: normalize any value to a string for lexicographic compare.
  // If it's a Date and we must treat it as string, format as "M/D/YYYY" to match your data.
  private toStringValue(v: any): string {
    if (v instanceof Date) {
      const m = v.getMonth() + 1;
      const d = v.getDate();
      const y = v.getFullYear();
      return `${m}/${d}/${y}`;
    }
    return String(v ?? '');
  }

  // Optional: careful date parse that works for either Date object, ISO, or M/D/YYYY
  private toDateValue(v: any): Date | null {
    if (v instanceof Date && !isNaN(v.getTime())) return v;
    if (v == null) return null;
    const s = String(v).trim();
    // Try M/D/YYYY quickly
    const mdy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (mdy) {
      const m = +mdy[1], d = +mdy[2], y = +mdy[3];
      const dt = new Date(y, m - 1, d);
      return (dt.getFullYear() === y && dt.getMonth() === m - 1 && dt.getDate() === d) ? dt : null;
    }
    // Fallback for ISO/RFC formats (engine-dependent but OK as fallback)
    const dt = new Date(s);
    return isNaN(dt.getTime()) ? null : dt;
  }


  private dlog(...args: any[]) {
    //if (this.DEBUG_SORT) 
    if (false)
      console.log('[sort]', ...args);
  }



  sortThePartitioned(
    a: any[],
    b: any[],
    keys: { key: string; order: string }[],

  ): number {

    const tieBreakerKey = '__origIndex'

    // Resolve equality on a key: for asc → keep `a` before `b`; for desc → put `b` before `a`.
    const resolveTie = (order: string): number => {
      // If you simply want the policy without needing the index, you can do:
      // return order === 'asc' ? -1 : 1;
      // BUT better to make it stable with a tiebreaker:
      const ia = a[tieBreakerKey] ?? 0;
      const ib = b[tieBreakerKey] ?? 0;

      if (ia === ib) {
        // Fall back to policy if identical (rare if index is unique)
        //return order === 'asc' ? -1 : 1;
        this.dlog('= TIE (equal indices)', { ia, ib, order, a, b });
        return 0;
      }
      // Asc: keep a before b -> if a was earlier (smaller index), return -1
      // Desc: put b before a -> if a was earlier (smaller index), return +1 so `a` goes after `b`

      const r = (order === 'asc') ? (ia < ib ? -1 : 1) : (ia < ib ? 1 : -1);
      this.dlog('= TIE → by __origIndex', { ia, ib, order, result: r });
      return r;

    };

    const cmp = (va: any, vb: any, order: string): number => {
      if (va === vb) return 0;
      if (order === 'asc') return va < vb ? -1 : 1;
      return va < vb ? 1 : -1;
    };

    this.dlog('--- COMPARISON START ---', {
      aIdx: a[tieBreakerKey], bIdx: b[tieBreakerKey],
      keys
    });


    for (const { key, order = 'asc' } of keys) {
      // if(order === 'desc'){

      //   if (a[key] > b[key]) return -1;
      // } else {
      //   if (a[key] < b[key]) return -1;
      // }
      //check if data type is date/datetime

      if (key) {
        var column: ColumnNameDatatypeName = null;
        if (this.config.flexCheckHasHeaders) {
          if (this.fileType === FileType.MSExcel1 || this.fileType === FileType.MSExcel2 ||
            this.fileType === FileType.MSExcel3
          ) {
            column = this.columnDatatype.find(
              (x) => x.index === +key
            );
          } else {
            column = this.columnDatatype.find(
              (x) => x.ColumnName === key.toUpperCase()
            );
          }
        } else {
          column = this.columnDatatype.find((x) => x.index === +key);
        }
        if (column) {

          const dtype = String(column.DatatypeName || '').toLowerCase();

          if (dtype.includes('date')) {
            const ta = +new Date(a[key]);
            const tb = +new Date(b[key]);
            if (ta < tb) return order === 'asc' ? -1 : 1;
            if (ta > tb) return order === 'asc' ? 1 : -1;
            // equal -> apply tie behaviour
            const r = cmp(ta, tb, order);
            if (r !== 0) return r;
            continue;
          }

          else if (dtype === 'int' || dtype.includes('int')) {
            const na = +a[key], nb = +b[key];
            if (na < nb) return order === 'asc' ? -1 : 1;
            if (na > nb) return order === 'asc' ? 1 : -1;
            // equal -> apply tie behaviour
            return resolveTie(order);
          } else {
            //string and other
            const sa = this.toStringValue(a[key]);
            const sb = this.toStringValue(b[key]);
            // if (a[key] < b[key]) return order === 'asc' ? -1 : 1;
            // if (a[key] > b[key]) return order === 'asc' ? 1 : -1;
            // equal -> apply tie behaviour
            const r = order === 'asc' ? sa.localeCompare(sb) : -sa.localeCompare(sb);
            this.dlog('key:string', { key, order, sa, sb, result: r });
            if (r !== 0) return r;
            continue;

          }
        } else {

          // Unknown column type: generic compare
          if (a[key] < b[key]) return order === 'asc' ? -1 : 1;
          if (a[key] > b[key]) return order === 'asc' ? 1 : -1;
          // EQUAL → apply tie behavior
          return resolveTie(order);

          // if (a[key] < b[key]) return order === 'asc' ? -1 : 1;
          // if (a[key] > b[key]) return order === 'asc' ? 1 : -1;

        }
      }
    }
    const primaryOrder: string = keys[0]?.order || 'asc';
    return resolveTie(primaryOrder);

  }

  partitionBy(data1: any[], key: string[], orderKey: string) {
    //get all the keyColumns
    // const data = [
    //   { category: "A", value: 10 },
    //   { category: "B", value: 20 },
    //   { category: "A", value: 15 },
    //   { category: "B", value: 25 },
    // ];
    //const result = {};

    const groups = data1.reduce((a, i) => {
      const key_ = this.createKeyForPartition(i, key); // i[`${key.toLowerCase()}`];
      if (!a[key_]) {
        a[key_] = [];
      }
      a[key_].push(i);
      return a;
    }, {});

    ////console.log(groups);

    //sort each group by orderkey and get the latest entry
    //const sortKeys = [{key: 'date', order: this.config.order_by_column_list_name_sort_dir}];
    var sortKeys: { key: string, order: 'asc' | 'desc' }[] = []
    //var sortKeys = [];
    if (
      this.fileType === FileType.MSExcel1 ||
      this.fileType === FileType.MSExcel2 ||
      this.fileType === FileType.MSExcel3
    ) {
      if (this.config.flexCheckHasHeaders) {
        this.columnsForDedupIndexSelected.forEach((x) => {
          if (x) {
            sortKeys.push({
              key: x,
              order: this.config.order_by_column_list_name_sort_dir as 'asc' | 'desc',
            });
          }
        });
      }
    } else {
      if (this.config.flexCheckHasHeaders) {
        this.columnsForDedupSelected.forEach((x) => {
          if (x) {
            sortKeys.push({
              key: x,
              order: this.config.order_by_column_list_name_sort_dir as 'asc' | 'desc',
            });
          }
        });
      } else {
        this.columnsForDedupIndexSelected.forEach((x) => {
          if (x) {
            sortKeys.push({
              key: x,
              order: this.config.order_by_column_list_name_sort_dir as 'asc' | 'desc',
            });
          }
        });
      }
    }

    const winners = (Object.values(groups) as any[][]).map((group: any[]) => {
      //let temp = group.sort((a, b) => this.sortThePartitioned(a, b, sortKeys));
      return group.sort((a, b) => this.sortThePartitioned(a, b, sortKeys))[0];
    });

    // **NEW**: order the winners by any key(s) you like
    // Example 1: order by DATE_OF_BIRTH as STRING DESC
    //this.sortWinnersByKeys(winners, [{ key: 'DATE_OF_BIRTH', order: 'desc' }]);

    // Example 2: multi-key: DATE_OF_BIRTH (desc), then DATETIME (desc)
    this.sortWinnersByKeys(winners, sortKeys);

    return winners;

  }


  private getDtypeForKey(key: string): 'date' | 'number' | 'string' {
    // Look up from your columnDatatype. Adjust the lookup to your shapes.
    let col: ColumnNameDatatypeName = null;
    if (this.config?.flexCheckHasHeaders) {
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        // If your keys are indices in these modes, use index; if names, switch to ColumnName
        col = this.columnDatatype?.find((x: any) => String(x.ColumnName || '').toUpperCase() === String(key).toUpperCase());
      } else {
        col = this.columnDatatype?.find((x: any) => String(x.ColumnName || '').toUpperCase() === String(key).toUpperCase());
      }
    } else {
      // If keys are indices when no headers
      col = this.columnDatatype?.find((x: any) => x.index === +key);
    }
    const dtype = String(col?.DatatypeName || '').toLowerCase();

    if (dtype.includes('date')) return 'date';
    if (dtype.includes('int') || dtype.includes('number') || dtype.includes('decimal') || dtype.includes('float')) return 'number';
    return 'string'; // default per your rule
  }

  /**
 * Sort winners across groups by arbitrary keys.
 * @param winners The one-per-group array you already produced.
 * @param sortKeys Array like: [{ key: 'DATE_OF_BIRTH', order: 'desc' }, { key: 'DATETIME', order: 'asc' }]
 *                 Keys can be any columns; order is 'asc' | 'desc'.
 */
  private sortWinnersByKeys(
    winners: Record<string, any>[],
    sortKeys: { key: string; order: 'asc' | 'desc' }[]
  ) {
    const cmpNum = (a: number, b: number, order: 'asc' | 'desc') =>
      order === 'asc' ? (a === b ? 0 : (a < b ? -1 : 1)) : (a === b ? 0 : (a < b ? 1 : -1));

    winners.sort((a, b) => {
      for (const { key, order } of sortKeys) {
        if (!key) continue;

        const dtype = this.getDtypeForKey(key);

        if (dtype === 'date') {
          const da = this.toDateValue(a[key]);
          const db = this.toDateValue(b[key]);
          const ta = da ? da.getTime() : Number.NEGATIVE_INFINITY;
          const tb = db ? db.getTime() : Number.NEGATIVE_INFINITY;
          const r = cmpNum(ta, tb, order);
          if (r !== 0) return r;
          continue;
        }

        if (dtype === 'number') {
          const na = (a[key] == null || a[key] === '') ? Number.NEGATIVE_INFINITY : +a[key];
          const nb = (b[key] == null || b[key] === '') ? Number.NEGATIVE_INFINITY : +b[key];
          const r = cmpNum(na, nb, order);
          if (r !== 0) return r;
          continue;
        }

        // STRING (default)
        const sa = this.toStringValue(a[key]);
        const sb = this.toStringValue(b[key]);
        const r = sa.localeCompare(sb);
        if (r !== 0) return order === 'asc' ? r : -r;
        // else equal → move to next key
      }

      // All keys equal → keep original relative order (stable enough in modern engines)
      const ia = a['__origIndex'] ?? 0;
      const ib = b['__origIndex'] ?? 0;
      return ia - ib;
    });
  }

  removeDups(data: any[], columns: string[]): any[] {

    const seenRows = new Set<string>();
    const uniqueData = [];

    var key = '';
    //no longer needed
    //sortmanyrecords already sorted by desc or asc
    // if (this.config.order_by_column_list_name_sort_dir === 'desc') {
    //   data = data.reverse();
    // }

    for (const row of data) {
      //why did i slice .slice(0, this.columnDatatype.length)
      key = '';
      if (this.config.flexCheckHasHeaders) {
        if (
          this.fileType === FileType.MSExcel1 ||
          this.fileType === FileType.MSExcel2 ||
          this.fileType === FileType.MSExcel3
        ) {
          key = this.keyColumnIndexSelected
            .map((index) => row[index])
            .join('|');
        } else {
          key = columns.map((col) => row[col]).join('|');
        }

        if (!seenRows.has(key.replace('\r', ''))) {
          seenRows.add(key);
          uniqueData.push(row);
        }
      } else {
        // this.keyColumnIndexSelected.forEach(i =>{
        //   key = row[i];

        //   if (!seenRows.has(key.replace('\r', ''))) {
        //     seenRows.add(key);
        //     uniqueData.push(row);
        //   }
        // })

        key = this.keyColumnIndexSelected
          .map((index) => row[index]?.trim())
          .join('|');
        //key = columns.map(col => row[col]).join('|');

        if (!seenRows.has(key.trim().replace('\r', ''))) {
          seenRows.add(key);
          uniqueData.push(row);
        }
      }

      // if (!seenRows.has(key.replace('\r', ''))) {
      //   seenRows.add(key);
      //   uniqueData.push(row);
      // }
    }

    return uniqueData;
  }

  sortRecords(unorderd: any[], index: number): any[] {
    unorderd = unorderd.sort((a, b) => {
      // > desc, < asc
      if (this.config.order_by_column_list_name_sort_dir === 'desc') {
        if (a[this.headersRow[index]] > b[this.headersRow[index]]) {
          return -1;
        } //desc
      } else {
        if (a[this.headersRow[index]] < b[this.headersRow[index]]) {
          return -1;
        } //asc
      }
      return 0;
    });

    return unorderd;
  }

  sortManyRecords(unorderd: any[], index: string[]): any[] {
    index.forEach((i) => {
      unorderd = unorderd.sort((a, b) => {
        const isDesc = this.config.order_by_column_list_name_sort_dir === 'desc';
        const currentKey = this.config.flexCheckHasHeaders && this.fileType !== FileType.MSExcel1 && this.fileType !== FileType.MSExcel2 && this.fileType !== FileType.MSExcel3
          ? this.headersRow[i] : i;
        //get raw values;
        let valA = a[currentKey];
        let valB = b[currentKey];

        // --- NUMERIC SORT ---
        if (this.columnDatatype[+i]?.DatatypeName === 'int') {
          const numA = parseFloat(valA) || 0;
          const numB = parseFloat(valB) || 0;

          if (numA === numB) return 0;
          // Descending: Higher numbers first (numB - numA)
          // Ascending: Lower numbers first (numA - numB)
          return isDesc ? (numB - numA) : (numA - numB);
        }

        // Handle String Logic: Safe replace (only if it's a string)
        const cleanA = typeof valA === 'string' ? valA.replace('\r', '') : (valA ?? '');
        const cleanB = typeof valB === 'string' ? valB.replace('\r', '') : (valB ?? '');

        // if (cleanA === cleanB) return 0;
        // if (isDesc) {
        //   return cleanA > cleanB ? -1 : 1;
        // } else {
        //   return cleanA < cleanB ? -1 : 1;
        // }

        // --- STRING SORT (case-insensitive, accent-insensitive) ---
        const cmp = cleanA.toString().localeCompare(
          cleanB.toString(),
          undefined,
          { sensitivity: 'base' }   // ignores case + accents
        );

        return isDesc ? -cmp : cmp;

        {
          // // > desc, < asc
          // if (this.config.flexCheckHasHeaders) {
          //   if (this.config.order_by_column_list_name_sort_dir === 'desc') {
          //     if (this.columnDatatype[+index].DatatypeName === 'int') {
          //       a[i] = +a[i];
          //       b[i] = +b[i];
          //     }
          //     if (
          //       this.fileType === FileType.MSExcel1 ||
          //       this.fileType === FileType.MSExcel2 ||
          //       this.fileType === FileType.MSExcel3
          //     ) {

          //       if (a[i]?.replace('\r', '') > b[i]?.replace('\r', '')) {
          //         return -1;
          //       } //desc
          //     } else {
          //       if (
          //         a[this.headersRow[i]]?.replace('\r', '') >
          //         b[this.headersRow[i]]?.replace('\r', '')
          //       ) {
          //         //if (a[i].replace('\r', '') > b[i].replace('\r', '')) {
          //         return -1;
          //       } //desc
          //     }
          //   } else {
          //     if (
          //       this.fileType === FileType.MSExcel1 ||
          //       this.fileType === FileType.MSExcel2 ||
          //       this.fileType === FileType.MSExcel3
          //     ) {
          //       if (a[i]?.replace('\r', '') > b[i]?.replace('\r', '')) {
          //         return -1;
          //       } //desc
          //     } else {
          //       if (
          //         a[this.headersRow[i]]?.replace('\r', '') <
          //         b[this.headersRow[i]]?.replace('\r', '')
          //       ) {
          //         return -1;
          //       } //asc
          //     }
          //   }
          // } else {
          //   if (this.config.order_by_column_list_name_sort_dir === 'desc') {
          //     if (a[i]?.replace('\r', '') > b[i]?.replace('\r', '')) {
          //       return -1;
          //     } //desc
          //   } else {
          //     if (a[i] && b[i]) {
          //       if (a[i]?.replace('\r', '') < b[i]?.replace('\r', '')) {
          //         return -1;
          //       } //asc
          //     }
          //   }
          // }
          // return 0;
        }
      });
    });

    return unorderd;
  }







  stopPropagation(event: any) {
    event.stopPropagation();
  }




  processNames$ = new Observable<ProcessNames[]>();
  private processNameSearchTerm$ = new BehaviorSubject<string>('');

  processNameSearchFn(term: string, item: any) {
    term = term.toLowerCase();
    //return this.processNames.filter(x=> x.processNames.toLowerCase().includes(term)) ||this.processNames.filter(x=> x.description.toLowerCase().includes(term))

    return (
      item.processNames.toLowerCase().includes(term) ||
      item.description.toLowerCase().includes(term)
    );
  }

  onSearchProcessName(term: string) {
    this.processNameSearchTerm$.next(term);
  }

  fillMultisheetDetails() {

  }

  onSheetCheckboxChange(event: Event | { target: { checked: boolean } }, wk: any): void {
    // Normalize `checked` regardless of input type
    const checked =
      'target' in event && 'checked' in event.target
        ? (event.target as any).checked
        : false;

    if (!checked) {

      const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.title = 'Ignore Sheet';
      modalRef.componentInstance.message = `Do you want to ignore the sheet "${wk.sheetName}"?`;
      modalRef.result.then((result) => {
        if (result === true) {
          // this.selectedOption = 'Yes';
          // this.updateEquipmentStatus();
          // this.proceedNextStep = true;
          // this.onNext();

          wk.selected = true;
          const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
          if (sheet) {
            sheet.selected = false;
            sheet.ignoreSheet = true;
            this.multiSheetConfigs[wk.sheetName].ignoreSheet = true;
            this.multiSheetConfigs[wk.sheetName].frmSubmitted = this.isSelectedExistingProcess == true ? true : false;
            this.displayRecordByWorkSheetName(wk.sheetName);
          }

        } else {
          //this.selectedOption = 'No';
          wk.selected = true;
        }
      }, (reason) => {
        //this.selectedOption = 'No';
        const checkbox = event.target as HTMLInputElement;
        checkbox.checked = true;
        wk.selected = true;
        const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
        if (sheet) {
          sheet.ignoreSheet = false;
          this.multiSheetConfigs[wk.sheetName].ignoreSheet = false;

          this.displayRecordByWorkSheetName(wk.sheetName);
        }
        console.log('Modal dismissed with reason:', reason);
      });
      // const confirmed = confirm(`Do you want to ignore the sheet "${wk.sheetName}"?`);
      // if (confirmed) {
      //   wk.selected = true;
      //   const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
      //   if (sheet) {
      //     sheet.selected = false;
      //     sheet.ignoreSheet = true;
      //     this.multiSheetConfigs[wk.sheetName].ignoreSheet = true;
      //     this.multiSheetConfigs[wk.sheetName].frmSubmitted = false;
      //     this.displayRecordByWorkSheetName(wk.sheetName);
      //   }
      // } else {
      //   wk.selected = false;
      // }
    } else {
      wk.selected = false;
      const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
      if (sheet) {
        sheet.ignoreSheet = false;
        this.multiSheetConfigs[wk.sheetName].ignoreSheet = false;
        this.displayRecordByWorkSheetName(wk.sheetName);
      }
    }

  }
  noOfActiveSheets: number = 0;

  //   onSheetCheckboxChange(event: Event | event: { target: { checked: boolean } }, wk: any): void {

  //   const checkbox = event.target as HTMLInputElement;

  //   if (!checkbox.checked) {
  //     const confirmed = confirm(`Do you want to ignore the sheet "${wk.sheetName}"?`);
  //     if (confirmed) {
  //       wk.selected = true;
  //       const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
  //       if (sheet) {
  //         sheet.selected = false;
  //         sheet.ignoreSheet = true;
  //         this.multiSheetConfigs[wk.sheetName].ignoreSheet = true; 
  //         this.multiSheetConfigs[wk.sheetName].frmSubmitted = false;     
  //         this.displayRecordByWorkSheetName(wk.sheetName);
  //       }
  //       // You can add logic here to actually ignore the sheet
  //     } else {
  //       checkbox.checked = false;
  //       wk.selected = false;
  //     }
  //   } else {
  //     wk.selected = false;
  //     const sheet = this.wksheets.find(x => x.sheetName === wk.sheetName);
  //       if (sheet) {
  //         sheet.ignoreSheet = false;
  //        this.multiSheetConfigs[wk.sheetName].ignoreSheet = false;
  //         this.displayRecordByWorkSheetName(wk.sheetName);
  //       }
  //   }
  // }




  onSheetConfigChanged(updatedConfig: AdditionalSettings) {
    // Save the updated config for the current sheet
    this.multiSheetConfigs[this.selectedWorkSheetName] = updatedConfig;

    // Now re-parse/reload the data for the active sheet only
    this.parseSheet(this.selectedWorkSheetName, updatedConfig);
  }

  getSheetNameforNotification(sheetName: string) {
    return this.FilePreviewNotification.find(fp => fp.sheetName === sheetName);
  }
  checkIgnoreSheet(sheetName: string): boolean {
    return !!this.wksheets.find(s => s.sheetName === sheetName && s.ignoreSheet);
  }




  parseSheet(sheetName: string, config: AdditionalSettings) {
    const sheet = this.wksheets.find(x => x.sheetName === sheetName);
    if (!sheet) return;

    // Update only the data for the active sheet
    this.dataxlsx = sheet.workBook;
    this.recordsArray = sheet.workBook;
    this.recordsArrayForDisplay = sheet.workBook;


    // Call your parsing logic here, passing the config (including spanish_to_english)
    // For example:
    // this.myParser.parseExcel(
    //   this.file, // or the file for this sheet
    //   config.flexCheckHasHeaders,
    //   config.skip_header_rows,
    //   config.flexCheckSkipEmptyLines,
    //   //config.spanish_to_english // <-- pass this flag
    // ).subscribe(result => {
    //   // Update your data for this sheet
    //   this.recordsArray = result.data;
    //   // ...etc
    // });
  }

  compareWithFn(item: any, value: any): boolean {
    return item?.flpConfigurationId === value;
  }

  columnDataTypeHasChanges(tempColumnDataType: ColumnNameDatatypeName[],
    existingColumnDataType: string[],
  ): boolean {
    let isSameOrder: boolean = true;


    if (this.config.spanish_to_english) {
      this.convertToEnglishCharacters(tempColumnDataType);
    }
    if (this.config.roman_numerals_only) {
      this.displayOnlyRomanNumerals(tempColumnDataType);
    }

    this.helperUtil.findDuplicateColumnAndGenerateNew(tempColumnDataType);
    const columnNameFromColumnDataType = existingColumnDataType;
    isSameOrder = tempColumnDataType.map(col => col.ColumnName).every((value, index) => value === columnNameFromColumnDataType[index]);

    return isSameOrder;


  }

  onApplyValidateSchema() {
    this.childComponent.processConfigurationForm.get('is_validate_fileschema_with_target_table').setValue(true);
    this.childComponent?.handleFormChange('validate_fileschema', true);
  }

  hasColumnExcluded() {

    if (this.fileType === FileType.CommaSeparatedValues || this.fileType === FileType.TextFiles) {
      var hasExclusion = this.columnDatatype.find(x => x.willInclude === false);
      var validateSchemaIsChecked = this.config.validate_fileschema;
      //return hasExclusion && !validateSchemaIsChecked;
    } else {
      var hasExclusion = this.columnDatatypePerSheet[this.selectedWorkSheetName].find(x => x.willInclude === false);
      var validateSchemaIsChecked = this.multiSheetConfigs[this.selectedWorkSheetName].validate_fileschema;
    }

    return hasExclusion && !validateSchemaIsChecked;
  }

  // for landing layer
  selectedFiles: SelectedFiles[] = [];
  confirmIndex: number | null = null;
  totalFileSizeInMb: number = 0;
  displayFileNames(event: any) {
    const newFiles = Array.from(event.target.files) as File[];

    //create a unique key so duplicates won't be added
    const existingKeys = new Set(
      this.selectedFiles.map(f => `${f.File.name}_${f.File.size}`)
    );

    const filesToAdd = newFiles.filter(
      f => !existingKeys.has(`${f.name}_${f.size}`))
      .map(f => ({
        File: f,
        status: 'Valid'
      }));

    //clear previous selection and load new ones
    this.selectedFiles = [...this.selectedFiles, ...filesToAdd];
    if (this.selectedFiles) {


      //update files statuses
      this.helperUtil.updateSelectedFileStatuses(this.selectedFiles, this.regexValues, this.selectedExtensions);

      //initialize 
      if (!this.config?.flpConfigurationId) {
        this.isNewProcess = true;
        this.isFileUploaded = true;
        if (!this.config?.frmSubmitted) {
          this.config = this.resetAdditionalSettings();
        }
      }

      //validation flags

      const maxFileCount = this.landingLayerConfiguration.noOfAllowedFilesToUpload;
      const maxTotalSizeMb = this.landingLayerConfiguration.totalFileSize;

      const exceedsFileCount = this.selectedFiles.length > maxFileCount;


      // Calculate total file size (MB)
      this.totalFileSizeInMb = 
          this.selectedFiles.reduce((sum, f) => sum + f.File.size, 0) /
          (1024 * 1024)        
      ;

      const exceedsTotalSize = this.totalFileSizeInMb > maxTotalSizeMb;



      // Show error messages
      if (exceedsFileCount && exceedsTotalSize) {
        this.toastr.error(
          `Total file count exceeds the limit of ${maxFileCount} files.
          Total file size also exceeds the limit of ${maxTotalSizeMb} MB.
          Please remove some files and try again.`
        );
      } else if (exceedsFileCount) {
        this.toastr.error(
          `Total file count exceeds the limit of ${maxFileCount} files.
          Please remove some files and try again.`
        );
      } else if (exceedsTotalSize) {
        this.toastr.error(
          `Total file size exceeds the limit of ${maxTotalSizeMb} MB.
          Please remove some files and try again.`
        );
      }


    }
    //reset input so the same file can be selected again
    event.target.value = '';
  }

  askRemove(i: number) {
    this.confirmIndex = i;
  }

  cancelRemove() {
    this.confirmIndex = null;
  }
  removeFile(index: number) {
    this.selectedFiles = this.selectedFiles.filter((_, i) => i !== index);
    this.totalFileSizeInMb = parseFloat((this.selectedFiles.reduce((sum, f) => sum + f.File.size, 0) / (1024 * 1024)).toFixed(1));
    this.cancelRemove();
  }
  selectedExtensions: string[] = [];
  regexValues: RegexItem[] = [];
  updateselectedFiles(selectedExtensions: string[]) {
    this.selectedExtensions = selectedExtensions;
    // this.selectedFiles.forEach(f => {
    //   f.status = selectedExtensions.includes(f.file.name.split('.').pop()?.toLowerCase() || '') ? 'valid' : 'invalid';
    // });
    this.helperUtil.updateSelectedFileStatuses(this.selectedFiles, this.regexValues, this.selectedExtensions);
  }

  validateFileNamesWithRegex(regexValues: RegexItem[]) {
    this.regexValues = regexValues;
    this.helperUtil.updateSelectedFileStatuses(this.selectedFiles, this.regexValues, this.selectedExtensions);
    // const { matches, invalidRegexes } =
    //   this.helperUtil.validateFileNamesWithRegex(this.selectedFiles, regexValues);


    // for (const m of matches) {
    //   const sf = this.selectedFiles.find(f => f.file === m.file);
    //   if (!sf) continue; // defensive
    //   sf.status = m.matchedIndex !== null ? 'valid' : 'invalid';
    // }

  }

  keepOrder = (a: any, b: any) => 0;

  greyTheColumnForExcel(index: number) {

    var colIndex = this.columnDatatype[index]?.willInclude;
    return !colIndex;
  }
}

