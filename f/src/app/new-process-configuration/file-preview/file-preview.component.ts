import { AfterViewInit, Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild, } from '@angular/core';
import { AbstractControl, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { ToastrService } from 'ngx-toastr';
import { DataSourceType, FileType, ModalMessages, ToastrMessages } from '../../shared/enum';
import { DiParserService } from '../../core/services/di-parser.service';
import { ColumnNameDatatypeName, ColumnNameDatatypeNameForOfflineMode } from '../../core/models/columnNameDatatypeName';
import { Helper } from '../../core/utils/helper';
import { FilePreviewValues } from '../../core/models/additionalSettings';
import { KeyValuePair } from '../../core/models/fileConfiguration';
import { ConfigurationService } from '../../core/services/configuration.service';
import { APIResponse } from '../../shared/models/apiResponse';
import { DatatypeName, DateTimeFormats } from '../../core/models/datatypeNames';
import { lastValueFrom } from 'rxjs';
import { BusyService } from '../../core/services/busy.service';
import { EnglishOnlyCharacters } from '../../core/models/englistOnlyCharacters';
import { cleanColumnName, convertToRoman } from '../../core/services/di-parser.service';
import { NavigateService } from '../../core/services/navigate.service';
import { PayLoad } from '../../core/models/DataInsider';
import { ModalService } from '../../core/services/confirm-modal.service';
import { Control } from 'child_process';
type AOA = any[][];

@Component({
  selector: 'app-file-preview',
  templateUrl: './file-preview.component.html',
  styleUrl: './file-preview.component.css',
  standalone: false
})
export class FilePreviewComponent implements OnInit {
  @ViewChild('delimiter') delimiterInput: ElementRef | undefined;
  @Output() emitFilePreviewValues = new EventEmitter<{ filePreviewValues: FilePreviewValues, payLoad: PayLoad }>();
  @Output() closeWindow = new EventEmitter<string>();
  @Output() disableDelimiter = new EventEmitter<string>;
  @Output() rulesetChanged = new EventEmitter<boolean>();
  @Input() filePreviewValues: FilePreviewValues;
  @Input() ruleSetPayload: PayLoad;

  @Input() dataTypeNames: DatatypeName[];
  @Input() dateTimeNames: DateTimeFormats[];
  @Input() flpConfigurationId: string;
  @Input() drop_main_table: boolean;
  @Input() is_validate_fileschema_with_target_table!: AbstractControl<boolean>;
  @Input() englishOnlyCharacters: EnglishOnlyCharacters[];
  @Input() nameOfParamChange: string;

  modalRef?: BsModalRef;

  delimiter: string;
  txtQuoteCharacter: string;
  skipRows: number;
  skipFooterRows: number;
  //fileName: string;
  recordHeaders: ColumnNameDatatypeNameForOfflineMode[] = [];
  recordHeadersOG: ColumnNameDatatypeNameForOfflineMode[] = [];
  headersRow: string[] | undefined = [];

  filePreviewForm: FormGroup = new FormGroup({});

  file: any | null = null;
  fileType: string = FileType.CommaSeparatedValues; //default

  //dateTimeNames: DateTimeFormats[] = [];
  dateTimeNamesValues: DateTimeFormats[] = [];
  //dataTypeNames: DatatypeName[] = [];

  dataxlsx: AOA;
  wksheets: KeyValuePair[] = [];
  recordsArrayForDisplay: any[] = [];

  showDateTimeOptions: boolean = false;
  columnNamesSelected: string;
  keyColumnNamesSelected: string;
  columnsForDedupSelected: string;
  //englishOnlyCharacters: EnglishOnlyCharacters[];
  recordsArray: any[];
  configurationProcessType: number = 1;
  constructor(
    public bsModalRef: BsModalRef,
    private fb: FormBuilder,
    private toastr: ToastrService,
    private myParser: DiParserService,
    private helperUtil: Helper,
    private configService: ConfigurationService,
    private busyService: BusyService,
    private navigateService: NavigateService,
    private confirmModalService: ModalService
  ) { }

  ngOnInit(): void {
    this.configurationProcessType = this.navigateService.configurationProcess;
    this.isValidDataType = true;
    this.initializeForm();
    //this.getDataTypes();
    //this.getAllDateTimeFormats();
    //this.getAllEnglishCharactersOnly();

    if (this.filePreviewValues?.fileName) {
      this.filePreviewForm
        .get('formFile')
        .setValue(this.filePreviewValues?.fileName);
    }

    this.recordHeaders = structuredClone(this.filePreviewValues?.recordHeaders);

    this.recordsArrayForDisplay =
      this.filePreviewValues?.recordsArrayForDisplay;

    this.file = this.filePreviewValues?.file;
    this.fileType = this.file?.name.split('.').pop();
    this.headersRow = this.filePreviewValues?.headersRow;
    this.updateFilePreview();
  }

  updateFilePreview() {

    setTimeout(() => {
      if (this.file && this.filePreviewValues.paramChanged) {
        //console.log(this.file, this.filePreviewValues.paramChanged);
        this.previewFile();
      } else {

        this.recordHeaders = structuredClone(this.filePreviewValues?.recordHeaders);
        this.prePopulate();
      }

      this.recordHeadersOG = structuredClone(this.filePreviewValues?.recordHeaders); //[...this.filePreviewValues?.recordHeaders];
    }, 500);
  }
  initializeForm() {
    // //console.log(`delimiter: ${this.delimiter}, flexCheckHasHeaders: ${this.flexCheckHasHeaders}, txtQuoteCharacter: ${this.txtQuoteCharacter}`);
    this.filePreviewForm = this.fb.group({
      formFile: [{ value: '', disabled: true }, Validators.required],
    });
  }

  getDataTypes() {
    this.configService.getAllDataTypeNames().subscribe({
      next: (response: APIResponse<DatatypeName[] | null>) => {
        if (response) {
          if (response.responseCode === 200) {
            if (response.result) {
              this.dataTypeNames = response.result;

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

              setTimeout(() => {
                this.prePopulate();
              }, 1000);
            }
          }
        }
      },
      error: (error) => {
        this.toastr.error("Can't find all date time formats");
      },
    });
  }

  onFileChange(event: any) {
    const validExtensions = ['csv', 'txt', 'xls', 'xlsx', 'xlsb'];

    if (event.target.files.length > 0) {
      const file = event.target.files[0] as File;

      let fileIsValid =
        validExtensions.indexOf(file.name.split('.').pop()) > -1;

      if (!fileIsValid) {
        this.toastr.error(ToastrMessages.InvalidFileExtension);
        return;
      }

      let fileSizeIsBig = event.target.files[0].size > 10 * 1024 * 1024; //(250 * 1024 * 1024); 10mb
      if (fileSizeIsBig) {
        this.toastr.error(ToastrMessages.FileIsTooLarge);
        return;
      }

      this.file = file;
      this.fileType = this.file.name.split('.').pop();
      this.filePreviewForm.get('formFile')?.setValue(file.name);
      //reset the file extension

      this.filePreviewValues.delimiter = '';
      this.disableDelimiter.emit(this.file.name.split('.').pop());
      this.previewFile();
    } else {
      this.recordHeaders = [];
      this.filePreviewForm.get('formFile')?.setValue('');
    }
  }

  async previewFile() {
    //if(!newFile) return;
    //debugger;
    this.recordHeaders = [];
    switch (this.fileType) {
      case FileType.CommaSeparatedValues:
      case FileType.TextFiles:


        const parsedCSVTXT = await this.myParser.asyncParseCSVTXT(this.file, {
          delimiter: this.filePreviewValues.delimiter,
          hasHeader: this.filePreviewValues.hasHeaders,
          skipEmptyLines: true,
          quoteCharacter: this.filePreviewValues.txtQuoteCharacter,
          worker: true,
          useOnlyRomanNumerals: this.filePreviewValues.roman_numerals_only,
          useOnlyEnglishLetters: this.filePreviewValues.spanish_to_english,
          englishOnlyCharacters: this.englishOnlyCharacters,
        });

        if (parsedCSVTXT) {
          this.recordsArrayForDisplay = parsedCSVTXT.data;
          this.headersRow = parsedCSVTXT.meta.fields;
          var dataToSliced = [...parsedCSVTXT.data];
          this.filePreviewValues.delimiter = parsedCSVTXT.meta.delimiter;
          if (this.filePreviewValues.hasHeaders) {
            if (this.filePreviewValues.skip_header_rows > 0) {
              if (
                parsedCSVTXT.meta.fields &&
                this.filePreviewValues.skip_header_rows === 1
              ) {
                dataToSliced = parsedCSVTXT.data.slice(
                  this.filePreviewValues.skip_header_rows
                );
              } else {
                dataToSliced = [
                  ...parsedCSVTXT.data.slice(
                    this.filePreviewValues.skip_header_rows
                  ),
                ];
              }

              this.recordsArrayForDisplay = [...dataToSliced];
            }
            //let headerNames : string[] =this.helperUtil.detectAndFixSameColumnName(result.meta.fields);
            // //console.log(headerNames);
            for (var i = 0; i < parsedCSVTXT.meta.fields.length; i++) {
              let cleanHeaderName = cleanColumnName(parsedCSVTXT.meta.fields[i]); //this.helperUtil.cleanColumnName(result.meta.fields[i]);
              this.recordHeaders.push({
                index: i,
                ColumnName: cleanHeaderName,
                DbColumnName: cleanHeaderName,
                OgColumnName: cleanHeaderName,
                OgDbColumnName: cleanHeaderName,
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
            this.recordsArrayForDisplay = [...dataToSliced];

            if (this.filePreviewValues.skip_header_rows > 0) {
              dataToSliced = dataToSliced.slice(
                this.filePreviewValues.skip_header_rows
              );
              this.recordsArrayForDisplay = [...dataToSliced];
            }

            let tempHeaders = this.createCustomHeaderRow(parsedCSVTXT.data);
            this.headersRow = tempHeaders;
            tempHeaders.forEach((header, index) => {
              let cleanHeaderName = header; //this.helperUtil.cleanColumnName(header);
              this.recordHeaders.push({
                index: index,
                ColumnName: cleanHeaderName,
                DbColumnName: cleanHeaderName,
                OgColumnName: cleanHeaderName,
                OgDbColumnName: cleanHeaderName,
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


          }

          if (this.filePreviewValues.skip_footer_rows > 0) {
            dataToSliced = dataToSliced
              .reverse()
              .slice(this.filePreviewValues.skip_footer_rows);
            this.recordsArrayForDisplay = [...dataToSliced.reverse()];
          }

          if (parsedCSVTXT.meta.fields === undefined) {
            if (this.filePreviewValues.hasHeaders === false) {
            }
          }

          this.columnNamesSelected = this.recordHeaders
            .map((x) => x.ColumnName)
            .join(',');

          this.findDuplicateColumnAndGenerateNew();

          this.recordHeadersOG = structuredClone(this.recordHeaders); //[...this.recordHeaders];
        }

        {
          // this.myParser
          //   .parseCSVTXT(this.file, {
          //     delimiter: this.filePreviewValues.delimiter,
          //     hasHeader: this.filePreviewValues.hasHeaders,
          //     skipEmptyLines: true,
          //     quoteCharacter: this.filePreviewValues.txtQuoteCharacter,
          //     worker: true,
          //     useOnlyRomanNumerals: this.filePreviewValues.roman_numerals_only,
          //     useOnlyEnglishLetters: this.filePreviewValues.spanish_to_english,
          //     englishOnlyCharacters: this.englishOnlyCharacters,
          //   })
          //   .subscribe({
          //     next: (result) => {

          //       this.recordsArrayForDisplay = result.data;
          //       this.headersRow = result.meta.fields;
          //       var dataToSliced = [...result.data];
          //       this.filePreviewValues.delimiter = result.meta.delimiter;
          //       if (this.filePreviewValues.hasHeaders) {
          //         if (this.filePreviewValues.skip_header_rows > 0) {
          //           if (
          //             result.meta.fields &&
          //             this.filePreviewValues.skip_header_rows === 1
          //           ) {
          //             dataToSliced = result.data.slice(
          //               this.filePreviewValues.skip_header_rows
          //             );
          //           } else {
          //             dataToSliced = [
          //               ...result.data.slice(
          //                 this.filePreviewValues.skip_header_rows
          //               ),
          //             ];
          //           }

          //           this.recordsArrayForDisplay = [...dataToSliced];
          //         }
          //         //let headerNames : string[] =this.helperUtil.detectAndFixSameColumnName(result.meta.fields);
          //         // //console.log(headerNames);
          //         for (var i = 0; i < result.meta.fields.length; i++) {
          //           let cleanHeaderName = cleanColumnName(result.meta.fields[i]); //this.helperUtil.cleanColumnName(result.meta.fields[i]);
          //           this.recordHeaders.push({
          //             index: i,
          //             ColumnName: cleanHeaderName,
          //             DbColumnName: cleanHeaderName,
          //             OgColumnName: cleanHeaderName,
          //             OgDbColumnName: cleanHeaderName,
          //             DatatypeName: 'string',
          //             willInclude: true,
          //             ColumnKey: false,
          //             newColumn: false,
          //             missingColumn: false,
          //             invalidDataType: false,
          //             willAddNewColumn: true,
          //             invalidColumnName: false,
          //             columnForDedeup: false,
          //             dateTimeFormatId: 0,
          //             isDuplicateColumn: false,
          //             useMultipleSheets: true
          //           });
          //         }
          //       } else {
          //         this.recordsArrayForDisplay = [...dataToSliced];

          //         if (this.filePreviewValues.skip_header_rows > 0) {
          //           dataToSliced = dataToSliced.slice(
          //             this.filePreviewValues.skip_header_rows
          //           );
          //           this.recordsArrayForDisplay = [...dataToSliced];
          //         }

          //         let tempHeaders = this.createCustomHeaderRow(result.data);
          //         this.headersRow = tempHeaders;
          //         tempHeaders.forEach((header, index) => {
          //           let cleanHeaderName = header; //this.helperUtil.cleanColumnName(header);
          //           this.recordHeaders.push({
          //             index: index,
          //             ColumnName: cleanHeaderName,
          //             DbColumnName: cleanHeaderName,
          //             OgColumnName: cleanHeaderName,
          //             OgDbColumnName: cleanHeaderName,
          //             DatatypeName: 'string',
          //             willInclude: true,
          //             ColumnKey: false,
          //             newColumn: false,
          //             missingColumn: false,
          //             invalidDataType: false,
          //             willAddNewColumn: true,
          //             invalidColumnName: false,
          //             columnForDedeup: false,
          //             dateTimeFormatId: 0,
          //             isDuplicateColumn: false,
          //             useMultipleSheets: true
          //           });
          //         });
          //       }

          //       if (this.filePreviewValues.skip_footer_rows > 0) {
          //         dataToSliced = dataToSliced
          //           .reverse()
          //           .slice(this.filePreviewValues.skip_footer_rows);
          //         this.recordsArrayForDisplay = [...dataToSliced.reverse()];
          //       }

          //       if (result.meta.fields === undefined) {
          //         if (this.filePreviewValues.hasHeaders === false) {
          //         }
          //       }

          //       this.columnNamesSelected = this.recordHeaders
          //         .map((x) => x.ColumnName)
          //         .join(',');

          //       // if (this.filePreviewValues.spanish_to_english) {
          //       //   this.displayOnlyEnglishCharacters();
          //       // }
          //       this.findDuplicateColumnAndGenerateNew();

          //       // if (
          //       //   // !this.filePreviewValues.spanish_to_english &&
          //       //   this.filePreviewValues.roman_numerals_only
          //       // ) {
          //       //   this.displayOnlyRomanNumerals(
          //       //     this.filePreviewValues.roman_numerals_only
          //       //   );
          //       // }

          //       //this.prePopulate();
          //     },
          //   });
        }
        break;
      case FileType.MSExcel1:
      case FileType.MSExcel2:
      case FileType.MSExcel3:
        this.busyService.idle();
        this.myParser
          .parseExcel(
            this.file,
            this.filePreviewValues.hasHeaders ? 1 : 0,
            0,
            this.filePreviewValues.flexCheckSkipEmptyLines
          )
          .subscribe({
            next: (res: KeyValuePair[]) => {
              this.busyService.idle();
              this.wksheets = res;
              //debugger;
              let tabName = res[0].sheetName;
              this.recordsArray = [];
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

              if (
                this.filePreviewValues.skip_header_rows >=
                (this.filePreviewValues.hasHeaders
                  ? this.dataxlsx.length - 1
                  : this.dataxlsx.length)
              ) {
                this.toastr.error(
                  ModalMessages.SkipHeaderRowsMoreThanTotalRowcount
                );
                return;
              }

              if (this.skipRows > 0) {
                dataToSliced = dataToSliced.slice(this.skipRows);
                this.recordsArrayForDisplay = dataToSliced;
                this.dataxlsx = [...dataToSliced];
              }
              this.recordsArray = this.dataxlsx;
              // //console.log(this.dataxlsx);
              if (this.filePreviewValues.hasHeaders) {
                var headerCount = 0;
                var headerNames: string[] = [];
                this.dataxlsx.slice(0, 1).forEach((x, i) => {
                  x.forEach((d: string, j) => {
                    var headerName =
                      d.trim().length === 0
                        ? `COL${j}`
                        : cleanColumnName(d.trim()); //this.helperUtil.cleanColumnName(d.trim());
                    headerCount++;

                    // //console.log(headerName);
                    this.recordHeaders.push({
                      index: j,
                      ColumnName: headerName,
                      DbColumnName: headerName,
                      OgColumnName: headerName,
                      OgDbColumnName: headerName,
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
                      isDuplicateColumn: this.recordHeaders.find(x => x.ColumnName === headerName) ? true : false,
                      useMultipleSheets: true
                    });
                  });
                });

                //find duplicate columns
                // let cleanHeaderNames: string[] =
                //   this.helperUtil.detectAndFixSameColumnName(headerNames);
                // //console.log(cleanHeaderNames);

                // cleanHeaderNames.forEach((headerName, i) => {
                //   this.recordHeaders.push({
                //     index: i,
                //     ColumnName: headerName,
                //     DbColumnName: headerName,
                //     DatatypeName: 'string',
                //     willInclude: true,
                //     ColumnKey: false,
                //     newColumn: false,
                //     missingColumn: false,
                //     invalidDataType: false,
                //     willAddNewColumn: true,
                //     invalidColumnName: false,
                //     columnForDedeup: false,
                //     dateTimeFormatId: 0,
                //     isDuplicateColumn: false,
                //   });
                // });
              } else {
                this.recordHeaders = this.createCustomHeaderRowForExcel(
                  this.dataxlsx.slice(0, 1)
                );
              }
              //debugger;
              if (this.filePreviewValues.spanish_to_english) {
                this.displayOnlyEnglishCharacters();
              }

              if (this.filePreviewValues.roman_numerals_only) {
                this.displayOnlyRomanNumerals(this.filePreviewValues.roman_numerals_only);
              }

              this.findDuplicateColumnAndGenerateNew();
              // this.displayOnlyEnglishCharacters();
              this.columnNamesSelected = this.recordHeaders
                .map((x) => x.ColumnName)
                .join(',');
              // //console.log(this.filePreviewValues, this.recordHeaders);

              this.prePopulate();


            },
          });
        break;
    }
  }

  prePopulate() {
    if (this.recordHeaders) {
      this.recordHeaders.forEach((header, i) => {
        const elemDateTimeOptions = document.getElementById(
          `showDateTimeOptions${i + 1}`
        ) as HTMLElement;
        let selectedDateTimeFormatId: number = 0;
        let selectedDateTimeFormat: string = '';
        if (elemDateTimeOptions) elemDateTimeOptions.style.display = 'none';

        switch (header.DatatypeName.toLowerCase()) {
          case 'date':
          case 'datetime':
          case 'time':
            if (elemDateTimeOptions) {
              elemDateTimeOptions.style.display = 'block';

              // this.dateTimeNames.forEach((c, i) => {
              //   if (c.dataTypeName === header.DatatypeName) {

              //     selectedDateTimeFormat = this.dateTimeNames.filter(x => x.dataTypeName === header.DatatypeName)[0].format;
              //     selectedDateTimeFormatId = this.dateTimeNames.filter(x => x.dataTypeName === header.DatatypeName)[0].formatId;

              //     this.dateTimeNamesValues.push({ formatId: c.formatId, format: c.format, dataTypeName: c.dataTypeName });
              //   }
              // });
            }
            break;
          default:
            if (elemDateTimeOptions) elemDateTimeOptions.style.display = 'none';
            break;
        }
      });
      if (!this.filePreviewValues.file && this.filePreviewValues.paramChanged) {
        if (this.filePreviewValues.spanish_to_english) {
          this.displayOnlyEnglishCharacters();
        }

        if (this.filePreviewValues.roman_numerals_only && this.filePreviewValues.paramChanged) {
          this.displayOnlyRomanNumerals(this.filePreviewValues.roman_numerals_only);
        }
      }

    } else {
    }

    this.toggleDedupCheckBox();
  }

  findDateTimeValues(column: ColumnNameDatatypeName, value: string) {
    let dateTimeFormats = this.dateTimeNames.filter(
      (x) => x.dataTypeName === value
    );
    if (dateTimeFormats.length > 0) {
      if (value != column.DatatypeName) {
        column.dateTimeFormatId = dateTimeFormats[0].formatId;
      }
    }
    return dateTimeFormats;
  }

  onClose() {
    //this.recordHeaders = structuredClone(this.recordHeadersOG);
    this.filePreviewValues.recordHeaders = structuredClone(this.recordHeadersOG);
    this.closeWindow.emit(this.nameOfParamChange);

  }

  onSubmit() {
    //this.bsModalRef?.hide();

    if (
      !this.isValidDataType ||
      this.recordHeaders.find((x) => x.invalidColumnName === true || x.invalidDataType === true) ||
      this.recordHeaders.find(x => x.DbColumnName.length > 255)
    ) {
      this.toastr.error(ModalMessages.CantProceedPleaseCorrectOtherDetails);
      return;
    }

    //recordHeaders -
    //headersRow -
    this.rulesetChanged.emit(this.rulesetUpdated);
    this.emitFilePreviewValues.emit({
      filePreviewValues: {
        paramChanged: false,
        file: this.file,
        fileName: this.file ? this.file.name : this.filePreviewValues.fileName,
        recordHeaders: this.recordHeaders,
        delimiter: this.filePreviewValues.delimiter,
        hasHeaders: this.filePreviewValues.hasHeaders,
        txtQuoteCharacter: this.filePreviewValues.txtQuoteCharacter,
        skip_header_rows: this.filePreviewValues.skip_header_rows,
        skip_footer_rows: this.filePreviewValues.skip_footer_rows,
        recordsArrayForDisplay: this.recordsArrayForDisplay,
        headersRow: this.headersRow,
        spanish_to_english: this.filePreviewValues.spanish_to_english,
        roman_numerals_only: this.filePreviewValues.roman_numerals_only,
        flexCheckSkipEmptyLines: this.filePreviewValues.flexCheckSkipEmptyLines,
      }, payLoad: this.rulesetUpdated ? this.newRuleSetPayload : this.ruleSetPayload
    });

    this.closeWindow.emit();
    // this.outputEmitfilePreviewValues.emit({
    //   fileName: this.file ? this.file.name : this.fileName,
    //   recordHeaders: this.recordHeaders,
    //   columnNamesSelected: this.columnNamesSelected,
    //   keyColumnNamesSelected: this.keyColumnNamesSelected,
    //   columnsForDedupSelected: this.columnsForDedupSelected
    // });
  }

  rulesetUpdated: boolean = false;

  newRuleSetPayload: PayLoad;
  onExcludeColumn(event: any, columnName: string) {
    let foundValue = this.recordHeaders.find(
      (item) => item.ColumnName === columnName
    );
    const input = event.target as HTMLInputElement;
    const previousChecked = !!foundValue?.willInclude;
    this.rulesetUpdated = false;
    var columnHasRule;
    if (foundValue) {

      columnHasRule = this.ruleSetPayload?.ruleSets?.find(x => {
        if (x.ruleColumnName.includes(foundValue.ColumnName) || x.ruleColumnName2 === foundValue.ColumnName)
          return x;

        const re = new RegExp(`(^|[^@])@${foundValue.ColumnName}\\b`, 'i');
        if (re.test(x.prompt)) return x;

        return null;
      }
      );
      if (columnHasRule) {
        this.confirmModalService
          .confirm('Validations', `Column ${foundValue.ColumnName} is part of a rule set.<br> If excluded, the rule will be removed. Would you like to proceed?`)
          .then((confirmed) => {
            if (confirmed === true) {
              //remove rule 
              this.rulesetUpdated = true;
              this.newRuleSetPayload = this.ruleSetPayload;
              this.newRuleSetPayload.ruleSets = this.newRuleSetPayload.ruleSets.filter(x => x.id !== columnHasRule.id);
              foundValue.willInclude = !foundValue.willInclude;
              foundValue.columnForDedeup = false;
              foundValue.ColumnKey = false;
              foundValue.DatatypeName = 'string';

              const elemDateTimeOptions = document.getElementById(
                `showDateTimeOptions${foundValue.index + 1}`
              ) as HTMLElement;
              if (elemDateTimeOptions) elemDateTimeOptions.style.display = 'none';
              return;
            } else {
              foundValue.willInclude = previousChecked;
              input.checked = !previousChecked;
              return;
            }
          });
      } else {
        foundValue.willInclude = !foundValue.willInclude;
        foundValue.columnForDedeup = false;
        foundValue.ColumnKey = false;
        foundValue.DatatypeName = 'string';

        const elemDateTimeOptions = document.getElementById(
          `showDateTimeOptions${foundValue.index + 1}`
        ) as HTMLElement;
        if (elemDateTimeOptions) elemDateTimeOptions.style.display = 'none';
      }
    }

    //this.columnNamesSelected = this.recordHeaders.filter(x => x.willInclude === true).map(x => x.ColumnName).join(",");
  }

  onColumnKeyListChecked(event: any, columnName: string) {
    let keyColumnsList: string = '';
    let foundValue = this.recordHeaders.find(
      (item) => item.ColumnName === columnName
    );
    if (foundValue) {
      foundValue.ColumnKey = !foundValue.ColumnKey;
    }

    //this.keyColumnNamesSelected = this.recordHeaders.filter(x => x.ColumnKey === true).map(x => x.ColumnName).join(",");

    this.toggleDedupCheckBox();

    switch (foundValue.DatatypeName.toLowerCase()) {
      case "date":
      case "datetime":
      case "time":
        this.validateDataOnDataType(
          foundValue.index,
          this.dateTimeNames.find((x) => x.formatId === foundValue.dateTimeFormatId).dataTypeName,
          foundValue.ColumnName
        );
        break;
      default:
        this.validateDataOnDataType(
          foundValue.index,
          foundValue.DatatypeName,
          foundValue.ColumnName
        );
        break;
    }
  }

  toggleDedupCheckBox() {
    let keyColumnsList: string = this.recordHeaders
      .filter((x) => x.ColumnKey)
      .map((x) => x.ColumnName)
      .join(', ');
    if (keyColumnsList.length > 0) {
      //enable all dedup
      for (let i = 0; i < this.recordHeaders.length; i++) {
        //if (
        //  this.recordHeaders.find((x) => x.ColumnKey === true && x.index !== i)
        //) {
        const elem = document.getElementById(
          'checkKeyColumnForDedup' + (i + 1)
        ) as HTMLElement;
        elem.removeAttribute('disabled');
        //}
      }
    } else {
      for (let i = 0; i < this.recordHeaders.length; i++) {
        const elem = document.getElementById(
          'checkKeyColumnForDedup' + (i + 1)
        ) as HTMLElement;
        elem?.setAttribute('disabled', 'true');

        this.recordHeaders.find((x) => x.index === i).columnForDedeup = false;
      }
    }
  }

  onColumnForDedupListChecked(event: any, columnName: string) {
    let foundValue = this.recordHeaders.find(
      (item) => item.ColumnName === columnName
    );
    if (foundValue) {
      foundValue.columnForDedeup = !foundValue.columnForDedeup;
    }

    //this.columnsForDedupSelected = this.recordHeaders.filter(x => x.columnForDedeup === true).map(x => x.ColumnName).join(",");
  }

  // onDelimiterChanged(event: any) {
  //   if (event.target.innerText === 'Other') {
  //     //this.processConfigurationForm.get('delimiter')?.setValue('');
  //     //this.delimiterInput?.nativeElement.focus();
  //   } else {
  //     let val = event.target.innerText;
  //     switch (event.target.innerText) {
  //       case 'comma (,)':
  //         val = ',';
  //         break;
  //       case 'tab (\\t)':
  //         val = '\\t';
  //         break;
  //       case 'pipe (|)':
  //         val = '|';
  //         break;
  //       case 'semicolon (;)':
  //         val = ';';
  //         break;
  //       default:
  //         break;
  //     }
  //     //this.processConfigurationForm.get('delimiter')?.setValue(val);
  //     //this.formValues.delimiter = val;
  //   }
  //   //this.previewToParent.emit(this.formValues);
  // }

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

  createCustomHeaderRowForExcel(records: any[]): ColumnNameDatatypeNameForOfflineMode[] {
    // const headersRow: ColumnNameDatatypeName[] = [];
    // if (records) {
    //   for (
    //     var i = 0;
    //     i < Object.keys(records[0].filter((x) => x)).length;
    //     i++
    //   ) {
    //     //this.headersRow.push(`COL${i}`);

    //     headersRow.push({
    //       index: i,
    //       ColumnName: `COL${i}`,
    //       DbColumnName: `COL${i}`,
    //       DatatypeName: 'string',
    //       willInclude: true,
    //       ColumnKey: false,
    //       newColumn: false,
    //       missingColumn: false,
    //       invalidDataType: false,
    //       willAddNewColumn: true,
    //       invalidColumnName: false,
    //       columnForDedeup: false,
    //       dateTimeFormatId: 0,
    //       isDuplicateColumn: false,
    //     });
    //   }
    // }

    // return headersRow;
    const headersRow: ColumnNameDatatypeNameForOfflineMode[] = [];
    if (records) {
      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        for (
          var i = 0;
          i < Object.keys(records[0].filter((x) => x)).length;
          i++
        ) {
          //this.headersRow.push(`COL${i}`);

          headersRow.push({
            index: i,
            ColumnName: `COL${i}`,
            DbColumnName: `COL${i}`,
            OgColumnName: `COL${i}`,
            OgDbColumnName: `COL${i}`,
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
        for (var i = 0; i < records[0].length; i++) {
          //this.headersRow.push(`COL${i}`);
          headersRow.push({
            index: i,
            ColumnName: `COL${i}`,
            DbColumnName: `COL${i}`,
            OgColumnName: `COL${i}`,
            OgDbColumnName: `COL${i}`,
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

  onClickDataTypes(event: any) {
    let configurationMsg = this.configurationProcessType === DataSourceType.DataBricks ? 'Databricks' : 'Database';
    if (this.flpConfigurationId && this.flpConfigurationId != '') {
      if (!this.drop_main_table) {


        this.toastr.info(
          `Please allow first "Drop Main Table" in "${configurationMsg} Settings"`
        );

        return;
      }
    }
  }

  onGetSelectedDatatypes(event: any, headerName: string, index: number) {
    let foundValue = this.recordHeaders.find((item) => item.index === index);

    let previousValue = foundValue.DatatypeName;

    let configurationMsg = this.configurationProcessType === DataSourceType.DataBricks ? 'Databricks' : 'Database';
    if (this.flpConfigurationId && this.flpConfigurationId != '') {
      if (!this.drop_main_table) {
        this.toastr.info(
          `Please allow first "Drop Main Table" in "${configurationMsg} Settings"`
        );

        foundValue.DatatypeName = previousValue;

        return;
      }
    }

    let selectedDateTimeFormatId: number = 0;
    let selectedDateTimeFormat: string = '';
    if (foundValue) {
      foundValue.DatatypeName = event.target.value;

      this.showDateTimeOptions = false;
      selectedDateTimeFormat = '';
      selectedDateTimeFormatId = 0;
      const elemDateTimeOptions = document.getElementById(
        `showDateTimeOptions${index + 1}`
      ) as HTMLElement;
      elemDateTimeOptions.style.display = 'none';
      this.dateTimeNamesValues = [];
      switch (event.target.value) {
        case 'date':
        case 'time':
        case 'datetime':
          this.showDateTimeOptions = true;

          this.dateTimeNames.forEach((c, i) => {
            if (c.dataTypeName === event.target.value) {

              selectedDateTimeFormat = this.dateTimeNames.filter(x => x.dataTypeName === event.target.value)[0].format;
              selectedDateTimeFormatId = this.dateTimeNames.filter(x => x.dataTypeName === event.target.value)[0].formatId;

              //this.dateTimeNamesValues.push({ formatId: c.formatId, format: c.format, dataTypeName: c.dataTypeName });
            }
          });

          break;
      }

      this.selectedDateTimeFormat = selectedDateTimeFormat;

      this.validateDataOnDataType(
        index,
        foundValue.DatatypeName,
        foundValue.ColumnName
      );

      if (this.showDateTimeOptions) {
        elemDateTimeOptions.style.display = 'block';
        foundValue.dateTimeFormatId = selectedDateTimeFormatId;
      }
    }
  }
  onValidateDateTimeFormat(event: any, headerName: string, index: number) {
    let column = this.recordHeaders.find((x) => x.index === index);
    if (column) {
      //column.dateTimeFormatId = +event.target.value;
      this.recordHeaders.find((x) => x.index === index).dateTimeFormatId =
        +event.target.value;
      this.selectedDateTimeFormat = this.dateTimeNames.find(
        (x) => x.formatId === +event.target.value
      ).format;
      this.validateDataOnDataType(
        index,
        this.dateTimeNames.find((x) => x.formatId === +event.target.value)
          .dataTypeName,
        headerName
      );
    }
  }

  seen: any[] = [];
  // async onColumnNameChange(event: any, index: number) {
  //   let column = this.recordHeaders.find((x) => x.index === index);
  //   let columnName = cleanColumnName(event.target.value); //this.helperUtil.cleanColumnName(event.target.value);

  //   column.ColumnName = event.target.value;

  //   if (this.filePreviewValues.spanish_to_english) {
  //     //&& this.helperUtil.checkForAccents(columnName)
  //     column.ColumnName = await this.convertToEnglishOnlyCharacters(
  //       'spanish',
  //       columnName
  //     );
  //   }

  //   if (this.filePreviewValues.roman_numerals_only) {
  //     this.displayOnlyRomanNumerals(this.filePreviewValues.roman_numerals_only);
  //   }

  //   if (columnName === '') {
  //     column.invalidColumnName = true;
  //     this.toastr.error(ModalMessages.UniqueColumnName);
  //     return;
  //   }

  //   if (
  //     this.recordHeaders.find(
  //       (item) => item.index !== index && item.ColumnName === columnName
  //     )
  //   ) {
  //     column.invalidColumnName = true;
  //     this.seen.push(column);
  //     //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.UniqueColumnName);
  //     this.toastr.error(ModalMessages.UniqueColumnName);
  //   } else {
  //     //column.invalidColumnName = false;

  //     //this.columnDatatype.find(item => item.index === index).ColumnName = event.target.value;
  //     column.invalidColumnName = false;
  //     //check if the column changed was invalid column before removing anything from seen
  //     const existingColumn = this.seen.find(
  //       (x) => x.index === index && x.invalidColumnName === false
  //     );
  //     if (existingColumn) {
  //       this.seen.splice(
  //         this.seen.findIndex((x) => x.index === index),
  //         1
  //       );
  //     }
  //   }

  //   const duplicates = [];
  //   this.recordHeaders.forEach((item, i) => {
  //     const value = item.ColumnName;

  //     const found = this.seen.find(
  //       (x) => x.ColumnName === value && x.index !== i
  //     );
  //     if (found || value === '') {
  //       found.invalidColumnName = true;
  //       duplicates.push(item);
  //     } else {
  //       this.recordHeaders.find(
  //         (x) => x.ColumnName === item.ColumnName
  //       ).invalidColumnName = false;
  //     }
  //     // if (this.seen[value]) {
  //     //   duplicates.push(item);
  //     //   item.invalidColumnName = true;
  //     // } else {
  //     //   //this.seen[value] = true;
  //     //   item.invalidColumnName = false;
  //     // }
  //   });

  //   if (duplicates.length === 0) this.seen = [];
  // }

  async onDBColumnNameChange(event: any, index: number) {

    let column = this.recordHeaders.find((x) => x.index === index);



    let columnName = cleanColumnName(event.target.value); //this.helperUtil.cleanColumnName(event.target.value);
    //check first if it has a rule
    var columnHasRule;
    var tempRuleSet;

    tempRuleSet = this.ruleSetPayload?.ruleSets;

    columnHasRule = tempRuleSet?.find(x => {
      if (x.ruleColumnName.includes(column.ColumnName) || x.ruleColumnName2 === column.ColumnName)
        return x;

      const re = new RegExp(`(^|[^@])@${column.ColumnName}\\b`, 'i');
      if (re.test(x.prompt)) return x;

      return null;
    });


    if (columnHasRule) {

      const confirmed = await this.confirmModalService.confirm('Validations', `Column ${column.ColumnName} is part of a rule set.<br> If changed, the rule will be removed. Would you like to proceed?`);

      if (!confirmed) {
        event.target.value = column.DbColumnName;
        return;
      }        

      this.ruleSetPayload.ruleSets = this.ruleSetPayload?.ruleSets?.filter(x => x.id !== columnHasRule.id);
    }

    column.DbColumnName = columnName;

    if (event.target.value === '' || columnName === '') {
      column.invalidColumnName = true;
      this.toastr.error(ModalMessages.UniqueColumnName);
      return;
    }

    if (this.filePreviewValues.spanish_to_english) {
      //&& this.helperUtil.checkForAccents(columnName)
      column.DbColumnName = this.convertToEnglishOnlyCharacters(
        'spanish',
        columnName
      );
    }

    if (this.filePreviewValues.roman_numerals_only) {
      //this.displayOnlyRomanNumerals(this.filePreviewValues.roman_numerals_only);
      columnName = convertToRoman(columnName, false);
      column.DbColumnName = columnName;
      column.OgDbColumnName = columnName;
    }

    if (
      this.recordHeaders.find(
        (item) => item.index !== index && item.DbColumnName === column.DbColumnName
      )
    ) {
      column.invalidColumnName = true;
      this.seen.push(column);
      //this.modalService.showNotification(false, ModalTitles.TPDataIngestion, ModalMessages.UniqueColumnName);
      this.toastr.error(ModalMessages.UniqueColumnName);
    } else {
      //column.invalidColumnName = false;

      //this.columnDatatype.find(item => item.index === index).ColumnName = event.target.value;
      //column.invalidColumnName = false;

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
    this.recordHeaders.forEach((item, i) => {
      const value = item.DbColumnName;

      const found = this.seen.find(
        (x) => x.DbColumnName === value && x.index !== i
      );
      if (found) {
        found.invalidColumnName = true;
        duplicates.push(item);
      } else {
        this.recordHeaders.find(
          (x) => x.DbColumnName === item.DbColumnName
        ).invalidColumnName = false;
      }
    });

    if (duplicates.length === 0) this.seen = [];
  }

  isValidDataType: boolean = false;
  selectedDateTimeFormat: string = '';

  validateDataOnDataType(
    index: number,
    dataTypeName: string,
    headerName: string
  ): boolean {
    let currentColumn = this.recordHeaders.find((x) => x.index === index);
    if (currentColumn) {
      if (
        (dataTypeName === 'date' ||
          dataTypeName === 'datetime' ||
          dataTypeName === 'time') &&
        +this.selectedDateTimeFormat === 0
      ) {
        let dateTimeFormats = this.dateTimeNames.filter(
          (x) => x.dataTypeName === dataTypeName
        );
        if (dateTimeFormats.length > 0) {
          this.selectedDateTimeFormat = dateTimeFormats[0].format.toString();
        }
      }

      currentColumn.invalidDataType = false;

      if (
        this.fileType === FileType.MSExcel1 ||
        this.fileType === FileType.MSExcel2 ||
        this.fileType === FileType.MSExcel3
      ) {
        let data: any[] = [];
        if (this.filePreviewValues.hasHeaders) {
          data = this.recordsArrayForDisplay.slice(1, 20).map((x) => x[index]);
        } else {
          data = this.recordsArrayForDisplay.slice(0, 20).map((x) => x[index]);
        }

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
          let currentColumn = this.recordHeaders.find((x) => x.index === index);

          this.recordHeaders.find((x) => x.index === index).invalidDataType = true;

          //if(spanHeader) spanHeader.setAttribute("style", "color:red");
          //if (spanHeader2) { spanHeader2.setAttribute("style", "color:red"); }

          //this.modalService.showNotification(false, ModalTitles.CorrectionNeededOnSource, ModalMessages.InvalidDataType);
          this.toastr.error(ToastrMessages.InvalidDataType);
          return false;
        }
      } else if (
        this.fileType === FileType.CommaSeparatedValues ||
        this.fileType === FileType.TextFiles
      ) {
        var slicedData: any[] = [];
        if (this.filePreviewValues.hasHeaders)
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
          this.recordHeaders.find((x) => x.index === index).invalidDataType =
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

  stopPropagation(event: any) {
    event.stopPropagation();
  }

  displayOnlyRomanNumerals(value) {
    this.filePreviewValues.roman_numerals_only = value;
    if (this.filePreviewValues.roman_numerals_only) {
      this.recordHeaders.forEach((c) => {
        if (this.filePreviewValues.spanish_to_english) {
          c.ColumnName = convertToRoman(c.ColumnName, c.isDuplicateColumn);//this.helperUtil.convertToRoman(c.ColumnName,c.isDuplicateColumn);
          c.DbColumnName = convertToRoman(c.DbColumnName, c.isDuplicateColumn); //this.helperUtil.convertToRoman(c.DbColumnName,c.isDuplicateColumn);
        } else {
          c.ColumnName = convertToRoman(c.OgColumnName, c.isDuplicateColumn);//this.helperUtil.convertToRoman(c.ColumnName,c.isDuplicateColumn);
          c.DbColumnName = convertToRoman(c.OgDbColumnName, c.isDuplicateColumn); //this.helperUtil.convertToRoman(c.DbColumnName,c.isDuplicateColumn);
        }
      });
    }
  }

  displayOnlyEnglishCharacters() {
    //debugger;
    var tempVal: string = '';
    let convertedWord: string = '';
    let hasNewName = false;
    this.recordHeaders.forEach((c, i) => {
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
      //c.DbColumnName = convertedWord;
    });

    this.recordHeaders.forEach((c, i) => {
      convertedWord = c.DbColumnName;
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
      c.DbColumnName = convertedWord;
      //c.DbColumnName = convertedWord;
    });
  }

  convertToEnglishOnlyCharacters(
    language: string,
    wordToBeConverted: string
  ) {
    // const result = await lastValueFrom(
    //   this.configService.convertToEnglishOnlyCharacters(
    //     language,
    //     wordToBeConverted
    //   )
    // );

    // if (result) {
    //   if (result.responseCode === 200) {
    //     if (result.result) {
    //       return result.result;
    //     }
    //   }
    // }

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

  convertToEnglishCharacters() {
    let convertedWord: string = '';
    let hasNewName = false;
    this.recordHeaders.forEach((c, i) => {
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

  findDuplicateColumnAndGenerateNew() {
    var listOfColumnNames = this.recordHeaders.map((x) => x.ColumnName).sort();

    let results = [];
    for (let i = 0; i < listOfColumnNames.length - 1; i++) {
      if (listOfColumnNames[i + 1] == listOfColumnNames[i]) {
        results.push(listOfColumnNames[i]);
        // results.push(this.columnDatatype.find(x => x.ColumnName === listOfColumnNames[i]));
      }
    }

    results.forEach((duplicateName) => {
      //find the columns which are similar
      let foundColumns = this.recordHeaders.filter(
        (x) => x.ColumnName === duplicateName
      );
      foundColumns.forEach((c, i) => {
        if (i > 0) {
          c.ColumnName = `${c.ColumnName}_${i}`;
          c.isDuplicateColumn = true;
        }
        c.DbColumnName = c.ColumnName;
      });
    });
  }

  hasColumnExcluded() {
    var hasExclusion = this.recordHeaders.find(x => x.willInclude === false);
    var validateSchemaIsChecked = this.is_validate_fileschema_with_target_table.value;
    return hasExclusion && !validateSchemaIsChecked
  }
  onApplyValidateSchema() {
    this.is_validate_fileschema_with_target_table.setValue(true);
  }
}
