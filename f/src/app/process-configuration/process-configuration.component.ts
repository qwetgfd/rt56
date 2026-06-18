declare var bootstrap: any;
import { Component, ElementRef, EventEmitter, Input, OnInit, OnChanges, Output, SimpleChanges, ViewChild, ChangeDetectionStrategy, IterableDiffers, DoCheck, IterableDiffer } from '@angular/core';
import { AdditionalSettings, RegexItem } from '../core/models/additionalSettings';
import { AbstractControl, AsyncValidatorFn, FormArray, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { map, finalize, Observable, Subscription, debounceTime, switchMap, take, distinctUntilChanged, BehaviorSubject, Subject, EMPTY, tap, of, filter, firstValueFrom } from 'rxjs';
import { ConfigurationService } from '../core/services/configuration.service';
import { FileType, ModalMessages, RuleTypeNames, SubRuleTypes, ToastrMessages } from '../shared/enum';
import { ColumnNameDatatypeName } from '../core/models/columnNameDatatypeName';
import { APIResponse } from '../core/models/apiResponse';
import { DIClientNames } from '../core/models/DIClientNames';
import { DIRegions } from '../core/models/DIRegions';
import { DISubRegions } from '../core/models/DISubRegions';
import { DIDatabaseNames } from '../core/models/DIDatabaseNames';
import { ToastrService } from 'ngx-toastr';
import { DdlData, SubRegion } from '../core/models/dsRegion';
import { DataSliceService } from '../core/services/dataslice.service';
import { ProcessConfigService } from '../core/services/process-config.service';
import { DataSourceType } from '../shared/enum';
import { StorageAccount } from '../core/models/fileProcessConfig';
import { GraphApiTokenService } from '../core/services/graph-api-token.service';
import { CampaignNames, SecurityGroup } from '../core/models/userDetails';
import { arraysEqual, cleanBlobSourceLocation, formatWithOrAnd, Helper, isNullUndefinedEmptyArrays, noWhitespaceValidator, requiredArrayValidatorTemp, ValidationHelper } from '../core/utils/helper';
import { ConditionalOperator, LogicalOperator, Patterns, RuleTypes, RuleType, SubRule, ExcelRule, RuleSetNames, SPNames, DataAssistsRequest } from '../core/models/DataInsider';
import { DataInsiderService } from '../core/services/data-insider.service';
import { cleanColumnName } from '../core/services/di-parser.service';
import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ModalService } from '../core/services/confirm-modal.service';
import { FileNameExtension, Prefixes } from '../core/models/LandingLayer/landingLayer';
import { DateTimeFormats } from '../core/models/datatypeNames';
import { FabService } from '../core/services/FAB/fab-service.service';
import { ModalServiceService } from '../core/services/modal-service.service';
import { RegexBuilderComponent } from '../regex-builder/regex-builder.component';

type MsgType = 'primary' | 'success' | 'warning' | 'danger' | 'secondary';

@Component({
  selector: 'app-process-configuration',
  templateUrl: './process-configuration.component.html',
  styleUrl: './process-configuration.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: false
})
export class ProcessConfigurationComponent implements OnInit, OnChanges, DoCheck {
  //@Input() formValues: Observable<AdditionalSettings> | undefined;

  @ViewChild('txtareaAiPrompt', { static: false }) txtareaAiPromptRef?: ElementRef<HTMLTextAreaElement>;
  @Input() dateTimeNames: DateTimeFormats[] = [];
  @Input() formValues: AdditionalSettings;
  @Input() fileType: string;
  @Input() rowCount: number;
  @Input() file: any;
  @Input() columnDatatype: ColumnNameDatatypeName[];
  @Input() columnNameHasChanges!: () => boolean;

  @Input() keyColumns: Observable<string>;
  @Input() dedupColumns: Observable<string>;
  @Input() showEnglishConversionOnProcess: boolean;
  @Input() showRomanNumeralsConversionOnProcess: boolean;
  @Input() dataSource: DataSourceType;
  @Input() currentWorkSheetName: string;
  @Input() clientInfoForm: FormGroup;
  // @Input() ModifySettingValidationList :ModifySettings[] =[];
  private keyColumnsSubscription: Subscription;
  private dedupColumnsSubscription: Subscription;
  @Input() previewToParent2!: (data: AdditionalSettings) => Promise<boolean>;
  @Output() updateselectedFiles = new EventEmitter<string[]>()
  @Output() validateFileNamesWithRegex = new EventEmitter<RegexItem[]>()
  @Output() useRomanNumeralsOnlyEvent = new EventEmitter<boolean>();
  @Output() updateConfigOnly = new EventEmitter<[AdditionalSettings, ColumnNameDatatypeName[], ExcelRule[], RuleSetNames[], FormGroup | null]>();
  @Output() convertSpanishToEnglishChar = new EventEmitter<AdditionalSettings>();
  @Output() ignoreYNSortBy = new EventEmitter<{ ignoreYN: boolean, sortBy: string }>();
  @Output() closeWindow = new EventEmitter();
  @Output() processConfigurationFormHasError = new EventEmitter<boolean>();
  // @Output() ModifySettingChange = new EventEmitter<ModifySettings>();
  private formValuesEventSubscription: Subscription | undefined;

  @ViewChild('delimiter') delimiterInput: ElementRef | undefined;

  config: AdditionalSettings | undefined;
  databaseNames: DIDatabaseNames[] = [];
  activeTab = "client-tab";
  processNamePattern = "^[\\w\-\\s]+$"; //alphanumeric but does not start with numbers
  descriptionPattern = "^[a-zA-Z0-9\\s_.,\\-]*$";
  alphaNumericPattern = "^[a-zA-Z0-9]+$";
  onlyNumbersPattern = "^[0-9]+$";
  // @Input() processName: string | undefined | null= '';
  // @Input() description: string | undefined | null = '';



  //  processConfigurationForm: FormGroup;
  @Input() processConfigurationForm: FormGroup;
  DIRegions: DIRegions[] = [];
  DISubRegions: DISubRegions[] = [];
  DIClientnames: DIClientNames[] = [];

  databaseNameId: number = 0;
  delimiter: string = ',';
  disableDelimiter = false;
  dsRegion: DdlData[] = [];
  dsREgionTemp: DdlData[] = [];
  dsSubRegion: SubRegion[] = [];
  dsClient: DdlData[] = [];
  isRegionLoading: boolean = false;
  isSubRegionLoading: boolean = false;
  isClientLoading: boolean = false;
  isRuleTypesLoading: boolean = false;
  isSPNamesLoading: boolean = false;
  isSubRuleTypesLoading: boolean = false;
  isPatternTypesLoading: boolean = false;
  regionName: string;
  subRegionName: string;
  clientName: string = '';
  validationSection: boolean = false;
  showUIValidationRule: boolean = false;
  showBEValidationRule: boolean = false;
  validation: string = '';

  DataSourceTypes = DataSourceType;
  storageAccount: StorageAccount[];
  isSubmitted: boolean = false;
  searchGroup$ = new Subject<string>();
  searchRuleSetName$ = new Subject<string>();
  securityGroups: { id: string, displayName: string }[] = []; // To store fetched security groups
  selectedSecurityGroups: SecurityGroup[] = []; // Selected groups

  securityGroups$ = new Observable<any[]>;
  rules: RuleTypes[] = [];
  spNames: SPNames[] = [];
  subRules: SubRule[] = [];
  patterns: Patterns[] = [];
  logicalOperators: LogicalOperator[] = [];
  conditionalOperators: ConditionalOperator[] = [];
  ruleSetNames: RuleSetNames[] = [];
  invalidTabNameList: string[] = [];
  selectedGloGenRuleSetNames: RuleSetNames[] = [];
  removedItemsGloGenRuleSetNames: RuleSetNames[] = [];

  private securityGroupsSearchTerm$ = new BehaviorSubject<string>('');
  requiredRule = '';
  uniqueRule = '';
  public excelFileRule: ExcelRule[] = [];
  visible = false;

  availableColumns1: ColumnNameDatatypeName[] = []
  availableColumns2: ColumnNameDatatypeName[] = []
  validationHelper = new ValidationHelper();
  toggleVisibility() {
    this.visible = !this.visible;
  }
  RuleTypes = RuleTypeNames;
  SubRuleTypes = SubRuleTypes;
  selectedSubRule: any;
  invalidFieldList = [
    { field: 'delimiter', tab: 'addtional-setting-tab' },
    { field: 'skip_header_rows', tab: 'addtional-setting-tab' },
    { field: 'databaseConfigurationId', tab: 'database-settings-tab' },
    { field: 'databaseName', tab: 'database-settings-tab' },
    { field: 'tableName', tab: 'database-settings-tab' },
    { field: 'deltaJobId', tab: 'addtional-setting-tab' }
  ];

  expandedTd: number | null = null;
  uiValidationRulesInvalid: boolean = false;
  totalCountOfRules: number = 0;
  dateOnlyFormats: DateTimeFormats[] = [];
  timeOnlyFormats: DateTimeFormats[] = [];
  databaseDestinationSettingsError: string = '';
  constructor(
    private fb: FormBuilder,
    private configService: ConfigurationService,
    private toastr: ToastrService,
    private dsService: DataSliceService,
    private processService: ProcessConfigService,
    private tokenService: GraphApiTokenService,
    private dataInsiderService: DataInsiderService,
    //private confirmModalService: NgbModal,
    private confirmModalservice: ModalService,
    private helperUtil: Helper,
    private iterableDiffers: IterableDiffers,
    private fabService: FabService,
    private modalService: ModalServiceService

  ) {
  }
  ngDoCheck(): void {

    const changes = this.iterableDiffer.diff(this.columnDatatype);
    if (changes) {
      // detects add/remove/move of items
      //changes.forEachAddedItem(r => console.log('Added', r.item));
      //changes.forEachRemovedItem(r => console.log('Removed', r.item));
      this.availableColumns1 = [...this.columnDatatype.filter(x => x.willInclude)];
      this.availableColumns2 = [...this.columnDatatype.filter(x => x.willInclude)];
      this.processConfigurationForm?.get('ruleColumnName')?.disable();
    }

  }

  keyColumnsSelected: string = "";
  dedupColumnsSelected: string = "";

  private iterableDiffer!: IterableDiffer<ColumnNameDatatypeName>;

  async ngOnInit(): Promise<void> {

    this.activeTab = "client-tab";
    this.iterableDiffer = this.iterableDiffers.find([]).create<ColumnNameDatatypeName>((index, item) => item.willInclude);
    //this.getDsConfiguration();
    this.initializeForm();
    //this.getRegionBySecurityGroup();
    this.dateOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'date');
    this.timeOnlyFormats = this.dateTimeNames.filter(x => x?.dataTypeName === 'time');

    await this.populateForm();

    this.clientInfoForm?.get('RegionId')?.disable();
    this.clientInfoForm?.get('SubRegionId')?.disable();
    this.clientInfoForm?.get('ClientId')?.disable();

    if (this.columnDatatype) {
      this.availableColumns1 = [...this.columnDatatype.filter(x => x.willInclude)];
      this.availableColumns2 = [...this.columnDatatype.filter(x => x.willInclude)];
    }

    if (this.dataSource === DataSourceType.DataBricks || this.dataSource === DataSourceType.LandingLayer) this.getStorageAccountDetails();

    this.processConfigurationForm?.get('ruleColumnName')?.disable();
    this.keyColumnsSubscription = this.keyColumns?.subscribe((val: string | null) => {

      if (val == null) return;
      if (val.length === 0) {
        this.processConfigurationForm.get('ignore_duplicate_rows').setValue(false);
        this.processConfigurationForm.get('ignore_duplicate_rows').disable();
        this.processConfigurationForm.get('order_by_column_list_name_sort_dir').disable();
        this.processConfigurationForm.get('order_by_column_list_name_sort_dir').setValue('desc');
      } else {
        this.processConfigurationForm.get('ignore_duplicate_rows').setValue(true);
        if (this.formValues?.flpConfigurationId === '') {
          this.processConfigurationForm.get('ignore_duplicate_rows').enable();
          this.processConfigurationForm.get('order_by_column_list_name_sort_dir').enable();
        }
      }

      this.keyColumnsSelected = val;

    });

    this.dedupColumnsSubscription = this.dedupColumns?.subscribe((val: string | null) => {
      this.dedupColumnsSelected = val;
    });

    if (this.formValues?.flpConfigurationId) {
      this.formValues.securityGroups.forEach(item => {
        this.securityGroups.push({ id: item.securityGroupId, displayName: item.securityGroupName });
      });
      this.clientInfoForm.get('security_group').setValue(this.securityGroups);

      //disable isExternalProject
      if (this.formValues.campaignId) {
        this.clientInfoForm.get('isExternalProject').setValue(true);
        this.clientInfoForm.get('isExternalProject').disable();
      }
    } else {
      this.setupSearchGroup();
      this.onSearchGroup(sessionStorage.getItem('UserDefaultGroup'));
      if (this.dataSource !== DataSourceType.LandingLayer) {
        this.getRuleTypes();
        this.getSPNames();
        this.getConditionalOperators();
        this.setupSearchRuleSetNames();
      } else {

      }
    }



