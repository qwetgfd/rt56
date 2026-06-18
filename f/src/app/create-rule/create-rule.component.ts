import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { BusyService } from '../core/services/busy.service';
import { DataInsiderService } from '../core/services/data-insider.service';
import { ConditionalOperator, DataAssistsRequest, ExcelRule, LogicalOperator, Patterns, PayLoad, RuleSetNames, RuleTypes, SPNames, SubRule } from '../core/models/DataInsider';
import { APIResponse } from '../core/models/apiResponse';
import { arraysEqual, findDuplicateAndGenerateNew, formatWithOrAnd, getInvalidFields, Helper, isNullUndefinedEmptyArrays } from '../core/utils/helper';
import { ModalMessages, PageNames, RuleTypeNames, SubRuleTypes, ToastrMessages } from '../shared/enum';
import { debounceTime, EMPTY, map, Observable, of, Subject, switchMap, tap } from 'rxjs';
import { cleanColumnName } from '../core/services/di-parser.service';
import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { SecurityGroup } from '../core/models/userDetails';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { ColumnNameDatatypeName } from '../core/models/columnNameDatatypeName';

@Component({
  selector: 'app-create-rule',
  templateUrl: './create-rule.component.html',
  styleUrl: './create-rule.component.css',
  standalone: false
})
export class CreateRuleComponent implements OnInit {
  @ViewChild('txtareaAiPrompt', { static: false }) txtareaAiPromptRef?: ElementRef<HTMLTextAreaElement>;

  @Output() notifyParent = new EventEmitter<PayLoad>();
  @Output() notifyParentToClose = new EventEmitter<{ showUIValidation: boolean, saveCancel: string, payLoad: PayLoad | null }>();
  ruleCreationFormGroup: FormGroup;
  public loggedInUser: string | null = null;

  excelFileRule: ExcelRule[] = [];
  rulesTypes: RuleTypes[] = [];
  spNames: SPNames[] = [];
  logicalOperators: LogicalOperator[] = [];
  patterns: Patterns[] = [];
  subRules: SubRule[] = [];
  conditionalOperators: ConditionalOperator[] = [];
  ruleSetNames: RuleSetNames[] = [];
  selectedGloGenRuleSetNames: RuleSetNames[] = [];
  removedItemsGloGenRuleSetNames: RuleSetNames[] = [];

  existingRuleId = 0;
  selectedSubRule: any;
  showFormat: boolean = false;

  isRuleTypesLoading: boolean = false;
  isSubRuleTypesLoading: boolean = false;
  isEditing: boolean = false;
  isPatternTypesLoading: boolean = false;
  isgetRuleSetByRuleSetNameIdLoading: boolean = false;
  isgetRuleSetBySecGrpIdsLoading: boolean = false;
  isCheckingName: boolean = false;
  isSPNamesLoading: boolean = false;

  existingRuleSetNameSelected: boolean = false;

  RuleTypes = RuleTypeNames;
  SubRuleTypes = SubRuleTypes;
  @Input() ruleSetNameId: string = '';
  @Input() pageName: PageNames;
  @Input() column_name_list: string;
  @Input() selectedSecurityGroups: SecurityGroup[] = [];
  @Input() processName: string = '';
  @Input() tabName: string = null;
  @Input() inputRuleSetPayload: PayLoad;
  PageNames = PageNames;

  columnNames: string[] = [];

  searchRuleSetName$ = new Subject<string>();

  availableColumns1: { columnName: string }[] = [];
  availableColumns2: { columnName: string }[] = [];

  constructor(
    private fb: FormBuilder,
    private toastr: ToastrService,
    private dataInsiderService: DataInsiderService,
    private busyService: BusyService,
    private confirmModalService: NgbModal,
    private helperUtil: Helper
  ) {

  }
  ngOnInit(): void {
    this.loggedInUser = sessionStorage.getItem('upn');
    this.initializeForm();
    this.isEditing = false;

    if (!this.ruleSetNameId) {
      if (this.pageName === PageNames.Offline_Process) {

        let processName = `${this.processName.replace('DI_', '')}${this.tabName ? `_${this.tabName}` : ''}`;
        this.ruleCreationFormGroup.get('ruleSetName')?.setValue(processName);
        this.ruleCreationFormGroup.get('ruleSetName')?.disable();
        if (this.inputRuleSetPayload?.ruleSets?.length > 0) {
          this.excelFileRule = this.inputRuleSetPayload.ruleSets.map(rule => ({ ...rule, isIrrelevant: false }));
          this.populateOtherFieds();
        }
      }
    } else {

      this.isEditing = true;

      if (this.pageName === PageNames.Offline_Process) {
        this.excelFileRule = this.inputRuleSetPayload?.ruleSets?.map(rule => ({ ...rule, isIrrelevant: false }));
        this.populateOtherFieds();
      } else {
        this.viewDetails(this.ruleSetNameId);
      }
    }


    this.getRuleTypes();
    this.getSPNames();
    this.getConditionalOperators();
    this.setupSearchRuleSetNames();
    if (this.pageName !== PageNames.Generic_RuleList) this.getRuleSetNamesBySecGrpIds(this.selectedSecurityGroups.map(sg => sg.securityGroupId).join(','));

    this.ruleCreationFormGroup?.get('columnNames')?.disable();
  }

  initializeForm() {
    this.ruleCreationFormGroup = this.fb.group({
      ruleSetName: ['', Validators.required],
      description: ['', [Validators.maxLength(1000), Validators.pattern(/^[a-zA-Z0-9,. _]*$/)]],
      ruleType: [null],
      subRuleType: [null],
      patternType: [null],
      columnNames: [''],
      columnNames2: [null],
      isCombinationRule: [{ value: false, disabled: true }],
      conditionType: [null],
      formatType: [''],
      isGlobal: [false],
      fromValue: [0],
      toValue: [0],
      numericValue: [''],
      aiPrompt: [''],
      ruleSetNames: [null],
      spName: [null],
      isAllowNullOrEmptySpaces: [false]
    });


    if (this.column_name_list?.trim().length > 0) {
      this.columnNames = this.column_name_list.split(',');
      this.availableColumns1 = this.column_name_list.split(',').map(col => { return { columnName: col } });
      this.availableColumns2 = this.column_name_list.split(',').map(col => { return { columnName: col } });
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
            if (this.pageName === PageNames.Generic_RuleList) {
              this.rulesTypes = response.result.filter(r => r.category !== 'Generic' && r.ruleTypeId !== RuleTypeNames.Custom);
            } else {
              this.rulesTypes = response.result;
            }


            this.isRuleTypesLoading = false;
          }
        }
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

