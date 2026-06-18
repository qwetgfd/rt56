import { ChangeDetectorRef, Component, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { NgbActiveModal, NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { FileType, ModalMessages, ToastrMessages } from '../../shared/enum';
import { DiParserService } from '../../core/services/di-parser.service';
import { KeyValuePair } from '../../core/models/fileConfiguration';
import { EibService } from '../../core/services/eib/eib.service';
import { APIResponse } from '../../core/models/apiResponse';
import { BusinessProcessDetails, DBView, EIBConfigurationDetails, EIBCountry, MappingDetails } from '../../core/models/EIB/dbView';
import { ToastrService } from 'ngx-toastr';
import { CustomEIBViewComponent } from '../custom-eib-view/custom-eib-view.component';
import { ModalService } from '../../core/services/confirm-modal.service';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { trigger, transition, style, animate } from '@angular/animations';
import { TablePageEvent } from 'primeng/table';
@Component({
  selector: 'app-create-eib',
  templateUrl: './create-eib.component.html',
  styleUrl: './create-eib.component.css',
  standalone: false,
  animations: [
    trigger('slideIn', [
      transition(':enter', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate('500ms ease-out', style({ transform: 'translateX(0)', opacity: 1 }))
      ]),
      transition(':leave', [
        animate('500ms ease-in', style({ transform: 'translateX(100%)', opacity: 0 }))
      ])
    ])
  ]
})
export class CreateEibComponent implements OnInit {


  isDragOver = false;
  isFileUploaded = false;
  createEIBForm: FormGroup = new FormGroup({});
  dbViews: DBView[] = [];
  showModal: boolean = false;
  paramsId: string = '';
  eibConfiguration: EIBConfigurationDetails;
  totalRecords: number = 0;
  mapping: MappingDetails[] = [];
  country: EIBCountry[] = [];
  constructor(
    private fb: FormBuilder,
    private myParser: DiParserService,
    private eibService: EibService,
    private toastr: ToastrService,
    private modalService: ModalService,
    private cdr: ChangeDetectorRef,
    private route: ActivatedRoute,
    private router: Router,

  ) { }

  ngOnInit(): void {

    this.route.queryParams.subscribe(params => {
      this.paramsId = params['eibid'] || '';
      if (this.paramsId) {
        this.isFileUploaded = true;
      }
    });


    //  this.form.get('formFile')?.valueChanges.subscribe(value => {
    //     console.log('Input changed:', value);
    //   });


    this.initializeForm();
    this.getAllDBViews();
    this.getAllCountries();
    if (this.paramsId) {
      this.GetEIBByEIBId();
    }
  }

  initializeForm() {
    this.createEIBForm = this.fb.group({
      formFile: [{ value: '', disabled: true }, Validators.required],
      eibName: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(400)]],
      description: [''],
      businessProcess: [null],
      viewName: [null],
      mappingCount: 0,
      noOfBusinessProcess: 0,
      country: [null, [Validators.required]]
    });

    if (this.paramsId !== '') {
      this.createEIBForm.get('eibName')?.disable();
      this.createEIBForm.get('country')?.disable();
    }
  }

  GetEIBByEIBId() {
    this.eibService.GetEIBByEIBId(this.paramsId).subscribe({
      next: (response: APIResponse<EIBConfigurationDetails>) => {
        if (response?.responseCode === 200 && response.result) {
          this.eibConfiguration = response.result;

          //lets populate the form
          this.createEIBForm.get('eibName')?.setValue(this.eibConfiguration.eibName);
          this.createEIBForm.get('description')?.setValue(this.eibConfiguration.description);
          this.createEIBForm.get('noOfBusinessProcess')?.setValue(this.eibConfiguration.noOfBusinessProcess);
          //todo:
          this.createEIBForm.get('country')?.setValue(this.eibConfiguration.countryId);
          const map = new Map<string, { id: number; processName: string; dbView: DBView[], isDeleted: false }>();
          for (const [i, item] of this.eibConfiguration.businessProcessDBViewMapping.entries()) {
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
                  id: map.size,
                  processName: item.businessProcessName,
                  dbView: [dbViewEntry],
                  isDeleted: false
                });
            }
          }
          this.mapping = Array.from(map.values());
          this.recordsToDisplay = this.mapping;
          this.businessProcessDetails = this.eibConfiguration.businessProcessNames.map((x, i) => ({
            row: i + 1,
            processNameId: x.businessProcessNameId,
            processName: x.businessProcessName,
            disabled: x.fieldCount === 0 ? true : false,
            fieldCount: x.fieldCount,
            isMapped: this.eibConfiguration.businessProcessDBViewMapping.find(m => m.businessProcessName === x.businessProcessName) ? true : false,
            isRequired: x.isRequired,
            category: ''
          }));

          //creating grouping
          this.businessProcessDetails.forEach(v => {
            if (v.isRequired) {
              v.category = 'Required'
            } else {
              if (!v.disabled) {
                v.category = 'Optional'
              } else {
                v.category = 'Others'
              }
            }
          });

          this.getMappedRequiredBusinessProcess();
        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }

      },
      error: error => {
        console.error("Error retrieving EIB Configuration Details");
      }
    })
  }

  EIBRequiredBPKeyword: string = '';
  GetEIBRequiredBPKeyword(): Promise<void> {
    return new Promise((resolve, reject) => {
      this.eibService.GetEIBRequiredBPKeyword().subscribe({
        next: (response: APIResponse<string>) => {
          if (response?.responseCode === 200 && response.result) {
            this.EIBRequiredBPKeyword = response.result?.trim().toLocaleLowerCase();
            resolve();
          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            resolve();
            // Optionally show a message to the user or handle fallback logic here
          }
        },
        error: error => {
          console.error("Error retrieving EIB Required BP Keyword.");
          reject();
        }
      });
    })

  }

  getAllDBViews() {
    this.eibService.GetAllViews().subscribe({
      next: (response: APIResponse<DBView[]>) => {
        if (response?.responseCode === 200 && response.result) {
          this.dbViews = response.result;


        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }

      },
      error: error => {
        console.error("Error retrieving DB Views");
      }
    });
  }

  getAllCountries() {
    this.eibService.GetAllCountries().subscribe({
      next: (response: APIResponse<EIBCountry[]>) => {
        if (response?.responseCode === 200 && response.result) {
          this.country = response.result;


        }
        else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }

      },
      error: error => {
        console.error("Error retrieving EIB Countries");
      }
    })
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
    if (event.dataTransfer && event.dataTransfer.files.length > 0) {
      const file = event.dataTransfer.files[0];
      // You can call your file change handler here
      this.onFileChange({ target: { files: [file] } });
    }
  }
  file: File;
  wksheets: KeyValuePair[] = [];
  businessProcessDetails: BusinessProcessDetails[] = [];
  async onFileChange(event: any) {
    const file = event.target.files[0] as File;

    if (file.name.split('.').pop() !== FileType.MSExcel2) {
      this.toastr.error(ModalMessages.InvalidFile);
      event.target.value = '';
      return;
    }

    if (this.EIBRequiredBPKeyword === '') {
      await this.GetEIBRequiredBPKeyword();

    }
    this.processFile(file);

  }

  isCheckingActiveEIBCofiguration: boolean = false;
  CheckActiveEIBConfiguration() {
    const control = this.createEIBForm.get('eibName');

    if (control?.value.trim().length === 0 || control?.value.trim().length < 5 || control?.value.trim().length > 400) { return; }
    this.isCheckingActiveEIBCofiguration = true;

    this.eibService.CheckActiveEIBConfiguration(control?.value).subscribe({
      next: (response: APIResponse<boolean>) => {
        if (response?.responseCode === 200) {
          control?.setErrors(null);
          if (response.result === true) {
            control?.setErrors({ eibNameExist: true });
          } else {

          }
        } else {
          console.warn(`Unexpected response code: ${response?.responseCode}`);
        }
        this.isCheckingActiveEIBCofiguration = false;
      },
      error: error => {
        console.log(error);
        this.isCheckingActiveEIBCofiguration = false;
      }
    });
  }

  async processFile(file: File) {
    //todo: create a checker if same EIB version is uploaded. version is found in cell b2 
    if (file) {

      if (file.name.split('.').pop() === FileType.MSExcel2) {
        if (file.size > 10 * 1024 * 1024) {
          this.createEIBForm.get('formFile')?.setErrors({ fileIsTooLarge: true });
          return;
        }
        this.createEIBForm.get('formFile')?.setErrors(null);

        this.mapping = [];
        this.recordsToDisplay = [];
        this.createEIBForm.get('formFile')?.setValue(file.name);
        this.isFileUploaded = true;

        this.createEIBForm.markAsDirty();
        try {
          const parsedExcel = await this.myParser.asyncParseExcel(
            file, 1, 0, false, 'sheetName'
          );
          if (parsedExcel) {
            this.file = file;
            this.wksheets = parsedExcel.filter(wk => wk.sheetName && wk.sheetName.trim().length > 0);
            if (this.wksheets[0].sheetName.toLowerCase() !== 'overview') {
              //todo: create an error
              this.toastr.error(`Inavlid Template. Worksheet "Overview" not found.`);
              this.isFileUploaded = false;
              return;
            }

            let eibName = this.wksheets[0].workBook[1][1];

            let description = this.wksheets[0].workBook[3][1];
            const values: BusinessProcessDetails[] = [];
            //read first the overview
            //assuming it starts B6
            for (let i = 6; i < this.wksheets[0].workBook.length; i++) {
              const processName = this.wksheets[0].workBook[i][1];
              const isRequired = this.wksheets[0].workBook[i][2]?.trim().toLowerCase().includes(this.EIBRequiredBPKeyword); //todo, put this in keyvault

              //if (processName == undefined || processName === "") break;
              if (processName.trim().length > 0) {
                values.push({
                  row: i + 1, processName: processName, processNameId: '', fieldCount: 0, isRequired: isRequired, disabled: false, isMapped: false,
                  category: ''
                });
              }
            }

            for (let i = 0; i < values.length; i++) {
              //if (!this.wksheets[0]?.workBook?.[5 + (i + 1)]?.[1]) break;
              const processName = this.wksheets[0].workBook[5 + (i + 1)][1];
              let ctr = 1;
              let count = 0;
              let continueWhile = true;
              while (continueWhile) {
                //B4
                //let wk = this.wksheets.find(x => x.workBook?.[0][0] === processName); use to retrieve cell A1
                //let wk = this.wksheets.find(x => x.sheetName === processName);

                //retrieve the worksheet by business process name
                let wk = this.wksheets.find(x => processName.toLowerCase().includes(x.sheetName.toLowerCase()));
                //if no worksheet, disable the option in ngselect
                if (!wk) {
                  let updateValues = values.find(x => x.processName === processName);
                  if (updateValues) {
                    updateValues.disabled = true;
                    updateValues.isRequired = false; //lets disable the required property since it has no worksheet
                  }

                  //nothing to process, no worksheet found
                  continueWhile = false;
                  //break;
                }
                else {
                  //lets count how many field count
                  const cell = wk.workBook[4][ctr++];
                  if (!cell || cell.v === "") break;
                  count++;
                }

              }

              //if (processName === undefined || processName === "") break;
              if (processName.trim().length > 0) {
                let foundSheet = values.find(x => x.processName === processName);
                if (foundSheet) {
                  foundSheet.fieldCount = count;
                }
              }
              //values.push({ row: i + 1, processName: processName, fieldCount: count });
            }

            if (values.length > 0) {

              const categoryOrder = ['Required', 'Optional', 'Others'];

              //creating grouping; cleansing
              values.forEach(v => {
                //clean the ngselect values; if fieldCount = 0, disable and remove required if applicable
                if (v.fieldCount === 0) {
                  v.isRequired = false;
                }
                if (v.isRequired) {
                  v.category = 'Required'
                } else {
                  if (!v.disabled) {
                    v.category = 'Optional'
                  } else {
                    v.category = 'Others'
                  }
                }
              });




              //let's sort
              values.sort((a, b) => {
                const categoryComparison = categoryOrder.indexOf(a.category) - categoryOrder.indexOf(b.category);
                if (categoryComparison !== 0) return categoryComparison;

                //if same category, compare alpha by processName
                return a.processName.localeCompare(b.processName);
              });

              this.businessProcessDetails = values;
            } else {
              this.toastr.error(`File doesn't contain any business process. You can not continue.`);
            }

            this.createEIBForm.get('eibName')?.setValue(eibName);
            this.createEIBForm.get('description')?.setValue(description);
            this.createEIBForm.get('noOfBusinessProcess')?.setValue(values.length);
            this.createEIBForm.get('country')?.setValue(null);
            this.createEIBForm.get('businessProcess')?.setValue(null);
            this.createEIBForm.get('viewName')?.setValue(null);
            //console.log("Column B values from B7 down:", values);
            this.CheckActiveEIBConfiguration();
            this.getMappedRequiredBusinessProcess();
          }
        } catch (error) {
          this.isFileUploaded = false;
          // Handle the error gracefully
          console.error('Failed to parse Excel:', error);

          // Example: Show a user-friendly message
          this.toastr.error('Error parsing Excel file. Please check the file format and try again.');

        }


      }
    }
  }

  onChangeFile(event: any) {
    this.isFileUploaded = false;
    this.paramsId = '';
    this.createEIBForm.get('eibName')?.enable();
    this.createEIBForm.get('country')?.enable();
    this.createEIBForm.markAsPristine();

    event.preventDefault();

    this.router.navigate(['/create-eib'], {
      replaceUrl: true,
      state: { fromCreateEIB: true }
    });

  }

  getMappedRequiredBusinessProcess() {
    let count = this.businessProcessDetails.filter(bp => bp.isRequired && !bp.disabled).length;
    let mapped = this.businessProcessDetails.filter(bp => bp.isRequired && !bp.disabled && bp.isMapped).length;
    (document.getElementById('noOfRequiredBPMap') as HTMLInputElement).value = count === 0 ? String("0") : `${mapped} of ${count}`;
    // return this.businessProcessDetails.filter(bp => bp.isRequired && !bp.disabled).length;
  }

  enableAddButton(): boolean {

    if (!this.file && this.paramsId === '') return true;
    if (this.recordsToDisplay.length === 0) return true;
    if (this.isCheckingActiveEIBCofiguration) return true;
    if (this.createEIBForm.get('country')?.value === '') return true;

    const requiredBP = this.businessProcessDetails
      .filter(bp => bp.isRequired)
      .map(bp => bp.processName);

    const isAllRequiredBPMapped = requiredBP.every(processName =>
      this.recordsToDisplay.some(mapped => mapped.processName === processName && mapped.isDeleted === false)
    );

    if (!isAllRequiredBPMapped) return true;
    if (!this.createEIBForm.valid) return true;

    return false;


  }

  recordsToDisplay: MappingDetails[] = [];
  onAdd() {




    //todo: add a column in mapping to show the exact sheetname

    // this.showModal = false;
    const selectedProcess: BusinessProcessDetails = this.createEIBForm.get('businessProcess').value;
    const selectedView: string[] = this.createEIBForm.get('viewName')?.value;

    //const selectedProcess : BusinessProcessDetails = this.businessProcessDetails.find(bp => bp.processName === selectedProcessValue);
    if (!selectedProcess || !selectedView) {
      console.log('nothing selected');
      return;
    }

    this.createEIBForm.get('businessProcess').enable();
    type DbView = {
      dbViewId: string;
      businessProcessNameId: string;
      viewId: number;
      viewName: string;
      columnCount: number;
      fromColumn: string;
      toColumn: string;
    };
    //const dbViewDetails = selectedView.map(viewName => this.dbViews.find(v => v.viewName === viewName));
    const dbViewDetails = selectedView.map(viewName => this.dbViews.find(v => v.viewName === viewName))
      .filter((v): v is DbView => !!v)
      .map(v => ({ ...v }));
    let totalColumnCount: number = 0;
    if (dbViewDetails.length > 1) {
      totalColumnCount = dbViewDetails.reduce((sum, view) => {
        return sum + (view?.columnCount || 0);
      }, 0);
    } else {
      totalColumnCount = dbViewDetails[0]?.columnCount || 0;
    }
    if (selectedProcess.fieldCount != totalColumnCount) {
      this.toastr.error(ToastrMessages.EIBColumnCountNotEqual);
      return;
    }

    if (this.isEditingMappedBPView) {
      this.mapping.find(m => m.processName === selectedProcess.processName && m.isDeleted === false).isDeleted = true;
      //this.mapping = this.mapping.filter(m => m.processName != selectedProcess.processName);
    }

    let mappedViews: DBView[] = [];

    dbViewDetails.forEach((view, i) => {
      let columnViews: string[] = [];
      if (i === 0) {
        columnViews = this.generateLetterSequence(view.columnCount, 'B');
      }
      else {
        const prevToColumn = dbViewDetails[i - 1].toColumn;
        const nextCharChode = prevToColumn.charCodeAt(0) + 1;
        const nextStartLetter = String.fromCharCode(nextCharChode);
        columnViews = this.generateLetterSequence(view.columnCount, nextStartLetter);
      }
      view.fromColumn = columnViews[0];
      view.toColumn = columnViews[columnViews.length - 1];
    });

    this.mapping = [{ id: this.mapping.length, processName: selectedProcess.processName, dbView: dbViewDetails, isDeleted: false }, ...this.mapping];
    this.recordsToDisplay = this.mapping.filter(x => x.isDeleted === false);
    this.totalRecords = this.recordsToDisplay.length;
    this.updateOptions();
    this.getMappedRequiredBusinessProcess();
    this.createEIBForm.get('businessProcess')?.setValue(null);
    this.createEIBForm.get('viewName')?.setValue(null);
    this.isEditingMappedBPView = false;
    this.editingRowIndex = null;
    this.confirmIndex = null;
    return;

    // // } else {

    // //   if (selectedProcess.fieldCount !== dbViewDetails[0].columnCount) {
    // //     this.toastr.error('Business Process Name Field Count is not same with selected View', '', { enableHtml: true });
    // //     return;
    // //   }



    // //   mappedViews = dbViewDetails;
    // // }


    // //put all mapped to an array
    // this.mapping = [...this.mapping, { processName: selectedProcess.processName, dbView: mappedViews }];
    // this.totalRecords = this.mapping.length;
    // this.updateOptions();
    // this.isEditingMappedBPView = false;
    // this.createEIBForm.get('businessProcess')?.setValue(null);
    // this.createEIBForm.get('viewName')?.setValue(null);

    // this.getMappedRequiredBusinessProcess();
  }


  generateLetterSequence(length: number, startingLetter: string): string[] {
    const result: string[] = [];

    // Convert startingLetter to its corresponding index (e.g., A = 1, Z = 26, AA = 27, etc.)
    function columnToIndex(col: string): number {
      let index = 0;
      for (let i = 0; i < col.length; i++) {
        index *= 26;
        index += col.charCodeAt(i) - 64; // 'A' is 65 in ASCII
      }
      return index;
    }

    // Convert index back to Excel-style column letters
    function indexToColumn(index: number): string {
      let col = '';
      while (index > 0) {
        index--; // Adjust for 1-based indexing
        col = String.fromCharCode((index % 26) + 65) + col;
        index = Math.floor(index / 26);
      }
      return col;
    }

    let startIndex = columnToIndex(startingLetter.toUpperCase());

    for (let i = 0; i < length; i++) {
      result.push(indexToColumn(startIndex + i));
    }

    return result;
  }


  updateOptions() {
    const selected = this.createEIBForm.get('businessProcess')?.value;
    const item = this.businessProcessDetails.find(bp => bp.processName === selected.processName);
    if (item) item.isMapped = true;
    this.createEIBForm.get('businessProcess')?.reset();
    this.cdr.detectChanges();
  }




  handleConfirm(data: any) {
    console.log('Confirmed with data:', data);
    this.showModal = false;
  }

  handleClose() {
    console.log('Modal closed');
    this.showModal = false;
  }

  getViewNames(mappedData: DBView[]): string {
    // if (mappedData.length === 1) {
    //   return mappedData[0].viewName;
    // }

    return mappedData?.map(view => {
      const name = view.viewName || 'Unnamed View';
      const from = view.fromColumn || '';
      const to = view.toColumn || '';
      return `${name}(${from}-${to})`;
    }).join(', ') || '';
  }

  onSubmit() {
    const eibName = this.createEIBForm.get('eibName')?.value;
    const description = this.createEIBForm.get('description')?.value;
    const noOfBusinessProcess = this.createEIBForm.get('noOfBusinessProcess')?.value;
    const country = this.createEIBForm.get('country')?.value;

    let payLoad: EIBConfigurationDetails = {
      configurationId: this.paramsId == undefined || this.paramsId == ''
        ? null
        : this.paramsId,
      EIBId: '',
      eibName: eibName,
      description: description,
      noOfBusinessProcess: noOfBusinessProcess,
      updatedDateTime: '',
      createdBy: sessionStorage.getItem('username'),
      updatedBy: sessionStorage.getItem('username'),
      isActive: true,
      businessProcessNames: [],
      businessProcessDBViewMapping: [],
      countryId: country,
      countryName: ''
    };

    this.businessProcessDetails.forEach(bp => {
      payLoad.businessProcessNames.push({
        businessProcessNameId: bp.processNameId,
        EIBId: '',
        businessProcessName: bp.processName,
        fieldCount: bp.fieldCount,
        creationDateTime: '',
        isActive: true,
        createdBy: sessionStorage.getItem('username'),
        updatedBy: sessionStorage.getItem('username'),
        isDisabled: bp.disabled,
        isRequired: bp.isRequired
      });
    })

    this.mapping.forEach(map => {
      map.dbView.forEach(view => {
        payLoad.businessProcessDBViewMapping.push({
          bpnViewId: view.dbViewId,
          businessProcessNameId: view.businessProcessNameId,
          viewName: view.viewName,
          viewNameId: view.viewId,
          fromColumn: view.fromColumn,
          toColumn: view.toColumn,
          isActive: !map.isDeleted,
          businessProcessName: map.processName,
          columnCount: view.columnCount,
          updatedBy: sessionStorage.getItem('username')
        });
      });
    });


    this.eibService.insertEIBConfigurationDetails(payLoad, this.file).subscribe({
      next: (response: APIResponse<boolean>) => {
        if (response?.responseCode === 200 && response.result) {
          this.toastr.success('Successfully saved EIB configuration details.');

          //clear the form
          this.router.navigate(['/eib']);
        }
        else {
          this.toastr.error(response?.responseMessage[0]);
          console.warn(`Unexpected response code: ${response?.responseCode}`);
          // Optionally show a message to the user or handle fallback logic here
        }
      },
      error: error => {
        console.log(error);
        this.toastr.error(ToastrMessages.SomethingWentWrong);
      }
    });






  }
  onReset() {
    this.confirmIndex = null;
    this.editingRowIndex = null;
    if (this.file) {
      this.createEIBForm.patchValue({ file: this.file });
      this.isEditingMappedBPView = false;
      this.createEIBForm.get('country')?.setValue(null);
      this.processFile(this.file);
    } else {
      this.GetEIBByEIBId();
    }
  }
  onBack() {
    if (this.createEIBForm.dirty) {
      this.modalService.confirm(ModalMessages.EIBCreationTitle, ModalMessages.EIBCreationMessage)
        .then(async confirmed => {
          if (confirmed === true) {
            this.router.navigate(['/eib'], { state: { fromCreateEIB: true } });
            return;
          }
        });
      return;
    }
    this.router.navigate(['/eib'], { state: { fromCreateEIB: true } });
  }

  hasError = (controlName: string, errorName: string) => {
    const control = this.createEIBForm.get(controlName);
    return control?.hasError(errorName) && control?.dirty && control?.touched;
  };

  isEditingMappedBPView: boolean = false;
  onEditEIB(mapped: MappingDetails, rowIndex: number) {
    //console.log(mapped);
    //close slide-in immediately
    this.confirmAction = null;
    this.confirmIndex = null;

    // 2) Mark the row as being edited
    this.editingRowIndex = rowIndex;

    this.isEditingMappedBPView = true;
    this.createEIBForm.get('businessProcess')?.disable();
    this.createEIBForm.get('businessProcess')?.setValue(this.businessProcessDetails.find(x => x.processName === mapped.processName));
    this.createEIBForm.get('viewName')?.setValue(mapped.dbView.map(x => x.viewName));
  }

  onRemoveEIB(mapped: MappingDetails) {
    let bpd = this.businessProcessDetails.find(x => x.processName === mapped.processName);
    if (bpd) {
      bpd.isMapped = false;
      this.mapping.find(x => x.processName === mapped.processName && x.isDeleted === false).isDeleted = true;
      //this.mapping = this.mapping.filter(x => x.processName !== mapped.processName);
      this.getMappedRequiredBusinessProcess();
      this.confirmIndex = null;

    }

    // this.modalService
    //   .confirm('EIB', `Are you sure you want to remove ${mapped.processName}?`)
    //   .then((confirmed) => {
    //     if (confirmed === true) {
    //       let bpd = this.businessProcessDetails.find(x => x.processName === mapped.processName);
    //       if (bpd) {
    //         bpd.isMapped = false;
    //         this.mapping.find(x => x.processName === mapped.processName).isDeleted = true;
    //         //this.mapping = this.mapping.filter(x => x.processName !== mapped.processName);
    //         this.getMappedRequiredBusinessProcess();
    //       }
    //     }
    //   });
  }

  onBusinessProcessChange(event: BusinessProcessDetails) {
    if (event) {
      const isProcessNameMapped = this.mapping.find(x => x.processName === event.processName && x.isDeleted === false);
      if (isProcessNameMapped) {
        this.isEditingMappedBPView = true;
        this.editingRowIndex = isProcessNameMapped.id;
        this.createEIBForm.get('viewName')?.setValue(isProcessNameMapped.dbView.map(x => x.viewName));
      }
    }
  }

  goBack() {
    this.router.navigate(['/eib']);
  }



  onClearMapping() {
    this.editingRowIndex = null;
    this.isEditingMappedBPView = false;
    this.createEIBForm.get('businessProcess')?.setValue(null);
    this.createEIBForm.get('businessProcess')?.enable();
    this.createEIBForm.get('viewName')?.setValue(null);
  }

  onCountryChange(event: EIBCountry) {

  }

  confirmIndex: number | null = null;
  confirmAction: 'edit' | 'delete' | null = null;
  editingRowIndex: number | null = null;
  askConfirm(action: 'edit' | 'delete', i: number) {
    this.confirmIndex = i;
    this.confirmAction = action;
  }

  cancelRemove() {
    this.confirmIndex = null;
    this.confirmAction = null;
  }


  get filteredRecords() {
    return this.recordsToDisplay.filter(r => !r.isDeleted);
  }

  rows = 2;
  first = 0;

  onPage(event: TablePageEvent) {


    // If the user changed the rows-per-page, reset to page 1.
    if (event.rows !== this.rows) {
      this.rows = event.rows;
      this.first = 0; // go back to first page
      return;
    }

    // Otherwise, just track normal page changes
    this.first = event.first;
  }



}
