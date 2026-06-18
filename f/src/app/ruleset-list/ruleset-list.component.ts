import { Component } from '@angular/core';
import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormGroup, FormControl, Validators, FormBuilder } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { Observable, of, tap, map, finalize } from 'rxjs';
import { APIResponse } from '../core/models/apiResponse';
import { ExcelRule, RuleSetNames, RuleTypes, LogicalOperator, ConditionalOperator, Patterns, SubRule, RuleSetListRequest, RuleSetListResponse, RuleSetConfigurationList, PayLoad } from '../core/models/DataInsider';
import { DataInsiderService } from '../core/services/data-insider.service';
import { cleanColumnName } from '../core/services/di-parser.service';
import { generateGUID, formatWithOrAnd, getInvalidFields, formatDate, isNullUndefinedEmptyArrays, findDuplicateAndGenerateNew } from '../core/utils/helper';
import { RuleTypeNames, SubRuleTypes, ModalMessages, ToastrMessages, PageNames } from '../shared/enum';
import { BusyService } from '../core/services/busy.service';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ProcessConfigListSearchDropdown } from '../core/models/processConfigurationlist';


@Component({
    selector: 'app-ruleset-list',
    templateUrl: './ruleset-list.component.html',
    styleUrl: './ruleset-list.component.css',
    standalone: false
})
export class RulesetListComponent {
  rulesetArea = false;
  isNewProcess = false;
  rulesetListForm: FormGroup;
  submittedRulesetList: any[] = [];
  rulesetList = [
    // { name: 'Ruleset1', desc: 'desc1', count: 5, createdDate: '2023-01-01', createdBy: 'User A'},
    // { name: 'Ruleset2', desc: 'desc2', count: 3, createdDate: '2023-01-02', createdBy: 'User B' },
  ]




  excelFileRule: ExcelRule[] = [];

  ruleCreationFormGroup: FormGroup;
  ruleSet: ExcelRule[] = [];
  ruleSetNames: RuleSetNames[] = [];
  rulesTypes: RuleTypes[] = [];
  logicalOperators: LogicalOperator[] = [];
  conditionalOperators: ConditionalOperator[] = [];
  patterns: Patterns[] = [];

  subRules: SubRule[] = [];
  selectedSubRule: any;
  filteredPatterns: Patterns[] = [];

  RuleTypes = RuleTypeNames;
  SubRuleTypes = SubRuleTypes;
  pageName: PageNames;

  isRuleTypesLoading: boolean = false;
  isSubRuleTypesLoading: boolean = false;
  isPatternTypesLoading: boolean = false;
  isCheckingName: boolean = false;
  showFormat: boolean = false;
  existingRuleId = 0;
  expandedTd: number | null = null;

  public first: number = 0;
  public rows: number = 10;
  public page: number = 0;
  showActiveRuleSet: boolean = true;
  selectedRuleSetForDelete: string[] = [];
  public searchDate: Date[] | null = null;
  public loggedInUser: string | null = null;
  public searchTerm: string | null = null;
  public totalRecords: number = 0;
  public selectedSearchValue: string | null = null;
  public searchLoading: boolean = false;
  public ruleSetNameList: RuleSetConfigurationList[];
  public apiErrorMessage: string = '';
  public pageLinks: number;

  public selectedSearchOnFields: any = null;
  public selectedSearchField: string = '';
  public disableSearchInput: boolean = true;
  public searchOnFields: ProcessConfigListSearchDropdown[] = [
    { databaseColumnName: 'rsn.ruleSetName', databaseLabel: 'Rule Set Name' },
    { databaseColumnName: 'rsn.[description]', databaseLabel: 'Description' },
    { databaseColumnName: 'rsn.username', databaseLabel: 'Created By' },
    // { databaseColumnName: 'flp.created_date', databaseLabel: 'ProcessConfigListSearchDropdown' },
    // { databaseColumnName: 'flp.userName', databaseLabel: 'Created By' },
  ]; //field name should be same as defined in FlpProcessConfiguration model

  constructor(
    private fb: FormBuilder,
    private toastr: ToastrService,
    private dataInsiderService: DataInsiderService,
    private busyService: BusyService,
    private confirmModalService: NgbModal,
  ) {

  }

  ngOnInit(): void {
    this.loggedInUser = sessionStorage.getItem('upn');
    this.pageName = PageNames.Generic_RuleList;

    this.initializeForm();
    this.getSearchedRuleSetTypeList('');
    this.getRuleTypes();
    this.getConditionalOperators();
  }