  onSubmit() {
    let invalidFields = [];

    invalidFields = getInvalidFields(this.ruleCreationFormGroup);

    if (invalidFields.length > 0) {
      this.toastr.error(
        ModalMessages.CantProceedPleaseCorrectOtherDetails + ' — ' + invalidFields.join(', '),
        undefined,
        { enableHtml: true }
      );
      return;
    }

    if (this.excelFileRule.length === 0) {
      this.toastr.error(ToastrMessages.NoRulesToSave, undefined, { enableHtml: true });
      return;
    }

    if (this.excelFileRule.filter(x => x.isIrrelevant && x.isActive).length > 0) {
      this.toastr.error(ToastrMessages.UnableToSave, undefined, { enableHtml: true });
      return;
    }

    //clean the excelFileRule, 
    //if a new ruleSetNameId and isActive = false is added, then, remove it from the list, this was added from generic and was removed by any reason
    //if a new rulSetNameId and isActive = true is added, then update the ruleSetNameId by the this.ruleSetNameId, this is a valid rule


    let data: ExcelRule[] = [];
    if (this.pageName === PageNames.Offline_Process) {
      data = this.excelFileRule.filter(rule => {
        const isNew = rule.ruleSetNameId !== '' && rule.ruleSetNameId !== this.ruleSetNameId;

        if (isNew) {
          if (rule.isActive === false) {
            return false;
          }
        }

        rule.ruleColumnName2 = rule.ruleColumnName2 ?? ''; //default to empty string

        rule.ruleSetNameId = this.ruleSetNameId ?? '';

        rule.isGlobal = this.ruleCreationFormGroup.get('isGlobal')?.value;

        return true;
      });
    } else {
      data = this.excelFileRule;
    }

    if (this.pageName === PageNames.Generic_RuleList) {

      data.forEach(rule => {
        rule.ruleSetName = this.isEditing ? this.ruleCreationFormGroup.get('ruleSetName')?.value : `RSN_${this.ruleCreationFormGroup.get('ruleSetName')?.value}`;
        rule.isGlobal = this.ruleCreationFormGroup.get('isGlobal')?.value;
      }
      );

      if (!this.ruleSetNameId) {
        //remove isActive=false
        data = data.filter(x => x.isActive);
      }
    }

    const payLoad: PayLoad = {
      ruleSets: data,  //,
      created_by: sessionStorage.getItem('upn').split('@')[0],
      username: sessionStorage.getItem('username'),
      description: this.ruleCreationFormGroup.get('description')?.value,
      ruleSetName: data[0].ruleSetName
    };


    this.notifyParentToClose.emit({ saveCancel: 'save', showUIValidation: false, payLoad });
    this.isEditing = false;
  }

  clearRule() {
    this.existingRuleId = 0;
    this.customRuleGenerated = false;
    this.ruleCreationFormGroup.get('ruleType')?.enable();
    this.ruleCreationFormGroup.get('ruleType')?.setValue(null);
    this.ruleCreationFormGroup.get('subRuleType')?.setValue(null);

    this.ruleCreationFormGroup.get('columnNames')?.setValue(null);
    this.ruleCreationFormGroup.get('columnNames')?.disable();
    this.ruleCreationFormGroup.get('columnNames2')?.setValue(null);

    this.ruleCreationFormGroup.get('columnNames')?.clearValidators();
    this.ruleCreationFormGroup.get('columnNames2')?.clearValidators();
    this.ruleCreationFormGroup.get('columnNames2')?.markAsPristine();
    this.ruleCreationFormGroup.get('columnNames2')?.markAsUntouched();

    this.ruleCreationFormGroup.get('columnNames')?.setErrors(null);
    this.ruleCreationFormGroup.get('columnNames2')?.setErrors(null);
    this.ruleCreationFormGroup.clearValidators();

    this.ruleCreationFormGroup.get('patternType')?.setValue(null);

    this.ruleCreationFormGroup.get('aiPrompt')?.setValue('');




    this.ruleCreationFormGroup.get('fromValue')?.setValue(0);
    this.ruleCreationFormGroup.get('toValue')?.setValue(0);
    this.ruleCreationFormGroup.get('formatType')?.setValue('');

    this.ruleCreationFormGroup.get('conditionType')?.enable();
    this.ruleCreationFormGroup.get('conditionType')?.setValue(null);

    this.ruleCreationFormGroup.get('isCombinationRule')?.setValue(false);
    this.ruleCreationFormGroup.get('isAllowNullOrEmptySpaces')?.setValue(false);

    this.ruleCreationFormGroup.get('spName')?.setValue(null);
    this.ruleCreationFormGroup.get('ruleSetNames')?.setValue(null);
    this.onClearRuleSetNames();
  }