    //this is to set the value of preProcessName
    //this is required cause we need to wait for the getProcessName to finish
    //setting value to the control;
    this.processConfigurationForm.get('processName')?.valueChanges
      .pipe(filter(value => !!value), take(1))
      .subscribe(value => {
        this.prevProcessName = value;
      });

  }

  previousRuleSetId: string = '';

  ngOnDestrory() {
    this.keyColumnsSubscription.unsubscribe();
    this.dedupColumnsSubscription.unsubscribe();
  }

  getRuleTypes() {
    this.rules = [];
    this.logicalOperators = [];
    this.conditionalOperators = [];
    this.patterns = [];
    this.isRuleTypesLoading = true;
    this.dataInsiderService.getRuleTypes().subscribe({
      next: (response: APIResponse<RuleTypes[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.rules = response.result;
            this.isRuleTypesLoading = false;
          }
        }
      },
      error: error => {
        console.log(error);
        this.isRuleTypesLoading = false;
      }
    });

  }

  getSPNames() {
    this.spNames = [];
    this.isSPNamesLoading = true;
    this.dataInsiderService.getSPNames().subscribe({
      next: (response: APIResponse<SPNames[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.spNames = response.result;
            this.isSPNamesLoading = false;
          }
        }
      },
      error: error => {
        console.log(error);
        this.isSPNamesLoading = false;
      }
    });
  }

  getSubRules(ruleTypeId: number | null): Observable<SubRule[]> {

    if (ruleTypeId === null) {
      this.subRules = [];
      this.isSubRuleTypesLoading = false;
      return of([]); //
    }

    this.subRules = [];

    this.isSubRuleTypesLoading = true;
    return this.dataInsiderService.getSubRule(ruleTypeId).pipe(
      tap((response: APIResponse<SubRule[]>) => {
        if (response?.responseCode === 200) {
          this.isSubRuleTypesLoading = false;
        } else {
          this.isSubRuleTypesLoading = true;
        }
      }),
      map(response => response.result)
    );
  }

  getConditionalOperators() {
    this.conditionalOperators = [];

    this.dataInsiderService.getConditionalOperators().subscribe({
      next: (response: APIResponse<ConditionalOperator[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.conditionalOperators = response.result;
          }
        }
      }
    });

  }

  getPatterns(subRuleId: number): Observable<Patterns[]> {

    if (subRuleId === null) {
      this.patterns = [null];
      this.isPatternTypesLoading = false;
      return of([]); //
    }

    //this.patterns = [null];
    this.isPatternTypesLoading = true;
    return this.dataInsiderService.getPatterns(subRuleId).pipe(
      tap((response: APIResponse<Patterns[]>) => {
        if (response?.responseCode === 200) {
          this.patterns = response.result;
          this.isPatternTypesLoading = false;
        } else {
          this.isPatternTypesLoading = true;
        }
      }),
      map(response => response.result)
    );
  }
  defaultRuleSetNames: RuleSetNames[] = [];
  getRuleSetNamesBySecGrpIds(sgIds: string) {
    this.ruleSetNames = [];
    this.isgetRuleSetBySecGrpIdsLoading = true;
    //let sgIds =  this.securityGroups.map(sg => sg.id).join(',');
    this.dataInsiderService.getRuleSetNamesBySecGrpId(sgIds).subscribe({
      next: (response: APIResponse<RuleSetNames[]>) => {
        if (response) {
          if (response.responseCode === 200) {

            this.isgetRuleSetBySecGrpIdsLoading = false;
            this.ruleSetNames = response.result;
            this.defaultRuleSetNames = this.ruleSetNames;
            //emit to parent
            //his.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
          }
        }
      }, error: () => {
        this.isgetRuleSetBySecGrpIdsLoading = false;
      }
    });
  }

  isgetRuleSetByRuleSetNameIdLoading: boolean = false;
  isgetRuleSetBySecGrpIdsLoading: boolean = false;
  private tmp_existingRuleSetNameIds = new Set<string>();
  getRuleSetByRuleSetNameId(rsnId: string) {



    // //only retrieve the newly selected item
    // let updatedRuleSetIdToRetrieve = rsnId.split(',').filter(id => !this.tmp_existingRuleSetNameIds.includes(id)).join(',');
    // if (updatedRuleSetIdToRetrieve === '') {
    //   updatedRuleSetIdToRetrieve = rsnId;
    // }

    //this.excelFileRule = [];
    if (rsnId.trim().length === 0) {
      //this.addRuleToFormValues();
      return;
    }

    //let sgIds =  this.securityGroups.map(sg => sg.id).join(',');

    this.isgetRuleSetByRuleSetNameIdLoading = true;
    this.dataInsiderService.getRuleSetByRuleSetNameId(rsnId).subscribe({
      next: (response: APIResponse<ExcelRule[]>) => {
        if (response) {
          if (response.responseCode === 200) {

            //lets add the ruleid and rsn in the tmp
            let newExcelFileRule: ExcelRule[] = response.result.map(rule => ({
              ...rule,
              ruleSetName: this.processConfigurationForm.get('ruleSetName')?.value,
              ruleSetNameId: '', //clear so as to save as a new rule              
              isIrrelevant: false,
              description: '', //lets clear this
              isGlobal: false, //explicitly set to not global
              ruleSetType: 1 //explicitly lets make it processrules
            }));

            const allDuplicate = response.result.every(x => this.tmp_existingRuleSetNameIds.has(x.ruleDescription));
            if (allDuplicate) {
              this.toastr.warning('Rule already exists. Please create new one.');
              this.isgetRuleSetByRuleSetNameIdLoading = false;
              return;
            }

            // if (this.tmp_existingRuleSetNameIds.has(rsnId)) {
            //   this.toastr.warning('Rule already exists. Please create new one.');
            //   this.isgetRuleSetByRuleSetNameIdLoading = false;
            //   return;
            // }

            // const uniqueNewRules = newExcelFileRule.filter(
            //   rule => !rsnId.includes(rule.ruleSetNameId)
            // );

            //add the new globa/generic rule
            newExcelFileRule.forEach(x => {
              var exists = this.tmp_newlyAddedRules.find(y => y.ruleDescription === x.ruleDescription);
              if (!exists) {
                this.tmp_newlyAddedRules.push(x);
                this.tmp_existingRuleSetNameIds.add(x.ruleDescription);
                this.excelFileRule.push(x);
              };
            });
            //this.excelFileRule.push(...newExcelFileRule);

            // if (this.removedItemsGloGenRuleSetNames.length > 0) {
            //   const newRuleSetNameIds = newExcelFileRule.map(rule => rule.ruleSetNameId);
            //   //combine nonGloGenRule and this.excelFileRule
            //   let nonGloGenRule = this.excelFileRule.filter(r => !(r.isGlobal) && r.ruleSetType !== 2);
            //   this.excelFileRule =
            //     [...nonGloGenRule,
            //     ...this.excelFileRule.filter(rule => (rule.isGlobal || rule.ruleSetType === 2) &&
            //       newRuleSetNameIds.includes(rule.ruleSetNameId))];
            // }

            const startingIndex = newExcelFileRule.length + 1;

            this.excelFileRule = this.excelFileRule.map((rule, index) => ({
              ...rule,
              id: startingIndex + index // Reassign sequential IDs starting from 1
            }));

            this.isgetRuleSetByRuleSetNameIdLoading = false;

            this.findAllIrrelevant();

            this.helperUtil.sortRuleSet(this.excelFileRule);
            //this.addRuleToFormValues();
            this.clearRule();
          }
        }
      }, error: () => {
        this.isgetRuleSetByRuleSetNameIdLoading = false;
        this.toastr.error('Something went wrong. Unable to retrieve Rule Sets');
      }
    });
  }

  findAllIrrelevant() {
    let hasMatch: any;
    const rules = this.excelFileRule;
    rules.forEach(rule => {
      if (rule.ruleTypeId != RuleTypeNames.Custom && rule.ruleTypeId !== RuleTypeNames.BEValidation) {
        const ruleColumns = rule.ruleColumnName;
        const columnNames = this.columnDatatype.map(col => col.ColumnName);

        if (rule.subRuleId !== SubRuleTypes.Comparison) {
          hasMatch = ruleColumns.some(col => columnNames.includes(col));
        } else {
          //TODO:
          hasMatch = [String(ruleColumns)].some(col => columnNames.includes(col));
        }

        if (!hasMatch) {
          rule.isIrrelevant = true;
        }

      }
    });
  }

  initializeForm() {
    //debugger;
    clientInfoForm: this.fb.group({
      RegionId: ['', Validators.required],
      SubRegionId: ['', Validators.required],
      ClientId: ['', Validators.required],
      processName: ['', Validators.required],
      description: ['', [Validators.maxLength(1000), Validators.pattern(/^[a-zA-Z0-9,. _]*$/)]],
      security_group: [[], Validators.required, requiredArrayValidatorTemp]
    });

    if (this.fabService.fabReady$) {
      //debugger;      
      this.clientInfoForm.addControl('isExternalProject', new FormControl<boolean>(false));
      this.clientInfoForm.addControl('campaignName', new FormControl<string | null>(null));
      this.clientInfoForm.get('campaignName').disable();
    }

    this.processConfigurationForm = this.fb.group({
      processName: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(70), Validators.pattern(this.processNamePattern)]], //[Validators.required, Validators.minLength(5), Validators.maxLength(40)], [this.validateProcessName()]
      description: ['', [Validators.maxLength(1000), Validators.pattern(/^[a-zA-Z0-9,. _]*$/)]],

      //additional settings
      delimiter: [{ value: this.delimiter, disabled: this.disableDelimiter }, Validators.required],
      flexCheckSkipEmptyLines: [],
      flexCheckHasHeaders: [false],
      txtQuoteCharacter: ['"', [Validators.required, Validators.minLength(1), Validators.maxLength(1)]],
      txtEncoding: ['UTF-8'],
      is_active: [],
      do_not_archive_file: [false],
      spanish_to_english: [],
      roman_numerals_only: [],
      ignore_duplicate_rows: [false],
      order_by_column_list_for_dedup: [],
      order_by_column_list_name: [],
      order_by_column_list_name_sort_dir: [],
      isSkipRow: [false],
      skip_header_rows: [{ value: '0', disabled: true }, [Validators.required, Validators.pattern("^\\d{1,2}$")]],
      skip_footer_rows: [{ value: '0', disabled: true }, [Validators.required, Validators.pattern("^\\d{1,2}$")]],

      databaseName: ['', Validators.required],
      databaseNameId: [],
      databaseConfigurationId: ['0', Validators.required],
      //databaseServer : [],
      //databaseServerId : [],
      tableName: ['', [Validators.required, Validators.pattern(this.processNamePattern)]],
      drop_history_table: [false],
      drop_main_table: [false],
      is_validate_fileschema_with_target_table: [false],

      mergeData: [false],
      createHistoryTable: [false],

      //for databricks
      deltaTableName: [''],
      deltaServerNameId: [''],
      deltaJobId: ['', [Validators.pattern(this.onlyNumbersPattern)]],
      deltaStorageAccountId: [''],
      deltaContainerName: [''],
      deltaSource: [''],
      search_group: [''],
      frmSubmitted: [false],
      ignoreSheet: [false],
      newSheet: [false],
      missingSheet: [false],
      //security_group: [[], requiredArrayValidatorTemp],

      //for apply rule
      ruleSetNames: [null],
      ruleSetName: [''],
      ruleSetDescription: ['', [Validators.maxLength(1000), Validators.pattern(/^[a-zA-Z0-9,. _]*$/)]],
      ruleType: [null],
      subRuleType: [null],
      patternType: [null],
      ruleColumnName: [],
      ruleColumnName2: [null],
      isCombinationRule: [{ value: false, disabled: true }],
      requiredRuleDescription: [''],
      uniqueRuleDescription: [''],
      formatType: [''],
      valueType: [''],
      conditionType: [null],
      aiPrompt: [''],
      fromValue: [0],
      toValue: [0],
      spName: [null],
      isAllowNullOrEmptySpaces: [false]

    });

    const deltaTableName = this.processConfigurationForm.get('deltaTableName');
    const deltaServerNameId = this.processConfigurationForm.get('deltaServerNameId');
    const deltaJobId = this.processConfigurationForm.get('deltaJobId');

    const deltaStorageAccountId = this.processConfigurationForm.get('deltaStorageAccountId');
    const deltaContainerName = this.processConfigurationForm.get('deltaContainerName');
    const deltaSource = this.processConfigurationForm.get('deltaSource');
    const databaseName = this.processConfigurationForm.get('databaseName');

    if (this.dataSource === this.DataSourceTypes.DataBricks) {


      deltaTableName.setValidators([Validators.required, Validators.pattern(/^(?!.*(__|--))[a-zA-Z0-9_-]*$/), Validators.maxLength(100)]); //
      deltaServerNameId.setValidators([Validators.required]);
      deltaJobId.setValidators([Validators.required, Validators.pattern(this.onlyNumbersPattern)]);
      deltaStorageAccountId.setValidators([Validators.required]);
      //deltaContainerName.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.pattern(/^(?!.*--)[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/), noWhitespaceValidator]); //
      deltaContainerName.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.pattern(/^(?!-+$)[a-z0-9][a-z0-9\- ]*$/), noWhitespaceValidator]);
      deltaSource.setValidators([Validators.required, Validators.maxLength(100), Validators.pattern(/^(?!.*([ _-])\1)[a-zA-Z0-9_/ -]+$/), noWhitespaceValidator]); //
      databaseName.clearValidators();
    } else if (this.dataSource === this.DataSourceTypes.Default) {
      deltaContainerName?.clearValidators();
      deltaSource?.clearValidators();
    }

    if (this.dataSource === this.DataSourceTypes.LandingLayer) {
      this.processConfigurationForm.addControl('landingLayerFileExtension', new FormControl([], Validators.required));
      this.processConfigurationForm.addControl('landingLayerRegex', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerDateformat', new FormControl(null));
      this.processConfigurationForm.addControl('landingLayerTimeformat', new FormControl(null));
      this.processConfigurationForm.addControl('landingLayerPrefix', new FormControl('', Validators.required));
      this.processConfigurationForm.addControl('landingLayerPrefixCheckbox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerDateformatCheckbox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerTimeformatCheckBox', new FormControl(''));
      this.processConfigurationForm.addControl('landingLayerAcceptedPath', new FormControl('', Validators.required));
      this.processConfigurationForm.addControl('landingLayerRejectedPath', new FormControl('', Validators.required));

      if (!this.formValues?.flpConfigurationId) {
        this.setupCheckboxToggle('landingLayerDateformatCheckbox', 'landingLayerDateformat');
        this.setupCheckboxToggle('landingLayerPrefixCheckbox', 'landingLayerPrefix');
        this.setupCheckboxToggle('landingLayerTimeformatCheckBox', 'landingLayerTimeformat');
      }
      deltaStorageAccountId.setValidators([Validators.required]);
      deltaContainerName.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(63), Validators.pattern(/^(?!-+$)[a-z0-9][a-z0-9\- ]*$/), noWhitespaceValidator]);
    }



  }

  setProcessSettingValues() {
    if (this.formValues) {
      this.clientInfoForm.get('processName')?.setValue(this.formValues?.processName);
      this.clientInfoForm.get('description')?.setValue(this.formValues?.description);
      if (this.formValues?.RegionId.trim().length > 0) {
        this.dsRegion = [{ id: +this.formValues?.RegionId, name: this.formValues?.region }];
        this.clientInfoForm.get('RegionId').setValue(+this.formValues?.RegionId);
      }
      if (this.formValues?.SubRegionId.trim().length > 0) {
        this.dsSubRegion = [{ id: this.formValues?.SubRegionId, name: this.formValues?.subRegion }];
        this.clientInfoForm.get('SubRegionId').setValue(this.formValues?.SubRegionId);
      }
      if (this.formValues?.ClientId.trim().length > 0) {
        this.dsClient = [{ id: +this.formValues?.ClientId, name: this.formValues?.clientName }];
        this.clientInfoForm.get('ClientId').setValue(+this.formValues?.ClientId);
      }
    }
  }
  async populateForm() {
    if (this.dataSource === this.DataSourceTypes.LandingLayer) {
      await this.getFileExtensions(); //workaround todo: create resolver
      this.setProcessSettingValues();

      if (this.formValues?.landingLayerFileExtension.length > 0) {
        const selected = (this.formValues.landingLayerFileExtension ?? []).map(x => Number(x));
        this.processConfigurationForm.get('landingLayerFileExtension')!.setValue(selected);
        //this.updateselectedFiles.emit(this.fileNameExtensions.filter(x => selected.includes(x.id)).map(x => x.fileExtension));
        this.updateselectedFiles.emit(this.fileNameExtensions.filter(x => selected.includes(x.id)).map(x => x.fileExtension));
      }

      if (this.formValues?.landingLayerRegex?.length > 0) {
        this.regexList = this.formValues.landingLayerRegex;
        //this.validateFileNamesWithRegex.emit(this.regexList);
        this.validateFileNamesWithRegex.emit(this.regexList);
      }

      if (this.formValues?.deltaStorageAccountId?.trim() != '') {
        this.processConfigurationForm.get('deltaStorageAccountId').setValue(this.formValues.deltaStorageAccountId);
      }

      if (this.formValues?.deltaContainerName?.trim() != '') {
        this.processConfigurationForm.get('deltaContainerName').setValue(this.formValues.deltaContainerName);
      }

      this.processConfigurationForm.get('landingLayerAcceptedPath').setValue(this.formValues.landingLayerAcceptedPath);
      this.processConfigurationForm.get('landingLayerRejectedPath').setValue(this.formValues.landingLayerRejectedPath);
      this.processConfigurationForm.get('landingLayerPrefix').setValue(this.formValues.landingLayerPrefix);
      this.processConfigurationForm.get('landingLayerDateformat').setValue(this.formValues.landingLayerDateformat);
      this.processConfigurationForm.get('landingLayerTimeformat').setValue(this.formValues.landingLayerTimeformat);


    } else {
      if (this.fileType === FileType.MSExcel1 || this.fileType === FileType.MSExcel2 || this.fileType === FileType.MSExcel3) { this.disableDelimiter = true; }

      if (this.formValues) {

        this.clientInfoForm.get('processName')?.setValue(this.formValues.processName);


        this.clientInfoForm.get('description')?.setValue(this.formValues.description);

        this.delimiter = this.formValues.delimiter;

        this.processConfigurationForm.get('delimiter')?.setValue(this.formValues.delimiter);
        this.processConfigurationForm.get('flexCheckSkipEmptyLines').setValue(this.formValues.flexCheckSkipEmptyLines);
        this.processConfigurationForm.get('flexCheckHasHeaders').setValue(this.formValues.flexCheckHasHeaders);
        this.processConfigurationForm.get('txtQuoteCharacter').setValue(this.formValues.txtQuoteCharacter);
        this.processConfigurationForm.get('is_active').setValue(this.formValues.is_active);
        this.processConfigurationForm.get('do_not_archive_file').setValue(this.formValues.do_not_archive_file);
        this.processConfigurationForm.get('spanish_to_english').setValue(this.formValues.spanish_to_english);
        this.processConfigurationForm.get('roman_numerals_only').setValue(this.formValues.roman_numerals_only);

        this.processConfigurationForm.get('order_by_column_list_for_dedup').setValue(this.formValues.flexCheckOrderByColumnListForDedup);

        this.processConfigurationForm.get('order_by_column_list_name').setValue(this.formValues.order_by_column_list_name);
        this.processConfigurationForm.get('order_by_column_list_name_sort_dir').setValue(this.formValues.order_by_column_list_name_sort_dir === '' ? 'desc' : this.formValues.order_by_column_list_name_sort_dir);
        this.processConfigurationForm.get('frmSubmitted')?.setValue(this.formValues.frmSubmitted);
        this.processConfigurationForm.get('ignoreSheet')?.setValue(this.formValues.ignoreSheet);
        this.processConfigurationForm.get('newSheet')?.setValue(this.formValues.newSheet);
        this.processConfigurationForm.get('missingSheet')?.setValue(this.formValues.missingSheet);
        if (this.formValues.RegionId.trim().length > 0) {
          this.dsRegion = [{ id: +this.formValues.RegionId, name: this.formValues.region }];
          //console.log(this.dsRegion)
          //this.getRegion([String(this.formValues.RegionId)]);
          //this.getSubRegion(this.formValues.RegionId);
          this.clientInfoForm.get('RegionId').setValue(+this.formValues.RegionId);
        }
        if (this.formValues.SubRegionId.trim().length > 0) {
          this.dsSubRegion = [{ id: this.formValues.SubRegionId, name: this.formValues.subRegion }];
          //  setTimeout(() => {
          //  this.getClient(this.formValues.SubRegionId);
          //  }, 2000);             
          this.clientInfoForm.get('SubRegionId').setValue(this.formValues.SubRegionId);
        }
        if (this.formValues.ClientId.trim().length > 0) {
          this.dsClient = [{ id: +this.formValues.ClientId, name: this.formValues.clientName }];
          this.clientInfoForm.get('ClientId').setValue(+this.formValues.ClientId);
        }


        this.processConfigurationForm.get('tableName').setValue(this.formValues.tableName);
        if (this.dataSource === DataSourceType.DataBricks) {
          if (this.formValues.tableName.trim() != '') {
            this.processConfigurationForm.get('deltaTableName').setValue(this.formValues.tableName);
          }
          // if (this.formValues.databaseConfigurationId.trim() != '') {
          //   this.processConfigurationForm.get('deltaServerNameId').setValue(+this.formValues.databaseConfigurationId);
          // }
          if (this.formValues.databaseConfigurationId) {
            this.processConfigurationForm.get('deltaServerNameId').setValue(+this.formValues.databaseConfigurationId);
          }
          if (this.formValues.sourcePath.trim() != '') {
            this.processConfigurationForm.get('deltaSource').setValue(this.formValues.sourcePath);
          }
          if (this.formValues.deltaContainerName.trim() != '') {
            this.processConfigurationForm.get('deltaContainerName').setValue(this.formValues.deltaContainerName);
          }
          if (this.formValues.deltaStorageAccountId.trim() != '') {
            this.processConfigurationForm.get('deltaStorageAccountId').setValue(this.formValues.deltaStorageAccountId);
          }
          if (this.formValues.deltaJobId.trim() != '') {
            this.processConfigurationForm.get('deltaJobId').setValue(this.formValues.deltaJobId);
          }
        }
        this.processConfigurationForm.get('databaseConfigurationId').setValue(+this.formValues.databaseConfigurationId);
        this.processConfigurationForm.get('databaseName').setValue(this.formValues.databaseName);

        this.processConfigurationForm.get('drop_history_table').setValue(this.formValues.drop_history_table);
        this.processConfigurationForm.get('drop_main_table').setValue(this.formValues.drop_main_table);
        this.processConfigurationForm.get('is_validate_fileschema_with_target_table').setValue(this.formValues.validate_fileschema);
        if (this.formValues.skip_header_rows > 0 || this.formValues.skip_footer_rows > 0) {
          this.processConfigurationForm.get('isSkipRow').setValue('true');
        }
        this.processConfigurationForm.get('skip_header_rows').setValue(this.formValues.skip_header_rows);
        this.processConfigurationForm.get('skip_footer_rows').setValue(this.formValues.skip_footer_rows);
        this.keyColumnsSelected = this.formValues.key_columns;
        this.dedupColumnsSelected = this.formValues.order_by_column_list_for_dedup;

        if (this.formValues.ignore_duplicate_rows === false) {
          this.processConfigurationForm.get('ignore_duplicate_rows').disable();
          this.processConfigurationForm.get('order_by_column_list_name_sort_dir').disable();
        } else {
          this.processConfigurationForm.get('ignore_duplicate_rows').enable();
          this.formValues.order_by_column_list_name_sort_dir = this.formValues.keep_first_row ? 'asc' : 'desc';
        }
        this.processConfigurationForm.get('ignore_duplicate_rows').setValue(this.formValues.ignore_duplicate_rows);
        this.processConfigurationForm.get('mergeData').setValue(this.formValues.mergeData);
        this.processConfigurationForm.get('createHistoryTable').setValue(this.formValues.createHistoryTable);

        // this.clientInfoForm.enable();
        // this.processConfigurationForm.enable();
        if (this.formValues?.flpConfigurationId) {
          // this.processConfigurationForm.disable();
          if (this.fabService.fabReady$) {
            this.showCampaignName = true;
            setTimeout(() => {
              //debugger;
              if (this.formValues.campaignId) {
                this.clientInfoForm.get('isExternalProject').setValue(true);
                this.campaignNames = this.fabService.FABUserAccount.map(x => ({ campaignId: x.campaignId, campaignName: x.campaignName }));
                this.clientInfoForm.get('campaignName').setValue(this.formValues.campaignId);
              }
            }, 500);

          }

          this.clientInfoForm.disable();
          this.processConfigurationForm.disable();

          // this.processConfigurationForm.disable({ emitEvent: false });

          // this.processConfigurationForm.get('processName').disable();
          //this.processConfigurationForm.get('delimiter').enable();
          // this.processConfigurationForm.get('description').disable();
          // this.processConfigurationForm.get('flexCheckHasHeaders').disable();


        } else {
          this.getRegionBySecurityGroup();
        }

      }

      if (this.formValues?.securityGroups && this.formValues?.securityGroups.length > 0) {
        const defaultGroups = this.formValues.securityGroups.map(item => ({
          id: item.securityGroupId,
          displayName: item.securityGroupName
        }));
        this.clientInfoForm.get('security_group').setValue(defaultGroups);
      }

      this.excelFileRule = [];
      this.formValues?.ruleSet?.forEach(rule => {
        this.excelFileRule.push({
          id: rule.id,
          ruleSetNameId: rule.ruleSetNameId,
          ruleSetName: rule.ruleSetName,
          ruleTypeId: rule.ruleTypeId,
          subRuleId: rule.subRuleId,
          patternId: rule.patternId,
          format: rule.format,
          conditionId: rule.conditionId,
          fromValue: rule.fromValue,
          toValue: rule.toValue,
          isCombinationRule: rule.isCombinationRule,
          ruleColumnName: rule.ruleColumnName, //.split(',').map(item => item.trim()),
          ruleColumnName2: rule.ruleColumnName2,
          isActive: true,
          prompt: rule.prompt,
          ruleDescription: rule.ruleDescription,
          isIrrelevant: false,
          isGlobal: false,
          ruleSetType: 1,
          description: '',
          isAllowNullOrEmptySpaces: rule.isAllowNullOrEmptySpaces,
          spNameId: rule.spNameId,
          isUpdated: false
        });
      });


    }

    if (this.formValues?.flpConfigurationId) {
      this.clientInfoForm.disable();
      this.processConfigurationForm.disable();
    } else {

      this.clientInfoForm.enable();
      this.processConfigurationForm.enable();
      this.getRegionBySecurityGroup();
    }
  }

  getStorageAccountDetails() {
    this.processService.getStorageAccountDetails(this.dataSource).subscribe({
      next: (response: APIResponse<StorageAccount[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.storageAccount = response.result.filter(acc => acc.configurationProcessType === this.dataSource);
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

  getAllDIRegions() {
    this.configService.getAllDIRegions().subscribe({
      next: (response: APIResponse<DIRegions[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DIRegions = response.result;
          this.DIRegions.unshift({ id: 0, name: "--Select--" });
        }
      }
    });
  }

  getAllDISubRegions() {
    this.configService.getAllDISubRegions().subscribe({
      next: (response: APIResponse<DISubRegions[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DISubRegions = response.result;
          this.DISubRegions.unshift({ id: 0, name: "--Select--" });
        }
      }
    });
  }

  getAllDIClientnames() {
    this.configService.getAllDIClientnames().subscribe({
      next: (response: APIResponse<DIClientNames[]> | undefined) => {
        if (response?.responseCode === 200) {
          this.DIClientnames = response.result;
          this.DIClientnames.unshift({ id: 0, name: "--Select--" });
        }
      }
    });
  }

  //TODO: should only check when there's a change happening
  //right now it is called thrice ?
  validateProcessName(): AsyncValidatorFn {
    return (control: AbstractControl) => {
      // return control.valueChanges.pipe(
      //   debounceTime(1000),
      //   take(1),
      //   switchMap(() => {

      return this.configService.checkProcessNameExists(control.value).pipe(
        map(result => {
          //this.additionalSettingsIsClear.emit(!result);              
          //this.processConfigurationForm.get('processName')?.value;
          return result.result ? { processNameExist: true } : null
        }),
        finalize(() => control.markAsTouched())
      )
      //   })
      // )
    }
  }

  async onDelimiterChanged(event: any) {

    var previousValue = this.processConfigurationForm.get('delimiter').value;

    var val = '';
    if (event.target.innerText === 'Other') {
      this.processConfigurationForm.get('delimiter')?.setValue(val);
      this.delimiterInput?.nativeElement.focus();
      return;
    } else {
      if (event.target.value) {
        val = event.target.value;
      } else {
        val = event.target.innerText;
        switch (event.target.innerText) {
          case 'comma (,)':
            val = ',';
            break;
          case 'tab (\\t)':
            val = "\\t";
            break;
          case 'pipe (|)':
            val = '|';
            break;
          case 'semicolon (;)':
            val = ';';
            break;
          case 'none':
            val = "none";
            break;
          default:
            break;
        }
      }

      this.processConfigurationForm.get('delimiter')?.setValue(val);
      this.formValues.delimiter = val;


    }

    // this.previewToParent2(this.formValues);

    //early exit if no rules are defined
    if (this.excelFileRule.length === 0) {
      this.formValues.delimiter = val;

      const retValue = await this.previewToParent2(this.formValues);
      return;
    }

    this.confirmModalservice.confirm(ModalMessages.HeaderSettingsHaveBeenChanged, ModalMessages.HeaderSettingsHaveBeenChangedConfirm)
      .then(async confirmed => {
        if (confirmed === true) {
          this.formValues.delimiter = val;


          const retValue = await this.previewToParent2(this.formValues);
          //this will always yield to true since we will be changing column headers to           
          this.toastr.info(ToastrMessages.ValidatonRulesHaveBeenReset);
        } else {
          this.processConfigurationForm.get('delimiter').setValue(previousValue);
        }
      });



    //this.closeWindow.emit();

    //this.previewToParent.emit(this.processConfigurationForm.value);


  }

  onSkipEmptyLines(event: any) {
    this.formValues.flexCheckSkipEmptyLines = event.target.checked;
    this.previewToParent2(this.formValues);
  }

  async onHasHeadersChange(event: any) {

    //this.alertUIValidationReset();
    //early exit if no rules are defined
    if (this.excelFileRule.length === 0) {
      this.formValues.flexCheckHasHeaders = event.target.checked;
      if (event.target.checked === false) {
        this.formValues.ignore_duplicate_rows = false;
      }
      const retValue = await this.previewToParent2(this.formValues);
      return;
    }
    this.confirmModalservice.confirm(ModalMessages.HeaderSettingsHaveBeenChanged, ModalMessages.HeaderSettingsHaveBeenChangedConfirm)
      .then(async confirmed => {
        if (confirmed === true) {
          this.formValues.flexCheckHasHeaders = event.target.checked;
          if (event.target.checked === false) {
            this.formValues.ignore_duplicate_rows = false;
          }
          //this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
          //this.previewToParent.emit(this.formValues);

          const retValue = await this.previewToParent2(this.formValues);
          //this will always yield to true since we will be changing column headers to 
          //COL0,COL1,COL2
          this.toastr.info(ToastrMessages.ValidatonRulesHaveBeenReset);
        } else {
          this.processConfigurationForm.get('flexCheckHasHeaders').setValue(!event.target.checked);
        }
      });
  }

  //TODO: not working for mutlisheet
  onConvertSpanishToEnglish(event: any) {

    //early exit if no rules are defined
    if (this.excelFileRule.length === 0) {
      this.formValues.spanish_to_english = event.target.checked;
      this.previewToParent2(this.formValues);
      return;
    }

    this.confirmModalservice.confirm(ModalMessages.HeaderSettingsHaveBeenChanged, ModalMessages.HeaderSettingsHaveBeenChangedConfirm)
      .then(async confirmed => {
        if (confirmed) {
          this.formValues.spanish_to_english = event.target.checked;
          const columnHeadersHaveChanged = await this.previewToParent2(this.formValues);
          if (!columnHeadersHaveChanged) {
            this.toastr.info(ToastrMessages.ValidatonRulesHaveBeenReset);
          }
        } else {
          this.processConfigurationForm.get('spanish_to_english').setValue(!event.target.checked);
        }
      });


  }

  async onConvertRomanNumerals(event: any) {

    //early exit if no rules are defined
    if (this.excelFileRule.length === 0) {
      this.formValues.roman_numerals_only = event.target.checked;
      this.previewToParent2(this.formValues);
      return;
    }

    this.confirmModalservice.confirm(ModalMessages.HeaderSettingsHaveBeenChanged, ModalMessages.HeaderSettingsHaveBeenChangedConfirm)
      .then(async confirmed => {
        if (confirmed) {
          this.formValues.roman_numerals_only = event.target.checked;
          const columnHeadersHaveChanged = await this.previewToParent2(this.formValues);
          if (!columnHeadersHaveChanged) {
            this.toastr.info(ToastrMessages.ValidatonRulesHaveBeenReset);
          }
        } else {
          this.processConfigurationForm.get('roman_numerals_only').setValue(!event.target.checked);
        }
      });

  }

  onQuoteCharacterChange(event: any) {
    if (event.target.value.trim() === '') this.processConfigurationForm.get('txtQuoteCharacter').setValue('"');
    if (event.target.value.length === 1) {
      this.formValues.txtQuoteCharacter = event.target.value;
      this.previewToParent2(this.formValues);
    }
  }

  onEncodingCharacterChange(event: any) {
    if (event.target.value.trim() === '') this.processConfigurationForm.get('txtEncoding').setValue('UTF-8');
    this.formValues.txtEncoding = event.target.value;
    this.previewToParent2(this.formValues);
  }

  skipHeadersRowsIsInvalid: boolean = false;
  onSkipHeadersRowChange(event: any) {

    if (this.excelFileRule.length === 0) {

      this.formValues.skip_header_rows = +event.target.value;
      if (this.isSkipHeaderFooterValid()) {
        if (this.processConfigurationForm.get('skip_header_rows')?.errors === null) {

          //this.alertUIValidationReset();
          this.formValues.keep_first_row = this.processConfigurationForm.get('isSkipRow').value;
          this.previewToParent2(this.formValues);
        }
      }
      return;
    }

    this.confirmModalservice.confirm(ModalMessages.HeaderSettingsHaveBeenChanged, ModalMessages.HeaderSettingsHaveBeenChangedConfirm)
      .then(async confirmed => {
        if (confirmed) {
          this.formValues.skip_header_rows = +event.target.value;
          if (this.isSkipHeaderFooterValid()) {
            if (this.processConfigurationForm.get('skip_header_rows')?.errors === null) {

              //this.alertUIValidationReset();
              this.formValues.keep_first_row = this.processConfigurationForm.get('isSkipRow').value;
              const columnHeadersHaveChanged = await this.previewToParent2(this.formValues);
              if (!columnHeadersHaveChanged) {
                this.toastr.info(ToastrMessages.ValidatonRulesHaveBeenReset);
              }
            }
          }
        } else {
          this.processConfigurationForm.get('isSkipRow').setValue(!event.target.checked);
        }
      });
  }
  skipFooterRowsIsInvalid: boolean = false;
  onSkipFooterRowChange(event: any) {
    // if (+event.target.value <= (this.processConfigurationForm.get('flexCheckHasHeaders').value ? this.rowCount - 1 : this.rowCount)) {
    //   this.toastr.error(ModalMessages.SkipHeaderRowsMoreThanTotalRowcount);
    //   event.target.value = 0;
    //   return;
    // }

    this.formValues.skip_footer_rows = +event.target.value;
    //this.skipFooterRowsIsInvalid = false;

    // if (
    //   this.formValues.skip_footer_rows >=
    //   (this.formValues.flexCheckHasHeaders
    //     ? (this.rowCount - this.formValues.skip_header_rows) - ((this.fileType === FileType.MSExcel1 || this.fileType === FileType.MSExcel2 || this.fileType === FileType.MSExcel3) ? 1 : 0)
    //     : (this.rowCount - this.formValues.skip_header_rows))
    // ) {
    //   //this.toastr.error(ModalMessages.SkipHeaderRowsMoreThanTotalRowcount);
    //   //this.config.skip_header_rows = 0;

    //   this.skipFooterRowsIsInvalid = true;
    //   return;

    // }



    if (this.isSkipHeaderFooterValid()) {
      if (this.processConfigurationForm.get('skip_footer_rows')?.errors === null) {
        this.formValues.keep_first_row = this.processConfigurationForm.get('isSkipRow').value;
        this.previewToParent2(this.formValues);
      }
    }
  }

  isSkipHeaderFooterValid(): boolean {
    this.skipFooterRowsIsInvalid = false;
    this.skipHeadersRowsIsInvalid = false;
    if (
      this.formValues.skip_footer_rows >=
      (this.formValues.flexCheckHasHeaders
        ? (this.rowCount - this.formValues.skip_header_rows)
        : (this.rowCount - this.formValues.skip_header_rows))
      //- ((this.fileType === FileType.MSExcel1 || this.fileType === FileType.MSExcel2 || this.fileType === FileType.MSExcel3) ? 1 : 0)
    ) {
      //this.toastr.error(ModalMessages.SkipHeaderRowsMoreThanTotalRowcount);
      //this.config.skip_header_rows = 0;

      this.skipFooterRowsIsInvalid = true;

    }

    if (
      this.formValues.skip_header_rows >=
      (this.formValues.flexCheckHasHeaders
        ? (this.rowCount - this.formValues.skip_footer_rows)
        : (this.rowCount - this.formValues.skip_footer_rows))
      //- ((this.fileType === FileType.MSExcel1 || this.fileType === FileType.MSExcel2 || this.fileType === FileType.MSExcel3) ? 1 : 0)
    ) {
      //this.toastr.error(ModalMessages.SkipHeaderRowsMoreThanTotalRowcount);
      //this.config.skip_header_rows = 0;
      this.skipHeadersRowsIsInvalid = true;

    }


    return !this.skipFooterRowsIsInvalid && !this.skipHeadersRowsIsInvalid;
  }

  //TODO: not working
  onOrderByColumnListForDedup(event: any) {

    event.target.checked ? this.processConfigurationForm.get('order_by_column_list_name').enable() : this.processConfigurationForm.get('order_by_column_list_name').disable();

    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
  }

  onOrderByChange(event: any) {
    //console.log('orderby:' + event.target.value);


    this.formValues.order_by_column_list_name_sort_dir = event.target.value;
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
    this.ignoreYNSortBy.emit({ ignoreYN: this.processConfigurationForm.get('ignore_duplicate_rows').value, sortBy: event.target.value });
  }

  onIgnoreDuplicateRows(event: any) {
    //console.log('ignore:' + event.target.checked);


    this.processConfigurationForm.get('ignore_duplicate_rows').disable();
    this.formValues.ignore_duplicate_rows = event.target.checked;

    if (this.formValues.ignore_duplicate_rows) {
      this.processConfigurationForm.get('order_by_column_list_name_sort_dir').enable();
      this.processConfigurationForm.get('ignore_duplicate_rows').enable();
    } else {
      this.processConfigurationForm.get('order_by_column_list_name_sort_dir').disable();
      this.processConfigurationForm.get('order_by_column_list_name_sort_dir').setValue('desc');
      this.keyColumnsSelected = '';
      this.dedupColumnsSelected = '';

      //since no keycolumn and dedup, let's remove all 
      this.columnDatatype.forEach(c => {
        c.ColumnKey = false;
        c.columnForDedeup = false;
      });
    }

    //let's pass together the columndataType in parent to update the columnDatatypePerSheet
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
    this.ignoreYNSortBy.emit({ ignoreYN: event.target.checked, sortBy: this.processConfigurationForm.get('order_by_column_list_name_sort_dir').value });
  }

  //TODO: 
  clientSettingsValuesValid = false;
  clientSettingsTabVisited = false;

  invalidTabClass(tabName: string): any {
    if (tabName === 'client-tab') {
      return { 'tabValidate': (this.validateProcessSettings() === false) }
    }
    else if (tabName === 'database-settings-tab') {
      var ret: Boolean = false;
      if (this.dataSource === 1) {
        if (this.formValues?.frmSubmitted) {
          // !this.formValues.databaseName ||
          if (+this.formValues?.databaseConfigurationId < 0 || !this.formValues.tableName) {
            ret = true;
          } else {
            ret = false;
          }
        } else {
          ret = true;
        }

      } else {
        //Databricks
        if (this.formValues?.frmSubmitted) {
          if (this.dataSource === this.DataSourceTypes.LandingLayer) {
            ret = !this.isLandingLayerDestinationValid();
          } else {
            if (!this.getInvalidDatabricksFields()) {
              ret = true;
            } else {
              ret = false;
            }
          }
        } else {

          ret = true;

        }

      }
      return { 'tabValidate': ret }
    } else if (tabName === 'addtional-setting-tab') {
      if (this.dataSource === DataSourceType.LandingLayer) {
        const extensionControl = this.processConfigurationForm.get('landingLayerFileExtension');

        const hasSelectedValues =
          Array.isArray(extensionControl?.value) &&
          extensionControl.value.length > 0;


        if (hasSelectedValues) {
          ret = false;
        } else {
          ret = true;
        }

        return { 'tabValidate': ret }
      }

    }
    // else if(tabName==='addtional-setting-tab')
    // {

    //   var ret:Boolean= true;       
    //    if(this.formValues.frmSubmitted){
    //      ret = false;
    //    }

    //   return {       


    //       'tabValidate':ret //this.isSubmitted==true ?!!this.invalidTabNameList.find(tb => tb === tabName):true,
    //       
    //   }
    // }
  }

  updateModifySetting() {
    //const updatedModifySettings = this.ModifySettingValidationList.map(x=>x.sheetName==this.currentWorkSheetName)
  }

  isDatabaseNamesLoading: boolean = false;
  prevProcessName: string = '';
  onTabChanged(tabName: any, event?: Event) {
    event?.preventDefault();
    event?.stopPropagation();
    this.activeTab = tabName;
    const tabButtons = document.querySelectorAll('#myTab .nav-link');
    tabButtons.forEach((button) => button.classList.remove('active'));
    document.getElementById(tabName)?.classList.add('active');
    let processConfigurationForm = this.processConfigurationForm;
    // const ctrl = this.processConfigurationForm.controls;
    // const invalid = [];
    // for (const name in ctrl) {
    //   if (ctrl[name].invalid) {
    //     invalid.push(name);
    //   }
    // }

    if (tabName === 'client-tab') {
      //this.clientSettingsTabVisited = true;
    }

    //if (!this.validateClientSettings()) return;

    // let regionId = this.processConfigurationForm.get('clientInfo.RegionId')?.value;
    // let subRegionId = this.processConfigurationForm.get('clientInfo.SubRegionId')?.value;
    // let clientNameId = this.processConfigurationForm.get('clientInfo.ClientId')?.value;

    // Get client info values from nested group
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    const regionId = clientInfo?.get('RegionId')?.value;
    const subRegionId = clientInfo?.get('SubRegionId')?.value;
    const clientNameId = clientInfo?.get('ClientId')?.value;


    if (tabName === 'database-settings-tab') {


      this.clientSettingsTabVisited = true;
      if (!this.validateClientSettings()) {

      } else {
        this.clientSettingsValuesValid = true;
        switch (this.dataSource) {
          case DataSourceType.Default:
            break;
          case DataSourceType.DataBricks:
            this.destinationValidationSection();
            break;
        }
        if (this.prevProcessName === this.processConfigurationForm.get('processName')?.value && this.databaseNames.length > 0) {
          return;
        }




        if (this.dataSource === DataSourceType.Default) {

          this.isDatabaseNamesLoading = true;
          this.configService.getDIDatabaseNames(regionId, subRegionId, clientNameId, this.dataSource).subscribe({
            next: (res: APIResponse<DIDatabaseNames[]>) => {
              //console.log(res);
              //debugger;
              this.databaseNames = [];
              if (res.responseCode === 200) {
                // if(res.result.length > 1) {
                //   this.clientForm.get('databaseName')?.setValue(res.result);
                // } else {
                //this.processConfigurationForm.get('databaseNames')?.setValue(res.result);

                // this.databaseNames = res.result; 


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

                // this.databaseNames.unshift({ id: null, databaseName: "--Select--", defaultDB: false,databaseServer:'' });
                // this.processConfigurationForm.get('databaseName')?.setValue(this.databaseNames[0].databaseName);
                if (res.result.length >= 1) {
                  let existingDatabaseConfigurationid = false;

                  this.databaseNames.forEach(x => {
                    if (x.id === +this.processConfigurationForm.get("databaseConfigurationId").value) {
                      existingDatabaseConfigurationid = true;
                      return;
                    }
                  });
                  if (existingDatabaseConfigurationid === false) {
                    this.processConfigurationForm.get('databaseName')?.setValue(this.databaseNames.filter(x => x.groupBy === 'Default')[0].databaseName);
                    this.processConfigurationForm.get("databaseConfigurationId").setValue(this.databaseNames.filter(x => x.groupBy === 'Default')[0].id);
                  } else {
                    //Now changed form control so used this line 
                    this.processConfigurationForm.get('databaseConfigurationId').setValue(+this.formValues.databaseConfigurationId);
                  }

                  if (this.formValues.flpConfigurationId !== '') return;
                  this.formValues.databaseConfigurationId = this.processConfigurationForm.get('databaseConfigurationId').value;
                  this.formValues.databaseName = this.processConfigurationForm.get('databaseName').value;
                  //update the tablename 
                  this.formValues.tableName = this.processConfigurationForm.get('tableName').value;
                  this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);




                }
                //this.clientSectionVals.emit(this.clientForm.value);    
                // }
              }

              this.isDatabaseNamesLoading = false;
            },
            error: (err) => {
              this.isDatabaseNamesLoading = false;
            }
          });
        } else if (this.dataSource === DataSourceType.DataBricks) {
          this.isDatabaseNamesLoading = true;



          this.configService.getDIDatabaseNames(regionId, subRegionId, clientNameId, this.dataSource).subscribe({
            next: (res: APIResponse<DIDatabaseNames[]>) => {

              if (res.responseCode === 200) {

                this.databaseNames = res.result;
                if (res.result.length >= 1) {
                  let existingDatabaseConfigurationid = false;





                  this.databaseNames.forEach(x => {
                    if (x.id === +this.processConfigurationForm.get("deltaServerNameId").value) {
                      existingDatabaseConfigurationid = true;
                      this.processConfigurationForm.get("deltaServerNameId").setValue(x.id);
                      return;
                    }
                  });
                  if (existingDatabaseConfigurationid === false) {


                    if (this.databaseNames.find(x => x.defaultDB === true)) {
                      this.processConfigurationForm.get("deltaServerNameId").setValue(this.databaseNames.find(x => x.defaultDB === true).id);
                      this.processConfigurationForm.get('databaseName')?.setValue(this.databaseNames.find(x => x.defaultDB === true).databaseName);
                    } else {
                      this.processConfigurationForm.get("deltaServerNameId").setValue(this.databaseNames[0].id);
                      this.processConfigurationForm.get('databaseName')?.setValue(this.databaseNames[0].databaseName);
                    }
                  }

                  if (this.formValues.flpConfigurationId !== '') {
                    this.isDatabaseNamesLoading = false;
                    return
                  };
                  this.formValues.deltaTableName = this.processConfigurationForm.get('deltaTableName').value;
                  this.formValues.tableName = this.processConfigurationForm.get('tableName').value;
                  this.formValues.deltaServerNameId = this.processConfigurationForm.get('deltaServerNameId').value;
                  this.formValues.databaseName = this.processConfigurationForm.get('databaseName').value;
                  this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
                }

              }


              this.isDatabaseNamesLoading = false;
            },
            error: (err) => {
              this.isDatabaseNamesLoading = false;
            }
          });
        } else if (this.dataSource === DataSourceType.LandingLayer) {

        }

      }
    }

    if (tabName === 'addtional-setting-tab') {

      let processName = this.currentWorkSheetName
        ? this.clientInfoForm.get('processName').value.replace('DI_', 'RSN_') + '_' + this.currentWorkSheetName
        : this.clientInfoForm.get('processName').value.replace('DI_', 'RSN_');

      this.formValues.ruleSetName = processName;
      this.processConfigurationForm.get('ruleSetName')?.setValue(processName);
    }

  }

  destinationValidationSection() {
    var processConfigurationForm = this.processConfigurationForm;
    if (this.dataSource === DataSourceType.DataBricks) {
      let deltaContainerControl = processConfigurationForm.get('deltaContainerName');
      if (!deltaContainerControl.value && processConfigurationForm.get('deltaStorageAccountId').touched && processConfigurationForm.get('deltaStorageAccountId').value) {
        deltaContainerControl.setErrors({ required: true });
        deltaContainerControl.markAsTouched();
        this.databaseDestinationSettingsError = ModalMessages.NoContainerName;
      }
      if (!processConfigurationForm.get('deltaSource').value && processConfigurationForm.get('deltaSource')?.touched) {
        processConfigurationForm.get('deltaSource').setErrors({ required: true });
        processConfigurationForm.get('deltaSource').markAsTouched();
        //this.handleFormChange('deltaSource');
      }


    }
  }

  onDatabaseNameChange(event: any) {
    //this.processConfigurationForm.get("databaseConfigurationId").setValue(event.target.value);
    let selValue = this.databaseNames.find(x => x.id === +event.id).databaseName
    if (this.dataSource === DataSourceType.Default) {
      this.processConfigurationForm.get("databaseName").setValue(selValue, { emitEvent: false });
    } else if (this.dataSource === DataSourceType.DataBricks) {
      this.processConfigurationForm.get("databaseName").setValue(selValue, { emitEvent: false });
    }
    this.formValues.databaseName = this.processConfigurationForm.get('databaseName').value;
    this.formValues.databaseConfigurationId = event.id;
    this.formValues.deltaServerNameId = this.processConfigurationForm.get('deltaServerNameId').value;
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
  }



  get sanitizedWorksheetId(): string {
    return 'btn' + this.currentWorkSheetName.replace(/\s+/g, '-');
  }

  getRequiredFieldNames(formGroup: FormGroup | FormArray): string[] {
    const requiredFields: string[] = [];

    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);

      if (control instanceof FormGroup || control instanceof FormArray) {
        requiredFields.push(...this.getRequiredFieldNames(control));
      } else {
        const validator = control?.validator;
        if (validator) {
          const validationResult = validator(control);
          if (validationResult && validationResult['required']) {
            requiredFields.push(key);
          }
        }
      }
    });

    return requiredFields;
  }

  onSubmit() {
    //debugger;
    this.isSubmitted = true;
    this.formValues.frmSubmitted = false;
    if (!this.validateClientSettings()) return;
    if (this.skipHeadersRowsIsInvalid || this.skipFooterRowsIsInvalid) {
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      const additionalSettingTab = document.getElementById('addtional-setting-tab') as HTMLElement;
      additionalSettingTab.click();
      additionalSettingTab.classList.add("active");
      return;
    }

    let invalidFields = [];
    if (this.dataSource === DataSourceType.Default) {
      invalidFields = this.getInvalidFields(this.processConfigurationForm);
    } else {

      this.processConfigurationForm.get('databaseName')?.clearValidators();
      this.processConfigurationForm.get('databaseName')?.updateValueAndValidity();

      invalidFields = this.getInvalidFields(this.processConfigurationForm);
    }

    if (invalidFields.length > 0) {
      this.processConfigurationFormHasError.emit(true);
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      return
    }

    //prepare the data
    this.formValues.processName = this.clientInfoForm.get('processName').value;
    this.formValues.description = this.clientInfoForm.get('description').value;
    this.formValues.delimiter = this.processConfigurationForm.get('delimiter').value;
    this.formValues.flexCheckHasHeaders = this.processConfigurationForm.get('flexCheckHasHeaders').value;
    this.formValues.txtQuoteCharacter = this.processConfigurationForm.get('txtQuoteCharacter').value;
    //this.formValues.txtEscapeCharacter = this.processConfigurationForm.get('txtEscapeCharacter').value;
    // this.formValues.flexCheckOrderByColumnListForDedup = this.processConfigurationForm.get('order_by_column_list_for_dedup').value;
    if (this.formValues.flexCheckOrderByColumnListForDedup) {
      this.formValues.order_by_column_list_name = this.processConfigurationForm.get('order_by_column_list_name').value;
      this.formValues.order_by_column_list_name_sort_dir = this.processConfigurationForm.get('order_by_column_list_name_sort_dir').value;
      this.formValues.order_by_column_list_for_dedup = this.processConfigurationForm.get('order_by_column_list_name').value + ' ' + this.processConfigurationForm.get('order_by_column_list_name_sort_dir').value;
    } else {
      this.formValues.order_by_column_list_for_dedup = "";
    }

    this.formValues.keep_first_row = this.formValues.order_by_column_list_name_sort_dir === 'asc' ? true : false;
    this.formValues.ignore_duplicate_rows = this.processConfigurationForm.get('ignore_duplicate_rows').value;

    if (this.keyColumnsSelected.length > 0) {
      this.formValues.csv_column_name_list = this.keyColumnsSelected;
      //this.formValues.ignore_duplicate_rows = true;
    } else {
      this.formValues.csv_column_name_list = "";
      //this.formValues.ignore_duplicate_rows = false;
    }

    this.formValues.do_not_archive_file = this.processConfigurationForm.get('do_not_archive_file').value;

    this.formValues.RegionId = this.clientInfoForm.get('RegionId').getRawValue.toString();
    this.formValues.SubRegionId = this.clientInfoForm.get('SubRegionId').getRawValue.toString();
    this.formValues.ClientId = this.clientInfoForm.get('ClientId').getRawValue.toString();

    this.formValues.tableName = this.processConfigurationForm.get('tableName').value;
    this.formValues.databaseName = this.processConfigurationForm.get('databaseName').value;
    this.formValues.databaseNameId = this.processConfigurationForm.get('databaseNameId').value;
    this.formValues.validate_fileschema = this.processConfigurationForm.get('is_validate_fileschema_with_target_table').value;
    this.formValues.drop_history_table = this.processConfigurationForm.get('drop_history_table').value;
    this.formValues.drop_main_table = this.processConfigurationForm.get('drop_main_table').value;
    this.formValues.databaseConfigurationId = this.processConfigurationForm.get('databaseConfigurationId').value.toString();
    //this.formValues.keep_first_row = this.processConfigurationForm.get('isSkipRow').value;
    this.formValues.skip_header_rows = +this.processConfigurationForm.get('skip_header_rows').value;
    this.formValues.skip_footer_rows = +this.processConfigurationForm.get('skip_footer_rows').value;
    this.formValues.region = this.regionName;
    this.formValues.subRegion = this.subRegionName;
    this.formValues.clientName = this.clientName;
    this.formValues.mergeData = this.processConfigurationForm.get('mergeData').value;
    this.formValues.createHistoryTable = this.processConfigurationForm.get('createHistoryTable').value;
    if (sessionStorage.getItem("emailID"))
      this.formValues.sender_communication_email = sessionStorage.getItem("emailID");
    else this.formValues.sender_communication_email = sessionStorage.getItem("upn");

    if (this.dataSource === DataSourceType.DataBricks) {
      this.formValues.deltaTableName = this.processConfigurationForm.get('deltaTableName').value;
      this.formValues.deltaServerNameId = +this.processConfigurationForm.get('deltaServerNameId').value;
      this.formValues.deltaJobId = this.processConfigurationForm.get('deltaJobId').value;
      this.formValues.deltaStorageAccountId = String(this.processConfigurationForm.get('deltaStorageAccountId').value);
      this.formValues.deltaContainerName = this.processConfigurationForm.get('deltaContainerName').value.trim();
      this.formValues.deltaSource = this.processConfigurationForm.get('deltaSource').value;
    } else {
      this.formValues.deltaTableName = '';
      this.formValues.deltaServerNameId = 0;
      this.formValues.deltaJobId = '';
      this.formValues.deltaStorageAccountId = '0';
      this.formValues.deltaContainerName = '';
      this.formValues.deltaSource = '';
    }

    this.formValues.securityGroups = this.selectedSecurityGroups;
    this.processConfigurationForm.get('frmSubmitted')?.setValue(true);
    this.formValues.frmSubmitted = true;
    this.formValues.ignoreSheet = false;
    this.formValues.newSheet == false;
    this.formValues.missingSheet == false;
    var campaignId = '';
    var internalCampaignId = '';
    if (this.fabService.isFabUserValue) {
      const campaignNameRawValue = this.clientInfoForm.get('campaignName')?.getRawValue();
      var fabAccount = this.fabService.FABUserAccount.find(x => x.campaignId === campaignNameRawValue);
      campaignId = fabAccount?.campaignId;
      internalCampaignId = fabAccount?.internalCampaignId;
    }
    this.formValues.campaignId = campaignId;
    this.formValues.internalCampaignId = internalCampaignId;

    //add values for landing layer start
    //additional settings
    if (this.dataSource === DataSourceType.LandingLayer) {

      this.formValues.deltaStorageAccountId = String(this.processConfigurationForm.get('deltaStorageAccountId').value);
      this.formValues.deltaContainerName = this.processConfigurationForm.get('deltaContainerName').value.trim();
      this.formValues.landingLayerFileExtension = this.processConfigurationForm.get('landingLayerFileExtension')?.value;
      this.formValues.landingLayerRegex = this.regexList;
      this.formValues.landingLayerPrefix = this.processConfigurationForm.get('landingLayerPrefix')?.value;
      var landingLayerDateformat = this.processConfigurationForm.get('landingLayerDateformat')?.value;
      this.formValues.landingLayerDateformat = !landingLayerDateformat ? this.dateOnlyFormats[0].formatId : landingLayerDateformat;
      var landingLayerTimeformat = this.processConfigurationForm.get('landingLayerTimeformat')?.value;
      this.formValues.landingLayerTimeformat = !landingLayerTimeformat ? this.timeOnlyFormats[0].formatId : landingLayerTimeformat;
      //destination settings
      this.formValues.landingLayerAcceptedPath = this.processConfigurationForm.get('landingLayerAcceptedPath')?.value;
      this.formValues.landingLayerRejectedPath = this.processConfigurationForm.get('landingLayerRejectedPath')?.value;
      //add values for landing layer end
    } else {
      this.formValues.landingLayerFileExtension = [];
      this.formValues.landingLayerRegex = [];
    }
    //reselect process settings

    this.activeTab = 'client-tab';

    const clientTab = document.getElementById('client-tab') as HTMLElement;
    clientTab.click();
    clientTab.classList.add("active");


    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);
    this.processConfigurationFormHasError.emit(false);
    this.closeWindow.emit();
    //this.processConfigurationForm

  }
  validateProcessSettings(): boolean {

    if (!this.processConfigurationForm) return false;
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    if (!clientInfo) return false;
    let regionId = clientInfo.get('RegionId')?.value;
    let subRegionId = clientInfo.get('SubRegionId')?.value;
    let clientNameId = clientInfo.get('ClientId')?.value;
    let security_group = clientInfo.get('security_group')?.value;

    if (!regionId || !subRegionId || !clientNameId || (!security_group || security_group.length === 0)) {

      if (!regionId) {
        clientInfo.get('RegionId').setErrors({ 'required': true });
      } else {
        clientInfo.get('RegionId').setErrors(null);
      }

      if (!subRegionId) {
        clientInfo.get('SubRegionId').setErrors({ 'required': true });
      } else {
        clientInfo.get('SubRegionId').setErrors(null);
      }

      if (!clientNameId) {
        clientInfo.get('ClientId').setErrors({ 'required': true });
      } else {
        clientInfo.get('ClientId').setErrors(null);
      }

      if (!security_group || security_group.length === 0) {
        clientInfo.get('security_group').setErrors({ 'required': true });
      } else {
        clientInfo.get('security_group').setErrors(null);
      }


      return false;
    }

    return true;
  }
  validateClientSettings(): boolean {
    if (!this.processConfigurationForm) return false;
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    if (!clientInfo) return false;
    let regionId = clientInfo.get('RegionId')?.value;
    let subRegionId = clientInfo.get('SubRegionId')?.value;
    let clientNameId = clientInfo.get('ClientId')?.value;
    let security_group = clientInfo.get('security_group')?.value;

    if (!regionId || !subRegionId || !clientNameId || (!security_group || security_group.length === 0)) {

      if (!regionId) {
        clientInfo.get('RegionId').setErrors({ 'required': true });
      } else {
        clientInfo.get('RegionId').setErrors(null);
      }

      if (!subRegionId) {
        clientInfo.get('SubRegionId').setErrors({ 'required': true });
      } else {
        clientInfo.get('SubRegionId').setErrors(null);
      }

      if (!clientNameId) {
        clientInfo.get('ClientId').setErrors({ 'required': true });
      } else {
        clientInfo.get('ClientId').setErrors(null);
      }

      if (!security_group || security_group.length === 0) {
        clientInfo.get('security_group').setErrors({ 'required': true });
      } else {
        clientInfo.get('security_group').setErrors(null);
      }

      this.activeTab = 'client-tab';

      const clientTab = document.getElementById('client-tab') as HTMLElement;
      clientTab.click();
      clientTab.classList.add("active");

      this.toastr.error(ModalMessages.ClientSettingsInvalid);

      this.clientSettingsTabVisited = true;

      return false;
    } else {
      clientInfo.get('RegionId').setErrors(null);
      clientInfo.get('SubRegionId').setErrors(null);
      clientInfo.get('ClientId').setErrors(null);
    }

    return true;
  }

  getInvalidFields(formGroup: FormGroup | FormArray): string[] {
    const invalidFields: string[] = [];
    // const invalidFields: { field: string, value: any }[] = [];

    Object.keys(this.processConfigurationForm.controls).forEach((key) => {
      const control = formGroup.get(key);
      var value = control?.value;

      if (control instanceof FormGroup || control instanceof FormArray) {
        // Recursively handle nested groups or arrays
        invalidFields.push(...this.getInvalidFields(control));

      } else if (control?.invalid) {
        //console.log(`Checking nested control: ${key}, invalid fields: ${invalidFields}`);
        invalidFields.push(key);
        // const existingError = control.errors || {};
        // control.setErrors({...existingError});
        // control.markAsTouched({onlySelf : true});
        // control.updateValueAndValidity({onlySelf : true, emitEvent : false});
      }
    });
    return invalidFields;
  }

  getInvalidDatabricksFields(): Boolean {
    let deltaSourceValue = this.processConfigurationForm.get('deltaSource').value;
    if (this.formValues.deltaStorageAccountId && this.formValues.deltaTableName
      && this.formValues.deltaServerNameId && this.formValues.deltaJobId
      && deltaSourceValue && this.formValues.deltaContainerName) {
      return true;
    }
    return false;
  }

  isLandingLayerDestinationValid(): Boolean {
    if (this.formValues.deltaStorageAccountId && this.formValues.deltaContainerName
      && this.formValues.landingLayerAcceptedPath && this.formValues.landingLayerRejectedPath
    ) {
      return true;
    }
    return false;
  }

  landingLayerAcceptedPath = '';
  landingLayerRejectedPath = '';

  getProcessName(selType: string) {

    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    const regionId = clientInfo.get('RegionId')?.value;
    const subRegionId = clientInfo.get('SubRegionId')?.value;
    const clientId = clientInfo.get('ClientId')?.value;

    if (selType == 'R') {
      clientInfo.get('SubRegionId').setValue('');
      clientInfo.get('ClientId').setValue('');
      this.getSubRegion(regionId);
    }
    if (selType == 'S') {

      clientInfo.get('ClientId').setValue('');
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
      this.formValues.processName = `DI_${this.sliceChars(regionName, 0, 2)}${this.sliceChars(subRegionName, 0, 3)}${this.sliceChars(clientName, 0, 2)}`.toUpperCase();
      this.configService
        .checkProcessNameExists(this.formValues.processName)
        .subscribe({
          next: (response) => {
            if (response) {
              if (response.responseCode === 200) {
                //   let num = +this.sliceChars(this.formValues.processName,10,70);
                //   this.formValues.processName = `${this.formValues.processName}${num + 1}`;

                this.formValues.processName = response.result;


                let processName = this.formValues.processName;
                this.clientInfoForm.get('processName')?.setValue(processName);

                // const processName= this.formValues.processName;
                this.updateTableName(processName, this.currentWorkSheetName, this.processConfigurationForm);
                //lets emit the values to parent to update the tableName and processName
                this.formValues.tableName = this.processConfigurationForm.get('tableName').value;
                this.formValues.processName = this.clientInfoForm.get('processName').value;
                this.formValues.deltaTableName = this.processConfigurationForm.get('deltaTableName').value;
                this.formValues.ruleSetName = this.processConfigurationForm.get('ruleSetName')?.value;
                //this.updateConfigOnly.emit([this.formValues, this.columnDatatype, [], this.ruleSetNames]); //was removed during data profiling.  

                if (this.dataSource === DataSourceType.LandingLayer) {
                  const today = new Date();

                  //get todays year
                  const year = today.getFullYear();
                  const month = String(today.getMonth() + 1).padStart(2, '0');
                  const day = String(today.getDate()).padStart(2, '0');
                  //get todays month
                  //get todays date
                  this.landingLayerAcceptedPath = `landing\\${this.regionName}\\${this.subRegionName}\\${this.clientName}\\${processName}\\${year}\\${month}\\${day}`;
                  this.landingLayerRejectedPath = `reject\\${this.regionName}\\${this.subRegionName}\\${this.clientName}\\${processName}\\${year}\\${month}\\${day}`;
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
      // this.clientInfoForm.get('processName')?.setValue(this.formValues.processName);
      // this.processConfigurationForm.get('tableName')?.setValue(this.formValues.processName.replace('DI_', 'tbImport_'));
      // this.processConfigurationForm.get('deltaTableName')?.setValue(this.formValues.processName.replace('DI_', 'tbImport_'));
    } else {
      this.clientInfoForm.get('processName')?.setValue('');
    }
  }

  //this is triggered as well from file-upload.component.ts when user switches from one sheet to another
  updateTableName(processName: string, currentWorkSheetName?: string, processConfigurationForm?: FormGroup) {
    this.isSubmitted = false; //reset 
    processConfigurationForm.get('processName')?.setValue(processName);

    const currentSheet = currentWorkSheetName?.trim();
    if (currentSheet) {
      processName += `_${cleanColumnName(currentSheet)}`;
    }
    //check if table name contains the filename, if yes then replace the filename with current process name    
    if (!processConfigurationForm.get('tableName').value && !processName.startsWith('_')) {
      processConfigurationForm.get('tableName')?.setValue(processName.replace('DI_', 'tbImport_'));
    }
    processConfigurationForm.get('deltaTableName')?.setValue(processName.replace('DI_', 'tbImport_'));
    processConfigurationForm.get('ruleSetName')?.setValue(processName.replace('DI_', 'RSN_'));

  }

  setUpdatedTableName(processName: string, table_name: string, currentWorkSheetName?: string, processConfigurationForm?: FormGroup) {

    processConfigurationForm
      .get('processName')
      ?.setValue(processName);


    const currentSheet = currentWorkSheetName?.trim();
    if (currentSheet) {
      processName += `_${currentSheet}`;
    }
    processConfigurationForm
      .get('tableName')
      ?.setValue(table_name);
    processConfigurationForm
      .get('deltaTableName')
      ?.setValue(table_name);
  }

  sliceChars(name: string, start: number, end: number): string {
    const val = name.replace(/[^a-zA-Z0-9]/g, '');
    return val.slice(start, end);
  }

  getRegionBySecurityGroup() {
    this.dsRegion = [];
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfoForm') as FormGroup;
    clientInfo?.get('RegionId')?.disable();
    this.isRegionLoading = true;

    this.clientInfoForm.get('isExternalProject').disable();
    this.dsService.getRegion2().subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {
          this.dsRegion = response.result.map(r => ({ id: r.region_ident, name: r.region }));
          this.dsREgionTemp = [...this.dsRegion];
          if (this.fabService.isFABUser$) {
            if (clientInfo.get('isExternalProject').value === true) {
              clientInfo.get('campaignName')?.enable();
              if (clientInfo.get('campaignName').value) {
                clientInfo.get('RegionId')?.disable();
              }
            } else {
              clientInfo.get('RegionId')?.enable();
            }
          } else {
            clientInfo.get('RegionId')?.enable();
          }
          this.clientInfoForm.get('isExternalProject').enable();

          this.isRegionLoading = false;
        }
      }
    });


  }
  getClient(regionId: string, subRegionId: string) {
    this.dsClient = [];
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    clientInfo.get('ClientId')?.disable();
    this.isClientLoading = true;
    this.dsService.getClients(regionId, subRegionId).subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {
          this.dsClient = response.result.map(r => ({ id: r.client_ident, name: r.client_full_name }));
          if (this.fabService.isFABUser$) {
            //&& this.clientInfoForm.get('campaignName').value) {
            if (clientInfo.get('isExternalProject').value === true) {
              if (clientInfo.get('campaignName').value) {
                const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === clientInfo.get('campaignName').value).map(x => x.clientId);
                this.dsClient = this.dsClient
                  .filter((c) => allowedIds.includes(c.id))
                  .map(c => ({ id: c.id, name: c.name }));

                clientInfo.get('ClientId')?.setValue(this.dsClient.length === 1 ? this.dsClient[0].id : null);

                this.getProcessName('C');
              }
            } else {
              if (clientInfo.get('RegionId').value && clientInfo.get('SubRegionId')) {
                clientInfo.get('ClientId')?.enable();
              }
            }
          } else {
            if (clientInfo.get('RegionId').value && clientInfo.get('SubRegionId')) {
              clientInfo.get('ClientId')?.enable();
            }
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
  //     this.processConfigurationForm.get('RegionId')?.disable();
  //     this.dsService.getToken().then((token: string) => {
  //       if (token) {
  //         if (regionId) {
  //           this.isRegionLoading = true;

  //           if (regionId.length > 0)
  //             data.filter = `[{'filterName':'region_ident','filterValue':'${regionId}','operatorType':'in'}]`;

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
  //               // if(regionId.length == 1){
  //               //   this.processConfigurationForm.get('SubRegionId')?.disable();
  //               //   this.dsSubRegion = reg?.data
  //               //     ?.map((r) => ({
  //               //       id: r.subsubregion_code,
  //               //       name: r.subsubregion,
  //               //     }))
  //               //     .filter(
  //               //       (value, index, self) =>
  //               //         index === self.findIndex((t) => t.id === value.id)
  //               //     );
  //               //     this.isSubRegionLoading = false;
  //               //   this.processConfigurationForm.get('SubRegionId')?.enable();
  //               // }
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
    const clientInfo = this.clientInfoForm;// this.processConfigurationForm.get('clientInfo') as FormGroup;
    clientInfo.get('SubRegionId')?.disable();
    clientInfo.get('ClientId')?.disable();
    this.isSubRegionLoading = true;
    this.dsService.getSubRegions(regionId).subscribe({
      next: (response) => {
        if (response && response.responseMessage[0] == "Success") {
          this.dsSubRegion = response.result.map(r => ({ id: r.subsubregion_code, name: r.subsubregion }));

          if (this.fabService.isFabUserValue) {
            //&& this.clientInfoForm.get('campaignName').value) {
            if (clientInfo.get('isExternalProject').value === true) {
              if (clientInfo.get('campaignName').value) {
                const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === clientInfo.get('campaignName').value).map(x => x.subRegionId);
                this.dsSubRegion = this.dsSubRegion
                  .filter((r) => allowedIds.includes(r.id))
                  .map(r => ({ id: r.id, name: r.name }));
                clientInfo.get('SubRegionId')?.setValue(this.dsSubRegion.length === 1 ? this.dsSubRegion[0].id : '');

                this.getProcessName('S');
              }
            } else {
              if (clientInfo.get('RegionId').value) {
                clientInfo.get('SubRegionId')?.enable();
              }
            }




          } else {
            if (clientInfo.get('RegionId').value) {
              clientInfo.get('SubRegionId')?.enable();
            }
          }
          // if (this.clientInfoForm.get('RegionId').value) {
          //   clientInfo.get('SubRegionId')?.enable();
          // }

          this.isSubRegionLoading = false;
        }
      },
      error: error => {
        console.log(error);
      }
    });

  }
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

  onMergeData(event: any) {
    if (!event.target.checked) {
      this.processConfigurationForm.get('createHistoryTable').setValue(false);
    }
    this.formValues.mergeData = event.target.checked;
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);

  }

  onCloudSource(): void {
    let src = this.processConfigurationForm.get('deltaSource')?.value.trim();

    if (src && src.length === 0) {
      this.processConfigurationForm.get('deltaSource').setValue("");
    }

    if (src) {
      //this.processConfigurationForm.get('deltaSource').setValue(src);      
      this.processConfigurationForm.get('deltaSource').setValue(cleanBlobSourceLocation(src));
    }

    this.formValues.deltaSource = this.processConfigurationForm.get('deltaSource').value;
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames, this.processConfigurationForm]);

  }

  onFoderNameChange(event: any): void {
    // this.processConfigurationForm.get('deltaContainerName').setValue(event.target.value.trim());
    // this.formValues.deltaContainerName = this.processConfigurationForm.get('deltaContainerName').value;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
    this.handleFormChange('deltaContainerName');
  }

  UserDefaultGroup: any = sessionStorage.getItem('UserDefaultGroup');
  securityGroupIsLoading: boolean = false;
  userSelectedSecurityGroupIsAdded: boolean = false;

  setupSearchGroup(): void {
    this.searchGroup$
      .pipe(
        debounceTime(300),
        switchMap((uniqueSearchTerm: string) => {
          const searchTerm = uniqueSearchTerm;//.split('-')[0];
          if (searchTerm.length >= 5) {
            this.securityGroups = [];
            this.securityGroupIsLoading = true;
            return this.tokenService.getAccessToken(['Group.Read.All']).pipe(

              switchMap((accessToken) =>
                this.processService.fetchSecurityGroups(searchTerm, accessToken)
              )
            );
          } else {
            this.securityGroups = [];
            return [];
          }
        })
      ).subscribe({
        next: (groups: any) => {

          let ctr = 0;
          var tempAny: any[] = [];
          if (this.selectedSecurityGroups.length === 0 && this.securityGroups.length === 0) {
            //this.securityGroups = 
            groups.value.slice(0, 10).forEach(c => {
              //this.securityGroups.push({ displayName: c.displayName, id: c.id });
              tempAny.push({ id: c.id, displayName: c.displayName });
            });

            this.securityGroups = tempAny;

          } else {

            //this.securityGroups = groups.value.slice(0,10);
            //groups.value.forEach((val, i) => {
            // for(var i = 0; i < groups.value.length;i++){
            //   let val = groups.value[i];
            //   if (this.selectedSecurityGroups.find(x => x.securityGroupId !== val.id)) {
            //     if(this.securityGroups.length === 20){
            //       break;
            //     }
            //     this.securityGroups.push({ id: val.id, displayName: val.displayName });
            //   }

            // };

            //this.securityGroups = groups.value.slice(0,10);

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

          //console.log(this.securityGroups);
          //this.securityGroups = groups.value.slice(0, 10);
          if (this.formValues?.flpConfigurationId) {

          } else {
            if (!this.userSelectedSecurityGroupIsAdded) {
              //&& this.securityGroups[0].displayName.toLowerCase() === sessionStorage.getItem('UserDefaultGroup').toLowerCase() 
              //a condition used if the SGs has same names
              if (this.securityGroups.length > 0 && this.securityGroups[0].displayName.toLowerCase() === sessionStorage.getItem('UserDefaultGroup').toLowerCase()) { //TODO: 
                let tempSG = this.securityGroups.find(x => x.displayName.toLowerCase() === sessionStorage.getItem('UserDefaultGroup').toLowerCase() && x.id === sessionStorage.getItem('GUID'));
                this.clientInfoForm?.get('security_group').setValue([tempSG]);
                this.selectedSecurityGroups.push({ securityGroupId: tempSG.id, securityGroupName: tempSG.displayName, userSelectedGroup: false });
                this.securityGroups = [];
                this.userSelectedSecurityGroupIsAdded = true;
              }

              //this.onSearchRuleSetName(this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));
              this.getRuleSetNamesBySecGrpIds(this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));

            }


          }

          this.securityGroupIsLoading = false;
        },
        error: (error) => {
          console.error('Error fetching security groups', error);
        }
      });
  }

  compareWithFn(item1: any, item2: any): boolean {
    return item1 && item2 ? item1 === item2 : item1 === item2;
  }



  fnCompareWithRuleSetNames(item: RuleSetNames, value: any): boolean {
    return item?.ruleSetNameId === value;
  }



  onSecurityGroupSelect(selectedGroup: any): void {
    // if (
    //   selectedGroup &&
    //   !this.selectedSecurityGroups.some((group) => group.securityGroupId === selectedGroup.id)
    // ) {
    //   this.selectedSecurityGroups.push(selectedGroup);
    // }

    this.selectedSecurityGroups = [];
    let sgs: { id: string, displayName: string }[] = [];;
    selectedGroup.forEach(g => {
      this.selectedSecurityGroups.push({ securityGroupId: g.id, securityGroupName: g.displayName, userSelectedGroup: false });
      sgs.push({ id: g.id, displayName: g.displayName });
    });
    this.clientInfoForm.get('security_group').setValue(sgs); //added to fix when a new SG is added it will retain
    //lets get all rulesetnames

    this.getRuleSetNamesBySecGrpIds(this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));

    if (!this.selectedSecurityGroups.find(x => x.securityGroupId === sessionStorage.getItem('GUID'))) {
      this.toastr.info(`${sessionStorage.getItem('UserDefaultGroup')} is not included.`);
    }

    //clear the dropdown options after selection
    this.securityGroups = [];
  }


  // Handle the search term emitted by the (search) event
  onSearchGroup(searchTerm: string): void {

    if (searchTerm.length >= 5) {
      const uniqueSearchTerm = `${searchTerm}`; // Append a timestamp -${new Date().getTime()
      this.searchGroup$.next(uniqueSearchTerm); // Emit the unique search term
    }
  }

  onBlurSearchGroup(event: any) {
    this.securityGroups = [];
  }


  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentWorkSheetName'] && !changes['currentWorkSheetName'].firstChange) {
      const clientTab = document.getElementById('client-tab') as HTMLElement;
      if (clientTab) clientTab.click();
    }

    // if (changes['columnDatatype']) {
    //   this.availableColumns1 = [...this.columnDatatype.filter(x => x.willInclude)];
    //   this.availableColumns2 = [...this.columnDatatype.filter(x => x.willInclude)];
    //   this.processConfigurationForm?.get('ruleColumnName')?.disable();
    // }


    if (changes['formValues']) {
      const prev = changes['formValues'].previousValue;
      const curr = changes['formValues'].currentValue;
      // if (prev !== undefined && curr !== undefined){
      //   if (prev.processName !== curr.processName) {
      //     //set the current

      //     //do this because once you click on -> in UI Validation, clientInfoForm.get('processName') is empty
      //     this.formValues.processName = curr.processName.trim().length === 0 ? prev?.processName : curr?.processName;
      //     this.clientInfoForm.get('processName')?.setValue(this.formValues.processName); 
      //     console.log(`myProperty changed from ${prev} to ${curr}`);
      //   }
      // }
    }
  }

  onIsSkipRow(event: any) {

    if (event.target.checked) {
      this.processConfigurationForm.get('skip_header_rows')?.enable();
      this.processConfigurationForm.get('skip_footer_rows')?.enable();
    } else {
      this.processConfigurationForm.get('skip_header_rows').setValue(0);
      this.processConfigurationForm.get('skip_footer_rows').setValue(0);
      this.processConfigurationForm.get('skip_header_rows')?.disable();
      this.processConfigurationForm.get('skip_footer_rows')?.disable();

      this.formValues.skip_header_rows = 0;
      this.formValues.skip_footer_rows = 0;
      this.previewToParent2(this.formValues);
    }
  }

  onStorageAccountChange(storageAccountId: number) {
    this.databaseDestinationSettingsError = '';

    const control = this.processConfigurationForm.get('deltaContainerName');
    this.formValues.deltaContainerName = this.storageAccount.find(s => s.storageAccountId === +storageAccountId)?.containerName || '';
    if (this.formValues.deltaContainerName) {
      control.setValue(this.formValues.deltaContainerName);
    } else {
      control.setValue('');
      this.databaseDestinationSettingsError = ModalMessages.NoContainerName;
    }

    control.markAsTouched();
    control.markAsDirty();
    control.updateValueAndValidity();
    this.handleFormChange('deltaStorageAccountId');
    this.handleFormChange('deltaContainerName');
  }

  onDeltaTableName() {
    this.handleFormChange('deltaTableName');
    // this.formValues.deltaTableName = this.processConfigurationForm.get('deltaTableName').value;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onDeltaJobId() {
    this.handleFormChange('deltaJobId');
    // this.formValues.deltaJobId = this.processConfigurationForm.get('deltaJobId').value;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onDoNotArchive(event: any) {
    this.handleFormChange('do_not_archive_file');
    // this.formValues.do_not_archive_file = event.target.checked;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onDropMainTable(event: any) {
    this.handleFormChange('drop_main_table');
    // this.formValues.drop_main_table = event.target.checked;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onValidateSchema(event: any) {
    this.handleFormChange('validate_fileschema');
    // this.formValues.validate_fileschema = event.target.checked;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onDropHistoryTable(event: any) {
    this.handleFormChange('drop_history_table');
    // this.formValues.drop_history_table = event.target.checked;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  onCreateHistoryTable(event: any) {
    this.handleFormChange('createHistoryTable');
    // this.formValues.createHistoryTable = event.target.checked;
    // this.updateConfigOnly.emit([this.formValues, this.columnDatatype, this.excelFileRule, this.ruleSetNames]);
  }

  handleFormChange(fieldName: string, isCheckbox: boolean = false, event?: any): void {

    if (isCheckbox && event) {
      this.formValues[fieldName] = event.target.checked;
    } else {
      if (fieldName === 'validate_fileschema') {
        this.formValues[fieldName] = this.processConfigurationForm.get('is_validate_fileschema_with_target_table')?.value;
      } else {
        this.formValues[fieldName] = this.processConfigurationForm.get(fieldName)?.value;
      }
    }

    this.updateConfigOnly.emit([
      this.formValues,
      this.columnDatatype,
      this.excelFileRule,
      this.ruleSetNames,
      this.processConfigurationForm
    ]);

  }




  enableBtnCreateRule(): boolean {

    const form = this.processConfigurationForm;
    const ruleType = form.get('ruleType')?.value;
    const ruleColumnName = form.get('ruleColumnName')?.value;
    const ruleColumnName2 = form.get('ruleColumnName2')?.value;
    const subRuleType = form.get('subRuleType')?.value;
    const conditionType = form.get('conditionType')?.value;
    const patternType = form.get('patternType')?.value;
    const formatType = form.get('formatType')?.value;
    const aiPrompt = form.get('aiPrompt')?.value;
    const fromValue = form.get('fromValue')?.value;
    const toValue = form.get('toValue')?.value;
    const genericRule = form.get('ruleSetNames')?.value;
    const spName = form.get('spName')?.value;

    if (!ruleType) return true;

    if (ruleType === RuleTypeNames.Required && isNullUndefinedEmptyArrays(ruleColumnName)) return true;

    if (ruleType === RuleTypeNames.Unique && isNullUndefinedEmptyArrays(ruleColumnName)) return true;

    if (ruleType === RuleTypeNames.Custom && isNullUndefinedEmptyArrays(aiPrompt)) return true;

    if ((ruleType === RuleTypeNames.Format || ruleType === RuleTypeNames.Value) && (isNullUndefinedEmptyArrays(subRuleType) || isNullUndefinedEmptyArrays(ruleColumnName))) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.Pattern && isNullUndefinedEmptyArrays(patternType)) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.Length && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(formatType))) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.NumericRange && (isNullUndefinedEmptyArrays(String(fromValue)) || isNullUndefinedEmptyArrays(String(toValue)))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.NumericRange && (isNullUndefinedEmptyArrays(String(fromValue)) || isNullUndefinedEmptyArrays(String(toValue)))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.Length && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(formatType))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.ExactMatch && (isNullUndefinedEmptyArrays(formatType.trim()))) return true;

    if (ruleType === RuleTypeNames.GenericRules && isNullUndefinedEmptyArrays(genericRule)) return true;

    if (ruleType === RuleTypeNames.BEValidation && isNullUndefinedEmptyArrays(spName)) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.Comparison
      && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(ruleColumnName) || isNullUndefinedEmptyArrays(ruleColumnName2))) return true;
    return false;

  }

  tmp_newlyAddedRules: ExcelRule[] = [];
  checkRuleChange(existingRuleId: number) {
    //let  i =0;

    const ruleSetName = this.processConfigurationForm.get('ruleSetName')?.value;
    const ruleType = this.processConfigurationForm.get('ruleType').value;
    let columnNames = this.processConfigurationForm.get('ruleColumnName').value;
    let columnNames2 = this.processConfigurationForm.get('ruleColumnName2')?.value;
    const isCombination = this.processConfigurationForm.get('isCombinationRule')?.value;
    const subRuleId = this.processConfigurationForm.get('subRuleType').value;
    const patternId = this.processConfigurationForm.get('patternType')?.value ?? null;
    const conditionId = this.processConfigurationForm.get('conditionType')?.value ?? null;
    const fromValue = this.processConfigurationForm.get('fromValue')?.value ?? null;
    const toValue = this.processConfigurationForm.get('toValue')?.value ?? null;
    const aiPrompt = this.processConfigurationForm.get('aiPrompt')?.value;
    const conditionalValue = this.processConfigurationForm.get('formatType')?.value;
    const ruleSetDescription = this.processConfigurationForm.get('ruleSetDescription')?.value;
    const spName = this.processConfigurationForm.get('spName')?.value;
    const isAllowNullOrEmptySpaces = this.processConfigurationForm.get('isAllowNullOrEmptySpaces')?.value;
    let spNameId: number = 0;
    this.uiValidationRulesInvalid = false;
    if (ruleType !== 6) {
      if ((!columnNames || columnNames.length === 0) && !(ruleType === RuleTypeNames.Custom)) {
        return;
      }

    }

    if (ruleType !== RuleTypeNames.BEValidation && ruleType !== RuleTypeNames.Custom) {
      if (subRuleId !== SubRuleTypes.Comparison) {
        const missingColumns = columnNames.filter(
          col => !this.availableColumns1.some(x => x.DbColumnName === col)
        );

        if (missingColumns.length > 0) {
          this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
          return;
        }
      } else {
        if (!this.availableColumns1.find(x => x.DbColumnName === columnNames) || !this.availableColumns2.find(x => x.DbColumnName === columnNames2)) {
          this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
          return;
        }
      }
    }

    if (ruleType === 5 && ((!columnNames || columnNames.length === 0)
      || (!columnNames2 || columnNames2.length === 0)) && !(ruleType === RuleTypeNames.Custom || ruleType === RuleTypeNames.Value)) return;

    const rule = this.rules.find(x => x.ruleTypeId === ruleType);
    if (!rule) return;

    const condition = this.conditionalOperators.find(x => x.conditionalOperatorId === conditionId);

    let format: string = '';
    let ruleDescription: string = '';
    let displayColumns: string = '';
    let displayColumns2: string = '';
    const rules = this.excelFileRule;
    let existingRuleIndex = rules.findIndex(r => r.id === existingRuleId);
    let parquetRuleColumnName: string[] = [];
    if (existingRuleIndex >= 0 && subRuleId !== SubRuleTypes.Comparison && rule.ruleTypeId !== RuleTypeNames.Custom && rule.ruleTypeId !== RuleTypeNames.BEValidation) {
      const existingColumns = rules[existingRuleIndex].ruleColumnName;
      const combinedColumns = Array.from(new Set([...columnNames, ...existingColumns]));
      //parquetRuleColumnName = this.columnDatatype.filter(x=> columnNames.includes(x.ColumnName)).map(x=> x.DbColumnName);
      //const combinedParquetColumnName = Array.from(new Set([...colu]))
      // let existingColumns2: string[] = [];
      // let combinedColumns2: any[];

      // if (subRuleId === SubRuleTypes.Comparison) {
      //   existingColumns2 = this.excelFileRule[existingRuleIndex].ruleColumnName2;
      //   combinedColumns2 = Array.from(new Set([...columnNames2, ...existingColumns2]));
      // }

      //if adding (), then use combine
      if (existingRuleId === 0) {
        //displayColumns = formatWithOrAnd(combinedColumns, subRuleId);
        displayColumns = formatWithOrAnd(combinedColumns, isCombination, rule.ruleTypeId);
        // if (subRuleId === SubRuleTypes.Comparison) {
        //   displayColumns2 = formatWithOrAnd(combinedColumns2, isCombination, rule.ruleTypeId);
        // }

      } else {
        //if editing then just add the new columnNames,        
        //displayColumns = formatWithOrAnd(columnNames, subRuleId);        
        displayColumns = formatWithOrAnd(columnNames, isCombination, rule.ruleTypeId);
        // if (subRuleId === SubRuleTypes.Comparison) {
        //   displayColumns2 = formatWithOrAnd(columnNames2, isCombination, rule.ruleTypeId);
        // }
      }

    } else {
      if (ruleType !== RuleTypeNames.BEValidation) {
        if (subRuleId !== SubRuleTypes.Comparison && rule.ruleTypeId !== RuleTypeNames.Custom) {
          displayColumns = formatWithOrAnd(columnNames, isCombination, rule.ruleTypeId);
        }
        // if (subRuleId === SubRuleTypes.Comparison) {
        //   displayColumns2 = formatWithOrAnd(columnNames2, isCombination, rule.ruleTypeId);
        // }
      }
    }


    switch (rule.ruleTypeId) {
      case RuleTypeNames.Required: //required  
        ruleDescription = isCombination ? `At least one of the following columns is required: ${displayColumns}` : `${displayColumns} ${columnNames.length === 1 ? 'is' : 'are'} required and must not be empty.`;
        break;

      case RuleTypeNames.Unique: //unique        
        ruleDescription = `Values in ${displayColumns} must be ${rule.ruleTypeName}. No duplicates allowed.`
        break;

      case RuleTypeNames.Custom: //custom
        columnNames = []; //set to empty 
        ruleDescription = this.processConfigurationForm.get('aiPrompt').value.replace(/\B@(?=\w)/g, '').trim();
        break;

      case RuleTypeNames.Format: //format

        //pattern
        switch (subRuleId) {

          case SubRuleTypes.Pattern:
            if (existingRuleIndex >= 0) {
              if (rules[existingRuleIndex].patternId !== patternId) {
                //lets trick existingRuleIndex to -1 to create a new excelFileRule
                existingRuleIndex = -1;

                //since it's a new pattern, reset the columns names
                displayColumns = this.processConfigurationForm.get('ruleColumnName').value;
              }
            }

            ruleDescription = conditionalValue
              ? `${displayColumns} should match this \\${conditionalValue}\\ pattern`
              : (columnNames.length == 1)
                ? `${displayColumns} is a valid ${this.patterns.find(p => p.patternId === patternId).sing}`
                : `${displayColumns} are valid ${this.patterns.find(p => p.patternId === patternId).plu}`;
            break;
          case SubRuleTypes.Length:
            ruleDescription = `${displayColumns} string length should be ${condition.conditionalOperatorName} ${conditionalValue}`;
            break;
          case SubRuleTypes.NumericRange:
            if (toValue <= fromValue) {
              this.toastr.error('From and To Values are invalid.');
              return;
            }
            ruleDescription = (displayColumns.length == 1)
              ? `${displayColumns} value should be between ${fromValue} to ${toValue}`
              : `${displayColumns} values should be between ${fromValue} to ${toValue}`;
            break;
        }

        if (aiPrompt) {
          ruleDescription = (displayColumns.length == 1)
            ? `${displayColumns} is ${aiPrompt}`
            : `${displayColumns} are ${aiPrompt}`;
        }

        if (isAllowNullOrEmptySpaces) {
          ruleDescription += ' and can be null or empty.';
        }

        break;

      case RuleTypeNames.Value: //Value

        if (subRuleId === SubRuleTypes.Length || subRuleId === SubRuleTypes.Numeric) {
          let conditionalOperators = this.conditionalOperators.find(x => x.conditionalOperatorId === this.processConfigurationForm.get('conditionType').value);
          if (!conditionalOperators) return;


          if (!conditionalValue) return;

          switch (conditionalOperators.conditionalOperatorId) {
            case 1: //Equal
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} to ${conditionalValue}`;
              break;
            case 2: //Not Equal
              ruleDescription = `${displayColumns} should ${conditionalOperators.conditionalOperatorName} to ${conditionalValue}`;
              break;
            case 3: //Greater Than
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${conditionalValue}`;
              break;
            case 4: //Less Than
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${conditionalValue}`;
              break;
            case 5: //Greater Than or Equal To
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${conditionalValue}`;
              break;
            case 6: //Less Than or Equal To
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${conditionalValue}`;
              break;
          }
        }

        if (subRuleId === SubRuleTypes.ExactMatch) {
          ruleDescription = (displayColumns.length === 1)
            ? `${displayColumns} column value should match exactly the value of '${conditionalValue}'`
            : `${displayColumns} column values should match exactly the value of '${conditionalValue}'`;
        }

        if (subRuleId === SubRuleTypes.Comparison) {
          let conditionalOperators = this.conditionalOperators.find(x => x.conditionalOperatorId === conditionId);

          if (!conditionalOperators) return;

          switch (conditionalOperators.conditionalOperatorId) {
            case 1: //Equal
              ruleDescription = `${columnNames} should be ${conditionalOperators.conditionalOperatorName} to ${columnNames2}`;
              break;
            case 2: //Not Equal
              ruleDescription = `${columnNames} should ${conditionalOperators.conditionalOperatorName} to ${columnNames2}`;
              break;
            case 3: //Greater Than
              ruleDescription = `${columnNames} should be ${conditionalOperators.conditionalOperatorName} ${columnNames2}`;
              break;
            case 4: //Less Than
              ruleDescription = `${columnNames} should be ${conditionalOperators.conditionalOperatorName} ${columnNames2}`;
              break;
            case 5: //Greater Than or Equal To
              ruleDescription = `${columnNames} should be ${conditionalOperators.conditionalOperatorName} ${columnNames2}`;
              break;
            case 6: //Less Than or Equal To
              ruleDescription = `${columnNames} should be ${conditionalOperators.conditionalOperatorName} ${columnNames2}`;
              break;
          }



          // ruleDescription = `Compare column ${columnNames} to column ${columnNames2}`;
        }

        if (subRuleId === SubRuleTypes.NumericRange) {

          if (toValue <= fromValue) {
            this.toastr.error('From and To Values are invalid.');
            return;
          }

          ruleDescription = (displayColumns.length == 1)
            ? `${displayColumns} value should be between ${fromValue} to ${toValue}`
            : `${displayColumns} values should be between ${fromValue} to ${toValue}`;
        }

        if (isAllowNullOrEmptySpaces) {
          ruleDescription += ' and can be null or empty.';
        }
        break;

      case RuleTypeNames.BEValidation:
        let spDetail = this.spNames.find(sp => sp.spNameId === spName);
        ruleDescription = `Execute ${spDetail?.spName}`;
        spNameId = spDetail.spNameId;
        break;
    }



    if (existingRuleIndex !== -1) {


      let currentRule = rules[existingRuleIndex];

      var tmpRule = {
        id: currentRule.id,
        ruleSetNameId: currentRule.ruleSetNameId,
        ruleSetName: currentRule.ruleSetName,
        ruleTypeId: rule.ruleTypeId,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames, //Array.isArray(columnNames) && columnNames.length > 0 ? columnNames : [],
        ruleColumnName2: isNullUndefinedEmptyArrays(columnNames2) ? "" : columnNames2, //Array.isArray(columnNames2) && columnNames2.length > 0 ? columnNames2 : [],
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: conditionalValue,
        prompt: aiPrompt || aiPrompt === undefined ? '' : aiPrompt,
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: currentRule.isGlobal, //set to false, because process rules are not global
        ruleSetType: currentRule.ruleSetType,  //1 for process rule, 2 for generic rule
        description: ruleSetDescription,
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: false
      };

      var ruleAlreadyExists = rules.find(x =>
        x.id !== tmpRule.id &&
        x.ruleTypeId === tmpRule.ruleTypeId &&
        x.subRuleId === tmpRule.subRuleId &&
        arraysEqual(x.ruleColumnName, tmpRule.ruleColumnName) &&
        //x.ruleColumnName === tmpExceRule.ruleColumnName &&
        x.ruleColumnName2 === tmpRule.ruleColumnName2 &&
        x.isCombinationRule === tmpRule.isCombinationRule &&
        x.format === tmpRule.format &&
        x.prompt === tmpRule.prompt &&
        x.patternId === tmpRule.patternId &&
        x.conditionId === tmpRule.conditionId &&
        x.fromValue === tmpRule.fromValue &&
        x.toValue === tmpRule.toValue &&
        //x.isGlobal === tmpExceRule.isGlobal &&
        x.isAllowNullOrEmptySpaces === tmpRule.isAllowNullOrEmptySpaces
      );

      if (ruleAlreadyExists) {
        this.toastr.warning('Rule already exists. Please create new one.');
        return;
      }

      rules[existingRuleIndex] = {
        id: currentRule.id,
        ruleSetNameId: currentRule.ruleSetNameId,
        ruleSetName: currentRule.ruleSetName,
        ruleTypeId: rule.ruleTypeId,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames, //Array.isArray(columnNames) && columnNames.length > 0 ? columnNames : [],
        ruleColumnName2: isNullUndefinedEmptyArrays(columnNames2) ? "" : columnNames2, //Array.isArray(columnNames2) && columnNames2.length > 0 ? columnNames2 : [],
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: conditionalValue,
        prompt: aiPrompt || aiPrompt === undefined ? '' : aiPrompt,
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: currentRule.isGlobal, //set to false, because process rules are not global
        ruleSetType: currentRule.ruleSetType,  //1 for process rule, 2 for generic rule
        description: ruleSetDescription,
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: false
      }

      if (rules[existingRuleIndex].ruleSetType === 2 || rules[existingRuleIndex].isGlobal) {
        var tmpGlobalRuleDescription = rules[existingRuleIndex].ruleDescription;
      }

      if (this.tmp_existingRuleSetNameIds.has(tmpGlobalRuleDescription)) {
        this.tmp_existingRuleSetNameIds.delete(tmpGlobalRuleDescription);
      }



    } else {


      const maxId = rules.length > 0 ? Math.max(...rules.map(r => r.id)) : 0;
      const nextId = maxId + 1;

      var tmpExceRule = {
        id: nextId,
        ruleSetNameId: '',
        ruleSetName: ruleSetName,
        ruleTypeId: rule.ruleTypeId,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames, //Array.isArray(columnNames) && columnNames.length > 0 ? columnNames : [],
        ruleColumnName2: isNullUndefinedEmptyArrays(columnNames2) ? "" : columnNames2, // Array.isArray(columnNames2) && columnNames2.length > 0 ? columnNames2 : [],
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: conditionalValue,
        prompt: aiPrompt,
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: false, //set to false, because process rules are not global
        ruleSetType: 1, //1 for process rule, 2 for generic rule
        description: ruleSetDescription,
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: false
      };

      var ruleAlreadyExists = rules.find(x =>
        x.ruleTypeId === tmpExceRule.ruleTypeId &&
        x.subRuleId === tmpExceRule.subRuleId &&
        arraysEqual(x.ruleColumnName, tmpExceRule.ruleColumnName) &&
        //x.ruleColumnName === tmpExceRule.ruleColumnName &&
        x.ruleColumnName2 === tmpExceRule.ruleColumnName2 &&
        x.isCombinationRule === tmpExceRule.isCombinationRule &&
        x.format === tmpExceRule.format &&
        x.prompt === tmpExceRule.prompt &&
        x.patternId === tmpExceRule.patternId &&
        x.conditionId === tmpExceRule.conditionId &&
        x.fromValue === tmpExceRule.fromValue &&
        x.toValue === tmpExceRule.toValue &&
        x.isIrrelevant === tmpExceRule.isIrrelevant &&
        x.isGlobal === tmpExceRule.isGlobal &&
        x.isAllowNullOrEmptySpaces === tmpExceRule.isAllowNullOrEmptySpaces
      );

      if (ruleAlreadyExists) {
        this.toastr.warning('Rule already exists. Please create new one.');
        return;
      }
      var tmpNewRule: ExcelRule = {
        id: nextId,
        ruleSetNameId: '',
        ruleSetName: ruleSetName,
        ruleTypeId: rule.ruleTypeId,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames, //Array.isArray(columnNames) && columnNames.length > 0 ? columnNames : [],
        ruleColumnName2: isNullUndefinedEmptyArrays(columnNames2) ? "" : columnNames2, // Array.isArray(columnNames2) && columnNames2.length > 0 ? columnNames2 : [],
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: conditionalValue,
        prompt: aiPrompt,
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: false, //set to false, because process rules are not global
        ruleSetType: 1, //1 for process rule, 2 for generic rule
        description: ruleSetDescription,
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: false
      };

      this.tmp_newlyAddedRules.push(tmpNewRule);
      this.excelFileRule.push(tmpNewRule);
    }

    this.helperUtil.sortRuleSet(this.excelFileRule);

    this.clearRule();
  }

  //private existingRuleCount : number = 0;
  onAddRule() {
    //this.existingRuleCount = this.excelFileRule.length;
    if (this.processConfigurationForm.get('ruleType').value === RuleTypeNames.GenericRules) {
      let selectedGenericRules = this.ruleSetNames.filter(x => x.ruleSetNameId === this.processConfigurationForm.get('ruleSetNames')?.value);
      this.onChangeRuleSetNames(selectedGenericRules);
      return;
    }
    this.checkRuleChange(0);
  }

  addRuleToFormValues() {
    this.formValues.ruleSet = this.excelFileRule;
  }

  onEditRule(existingRuleId: number) {
    this.checkRuleChange(existingRuleId);
    this.findAllIrrelevant();
  }

  validateRule() {
    this.isSubmitted = true;

    // const formValues = this.processConfigurationForm.value;
    const rules: RuleType[] = [];
    if (this.excelFileRule.length > 0) {
      this.excelFileRule.forEach((rule) => {
        rules.push({
          //rule_name: this.dataInsiderService.getRule().find(x => x.ruleName == rule.ruleType)?.ruleName,
          rule_name: this.rules.find(x => x.ruleTypeId === rule.ruleTypeId)?.ruleTypeName,
          columns: rule.ruleColumnName,
          //is_combination_rule: rule.is_combination_rule,
          rule_description: rule.description
        });
      });
    }
    //console.log(rules);
    const file = this.file;
    if (file) {
      this.dataInsiderService.validateRule(file, rules).subscribe({
        next: (res: any) => {
          //console.log(res);
          if (res.status === 'success') {
            this.toastr.success(res.message);
          } else {
            this.toastr.error(res.message);
          }
        },
        error: (err) => {
          console.error(err);
          this.toastr.error(err);
        }
      });
    } else {
      this.toastr.error('Please upload a file to validate the rules.');
    }
  }

  onRuleTypeChange(ruleType: RuleTypes) {
    if (ruleType) {
      this.processConfigurationForm.get('subRuleType')?.enable();
      this.processConfigurationForm.get('subRuleType')?.setValue(null);


      const allSame = this.subRules?.length > 0 ? this.subRules.every(sub => sub.ruleTypeId === ruleType.ruleTypeId) : false;
      if (!allSame) {
        this.getSubRules(ruleType.ruleTypeId).subscribe(subRules => {
          this.subRules = subRules;
        });
      }

      // this.filteredSubRules = [];
      // this.filteredSubRules = this.subRules.filter(x => x.ruleTypeId == ruleType);
      this.processConfigurationForm.get('ruleColumnName').enable();
      this.processConfigurationForm.get('spName')?.setValue(null);
      this.processConfigurationForm.get('aiPrompt')?.setValue('');
      this.processConfigurationForm.get('isCombinationRule')?.disable();
    }
  }
  showFormat: boolean = false;
  onSubRuleTypeChange(subRuleType: SubRule) {
    //this.patterns = [];
    // this.processConfigurationForm.get('ruleColumnName2')?.disable();
    if (subRuleType) {
      const allSame = this.patterns?.length > 0 ? this.patterns.every(pat => pat.subRuleId === subRuleType.subRuleId) : false;
      if (!allSame) {
        this.getPatterns(subRuleType.subRuleId).subscribe(patterns => {
          this.patterns = patterns;
          this.processConfigurationForm.get('patternType')?.setValue(null);
        });
      } else {
        this.processConfigurationForm.get('patternType')?.setValue(null);
      }
      this.processConfigurationForm.get('formatType')?.setValue('');
      //this.processConfigurationForm.get('ruleColumnName')?.setValue(null);
      this.selectedSubRule = subRuleType.subRuleName;
      this.willShowFormat(subRuleType.subRuleId);
    }
  }

  willShowFormat(subRuleId: number) {
    this.showFormat = subRuleId === SubRuleTypes.Length; //|| subRuleId === SubRuleTypes.Pattern;
  }

  clearRule() {

    this.existingRuleId = 0;
    this.showFormat = false;
    this.selectedSubRule = '';
    this.processConfigurationForm.get('ruleType')?.enable();
    this.processConfigurationForm.get('ruleType')?.setValue(null);
    this.processConfigurationForm.get('ruleColumnName')?.setValue(null);
    this.processConfigurationForm.get('ruleColumnName')?.disable();
    this.processConfigurationForm.get('ruleColumnName2')?.setValue(null);

    this.processConfigurationForm.get('subRuleType')?.enable();
    this.processConfigurationForm.get('subRuleType')?.setValue('');
    this.processConfigurationForm.get('aiPrompt')?.setValue('');
    this.processConfigurationForm.get('patternType')?.enable();
    this.processConfigurationForm.get('patternType')?.setValue('');
    this.processConfigurationForm.get('conditionType')?.enable();
    this.processConfigurationForm.get('conditionType')?.setValue(null);
    this.processConfigurationForm.get('fromValue')?.setValue(0);
    this.processConfigurationForm.get('toValue')?.setValue(0);
    this.processConfigurationForm.get('formatType')?.setValue('');

    this.processConfigurationForm.get('isCombinationRule')?.setValue(false);
    this.processConfigurationForm.get('isAllowNullOrEmptySpaces')?.setValue(false);
    this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
    this.onClearRuleSetName();
  }

  onColumnNameChange(columnNames: any, controlName: string) {
    this.enableBtnCreateRule();
    this.enableCombination();

    if (this.processConfigurationForm.get('subRuleType')?.value !== SubRuleTypes.Comparison) {
      return;
    }

    const otherControlName = controlName === 'ruleColumnName' ? 'ruleColumnName2' : 'ruleColumnName';
    const otherSelected = this.processConfigurationForm.get(otherControlName)?.value;

    if (controlName === 'ruleColumnName') {
      this.availableColumns2 = this.columnDatatype.filter(col => col.ColumnName !== columnNames.ColumnName);
    } else {
      this.availableColumns1 = this.columnDatatype.filter(col => col.ColumnName !== columnNames.ColumnName);
    }

    // If the other control has selected the same value, clear it
    if (columnNames.ColumnName === otherSelected) {
      this.processConfigurationForm.get(otherControlName)?.setValue(null);
    }

    // If current selection is cleared, restore full list
    if (!columnNames.ColumnName) {
      this.availableColumns1 = this.columnDatatype.filter(col => col.ColumnName !== this.processConfigurationForm.get('ruleColumnName')?.value);
      this.availableColumns2 = this.columnDatatype.filter(col => col.ColumnName !== this.processConfigurationForm.get('ruleColumnName2')?.value);
    }




  }





  // private updateOtherSelectOptions(sourceControlName : string){
  //   const isSource1 = sourceControlName === 'ruleColumnName1';
  //   const sourceControl = this.processConfigurationForm.get(sourceControlName);
  //   const otherControl = isSource1 ? this.processConfigurationForm.get('ruleColumnName2') : this.processConfigurationForm.get('ruleColumnName');
  //   const selectedValue = sourceControl?.value;

  //   // Determine the options list to update
  //   const optionsToUpdate = isSource1 ? 'columnsForSelect2' : 'columnsForSelect1';

  //   if (selectedValue) {
  //     // Filter the master list
  //     const newOptions = this.columnDatatype.filter(
  //       (option) => option.ColumnName !== selectedValue
  //     );
  //     this[optionsToUpdate] = [...newOptions];

  //     // If the other select's value is the one just selected, clear it
  //     if (otherControl?.value === selectedValue) {
  //       otherControl.setValue(null);
  //     }
  //   } else {
  //     // If nothing is selected, reset to all options
  //     this[optionsToUpdate] = [...this.columnDatatype];
  //   }
  // }


  onCombinationRuleChange(e: any) {
    //this.checkRuleChange(ruleId);
  }

  removeRule(ruleId: number) {

    let excelFileRuleIndex = -1;
    let rule: ExcelRule = null;
    const rules = this.excelFileRule;
    rules.some((x, index) => {
      if (x.id === ruleId) {
        excelFileRuleIndex = index;
        rule = x;
        return true;
      }
      return false;
    });

    var currentRuleDesc = rules.find(x => x.id === ruleId).ruleDescription;

    if (this.formValues?.flpConfigurationId) {
      rules[excelFileRuleIndex].isActive = false;
    } else {
      //if (this.excelFileRule.findIndex(x=> x.id=== ruleId) in this.tmp_existingRuleSetNameIds)
      if (this.tmp_existingRuleSetNameIds.has(currentRuleDesc)) {
        this.tmp_existingRuleSetNameIds.delete(currentRuleDesc);
      }

      //splice
      this.tmp_newlyAddedRules = this.tmp_newlyAddedRules.filter(x => x.ruleDescription !== currentRuleDesc);
      this.excelFileRule = rules.filter(x => x.id !== ruleId);


    }
    //delete because i set generic rules as multiple=false
    // //check if the rule is global/generic and if all rules under the 
    // //global/generic rule that was added still has rule in the excelFileRule
    // //if all rules was deleted, remove it from the selected ng-select
    // if (rule.isGlobal || rule.ruleSetType === 2) {
    //   let countOfRulesIn = this.excelFileRule.filter(r => r.ruleSetNameId === rule.ruleSetNameId && r.isActive);
    //   if (countOfRulesIn.length === 0) {


    //     // Remove the ruleSetNameId from the selected values in the form control
    //     const selectedRuleSetNames = this.processConfigurationForm.get('ruleSetNames')?.value || [];

    //     const updatedSelection = selectedRuleSetNames.filter(
    //       (id: string) => id !== rule.ruleSetNameId
    //     );

    //     this.processConfigurationForm.get('ruleSetNames')?.setValue(updatedSelection);

    //   }
    // }


    if (rules.length === 0) {
      this.clearRule();
    }


    //this is to strikethrough; 
    // var rule = this.excelFileRule.find(x => x.id === ruleId);
    // if (rule) {
    //   rule.isActive = false;
    // }

  }

  existingRuleId = 0;
  isColumnNameLoading: boolean = false;
  editRule(existingRuleId: number) {
    this.isColumnNameLoading = true;
    //if (this.existingRuleSetNameSelected) return;
    // if (this.processConfigurationForm.get('ruleSetNames')?.value) return;

    const rules = this.excelFileRule;
    let existingRuleIndex = rules.findIndex(x => x.id === existingRuleId);

    if (existingRuleIndex < 0) return;
    this.existingRuleId = existingRuleId;
    const existingRule = rules[existingRuleIndex];

    this.processConfigurationForm.get('ruleType')?.setValue(existingRule.ruleTypeId);
    this.processConfigurationForm.get('ruleType')?.disable();

    const allSame = this.subRules?.length > 0 ? this.subRules.every(sub => sub.ruleTypeId === existingRule.ruleTypeId) : existingRule.subRuleId === null ? true : false;

    this.processConfigurationForm.get('ruleColumnName')?.disable();
    this.processConfigurationForm.get('ruleColumnName2')?.disable();

    const setSubRule = () => {
      this.processConfigurationForm.get('subRuleType')?.patchValue(existingRule.subRuleId);
      this.selectedSubRule = this.subRules.find(subrule => subrule.subRuleId === existingRule.subRuleId)?.subRuleName;
      setTimeout(() => {
        this.processConfigurationForm.get('ruleColumnName')?.setValue(existingRule.ruleColumnName);
        this.processConfigurationForm.get('ruleColumnName2')?.setValue(existingRule.ruleColumnName2);

        this.processConfigurationForm.get('ruleColumnName')?.enable();
        this.processConfigurationForm.get('ruleColumnName2')?.enable();
        this.isColumnNameLoading = false;
      }, 500);

    };
    if (!allSame) {
      this.getSubRules(existingRule.ruleTypeId).subscribe(subRules => {
        this.subRules = subRules;

        setSubRule();
      });
    } else {
      setSubRule();
    }

    // this.subRules = [];
    // this.filteredSubRules = this.subRules.filter(x => x.ruleTypeId == existingRule.ruleTypeId);
    this.willShowFormat(existingRule.subRuleId ?? -1);

    this.processConfigurationForm.get('subRuleType')?.disable();

    this.processConfigurationForm.get('aiPrompt')?.setValue(existingRule.prompt ?? existingRule.description);
    // if (this.patterns.length === 0) {
    //   this.getPatterns(existingRule.subRuleId).subscribe(patterns => {
    //     this.patterns = patterns;
    //     this.processConfigurationForm.get('patternType')?.setValue(existingRule.patternId);
    //     this.processConfigurationForm.get('patternType')?.disable();
    //   });
    // }

    const setPattern = () => {
      this.processConfigurationForm.get('patternType')?.setValue(existingRule.patternId);
      this.processConfigurationForm.get('patternType')?.disable();
    }
    if (this.patterns.length === 0) {
      this.getPatterns(existingRule.subRuleId).subscribe(patterns => {
        this.patterns = patterns;
        setPattern();
      });
    } else {
      setPattern();
    }


    this.processConfigurationForm.get('formatType')?.setValue(existingRule.format);
    this.processConfigurationForm.get('conditionType')?.setValue(existingRule.conditionId);
    this.processConfigurationForm.get('conditionType')?.disable();
    const isRuleColumnMultiple = existingRule.subRuleId === SubRuleTypes.Comparison;




    this.processConfigurationForm.get('fromValue')?.setValue(existingRule.fromValue === null ? 0 : existingRule.fromValue);
    this.processConfigurationForm.get('toValue')?.setValue(existingRule.toValue === null ? 0 : existingRule.toValue);

    this.processConfigurationForm.get('isCombinationRule')?.setValue(existingRule.isCombinationRule);
    this.processConfigurationForm.get('isAllowNullOrEmptySpaces')?.setValue(existingRule.isAllowNullOrEmptySpaces);




  }

  onCancelRuleCreation() {
    this.clearRule();
  }

  backToTabs() {
    this.validationSection = false;

    setTimeout(() => {
      this.activeTab = 'addtional-setting-tab';
      const additionalSettingTab = document.getElementById('addtional-setting-tab') as HTMLElement;
      additionalSettingTab.click();
      additionalSettingTab.classList.add("active");
    }, 0);

  }

  CreateNew() {

  }

  onUIValidationRule(event: any) {

    this.showUIValidationRule = true;
    this.showBEValidationRule = false;
    this.validation = 'UI';
    //this.hasSubmitted = false;
    const offcanvasElement = document.getElementById('offcanvasWithBothOptions');
    if (offcanvasElement) {
      const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement);

      if (this.validateClientSettings()) {
        this.showHideValidationSection();
        //offcanvasInstance?.show();
        offcanvas.show();
      } else {
        //offcanvasInstance?.hide();
        offcanvas.hide();
      }
    }




  }

  closeUIValidation() {
    this.suggestions = [];
    const offcanvasElement = document.getElementById('offcanvasWithBothOptions');
    let offcanvas: any = null;

    if (offcanvasElement) {
      offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement);
    }

    // || this.hasSubmitted
    if (this.formValues.flpConfigurationId) {
      offcanvas?.hide();
      return;
    }

    if (this.tmp_newlyAddedRules.length === 0
      && !this.processConfigurationForm.get('ruleSetDescription').value
    ) {
      offcanvas?.hide();
      return;
    }

    if (this.tmp_newlyAddedRules.length > 0) {
      this.confirmModalservice
        .confirm('Validation Rule', `Closing this window will discard any unsaved changes.`)
        .then((confirmed) => {
          if (confirmed === true) {
            this.clearRule();
            //remove tmp_newAddedRules in ExcelFileRule
            const removeSet = new Set(this.tmp_newlyAddedRules.map(r => r.ruleDescription));
            this.excelFileRule = this.excelFileRule.filter(r => !removeSet.has(r.ruleDescription));
            //clear the tmp_newlyAddedRules
            this.tmp_newlyAddedRules = [];
            this.tmp_existingRuleSetNameIds.clear();
            this.validationSection = false;
            this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
            offcanvas?.hide();
          }
        });
    }

    // const modalRef = this.confirmModalService.open(ConfirmDialogComponent);

    // modalRef.componentInstance.title = 'Create Rule';
    // modalRef.componentInstance.message = `Closing this window will discard any unsaved changes.`;
    // modalRef.result.then((result) => {
    //   if (result) {
    //     this.clearRule();
    //     this.excelFileRule = [];
    //     this.validationSection = false;
    //     this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
    //     offcanvas?.hide();
    //   } else {

    //   }
    // });




  }

  onBEValidationRule(event: any) {
    this.showUIValidationRule = false;
    this.showBEValidationRule = true;
    this.validation = 'BE';
  }

  toggleText(id: number) {

    this.expandedTd = this.expandedTd === id ? null : id;
  }

  existingRuleSetNameSelected: boolean = false;
  onChangeRuleSetNames(rs: RuleSetNames[]) {
    //if (this.validateClientSettings()) {
    if (rs.length > 0) {
      //this.removedItemsGloGenRuleSetNames = this.selectedGloGenRuleSetNames.filter(item => !rs.includes(item));
      //this.selectedGloGenRuleSetNames = [...rs];
      //this.formValues.ruleSetName = this.processConfigurationForm.get('ruleSetNames')?.value;
      setTimeout(() => {
        this.getRuleSetByRuleSetNameId(rs.map(x => x.ruleSetNameId).join(","));
        this.existingRuleSetNameSelected = true;
      }, 50);
    } else {
      // this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
      // this.excelFileRule = this.excelFileRule.filter(r => !(r.isGlobal || r.ruleSetType === 2));
      // this.existingRuleSetNameSelected = false;
    }
    //}
  }

  onSearchRuleSetName(searchTerm: string): void {
    const uniqueSearchTerm = `${searchTerm}`; // Append a timestamp -${new Date().getTime()
    this.searchRuleSetName$.next(uniqueSearchTerm); // Emit the unique search term
  }

  setupSearchRuleSetNames(): void {
    this.searchRuleSetName$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((uniqueSearchTerm: string) => {
          const searchTerm = uniqueSearchTerm;//.split('-')[0];
          this.isgetRuleSetBySecGrpIdsLoading = true;

          // handle the empty

          if (searchTerm.length >= 1) {
            //this.ruleSetNames = [];


            // this.securityGroupIsLoading = true;
            return this.dataInsiderService.getRuleSetByRuleSetName(searchTerm, this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));
          } else {
            this.getRuleSetNamesBySecGrpIds(this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));
            return EMPTY;
          }
        })
      ).subscribe({
        next: (response: any) => {

          let ctr = 0;
          var tempAny: any[] = [];

          const results = response?.result || [];
          this.ruleSetNames = results.slice(0, 10).map((c: any) => ({
            ruleSetNameId: c.ruleSetNameId,
            ruleSetName: c.ruleSetName
          }));
          // response.result.forEach(c => {
          //   //this.securityGroups.push({ displayName: c.displayName, id: c.id });
          //   tempAny.push({ ruleSetNameId: c.ruleSetNameId, ruleSetName: c.ruleSetName });
          // });


          // this.ruleSetNames = [...tempAny];

          this.isgetRuleSetBySecGrpIdsLoading = false;
        },
        error: (error) => {
          console.error('Error fetching rule set names', error);
          this.isgetRuleSetBySecGrpIdsLoading = false;
        }
      });
  }

  onClearRuleSetName() {
    //this.searchRuleSetName$.next('');

    // const ids = this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(',');
    // return this.getRuleSetNamesBySecGrpIds(ids);
    this.ruleSetNames = this.defaultRuleSetNames;
  }

  onBlurRuleSetNames(): void {
    //this.ruleSetNames = [];
  }


  showHideValidationSection() {
    this.validationSection = !this.validationSection;
  }

  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.excelFileRule, event.previousIndex, event.currentIndex);
  }


  compareRuleTypes(o1: any, o2: any): boolean {
    return o1 && o2 ? o1.ruleTypeId === o2.ruleTypeId : o1 === o2;
  }

  enableCreateButton: boolean = false;
  hasSubmitted: boolean = false;
  onSaveValidation() {

    this.excelFileRule = [...this.excelFileRule];

    if (this.excelFileRule.length === 0) {
      this.toastr.error(ToastrMessages.NoRulesToSave, undefined, { enableHtml: true });
      return;
    }

    if (this.excelFileRule.filter(x => x.isIrrelevant && x.isActive).length > 0) {
      this.toastr.error(ToastrMessages.UnableToSave, undefined, { enableHtml: true });
      return;
    }

    if (this.processConfigurationForm.get('ruleSetDescription')?.invalid) {
      this.toastr.error(ToastrMessages.UnableToSave, undefined, { enableHtml: true });
      return;
    }



    // this.confirmModalservice
    //   .confirm('Create Rule', `Do you want to proceed and save the rule set?`)
    //   .then((confirmed) => {
    //     if (confirm) {



    let payLoad: ExcelRule[] = this.excelFileRule.filter(r => r.isIrrelevant === false).map(rule => ({
      ...rule,
      isGlobal: false,
      ruleSetType: 1, //set all to process rulue
      ruleSetName: this.processConfigurationForm.get('ruleSetName')?.value,
      ruleSetNameId: '',//clear so as to save as a new rule  
      ruleSetDescription: this.processConfigurationForm.get('ruleSetDescription')?.value
    }));

    this.hasSubmitted = true;
    this.tmp_newlyAddedRules = [];
    this.addRuleToFormValues();
    this.updateConfigOnly.emit([this.formValues, this.columnDatatype, payLoad, this.ruleSetNames, this.processConfigurationForm]);

    const offcanvasElement = document.getElementById('offcanvasWithBothOptions');
    if (offcanvasElement) {
      this.clearRule();
      this.validationSection = false;
      const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement);
      offcanvas.hide();

    }
    //   }
    // });

    // const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
    // modalRef.componentInstance.title = 'Create Rule';
    // modalRef.componentInstance.message = `Do you want to proceed and save the rule set?`;
    // modalRef.result.then((result) => {
    //   if (result) {
    //     let payLoad: ExcelRule[] = this.excelFileRule.filter(r => r.isIrrelevant === false).map(rule => ({
    //       ...rule,
    //       isGlobal: false,
    //       ruleSetType: 1, //set all to process rulue
    //       ruleSetName: this.processConfigurationForm.get('ruleSetName')?.value,
    //       ruleSetNameId: '',//clear so as to save as a new rule  
    //       ruleSetDescription: this.processConfigurationForm.get('ruleSetDescription')?.value
    //     }));

    //     this.hasSubmitted = true;
    //     this.addRuleToFormValues();
    //     this.updateConfigOnly.emit([this.formValues, this.columnDatatype, payLoad, this.ruleSetNames]);

    //     const offcanvasElement = document.getElementById('offcanvasWithBothOptions');
    //     if (offcanvasElement) {
    //       this.clearRule();
    //       this.validationSection = false;
    //       const offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasElement);
    //       offcanvas.hide();
    //     }
    //   }
    // });
  }

  onResetUIValidation() {
    if (this.excelFileRule.length === 0) return;

    this.confirmModalservice
      .confirm('Validations', `Are you sure you want to erase all current rules and start over?`)
      .then((confirmed) => {
        if (confirmed === true) {
          this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
          this.excelFileRule = [];
          this.tmp_newlyAddedRules = [];
          this.tmp_existingRuleSetNameIds.clear();
          this.clearRule();
        }
      });

    // const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
    // modalRef.componentInstance.title = 'Create Rule';
    // modalRef.componentInstance.message = `Are you want to reset the form`;
    // modalRef.result.then((result) => {
    //   if (result) {
    //     this.processConfigurationForm.get('ruleSetNames')?.setValue(null);
    //     this.excelFileRule = [];
    //     this.clearRule();
    //   }
    // });

  }

  getTotalCountOfRules() {
    return this.excelFileRule.filter(r => r.isActive).length;
    //return this.excelFileRule.filter(r => r.isActive).length;
  }

  suggestions: string[] = [];
  onCustomColumnNameChange(event: Event): void {
    const textArea = this.txtareaAiPromptRef.nativeElement;
    if (!textArea) return;

    const value = textArea.value;
    const caretPos = textArea.selectionStart;
    const match = value.slice(0, caretPos).match(/@(\w*)$/);
    const columns = this.columnDatatype.map(col => col.ColumnName) ?? [];
    if (match) {
      const query = match[1].toLowerCase();
      this.suggestions = columns.filter(col =>
        col.toLowerCase().startsWith(query)
      );

      this.activeSuggestionIndex = this.suggestions.length > 0 ? 0 : -1;

      //delay caret position update
      requestAnimationFrame(() => {
        this.updateCaretPosition(textArea);
      });
    } else {
      this.suggestions = [];
      this.activeSuggestionIndex = -1;
    }



  }

  selectSuggestion(suggestion: string): void {
    const textArea = this.txtareaAiPromptRef?.nativeElement;
    if (!textArea) return;

    const caretPos = textArea.selectionStart;
    const value = textArea.value;

    // Find the last @ before the caret
    const beforeCaret = value.slice(0, caretPos);
    const afterCaret = value.slice(caretPos);
    const atIndex = beforeCaret.lastIndexOf('@');

    if (atIndex === -1) return;

    const updatedBefore = beforeCaret.substring(0, atIndex) + '@' + suggestion + ' ';
    const newValue = updatedBefore + afterCaret;

    this.processConfigurationForm.get('aiPrompt')?.setValue(newValue);
    this.suggestions = [];
    this.activeSuggestionIndex = -1;

    // Move caret to end of inserted suggestion
    const newCaretPos = updatedBefore.length;
    setTimeout(() => {
      textArea.setSelectionRange(newCaretPos, newCaretPos);
      textArea.focus();
    });
  }


  caretTop = 0;
  caretLeft = 0;
  updateCaretPosition(textArea: HTMLTextAreaElement): void {
    const caretPos = textArea.selectionStart;
    const value = textArea.value;

    // Find the last @ before the caret
    const beforeCaret = value.slice(0, caretPos);
    const atIndex = beforeCaret.lastIndexOf('@');

    if (atIndex === -1) {
      this.caretTop = 0;
      this.caretLeft = 0;
      return;
    }

    // Create a hidden mirror div
    const mirrorDiv = document.createElement('div');
    const style = getComputedStyle(textArea);

    // Copy essential styles
    mirrorDiv.style.position = 'absolute';
    mirrorDiv.style.visibility = 'hidden';
    mirrorDiv.style.whiteSpace = 'pre-wrap';
    mirrorDiv.style.wordWrap = 'break-word';
    mirrorDiv.style.fontFamily = style.fontFamily;
    mirrorDiv.style.fontSize = style.fontSize;
    mirrorDiv.style.lineHeight = style.lineHeight;
    mirrorDiv.style.padding = style.padding;
    mirrorDiv.style.border = style.border;
    mirrorDiv.style.boxSizing = style.boxSizing;
    mirrorDiv.style.width = textArea.offsetWidth + 'px';
    mirrorDiv.style.height = textArea.offsetHeight + 'px';
    mirrorDiv.style.overflow = 'auto';

    // Mirror scroll position
    mirrorDiv.scrollTop = textArea.scrollTop;

    // Insert text up to the @ character
    const textUpToAt = value.substring(0, atIndex);
    mirrorDiv.textContent = textUpToAt;

    // Add a span to mark the @ position
    const span = document.createElement('span');
    span.textContent = '\u200b'; // zero-width space
    mirrorDiv.appendChild(span);

    // Append to the same container as the textarea
    const container = textArea.parentElement;
    if (!container) return;

    container.appendChild(mirrorDiv);

    const rect = span.getBoundingClientRect();
    const textareaRect = textArea.getBoundingClientRect();

    this.caretTop = rect.top - textareaRect.top - textareaRect.height + textArea.scrollTop + 20; // offset for dropdown
    this.caretLeft = rect.left - textareaRect.left + textArea.scrollLeft;

    container.removeChild(mirrorDiv);
  }
  activeSuggestionIndex = -1;
  onKeyDownCustomColumnName(event: KeyboardEvent): void {
    if (this.suggestions.length === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeSuggestionIndex =
        (this.activeSuggestionIndex + 1) % this.suggestions.length;
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeSuggestionIndex =
        (this.activeSuggestionIndex - 1 + this.suggestions.length) % this.suggestions.length;
    } else if (event.key === 'Tab') {
      if (this.activeSuggestionIndex >= 0) {
        event.preventDefault();
        const selected = this.suggestions[this.activeSuggestionIndex];
        this.selectSuggestion(selected);
      }
    }
  }

  alertUIValidationReset() {
    if (this.excelFileRule.length > 0) {
      // const box = document.getElementById('divValidationRuleReset');
      // const icon = document.getElementById('iconValidationRuleSet');

      this.excelFileRule = [];
      this.tmp_newlyAddedRules = [];
      this.processConfigurationForm.get('ruleSetDescription')?.setValue('');
      this.processConfigurationForm.get('ruleSetNames')?.setValue(null);

      // box.classList.remove('d-none');
      // icon.classList.add('attention-blink');
      // setTimeout(() => {
      //   icon.classList.remove('attention-blink')        
      // }, 3000);
    }
  }

  setRuleColumnToMultiple() {
    return this.processConfigurationForm.get('subRuleType')?.value !== SubRuleTypes.Comparison;
  }

  compareRuleColumnName1(item1: any, item2: any): boolean {

    if (!item1 || !item2) return false;


    // item1 is from availableColumns2 (object), item2 is from selected value (string or array of strings)
    if (typeof item2 === 'string') {
      return item1.ColumnName === item2;
    }

    // item2 is an array of strings
    if (Array.isArray(item2)) {
      return item1.includes(item1.ColumnName);
    }


    return false;

  }

  compareRuleColumnName2(item1: any, item2: any): boolean {

    if (!item1 || !item2) return false;


    // item1 is from availableColumns2 (object), item2 is from selected value (string or array of strings)
    if (typeof item2 === 'string') {
      return item1.ColumnName === item2;
    }

    // item2 is an array of strings
    if (Array.isArray(item2)) {
      return item2.includes(item1.ColumnName);
    }


    return false;

  }

  isGeneratingCustomRule: boolean = false;
  customRuleGenerated: boolean = false;
  onGenerateCustomRule() {
    this.isGeneratingCustomRule = true;
    this.customRuleGenerated = false;

    let fileHeaders: { Column: string, DataType: string }[] = this.columnDatatype.map(item => ({ Column: item.ColumnName, DataType: item.DatatypeName }));
    let column_headers_generated = { ColumnHeaders: fileHeaders };
    let payload: DataAssistsRequest = {
      flowid: 1092,
      versionId: 2,
      projectId: 2015,
      isDataValidation: true,
      isCodeSnippet: true,
      fileHeaders: column_headers_generated,
      validationRules: this.processConfigurationForm.get('aiPrompt')?.value,
      overrideExistingRules: true
    }
    this.dataInsiderService.GenerateCustomRule(payload).subscribe({
      next: (response: APIResponse<boolean>) => {
        if (response?.responseCode === 200) {

        }
        this.isGeneratingCustomRule = false;
        this.customRuleGenerated = true;
      },
      error: error => {
        console.log(error);
        this.toastr.error('Something went wrong in generating custom rule.');
        this.isGeneratingCustomRule = false;
      }
    });
  }

  isFileExtensionLoading: boolean = false;
  fileNameExtensions: FileNameExtension[] = [];
  async getFileExtensions(): Promise<void> {
    this.isFileExtensionLoading = true;

    try {
      const response: APIResponse<FileNameExtension[]> = await firstValueFrom(this.configService.getFileExtensionNames());

      if (response && response.responseCode === 200) {
        this.fileNameExtensions = response.result;
      }
    } catch (error) {
      console.log(error);
      this.toastr.error('Something went wrong in fetching file extensions.');
    } finally {
      this.isFileExtensionLoading = false;
    }
    // this.configService.getFileExtensionNames().subscribe({
    //   next: (response: APIResponse<FileNameExtension[]>) => {
    //     if (response) {
    //       if (response.responseCode === 200) {
    //         this.isFileExtensionLoading = false;
    //         this.fileNameExtensions = response.result;
    //       }
    //     }
    //   },
    //   error: error => {
    //     console.log(error);
    //     this.isFileExtensionLoading = false;
    //   }
    // });
  }

  regexList: RegexItem[] = [];

  editingRegexIndex: number | null = null;
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
      //emit this regex to parent to validate the file names
      this.validateFileNamesWithRegex.emit(value);
    }

    this.clearEditor();

  }


  /** Load a list item back into the textbox for editing */
  selectForEdit(index: number): void {
    this.editingRegexIndex = index;
    this.processConfigurationForm.patchValue({ landingLayerRegex: this.regexList[index] });

    const ref = this.modalService.showCustomRegexBuilder(this.regexList[index].regex);
    const comp = ref.content as RegexBuilderComponent;

    comp.onClose?.subscribe((result) => {

      if (result.action === 'save') {
        if (comp) {
          if (comp.result) {
            this.regexList[index].regex = comp.result;
            this.regexList[index].description = comp.generatedDescription;
            this.validateFileNamesWithRegex.emit(this.regexList); // emit to parent
          }
        }
      }
    });
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
    this.validateFileNamesWithRegex.emit(this.regexList);
  }


  /** Cancel editing and clear the input */
  clearEditor(): void {
    this.editingRegexIndex = null;
    this.processConfigurationForm.get('landingLayerRegex').setValue('');
  }

  trackByRegexListIndex(i: number) {
    return i;
  }

  extensionChange(selectedExtension: any) {
    var extensions: string[] = [];
    selectedExtension.forEach(x => {
      extensions.push(x.fileExtension);
    });

    //emit to parent
    //const retValue = await this.previewToParent2(this.formValues);
    this.updateselectedFiles.emit(extensions);
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

  preventSubmit(event: any) {
    event.preventDefault();
  }






  enableCombination() {
    const col1 = this.processConfigurationForm.get('ruleColumnName')?.value;
    this.processConfigurationForm.get('isCombinationRule')?.disable();
    if (col1?.length >= 2) {
      this.processConfigurationForm.get('isCombinationRule')?.enable();
      return;
    }
    this.processConfigurationForm.get('isCombinationRule')?.setValue(false);

  }
  campaignNames: CampaignNames[] = [];
  showCampaignName: boolean = false;
  onExternalProjectChange(event: any) {
    const ctrlCampaignName = this.clientInfoForm.get('campaignName');
    const ctrlSubRegion = this.clientInfoForm.get('SubRegionId');
    const ctrlClient = this.clientInfoForm.get('ClientId');
    const ctrlRegionId = this.clientInfoForm.get('RegionId');
    // ctrlRegionId.reset();

    // ctrlSubRegion.reset();
    // ctrlClient.reset();

    // ctrlRegionId.setErrors(null); //clears any residual errors explicitly
    // ctrlSubRegion.setErrors(null); //clears any residual errors explicitly
    // ctrlClient.setErrors(null); //clears any residual errors explicitly
    [ctrlCampaignName, ctrlSubRegion, ctrlClient, ctrlRegionId].forEach(c => c && resetControl(c));
    this.clientInfoForm.setErrors(null);
    this.clientInfoForm.updateValueAndValidity({ emitEvent: false });
    this.clientSettingsTabVisited = false;

    ctrlRegionId.disable();
    ctrlSubRegion.disable();
    ctrlClient.disable();
    this.campaignNames = [];
    this.showCampaignName = event.target.checked;
    if (this.showCampaignName) {
      ctrlCampaignName.enable();
      ctrlCampaignName.setValidators([Validators.required]);
      //ctrlCampaignName.updateValueAndValidity();

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

  onCampaignNameChange(event: any) {
    const ctrlRegionId = this.clientInfoForm.get('RegionId');
    const ctrlSubRegion = this.clientInfoForm.get('SubRegionId');
    const ctrlClient = this.clientInfoForm.get('ClientId');
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
              const allowedIds = this.fabService.FABUserAccount.filter(x => x.campaignId === event?.campaignId).map(x => x.regionId);
              this.dsRegion = this.dsRegion
                .filter((r) => allowedIds.includes(r.id))
                .map(r => ({ id: r.id, name: r.name }));
              ctrlRegionId.setValue(this.dsRegion.length === 1 ? this.dsRegion[0].id : '');
              this.getProcessName('R');
            }
          }
        }
      });
    } else {

    }
  }

  openCustomRegex() {
    const ref = this.modalService.showCustomRegexBuilder();
    const comp = ref.content as RegexBuilderComponent;

    comp.onClose?.subscribe((result) => {
      if (result?.action !== 'save' || !comp) {
        return;
      }

      const regex = comp.result?.trim();
      const description = comp.generatedDescription?.trim();

      // add only if regex exists and is not already in the list
      if (
        regex &&
        !this.regexList.some(item => item.regex === regex)
      ) {
        const newItem = {
          regex,
          description
        };

        //console.log(description);

        this.regexList.push(newItem);

        // emit updated list to parent
        this.validateFileNamesWithRegex.emit(this.regexList);
      }
    });
  }





}

function resetControl(control: AbstractControl, options?: { emitEvent?: boolean }) {
  // Clears value and status, keeping validators
  control.reset();

  // Clear any residual errors explicitly (helpful for group-level errors)
  control.setErrors(null);

  // Reset UI state so your *ngIf conditions hide errors
  control.markAsPristine();
  control.markAsUntouched();
  control.updateValueAndValidity({ onlySelf: true, emitEvent: options?.emitEvent ?? true });
}