  initializeForm() {
    this.ruleCreationFormGroup = this.fb.group({
      ruleSetName: ['', Validators.required],
      description: [''],
      ruleType: [null],
      subRuleType: [null],
      patternType: [null],
      conditionType: [null],
      isCombinationRule: [false],
      columnNames: [''],
      formatType: [''],
      isGlobal: [false],
      fromValue: [''],
      toValue: [''],
      aiPrompt: ['']
    });


  }

  getSearchedRuleSetTypeList(btnType: string, isActive: boolean = true) {

    this.page = 0;
    this.first = 0;
    this.rows = 10;
    this.searchDate = null;
    this.searchTerm = null;
    this.selectedSearchField = '';
    this.selectedSearchValue = null;
    this.selectedSearchOnFields = null;
    this.disableSearchInput = true;

    this.showActiveRuleSet = isActive;
    this.selectedRuleSetForDelete = [];
    // const checkboxDeleteAll = document.getElementById('checkboxDeleteAll') as HTMLInputElement;
    // checkboxDeleteAll.checked = false;    
    this.GetRuleSetConfigList(isActive);
  }

  GetRuleSetConfigList(isActive: boolean = true) {
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
      let data: RuleSetListRequest = {
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
      this.searchLoading = true;
      this.dataInsiderService.getRuleSetConfigList(data).pipe(finalize(() => this.searchLoading = false)).subscribe({
        next: (response: APIResponse<RuleSetListResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.ruleSetNameList = response.result.response;
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

  getRuleTypes() {
    this.rulesTypes = [];
    this.logicalOperators = [];
    //this.conditionalOperators = [];
    this.patterns = [];
    this.isRuleTypesLoading = true;
    this.dataInsiderService.getRuleTypes().subscribe({
      next: (response: APIResponse<RuleTypes[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.rulesTypes = response.result;
            this.isRuleTypesLoading = false;
          }
        }
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

  getPatterns(subRuleId: number | null): Observable<Patterns[]> {

    if (subRuleId === null) {
      this.patterns = [];
      this.isPatternTypesLoading = false;
      return of([]); //
    }

    this.patterns = [];
    this.isPatternTypesLoading = true;
    return this.dataInsiderService.getPatterns(subRuleId).pipe(
      tap((response: APIResponse<Patterns[]>) => {
        if (response?.responseCode === 200) {
          this.isPatternTypesLoading = false;
        } else {
          this.isPatternTypesLoading = true;
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

  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.excelFileRule, event.previousIndex, event.currentIndex);
  }

  sidePanelOpen() {
    this.rulesetArea = true;
    this.isEditing = false;
    this.currentRuleSetNameId = '';
  }
  sidePanelClose(onSubmit: boolean = false) {


    const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
    modalRef.componentInstance.title = this.isEditing ? 'Edit Rule' : 'Create Rule';
    modalRef.componentInstance.message = `Closing this window will discard any unsaved changes.`;
    modalRef.result.then((result) => {
      if (result) {
        this.sidePanelCloseAndClear();
      }
    });


  }

  sidePanelCloseAndClear() {
    this.clearRule();
    this.excelFileRule = [];
    this.ruleCreationFormGroup.get('ruleSetName').setErrors(null);
    this.ruleCreationFormGroup.get('ruleSetName')?.setValue('');
    this.currentRuleSetNameId = '';
    this.rulesetArea = false;
    this.isEditing = false;
  }


  onRuleTypeChange(ruleType: RuleTypes) {
    if (ruleType) {
      const allSame = this.subRules?.length > 0 ? this.subRules.every(sub => sub.ruleTypeId === ruleType.ruleTypeId) : false;
      if (!allSame) {
        this.getSubRules(ruleType.ruleTypeId).subscribe(subRules => {
          this.subRules = subRules;
          this.ruleCreationFormGroup.get('subRuleType')?.enable();
          this.ruleCreationFormGroup.get('subRuleType')?.setValue(null);
        });
      }
    }
  }



  onSubRuleTypeChange(subRuleType: SubRule) {
    this.filteredPatterns = [];
    if (subRuleType) {
      this.getPatterns(subRuleType.subRuleId).subscribe(patterns => {
        this.patterns = patterns;
        this.ruleCreationFormGroup.get('patternType')?.enable();
        this.ruleCreationFormGroup.get('patternType')?.setValue(null);
      });

      this.selectedSubRule = subRuleType.subRuleName;
      this.willShowFormat(subRuleType.subRuleId);
    }
  }

  willShowFormat(subRuleId: number) {
    this.showFormat = subRuleId === SubRuleTypes.Length; //|| subRuleId === SubRuleTypes.Pattern;
  }

  onCombinationRuleChange(e: any) {
    //this.checkRuleChange(ruleId);
  }

  onColumnNameChange(event: any) {
    const input = event.target.value;

    if (input.includes(',')) {
      // Split by comma, trim, then filter out empty strings
      const rawValues = input.split(',').map(v => v.trim()).filter(v => v.length > 0);

      // Now clean only the valid entries
      const cleanedValues = rawValues.map(v => cleanColumnName(v));

      const uniqueValues = findDuplicateAndGenerateNew(cleanedValues);
      const updatedValue = uniqueValues.join(',');

      this.ruleCreationFormGroup.get('columnNames')?.setValue(updatedValue);

      // Proceed with cleanedValues
    } else {
      this.ruleCreationFormGroup.get('columnNames')?.setValue(cleanColumnName(input));
    }
  }

  clearRule() {
    this.existingRuleId = 0;
    this.ruleCreationFormGroup.get('ruleType')?.enable();
    this.ruleCreationFormGroup.get('ruleType')?.setValue(null);
    this.ruleCreationFormGroup.get('subRuleType')?.setValue(null);

    this.ruleCreationFormGroup.get('columnNames')?.setValue('');
    this.ruleCreationFormGroup.get('patternType')?.setValue(null);

    this.ruleCreationFormGroup.get('aiPrompt')?.setValue('');

    this.ruleCreationFormGroup.get('conditionType')?.setValue(null);
    this.ruleCreationFormGroup.get('fromValue')?.setValue('');
    this.ruleCreationFormGroup.get('toValue')?.setValue('');
    this.ruleCreationFormGroup.get('formatType')?.setValue('');

    this.ruleCreationFormGroup.get('isCombinationRule')?.setValue(false);
  }



  toggleText(id: number) {
    this.expandedTd = this.expandedTd === id ? null : id;
  }

  editRule(existingRuleId: number) {
    let existingRuleIndex = this.excelFileRule.findIndex(x => x.id === existingRuleId);
    if (existingRuleIndex < 0) return;
    this.existingRuleId = existingRuleId;
    const existingRule = this.excelFileRule[existingRuleIndex];

    this.ruleCreationFormGroup.get('ruleType').setValue(existingRule.ruleTypeId);
    this.ruleCreationFormGroup.get('ruleType')?.disable();
    this.getSubRules(existingRule.ruleTypeId).subscribe(subRules => {
      this.subRules = subRules;
      this.ruleCreationFormGroup.get('subRuleType')?.patchValue(existingRule.subRuleId);
      this.selectedSubRule = this.subRules.find(subrule => subrule.subRuleId === existingRule.subRuleId)?.subRuleName;
    });
    // this.subRules = [];
    // this.filteredSubRules = this.subRules.filter(x => x.ruleTypeId == existingRule.ruleTypeId);
    this.willShowFormat(existingRule.subRuleId ?? -1);



    this.ruleCreationFormGroup.get('subRuleType')?.disable();

    this.ruleCreationFormGroup.get('aiPrompt')?.setValue(existingRule.prompt ?? existingRule.description);
    this.getPatterns(existingRule.subRuleId).subscribe(patterns => {
      this.patterns = patterns;
      this.ruleCreationFormGroup.get('patternType')?.setValue(existingRule.patternId);
      this.ruleCreationFormGroup.get('patternType')?.disable();
    });



    this.ruleCreationFormGroup.get('formatType')?.setValue(existingRule.format);
    this.ruleCreationFormGroup.get('conditionType')?.setValue(existingRule.conditionId);
    this.ruleCreationFormGroup.get('conditionType')?.disable();
    this.ruleCreationFormGroup.get('columnNames').setValue(existingRule.ruleColumnName.join(','));
    this.ruleCreationFormGroup.get('columnNames')?.enable();

    this.ruleCreationFormGroup.get('fromValue')?.setValue(existingRule.fromValue);
    this.ruleCreationFormGroup.get('toValue')?.setValue(existingRule.toValue);

    this.ruleCreationFormGroup.get('isCombinationRule')?.setValue(existingRule.isCombinationRule);
  }

  removeRule(id: number) {
    var rule = this.excelFileRule.find(x => x.id === id);
    if (rule) {
      rule.isActive = false;
    }
  }

  onRuleSetNameChange(event: any) {
    const ruleSetName = 'RSN_' + event.target.value;
    const control = this.ruleCreationFormGroup.get('ruleSetName');

    this.isCheckingName = true;

    this.dataInsiderService.checkRuleSetNameIfUnique(ruleSetName).subscribe({
      next: (response: APIResponse<boolean>) => {

        if (response?.responseCode === 200) {

          if (response?.result) {
            control?.setErrors({ ruleSetNameExist: true });
          } else {
            control?.setErrors(null);
          }
        }
        this.isCheckingName = false;

      },
      error: (error) => {
        console.log(error);
        this.isCheckingName = false;
      }
    });

  }

  hasError = (controlName: string, errorName: string) => {
    return this.ruleCreationFormGroup.get(controlName)?.hasError(errorName) && this.ruleCreationFormGroup.get(controlName)?.dirty;
  };

  //payLoad : PayLoad
  onSubmit(event: { showUIValidation: boolean, saveCancel: string, payLoad: PayLoad } | null) {

    if (event) {
      if (event.saveCancel === 'save') {

        this.dataInsiderService.insertRuleSets(event.payLoad, "", "").subscribe({
          next: (response: APIResponse<boolean>) => {
            this.toastr.success(this.isEditing ? ToastrMessages.RuleSetUpdatedSuccessfully : ToastrMessages.RuleSetCreatedSuccessfully);
            //need to close
            this.rulesetArea = false;
            this.GetRuleSetConfigList();
          },
          error: error => {
            console.log(error);
            this.toastr.error(ToastrMessages.SomethingWentWrong);
          }
        });
        return;
      } else if (event.saveCancel === 'cancel') {
        if (event.payLoad != null) {
          const modalRef = this.confirmModalService.open(ConfirmDialogComponent);
          modalRef.componentInstance.title = this.isEditing ? 'Edit Rule' : 'Create Rule';
          modalRef.componentInstance.message = `Closing this window will discard everything. Please click on ${this.isEditing ? 'Update' : 'Save'}`;
          modalRef.result.then((result) => {
            if (result) {
              this.rulesetArea = false;
            }
          });
        } else {
          this.rulesetArea = false;
        }
      }
    }

    // let invalidFields = [];

    // invalidFields = getInvalidFields(this.ruleCreationFormGroup);

    // if (invalidFields.length > 0) {
    //   this.toastr.error(
    //     ModalMessages.CantProceedPleaseCorrectOtherDetails + ' — ' + invalidFields.join(', '),
    //     undefined,
    //     { enableHtml: true }
    //   );
    //   return;
    // }

    // if (this.excelFileRule.length === 0) {
    //   this.toastr.error(ToastrMessages.UnableToSave, undefined, { enableHtml: true });
    //   return;
    // }

    // let data = this.excelFileRule;
    // const payLoad = {
    //   RuleSets: data.map(({ isIrrelevant, ...rule }) => rule),
    //   created_by: sessionStorage.getItem('upn').split('@')[0],
    //   username: sessionStorage.getItem('username'),
    //   description: this.ruleCreationFormGroup.get('description')?.value
    // };

    // this.dataInsiderService.insertRuleSets(payLoad).subscribe({
    //   next: (response: APIResponse<boolean>) => {
    //     this.clearRule();
    //     this.excelFileRule = [];
    //     this.ruleCreationFormGroup.get('ruleSetName')?.setValue('');
    //     this.ruleCreationFormGroup.get('description')?.setValue('');
    //     this.toastr.success(ToastrMessages.RuleSetCreatedSuccessfully);
    //     this.isEditing = false;
    //     this.rulesetArea = false;

    //     this.GetRuleSetConfigList();
    //   },
    //   error: error => {
    //     console.log(error);
    //     this.toastr.success(ToastrMessages.SomethingWentWrong);
    //   }
    // });

  }


  onPageChange(event: any) {
    
    this.first = event.first;
    //if user changed rows-per-page, reset to first page
    if (event.rows !== this.rows) {
      this.rows = event.rows;
      this.first = 0;
      this.page = 0;
    } else {
      this.first = event.first;
      this.page = event.page;
    }
    this.pageLinks = event.pageCount;

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
      let data: RuleSetListRequest = {
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
      this.searchLoading = true;
      this.dataInsiderService.getRuleSetConfigList(data).subscribe({
        next: (response: APIResponse<RuleSetListResponse>) => {
          if (response) {
            if (response.responseCode === 200) {
              this.ruleSetNameList = response.result.response;
              this.totalRecords = response.result.totalCount;
            } else {
              this.apiErrorMessage = response.responseMessage[0];
            }
          } else {
            this.apiErrorMessage = 'Failed to get data!';
          }

          document.getElementById('topOfTable').focus();
          
          this.searchLoading = false;
          

        },
        error: (err) => {
          this.searchLoading = false;
          console.log(err?.message);
        },
      });
    } catch (error) {
      console.log(error);
      this.apiErrorMessage = 'Some error occured while fetching the data!';
    } finally {

    }
  }
  currentRuleSetNameId: string = '';
  isEditing: boolean = false;

  viewDetails(ruleSetNameId: string) {
    this.isEditing = true;
    this.rulesetArea = true;
    this.currentRuleSetNameId = ruleSetNameId;

    this.dataInsiderService.getRuleSetByRuleSetNameId(ruleSetNameId).subscribe({
      next: (response: APIResponse<ExcelRule[]>) => {
        if (response?.responseCode === 200) {
          this.rulesetArea = true;
          this.excelFileRule = response.result;
          //use the ruleSetNameId 
          this.excelFileRule = this.excelFileRule.map(rule => ({ ...rule, ruleSetNameId: ruleSetNameId }))
          this.ruleCreationFormGroup.get('ruleSetName')?.setValue(this.excelFileRule[0].ruleSetName);
          this.ruleCreationFormGroup.get('ruleSetName')?.disable();
          this.ruleCreationFormGroup.get('description')?.setValue(this.excelFileRule[0].description);
          this.ruleCreationFormGroup.get('isGlobal')?.setValue(this.excelFileRule[0].isGlobal);
        }
      },
      error: error => {
        this.toastr.error(ToastrMessages.SomethingWentWrong);
        console.log(error);
      }
    });

  }

  clearDateFilter() {
    if (this.searchDate) {
      this.searchDate = [];
      //this.GetProcessConfigList();
    }
  }

  //todo revise
  onDropdownChange(event: ProcessConfigListSearchDropdown) {
    this.selectedSearchOnFields = event.databaseColumnName;
    this.selectedSearchValue = this.selectedSearchOnFields.toString();
    this.disableSearchInput = this.selectedSearchOnFields.length > 0 ? false : true;
  }

  clearSearch() {
    if (this.searchTerm) {
      this.searchTerm = '';
      //this.GetProcessConfigList();
    }
  }

  enableBtnCreateRule(): boolean {

    const form = this.ruleCreationFormGroup;
    const ruleType = form.get('ruleType')?.value;
    const ruleColumnName = form.get('columnNames')?.value;
    const subRuleType = form.get('subRuleType')?.value;
    const conditionType = form.get('conditionType')?.value;
    const patternType = form.get('patternType')?.value;
    const formatType = form.get('formatType')?.value;
    const aiPrompt = form.get('aiPrompt')?.value;
    const fromValue = form.get('fromValue')?.value;
    const toValue = form.get('toValue')?.value;

    if (!ruleType) return true;

    if (ruleType === RuleTypeNames.Required && isNullUndefinedEmptyArrays(ruleColumnName)) return true;

    if (ruleType === RuleTypeNames.Unique && isNullUndefinedEmptyArrays(ruleColumnName)) return true;

    if (ruleType === RuleTypeNames.Custom && isNullUndefinedEmptyArrays(aiPrompt)) return true;

    if ((ruleType === RuleTypeNames.Format || ruleType === RuleTypeNames.Value) && (isNullUndefinedEmptyArrays(subRuleType) || isNullUndefinedEmptyArrays(ruleColumnName))) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.Pattern && isNullUndefinedEmptyArrays(patternType)) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.Length && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(formatType))) return true;

    if (ruleType === RuleTypeNames.Format && subRuleType === SubRuleTypes.NumericRange && (isNullUndefinedEmptyArrays(fromValue) || isNullUndefinedEmptyArrays(toValue))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.NumericRange && (isNullUndefinedEmptyArrays(fromValue) || isNullUndefinedEmptyArrays(toValue))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.Length && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(formatType))) return true;

    return false;
  }

  onRestore(id: number) {
    var rule = this.excelFileRule.find(x => x.id === id);
    if (rule) {
      rule.isActive = true;
    }
  }
}