  onRuleTypeChange(ruleType: RuleTypes) {
    if (ruleType) {

      this.ruleCreationFormGroup.get('subRuleType')?.enable();
      this.ruleCreationFormGroup.get('subRuleType')?.setValue(null);

      const allSame = this.subRules?.length > 0 ? this.subRules.every(sub => sub.ruleTypeId === ruleType.ruleTypeId) : false;

      if (!allSame) {
        this.getSubRules(ruleType.ruleTypeId).subscribe(subRules => {
          this.subRules = subRules;
        });
      }

      this.ruleCreationFormGroup.get('columnNames').enable();

    }
    this.ruleCreationFormGroup.get('isCombinationRule')?.disable();
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

  onSubRuleTypeChange(subRuleType: SubRule) {

    if (subRuleType) {

      this.enableValidatorsForComparison();
      // this.ruleCreationFormGroup.get('columnNames')?.updateValueAndValidity();
      // this.ruleCreationFormGroup.get('columnNames2')?.updateValueAndValidity();
      // this.ruleCreationFormGroup.updateValueAndValidity();

      this.getPatterns(subRuleType.subRuleId).subscribe(patterns => {
        this.patterns = patterns;
        this.ruleCreationFormGroup.get('patternType')?.enable();
        this.ruleCreationFormGroup.get('patternType')?.setValue(null);
      });

      this.selectedSubRule = subRuleType.subRuleName;
      this.willShowFormat(subRuleType.subRuleId);

    }
  }

  enableValidatorsForComparison() {


    if (this.ruleCreationFormGroup.get('subRuleType')?.value === SubRuleTypes.Comparison) {
      this.ruleCreationFormGroup.get('columnNames').addValidators([Validators.required, Validators.pattern(/^[^,]+$/)]);
      this.ruleCreationFormGroup.get('columnNames2').addValidators([Validators.required, Validators.pattern(/^[^,]+$/),]);
      this.ruleCreationFormGroup.setValidators(columnNamesUniqueValidator());
    } else {
      this.ruleCreationFormGroup.get('columnNames')?.clearValidators();
      this.ruleCreationFormGroup.get('columnNames2')?.clearValidators();
      this.ruleCreationFormGroup.clearValidators();
    }

    this.ruleCreationFormGroup.get('columnNames')?.setErrors(null);
    this.ruleCreationFormGroup.get('columnNames2')?.setErrors(null);
  }

  willShowFormat(subRuleId: number) {
    this.showFormat = subRuleId === SubRuleTypes.Length; //|| subRuleId === SubRuleTypes.Pattern;
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

  enableBtnCreateRule(): boolean {

    const form = this.ruleCreationFormGroup;
    const ruleType = form.get('ruleType')?.value;
    const ruleColumnName = form.get('columnNames')?.value;
    const ruleColumnName2 = form.get('columnNames2')?.value;
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

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.ExactMatch && (isNullUndefinedEmptyArrays(formatType))) return true;

    if (ruleType === RuleTypeNames.Value && subRuleType === SubRuleTypes.Comparison && (isNullUndefinedEmptyArrays(conditionType) || isNullUndefinedEmptyArrays(ruleColumnName) || isNullUndefinedEmptyArrays(ruleColumnName2))) return true;

    if (ruleType === RuleTypeNames.GenericRules && isNullUndefinedEmptyArrays(genericRule)) return true;

    if (ruleType === RuleTypeNames.BEValidation && isNullUndefinedEmptyArrays(spName)) return true;

    for (const controlName of Object.keys(this.ruleCreationFormGroup.controls)) {
      if (controlName !== 'ruleSetName') {
        const control = this.ruleCreationFormGroup.get(controlName);
        if (control?.errors) {
          return true;
        }
      }
    }

    if (this.ruleCreationFormGroup.errors) return true;


    return false;
  }

  onColumnNameChange(event: any, controlName: string) {
    const input = event.target?.value ?? event;
    const otherControl = controlName === 'columnNames' ? 'columnNames' : 'columnName';
    if (!Array.isArray(input)) {
      if (input.includes(',')) {

        // Split by comma, trim, then filter out empty strings
        const rawValues = input.split(',').map(v => v.trim()).filter(v => v.length > 0);

        // Now clean only the valid entries
        const cleanedValues = rawValues.map(v => cleanColumnName(v));


        const uniqueValues = findDuplicateAndGenerateNew(cleanedValues);
        const updatedValue = uniqueValues.join(',');

        this.ruleCreationFormGroup.get(controlName)?.setValue(updatedValue);

        // Proceed with cleanedValues
      } else {

        this.ruleCreationFormGroup.get(controlName)?.setValue(cleanColumnName(input));
      }
    }
    this.enableCombination(input);
  }

  onAddRule() {

    if (this.ruleCreationFormGroup.get('ruleType').value === RuleTypeNames.GenericRules) {
      let selectedGenericRules = this.ruleSetNames.filter(x => x.ruleSetNameId === this.ruleCreationFormGroup.get('ruleSetNames')?.value);
      this.onChangeRuleSetNames(selectedGenericRules);
      return;
    }
    this.checkRuleChange(0);
    //this.addRuleToFormValues();
  }
  tmp_newlyAddedRules: ExcelRule[] = [];
  checkRuleChange(existingRuleId: number) {

    const ruleSetNameControl = this.ruleCreationFormGroup.get('ruleSetName');
    const ruleSetNameValue = ruleSetNameControl?.value;
    const ruleSetName = this.isEditing ? ruleSetNameValue : `RSN_${ruleSetNameValue}`;
    const ruleType = this.ruleCreationFormGroup.get('ruleType').value;
    let tempColumnNames = this.ruleCreationFormGroup.get('columnNames').value;
    let tempColumnNames2 = this.ruleCreationFormGroup.get('columnNames2').value;
    const isCombination = this.ruleCreationFormGroup.get('isCombinationRule')?.value;
    const subRuleId = this.ruleCreationFormGroup.get('subRuleType').value;
    const patternId = this.ruleCreationFormGroup.get('patternType')?.value ?? null;
    const conditionId = this.ruleCreationFormGroup.get('conditionType')?.value ?? null;
    const fromValue = this.ruleCreationFormGroup.get('fromValue')?.value ?? null;
    const toValue = this.ruleCreationFormGroup.get('toValue')?.value ?? null;
    const aiPrompt = this.ruleCreationFormGroup.get('aiPrompt')?.value;
    const formatType = this.ruleCreationFormGroup.get('formatType')?.value;
    const numericValue = this.ruleCreationFormGroup.get('numericValue')?.value;
    const spName = this.ruleCreationFormGroup.get('spName')?.value;
    const isAllowNullOrEmptySpaces = this.ruleCreationFormGroup.get('isAllowNullOrEmptySpaces')?.value;
    let spNameId = 0;
    //if checked, set value to 2
    //1 is for generic, 2 for global
    const isGlobal = this.ruleCreationFormGroup.get('isGlobal')?.value;
    let columnNames: any;
    let columnNames2: any;
    if (ruleType !== 6) {
      if ((!tempColumnNames || tempColumnNames.length === 0) && !(ruleType === RuleTypeNames.Custom)) return;

      if (ruleType === 5 && ((!columnNames || columnNames.length === 0)
        || (!columnNames2 || columnNames2.length === 0)) && !(ruleType === RuleTypeNames.Custom || ruleType === RuleTypeNames.Value)) return;
    }


    const rule = this.rulesTypes.find(x => x.ruleTypeId === ruleType);
    if (!rule) return;

    if (this.pageName === PageNames.Generic_RuleList) {
      if (ruleType !== RuleTypeNames.BEValidation && ruleType !== RuleTypeNames.Custom) {
        columnNames = tempColumnNames.split(',').map(v => v.trim());
        if (subRuleId === SubRuleTypes.Comparison) {
          columnNames2 = tempColumnNames2;
        } else {
          if (!tempColumnNames2) {
            columnNames2 = '';
          } else {
            columnNames2 = tempColumnNames2?.split(',').map(v => v.trim());
          }
        }
      } else {
        columnNames = [''];
      }
    } else {
      columnNames = tempColumnNames ?? [''];
      columnNames2 = tempColumnNames2;
    }

    if (ruleType !== RuleTypeNames.BEValidation && ruleType !== RuleTypeNames.Custom) {
      if (this.pageName === PageNames.Offline_Process) {
        const colsToCheck = Array.isArray(columnNames) ? columnNames : [columnNames];

        const missingColumns = colsToCheck.filter(
          col => !this.availableColumns1.some(x => x.columnName === col)
        );

        if (missingColumns.length > 0) {
          this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
          return;
        }
        const colsToCheck2 = Array.isArray(columnNames2) ? columnNames2 : [columnNames2];

        if (subRuleId === SubRuleTypes.Comparison) {
          const missingColumns = colsToCheck2.filter(
            col => !this.availableColumns2.some(x => x.columnName === col)
          );

          if (missingColumns.length > 0) {
            this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
          }
        }

        // }

        // if (ruleType !== RuleTypeNames.BEValidation && ruleType !== RuleTypeNames.Custom) {
        // if (subRuleId !== SubRuleTypes.Comparison) {
        //   const missingColumns = columnNames.filter(
        //     col => !this.availableColumns1.some(x => x.columnName === col)
        //   );

        //   if (missingColumns.length > 0) {
        //     this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
        //   }
        // } else {

        //   if (this.pageName === PageNames.Offline_Process) {
        //     if (!this.availableColumns1.find(x => x.columnName === columnNames) || !this.availableColumns2.find(x => x.columnName === columnNames2)) {
        //       this.toastr.error('Unable to add rule. Column is invalid/does not exist.');
        //       return;
        //     }
        //   }
        // }
      }
    }

    const condition = this.conditionalOperators.find(x => x.conditionalOperatorId === conditionId);

    let format: string = '';
    let ruleDescription: string = '';
    let displayColumns: string = '';
    let displayColumns2: string = '';

    let existingRuleIndex = this.excelFileRule.findIndex(r => r.id === existingRuleId);

    if (existingRuleIndex >= 0 && subRuleId !== SubRuleTypes.Comparison) {
      const existingColumns = this.excelFileRule[existingRuleIndex].ruleColumnName;
      const combinedColumns = Array.from(new Set([...columnNames, ...existingColumns]));

      //if adding (), then use combine
      if (existingRuleId === 0) {
        //displayColumns = formatWithOrAnd(combinedColumns, subRuleId);
        displayColumns = formatWithOrAnd(combinedColumns, isCombination, rule.ruleTypeId);
      } else {
        //if editing then just add the new columnNames,        
        //displayColumns = formatWithOrAnd(columnNames, subRuleId);       
        displayColumns = formatWithOrAnd(columnNames, isCombination, rule.ruleTypeId);
      }

    } else {
      if (ruleType !== RuleTypeNames.BEValidation) {
        if (subRuleId !== SubRuleTypes.Comparison) {
          displayColumns = formatWithOrAnd(columnNames, isCombination, rule.ruleTypeId);
        }
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
        ruleDescription = this.ruleCreationFormGroup.get('aiPrompt').value.replace(/\B@(?=\w)/g, '').trim();;
        break;

      case RuleTypeNames.Format: //format

        //pattern
        switch (subRuleId) {

          case SubRuleTypes.Pattern:
            if (existingRuleIndex >= 0) {
              if (this.excelFileRule[existingRuleIndex].patternId !== patternId) {
                //lets trick existingRuleIndex to -1 to create a new excelFileRule
                existingRuleIndex = -1;

                //since it's a new pattern, reset the columns names
                displayColumns = this.ruleCreationFormGroup.get('ruleColumnName').value;
              }
            }

            ruleDescription = formatType
              ? `${displayColumns} should match this \\${formatType}\\ pattern`
              : (columnNames.length == 1)
                ? `${displayColumns} is a valid ${this.patterns.find(p => p.patternId === patternId).sing}`
                : `${displayColumns} are valid ${this.patterns.find(p => p.patternId === patternId).plu}`;
            break;
          case SubRuleTypes.Length:
            ruleDescription = `${displayColumns} string length should be ${condition.conditionalOperatorName} ${formatType}`;
            break;
          case SubRuleTypes.NumericRange:
            ruleDescription = (displayColumns.length == 1)
              ? `${displayColumns} value should be between ${fromValue} to ${toValue}`
              : `${displayColumns} values should be between ${fromValue} to ${toValue}`;
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
          let conditionalOperators = this.conditionalOperators.find(x => x.conditionalOperatorId === this.ruleCreationFormGroup.get('conditionType').value);
          if (!conditionalOperators) return;


          if (!formatType) return;

          switch (conditionalOperators.conditionalOperatorId) {
            case 1: //Equal
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} to ${formatType}`;
              break;
            case 2: //Not Equal
              ruleDescription = `${displayColumns} should ${conditionalOperators.conditionalOperatorName} to ${formatType}`;
              break;
            case 3: //Greater Than
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${formatType}`;
              break;
            case 4: //Less Than
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${formatType}`;
              break;
            case 5: //Greater Than or Equal To
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${formatType}`;
              break;
            case 6: //Less Than or Equal To
              ruleDescription = `${displayColumns} should be ${conditionalOperators.conditionalOperatorName} ${formatType}`;
              break;
          }
        }

        if (subRuleId === SubRuleTypes.ExactMatch) {
          ruleDescription = (displayColumns.length === 1)
            ? `${displayColumns} column value should match exactly the value of '${formatType}'`
            : `${displayColumns} column values should match exactly the value of '${formatType}'`;
        }

        if (subRuleId === SubRuleTypes.NumericRange) {
          if (toValue <= fromValue) {
            this.toastr.error('From and To Values are invalid.');
            return;
          }

          ruleDescription = (displayColumns.length === 1)
            ? `${displayColumns} value should be between ${fromValue} to ${toValue}`
            : `${displayColumns} values should be between ${fromValue} to ${toValue}`;
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

      let currentRule = this.excelFileRule[existingRuleIndex];

      var tmpRule = {
        id: this.excelFileRule[existingRuleIndex].id,
        ruleSetNameId: this.excelFileRule[existingRuleIndex].ruleSetNameId,
        ruleSetName: this.excelFileRule[existingRuleIndex].ruleSetName,
        ruleTypeId: rule.ruleTypeId,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames === '' ? [columnNames] : Array.isArray(columnNames) ? columnNames : [columnNames],
        ruleColumnName2: columnNames2 ?? '',
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: formatType,
        prompt: aiPrompt === undefined || aiPrompt === '' || aiPrompt === null ? '' : aiPrompt,
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: this.excelFileRule[existingRuleIndex].isGlobal,
        ruleSetType: this.excelFileRule[existingRuleIndex].ruleSetType,
        description: '',
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: this.excelFileRule[existingRuleIndex].ruleSetNameId ? false : true
      }

      var ruleAlreadyExists = this.excelFileRule.find(x =>
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

      this.excelFileRule[existingRuleIndex] = tmpRule;

      if (currentRule.ruleSetType === 2 || currentRule.isGlobal) {
        var tmpGlobalRuleDescription = currentRule.ruleDescription;
      }

      if (this.tmp_existingRuleSetNameIds.has(tmpGlobalRuleDescription)) {
        this.tmp_existingRuleSetNameIds.delete(tmpGlobalRuleDescription);
      }


    } else {
      const maxId = this.excelFileRule.length > 0 ? Math.max(...this.excelFileRule.map(r => r.id)) : 0;
      const nextId = maxId + 1;

      var tmpExcelRule = {
        id: nextId,
        ruleSetNameId: '',
        ruleSetName: ruleSetName,
        ruleTypeId: ruleType,
        subRuleId: subRuleId === '' || subRuleId === null ? null : Number(subRuleId),
        ruleColumnName: columnNames === '' ? [columnNames] : Array.isArray(columnNames) ? columnNames : [columnNames],
        ruleColumnName2: columnNames2 ?? '',
        isCombinationRule: isCombination,
        ruleDescription: ruleDescription,
        format: formatType,
        prompt: aiPrompt ?? '',
        patternId: patternId === '' || patternId === null ? null : Number(patternId),
        conditionId: conditionId === '' || conditionId === null ? null : Number(conditionId),
        fromValue: fromValue === '' || fromValue === null ? null : Number(fromValue),
        toValue: toValue === '' || toValue === null ? null : Number(toValue),
        isActive: true,
        isIrrelevant: false,
        isGlobal: isGlobal,
        ruleSetType: this.pageName === PageNames.Offline_Process ? 1 : 2,
        description: '',
        isAllowNullOrEmptySpaces: isAllowNullOrEmptySpaces,
        spNameId: spNameId,
        isUpdated: false
      };

      var ruleAlreadyExists = this.excelFileRule.find(x =>
        x.ruleTypeId === tmpExcelRule.ruleTypeId &&
        x.subRuleId === tmpExcelRule.subRuleId &&
        arraysEqual(x.ruleColumnName, tmpExcelRule.ruleColumnName) &&
        x.ruleColumnName2 === tmpExcelRule.ruleColumnName2 &&
        x.isCombinationRule === tmpExcelRule.isCombinationRule &&
        x.format === tmpExcelRule.format &&
        x.prompt === tmpExcelRule.prompt &&
        x.patternId === tmpExcelRule.patternId &&
        x.conditionId === tmpExcelRule.conditionId &&
        x.fromValue === tmpExcelRule.fromValue &&
        x.toValue === tmpExcelRule.toValue &&
        (
          this.pageName !== PageNames.Offline_Process ||
          x.isIrrelevant === tmpExcelRule.isIrrelevant
        ) &&
        x.isGlobal === tmpExcelRule.isGlobal &&
        x.isAllowNullOrEmptySpaces === tmpExcelRule.isAllowNullOrEmptySpaces &&
        x.isActive === true
      );

      if (ruleAlreadyExists) {
        this.toastr.warning('Rule already exists. Please create new one.');
        return;
      }
      this.tmp_newlyAddedRules.push(tmpExcelRule);
      this.excelFileRule.push(tmpExcelRule);
    }

    this.helperUtil.sortRuleSet(this.excelFileRule);
    this.clearRule();

  }

  onEditRule(existingRuleId: number) {
    this.checkRuleChange(existingRuleId);
  }

  onResetRule() {
    this.clearRule();
  }

  onRuleSetNameChange(event: any) {
    if (!event.target.value) return;
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
    const control = this.ruleCreationFormGroup.get(controlName);
    return control?.hasError(errorName) && control?.dirty && control?.touched;
  };

  drop(event: CdkDragDrop<string[]>) {
    moveItemInArray(this.excelFileRule, event.previousIndex, event.currentIndex);
  }

  isColumnNameLoading: boolean = false;
  editRule(existingRuleId: number) {
    this.isColumnNameLoading = true;
    let existingRuleIndex = this.excelFileRule.findIndex(x => x.id === existingRuleId);
    if (existingRuleIndex < 0) return;
    this.existingRuleId = existingRuleId;
    const existingRule = this.excelFileRule[existingRuleIndex];
    this.ruleCreationFormGroup.get('spName').setValue(existingRule.spNameId);
    this.ruleCreationFormGroup.get('ruleType').setValue(existingRule.ruleTypeId);
    this.ruleCreationFormGroup.get('ruleType')?.disable();

    const allSame = this.subRules?.length > 0 ? this.subRules.every(sub => sub.ruleTypeId === existingRule.ruleTypeId) : false;
    this.ruleCreationFormGroup.get('columnNames')?.disable();
    this.ruleCreationFormGroup.get('columnNames2')?.disable();
    const setSubRule = () => {
      this.ruleCreationFormGroup.get('subRuleType')?.patchValue(existingRule.subRuleId);
      this.selectedSubRule = this.subRules.find(subrule => subrule.subRuleId === existingRule.subRuleId)?.subRuleName;

      setTimeout(() => {
        if (this.pageName === PageNames.Offline_Process) {

          // this.ruleCreationFormGroup.get('columnNames')?.setValue(existingRule.ruleColumnName);
          // this.ruleCreationFormGroup.get('columnNames2')?.setValue(existingRule.ruleColumnName2);


          let columnName =
            existingRule.subRuleId === SubRuleTypes.Comparison
              ? String(existingRule.ruleColumnName)
              : (existingRule.ruleColumnName.length === 1 && existingRule.ruleColumnName[0] === '')
                ? null
                : existingRule.ruleColumnName;

          this.ruleCreationFormGroup.get('columnNames')?.setValue(columnName);
          this.ruleCreationFormGroup.get('columnNames2')?.setValue(existingRule.subRuleId === SubRuleTypes.Comparison ? String(existingRule.ruleColumnName2) : existingRule.ruleColumnName2);
        } else {
          this.ruleCreationFormGroup.get('columnNames')?.setValue(existingRule.ruleColumnName.join(','));
          this.ruleCreationFormGroup.get('columnNames2')?.setValue(Array.isArray(existingRule.ruleColumnName2) ? existingRule.ruleColumnName2.join(',') : existingRule.ruleColumnName2);
        }

        this.isColumnNameLoading = false;
        this.ruleCreationFormGroup.get('columnNames')?.enable();
        this.ruleCreationFormGroup.get('columnNames2')?.enable();

        this.enableValidatorsForComparison();
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



    this.ruleCreationFormGroup.get('subRuleType')?.disable();

    this.ruleCreationFormGroup.get('aiPrompt')?.setValue(existingRule.prompt ?? existingRule.description);

    const setPattern = () => {
      this.ruleCreationFormGroup.get('patternType')?.setValue(existingRule.patternId);
      this.ruleCreationFormGroup.get('patternType')?.disable();
    }

    if (this.patterns.length === 0) {
      this.getPatterns(existingRule.subRuleId).subscribe(patterns => {
        this.patterns = patterns;
        setPattern();
      });
    } else {
      setPattern();
    }



    this.ruleCreationFormGroup.get('formatType')?.setValue(existingRule.format);
    this.ruleCreationFormGroup.get('conditionType')?.setValue(existingRule.conditionId);
    this.ruleCreationFormGroup.get('conditionType')?.disable();




    this.ruleCreationFormGroup.get('fromValue')?.setValue(existingRule.fromValue);
    this.ruleCreationFormGroup.get('toValue')?.setValue(existingRule.toValue);

    this.ruleCreationFormGroup.get('isCombinationRule')?.setValue(existingRule.isCombinationRule);
    this.ruleCreationFormGroup.get('isAllowNullOrEmptySpaces')?.setValue(existingRule.isAllowNullOrEmptySpaces);


  }

  removeRule(ruleId: number) {

    let excelFileRuleIndex = -1;
    let rule: ExcelRule = null;

    this.excelFileRule.some((x, index) => {
      if (x.id === ruleId) {
        excelFileRuleIndex = index;
        rule = x;
        return true;
      }
      return false;
    });

    this.excelFileRule[excelFileRuleIndex].isActive = false;


    //if (rule.isGlobal || rule.ruleSetType === 2) {
    // let countOfRulesIn = this.excelFileRule.filter(r => r.ruleSetNameId === rule.ruleSetNameId && r.isActive);
    // if (countOfRulesIn.length === 0) {


    //   // Remove the ruleSetNameId from the selected values in the form control
    //   const selectedRuleSetNames = this.ruleCreationFormGroup.get('ruleSetNames')?.value || [];

    //   const updatedSelection = selectedRuleSetNames.filter(
    //     (id: string) => id !== rule.ruleSetNameId
    //   );

    //   this.ruleCreationFormGroup.get('ruleSetNames')?.setValue(updatedSelection);

    // }
    var currentRuleDesc = this.excelFileRule.find(x => x.id === ruleId).ruleDescription;
    if (this.pageName === PageNames.Offline_Process) {
      if (!this.inputRuleSetPayload) {
        this.excelFileRule.find(x => x.id === ruleId).isActive = false;
      }
    } else {
      if (this.tmp_existingRuleSetNameIds.has(currentRuleDesc)) {
        this.tmp_existingRuleSetNameIds.delete(currentRuleDesc);

        this.tmp_newlyAddedRules = this.tmp_newlyAddedRules.filter(x => x.ruleDescription !== currentRuleDesc);
        this.excelFileRule = this.excelFileRule.filter(x => x.id !== ruleId);
      }

      this.tmp_newlyAddedRules = this.tmp_newlyAddedRules.filter(x => x.ruleDescription !== currentRuleDesc);
      this.excelFileRule = this.excelFileRule.filter(x => x.id !== ruleId);
    }

    // }

    if (this.excelFileRule.length === 0) {
      //this.clearRule();
    }

    // var rule = this.excelFileRule.find(x => x.id === id);
    // if (rule) {
    //   rule.isActive = false;
    // }
  }

  onRestore(id: number) {
    var rule = this.excelFileRule.find(x => x.id === id);
    if (rule) {
      rule.isActive = true;
    }
  }

  viewDetails(ruleSetNameId: string) {
    this.isEditing = true;
    this.dataInsiderService.getRuleSetByRuleSetNameId(ruleSetNameId).subscribe({
      next: (response: APIResponse<ExcelRule[]>) => {
        if (response?.responseCode === 200) {
          this.excelFileRule = response.result;
          //use the ruleSetNameId 
          this.excelFileRule = this.excelFileRule.map(rule => ({ ...rule, ruleSetNameId: ruleSetNameId }))
          this.populateOtherFieds();
        }
      },
      error: error => {
        this.toastr.error(ToastrMessages.SomethingWentWrong);
        console.log(error);
      }
    });

  }

  onChangeRuleSetNames(rs: RuleSetNames[]) {


    if (rs.length > 0) {
      // this.removedItemsGloGenRuleSetNames = this.selectedGloGenRuleSetNames.filter(item => !rs.includes(item));
      // this.selectedGloGenRuleSetNames = [...rs];

      this.getRuleSetByRuleSetNameId(rs.map(x => x.ruleSetNameId).join(","));
      this.existingRuleSetNameSelected = true;

    } else {
      this.ruleCreationFormGroup.get('ruleSetNames')?.setValue(null);

      this.excelFileRule = this.excelFileRule.filter(r => !(r.isGlobal || r.ruleSetType === 2));
      this.existingRuleSetNameSelected = false;
    }
  }
  private tmp_existingRuleSetNameIds = new Set<string>();
  getRuleSetByRuleSetNameId(rsnId: string) {
    // let existingRuleSetNameIds = this.excelFileRule.filter(r => r.isGlobal || r.ruleSetType === 2).map(x => x.ruleSetNameId);
    // //only retrieve the newly selected item
    // let updatedRuleSetIdToRetrieve = rsnId.split(',').filter(id => !existingRuleSetNameIds.includes(id)).join(',');
    // if (updatedRuleSetIdToRetrieve === '') {
    //   updatedRuleSetIdToRetrieve = rsnId;
    // }

    this.isgetRuleSetByRuleSetNameIdLoading = true;
    this.dataInsiderService.getRuleSetByRuleSetNameId(rsnId).subscribe({
      next: (response: APIResponse<ExcelRule[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            //clear all rulesetnameid, 

            let newExcelFileRule: ExcelRule[] = response.result.map(rule => ({
              ...rule,
              ruleSetName: this.ruleCreationFormGroup.get('ruleSetName')?.value,
              ruleSetNameId: '',//rule.ruleSetNameId,
              isIrrelevant: false,
              description: '' //lets clear this
            }));

            const allDuplicate = response.result.every(x => this.tmp_existingRuleSetNameIds.has(x.ruleDescription));
            if (allDuplicate) {
              this.toastr.warning('Rule already exists. Please create new one.');
              this.isgetRuleSetByRuleSetNameIdLoading = false;
              return;
            }

            if (this.tmp_existingRuleSetNameIds.has(response.result[0].ruleDescription)) {

            }


            // const uniqueNewRules = newExcelFileRule.filter(
            //   rule => !existingRuleSetNameIds.includes(rule.ruleSetNameId)
            // );

            //add the new globa/generic rule
            newExcelFileRule.forEach(x => {
              var exists = this.tmp_newlyAddedRules.find(y => y.ruleDescription === x.ruleDescription);
              if (!exists) {
                this.tmp_newlyAddedRules.push(x);
                this.tmp_existingRuleSetNameIds.add(x.ruleDescription);
                this.excelFileRule.push(x);
              }
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
            this.tmp_existingRuleSetNameIds.add(response.result[0].ruleDescription);
            this.isgetRuleSetByRuleSetNameIdLoading = false;

            this.findAllIrrelevant();
            this.helperUtil.sortRuleSet(this.excelFileRule);
            this.clearRule();
          }
        }
      }, error: () => {
        this.isgetRuleSetByRuleSetNameIdLoading = false;
        this.toastr.error('Something went wrong. Unable to retrieve Rule Sets')
      }
    });

  }

  findAllIrrelevant() {
    let hasMatch: any;

    this.excelFileRule.forEach(rule => {
      if (rule.ruleTypeId != RuleTypeNames.Custom) {
        const ruleColumns = rule.ruleColumnName;
        const columnNames = this.columnNames.map(col => col);

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

  onSearchRuleSetName(searchTerm: string): void {

    //if (searchTerm.length >= 5) {
    const uniqueSearchTerm = `${searchTerm}`; // Append a timestamp -${new Date().getTime()
    this.searchRuleSetName$.next(uniqueSearchTerm); // Emit the unique search term
    //}
  }
  defaultRuleSetNames: RuleSetNames[] = [];
  onClearRuleSetNames() {
    this.ruleSetNames = this.defaultRuleSetNames;
  }

  fnCompareWithRuleSetNames(item: RuleSetNames, value: any): boolean {
    return item?.ruleSetNameId === value;
  }

  setupSearchRuleSetNames(): void {
    this.searchRuleSetName$
      .pipe(
        debounceTime(300),
        switchMap((uniqueSearchTerm: string) => {
          const searchTerm = uniqueSearchTerm;//.split('-')[0];
          if (searchTerm.length >= 1) {
            //this.ruleSetNames = [];
            this.isgetRuleSetBySecGrpIdsLoading = true;
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


          response.result.slice(0, 10).forEach(c => {
            //this.securityGroups.push({ displayName: c.displayName, id: c.id });
            tempAny.push({ ruleSetNameId: c.ruleSetNameId, ruleSetName: c.ruleSetName });
          });

          this.ruleSetNames = tempAny;

          this.isgetRuleSetBySecGrpIdsLoading = false;
        },
        error: (error) => {
          console.error('Error fetching rule set names', error);
          this.isgetRuleSetBySecGrpIdsLoading = false;
        }
      });
  }

  getRuleSetNamesBySecGrpIds(sgIds: string) {
    this.ruleSetNames = [];
    this.defaultRuleSetNames = [];
    this.isgetRuleSetBySecGrpIdsLoading = true;
    //let sgIds =  this.securityGroups.map(sg => sg.id).join(',');
    this.dataInsiderService.getRuleSetNamesBySecGrpId(sgIds).subscribe({
      next: (response: APIResponse<RuleSetNames[]>) => {
        if (response) {
          if (response.responseCode === 200) {
            this.isgetRuleSetBySecGrpIdsLoading = false;
            this.ruleSetNames = response.result;
            this.defaultRuleSetNames = this.ruleSetNames;
          }
        }
      }, error: () => {
        this.isgetRuleSetBySecGrpIdsLoading = false;
      }
    });
  }

  onCancel() {


    //check if some fields were populated; else just close
    if (this.excelFileRule.length > 0 ||
      this.ruleCreationFormGroup.get('ruleSetName')?.value.trim().length > 0 ||
      this.ruleCreationFormGroup.get('description')?.value.trim().length > 0
    ) {

      //check if there are entries that were not saved
      if (this.tmp_newlyAddedRules.length > 0) {

      }

      // ruleSets : Omit<ExcelRule, 'isIrrelevant'>[];
      // created_by : string;
      // username : string;
      // description : string;
      const payLoad: PayLoad = {
        ruleSets: this.excelFileRule,  //,
        created_by: sessionStorage.getItem('upn').split('@')[0],
        username: sessionStorage.getItem('username'),
        description: this.ruleCreationFormGroup.get('description')?.value,
        ruleSetName: this.ruleCreationFormGroup.get('ruleSetName')?.value
      };

      this.notifyParentToClose.emit({ saveCancel: 'cancel', showUIValidation: false, payLoad: payLoad });
      return;
    }
    this.notifyParentToClose.emit({ saveCancel: 'cancel', showUIValidation: false, payLoad: null });

  }

  getTotalCountOfRules() {
    return this.excelFileRule.filter(r => r.isActive).length;
  }

  populateOtherFieds() {

    if (this.pageName === PageNames.Offline_Process) {
      //we used this if when creating process in offline mode
      this.ruleCreationFormGroup.get('description')?.setValue(this.inputRuleSetPayload.description);
      this.ruleCreationFormGroup.get('ruleSetName')?.setValue(this.inputRuleSetPayload.ruleSetName);
      this.ruleCreationFormGroup.get('ruleSetName')?.disable();
    } else {
      this.ruleCreationFormGroup.get('ruleSetName')?.setValue(this.excelFileRule[0].ruleSetName);
      this.ruleCreationFormGroup.get('ruleSetName')?.disable();
      this.ruleCreationFormGroup.get('description')?.setValue(this.excelFileRule[0]?.description);
      this.ruleCreationFormGroup.get('isGlobal')?.setValue(this.excelFileRule[0]?.isGlobal);
    }

  }

  suggestions: string[] = [];
  onCustomColumnNameChange(event: Event): void {
    const textArea = this.txtareaAiPromptRef.nativeElement;
    if (!textArea) return;

    const value = textArea.value;
    const caretPos = textArea.selectionStart;
    const match = value.slice(0, caretPos).match(/@(\w*)$/);
    const columns = this.columnNames.map(col => col) ?? [];
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

    this.ruleCreationFormGroup.get('aiPrompt')?.setValue(newValue);
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

  setRuleColumnToMultiple() {
    return this.ruleCreationFormGroup.get('subRuleType')?.value !== SubRuleTypes.Comparison;
  }

  compareWithColumnNames(item1: any, item2: any): boolean {
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

  isGeneratingCustomRule: boolean = false;
  customRuleGenerated: boolean = false;
  onGenerateCustomRule() {
    this.isGeneratingCustomRule = true;
    this.customRuleGenerated = false;


    // let fileHeaders: { Column: string, DataType: string }[] = this.columnDatatype.map(item => ({ Column: item.ColumnName, DataType: item.DatatypeName }));
    let column_headers_generated = { ColumnHeaders: [] };

    let payload: DataAssistsRequest = {
      flowid: 1092,
      versionId: 2,
      projectId: 2015,
      isDataValidation: true,
      isCodeSnippet: true,
      fileHeaders: column_headers_generated,
      validationRules: this.ruleCreationFormGroup.get('aiPrompt')?.value,
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

  enableCombination(input: string | any) {
    var rawValues;
    if (Array.isArray(input)) {
      rawValues = input;
    }
    else {
      rawValues = input.split(',').map(v => v.trim()).filter(v => v.length > 0);
    }
    this.ruleCreationFormGroup.get('isCombinationRule')?.disable();
    if (rawValues?.length >= 2) {
      this.ruleCreationFormGroup.get('isCombinationRule')?.enable();
      return;
    }
    this.ruleCreationFormGroup.get('isCombinationRule')?.setValue(false);

  }

  get activeRules(){
    return this.excelFileRule.filter(r => r.isActive);
  }



}


export function columnNamesUniqueValidator(): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const column1 = Array.isArray(group.get('columnNames')?.value) ? group.get('columnNames')?.value[0].trim() : group.get('columnNames')?.value?.trim();
    const column2 = Array.isArray(group.get('columnNames2')?.value) ? group.get('columnNames2')?.value[0].trim() : group.get('columnNames2')?.value?.trim();

    if (column1 && column2 && column1 === column2) {
      return { columnNamesNotUnique: true };
    }

    return null;
  };

}
