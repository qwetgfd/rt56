import { Injectable } from '@angular/core';
import moment from 'moment';
import { AbstractControl, FormArray, FormGroup, Validators } from '@angular/forms';
import { RuleTypeNames, SubRuleTypes } from '../../shared/enum';
import { sub } from 'date-fns';
import { ColumnNameDatatypeName } from '../models/columnNameDatatypeName';
import { cleanColumnName } from '../services/di-parser.service';
import { ExcelRule } from '../models/DataInsider';
import { HttpHeaders } from '@angular/common/http';
import { SelectedFiles } from '../models/LandingLayer/landingLayer';
import { RegexItem } from '../models/additionalSettings';

type AOA = any[][];
export type CompilePatternResult = { ok: boolean; regex: RegExp, error: string }



@Injectable({
  providedIn: 'root',
})
export class Helper {

  identifyDataType(data: any[], dataType: string, format: string | null, isKeyColumn: boolean) {
    // bool
    // datetime
    // double
    // float
    // int
    // long
    // string
    //const {isValid, parseISO} = require('date-fns');

    //not used; del temporarily
    // let allIsEmpty: boolean = false;
    // let countOfEmptyCell: number = 0;
    // for (let i = 0; i < data.length; i++) {
    //   if (data[i]?.trim().length === 0) {
    //     countOfEmptyCell++;
    //   }
    // }

    // if (countOfEmptyCell === data.length) {
    //   allIsEmpty = true;
    // }

    let retVal = true;
    if (format)
      format = format.replace('tt', 'a').replace('dd', 'DD').replace(/y/g, 'Y');
    switch (dataType) {
      case 'bool':
        const trueValueStr = ['1', 'true', '0', 'false'];

        for (let i = 0; i < data.length; i++) {
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {

            if (typeof data[i] === 'string') {
              retVal = trueValueStr.indexOf(data[i].toLowerCase()) >= 0;
            } else {
              retVal = trueValueStr.indexOf(data[i]) >= 0;
            }
            if (!retVal) break;
          }
        }

        break;
      case 'date':
        for (let i = 0; i < data.length; i++) {
          // var dateToTest = moment(data[i]);
          // console.log(dateToTest.isValid());
          //retVal = !isNaN(new Date(data[i]).getDate());

          //const {isValid, parseISO} = require('date-fns');

          // const dateRegex1 =new RegExp(/^\d{1,2}(\/|-|.)\d{1,2}(\/|-|.)\d{4}|(^\d{4}(\/|-|.)\d{1,2}(\/|-|.)\d{2})/); // 09/25/2024 09.25.2024 09-25-2024 2024.09.25
          // const dateRegex2 = new RegExp(/^(([0-9])|([0-2][0-9])|([3][0-1]))(\/|-|.)(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)(\/|-|.)\d{4}$/); //09.Sep.2024
          // const dateRegex3 = new RegExp(/^\d{2}[\/\-\.\/]\d{2}[\/\-\.\/]\d{2}$/);//06/12/23 06\12\23 06-12-23 06.12.23
          // const dateRegex4 =new RegExp(/^\d{1,2}(\/|-|.)\d{1,2}(\/|-|.)\d{2}|(^\d{2}(\/|-|.)\d{1,2}(\/|-|.)\d{2})/); // 3/15/2023 3/15/23 3/1/23 12/1/23 12/1/2024
          // let isDateRegex1 = data[i].match(dateRegex1);
          // let isDateRegex2 = data[i].toLowerCase().match(dateRegex2);
          // let isDateRegex3 = data[i].match(dateRegex3);
          // let isDateRegex4 = data[i].match(dateRegex4);
          // if(isDateRegex1=== null && isDateRegex2 === null && isDateRegex3 === null
          //   && isDateRegex4 === null
          // ){
          //   retVal = false;
          //   break;
          // } else {
          //   //validate if string using new Date
          //   retVal = !isNaN(new Date(data[i]).getDate());
          //   if (!retVal) break;
          // }
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {
            let isValid = moment(data[i], format, true).isValid();
            retVal = true;
            if (isValid === false) {
              retVal = false;
              break;
            }
          }
        }
        //return true;
        break;
      case 'time':
        // 01:01:01, 05:52:34Pm
        //var timeRegex1 = new RegExp(/^(\d{2}:\d{2}:\d{2})\s?([AaPp][Mm])|(\d{2}:\d{2}:\d{2})$/);
        for (let i = 0; i < data.length; i++) {
          // let isTimeRegex1 = data[i].match(timeRegex1);
          // if(isTimeRegex1=== null ){
          //   retVal = false;
          // } else {
          //    //validate if string using new Date
          //    retVal = !isNaN(new Date(data[i]).getDate());
          //    if (!retVal) break;
          // }
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {
            let isValid = moment(
              data[i],
              format.replace('tt', 'a').replace('TT', 'A'),
              true
            ).isValid();
            retVal = true;
            if (isValid === false || data[i].trim().length < 5) {
              retVal = false;
              break;
            }
          }
        }
        break;
      case 'datetime':
        //01-01-2024 01:01:00 PM, 01-01-2024 01:01:00
        //const dateTimeFormat1 = new RegExp(/^(\d{2}[\-\/]\d{2}[\-\/]\d{4}\s\d{2}:\d{2}:\d{2})\s?([AaPp][Mm])|(\d{2}[\-\/]\d{2}[\-\/]\d{4}\s\d{2}:\d{2}:\d{2})$/); // 2024-01-01 01:01:01
        //2024-01-01 01:01:00 PM, 2024-01-01 01:01:00
        //const dateTimeFormat2 = new RegExp(/^(\d{4}[\-\/]\d{2}[\-\/]\d{2}\s\d{2}:\d{2}:\d{2})\s?([AaPp][Mm])|(\d{4}[\-\/]\d{2}[\-\/]\d{2}\s\d{2}:\d{2}:\d{2})$/); // 2024-01-01 01:01:01
        //2018-01-04T05:52:20.698, 2018-01-04T05:52:34
        //const dateTimeFormat3 = new RegExp(/^\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d(?:\.\d+)?Z?$/);
        for (let i = 0; i < data.length; i++) {
          // let isDateTimeRegex1 = data[i].match(dateTimeFormat1);
          // let isDateTimeRegex2 = data[i].match(dateTimeFormat2);
          // let isDateTimeRegex3 = data[i].match(dateTimeFormat3);
          // if(isDateTimeRegex1=== null && isDateTimeRegex2 === null && isDateTimeRegex3 === null){
          //   retVal = false;
          // } else {
          //    //validate if string using new Date
          //    retVal = !isNaN(new Date(data[i]).getDate());
          //    if (!retVal) break;
          // }
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {
            let isValid = moment(data[i], format, true).isValid();
            retVal = true;
            if (isValid === false) { //|| data[i].trim().length < 16
              retVal = false;
              break;
            }
          }
        }
        break;
      case 'double':
      case 'float':
        let isDoubleFloat = false;
        //sample values
        //1) -13,4615384615385
        //2) -13.4615384615385

        const doubleFloatRegex = new RegExp(
          /^[+-]?(?:\d+(\.|\,)?\d*|\d*(\.|\,)?\d+)*$/
        );
        for (let i = 0; i < data.length; i++) {
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {
            isDoubleFloat = data[i].match(doubleFloatRegex);
            //if (retVal === null) break;
            if (isDoubleFloat === null) {
              retVal = false;
              break;
            }
          }
        }
        break;
      case 'int':
        const intRegex = new RegExp(/^([0-9]*)$/);
        let isInt = false;
        for (let i = 0; i < data.length; i++) {
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }
          if (data[i].trim().length !== 0) {
            isInt = data[i].replace('\r', '').match(intRegex);
            if (isInt === null) {
              retVal = false;
              break;
            }
          }
        }
        break;

      case 'long':
        // for (let i = 0; i < data.length; i++) {
        //   retVal = Number.isNaN(data[i]);
        //   if (!retVal) break;
        // }

        const minLong = -(2n ** 63n);
        const maxLong = 2n ** 63n - 1n;

        const longRegex = new RegExp(
          /^-?(?:0|[1-9][0-9]{0,2}(?:,[0-9]{3})*|[1-9][0-9]*)$/
        );
        const sciRegex = /^[+-]?\d+(\.\d+)?[eE][+-]?\d+$/;
        let isLong = false;
        for (let i = 0; i < data.length; i++) {
          if (data[i].trim().length === 0 && isKeyColumn) {
            retVal = false;
            break;
          }

          let numericValue: number | bigint | null = null;

          if (data[i].trim().length !== 0) {
            //check if the value matches the pattern
            if (longRegex.test(data[i])) {
              //remove comma
              const numericValue = parseInt(data[i].replace(/,/g, ''), 10);
              isLong = numericValue >= minLong && numericValue <= maxLong;

              //isLong = +(data[i].replace(/,/g, '')) >= -(2 ** 63) && +(data[i].replace(/,/g, '')) <= 2 ** 63 - 1;
              if (isLong === false) return false;
            } else if (sciRegex.test(data[i])) {
              //scientific notation format
              const parsed = Number(data[i]);
              if (Number.isNaN(parsed)) return false;

              //convert to integer safely
              numericValue = BigInt(Math.trunc(parsed));
              if (numericValue < minLong || numericValue > maxLong) {
                return false;
              }
            }
            else {
              const longRegex2 = new RegExp(/^[+-]?\d+(\.\d+)?[eE][+-]?\d+?$/);
              isLong = longRegex2.test(data[i]);
              if (!isLong) return false;
            }
          }
        }

        break;
      default: //string
        break;
    }

    return retVal;
  }

  convertToRoman(str: string, isDuplicate: boolean): string {
    // //console.log(str, isDuplicate);
    // if (str.indexOf('_') > 0 && isDuplicate) {
    //   const match = str.match(/(_\d)$/); // Check if the string ends with `_1`
    //   const matchLastPart = str.match(/[^_]+$/);
    //   let lastPart = matchLastPart ? matchLastPart[0] : '';
    //   const mainPart = match ? str.slice(0, -2) : str; // Extract the main part excluding `_1`// Replace numbers in the main part
    //   const converted = mainPart.replace(/\d+/g, (match) =>
    //     this.toRoman(parseInt(match))
    //   );
    //   return match ? converted + '_' + lastPart : converted; // Append `_1` back if it was present
    // } else {
    //   return str.replace(/(\d+)$/g, (match) => this.toRoman(parseInt(match)));
    // }


    let match: any;
    if (str.indexOf('_') > 0 && isDuplicate) {
      var regex = /(\d+)(?=_\d+$)/;// /(\d+)$/;
      //match = str.match(regex);
    } else {
      var regex = /(\d+)/;

    }

    match = str.match(regex);
    //let match = str.match(regex);
    if (match) {
      let number = parseInt(match[0]);

      const romanNumerals = [
        ["M", 1000],
        ["CM", 900],
        ["D", 500],
        ["CD", 400],
        ["C", 100],
        ["XC", 90],
        ["L", 50],
        ["XL", 40],
        ["X", 10],
        ["IX", 9],
        ["V", 5],
        ["IV", 4],
        ["I", 1]
      ];

      let result = ''
      for (let i = 0; i < romanNumerals.length; i++) {
        while (number >= +romanNumerals[i][1]) {
          result += romanNumerals[i][0];
          number -= +romanNumerals[i][1];
        }
      }


      return str.replace(regex, result);

    } else {
      return str;
    }
  }
  // convertToRoman(str: string, isDuplicate: boolean): string {
  //   //const regex = /(\d+)(?=_\d+$)/;// /(\d+)$/;
  //   console.log(str, 'string');
  //   let match: any;
  //   var regex = /\d+/g;
  //   let len: number;
  //   match = str.match(regex);
  //   if (str.indexOf('_') > 0 && isDuplicate) {
  //     //   // var regex = /(\d+)(?=_\d+)/g; // /(\d+)$/;
  //     //   //match = str.match(regex);
  //     len = match?.length - 1;
  //   } else {
  //     //   var regex = /\d+/g;
  //     len = match?.length;
  //     //   // matches = str.match(/\d+/g);
  //   }
  //   //let match = str.match(regex);
  //   const romanNumerals = [
  //     ['M', 1000],
  //     ['CM', 900],
  //     ['D', 500],
  //     ['CD', 400],
  //     ['C', 100],
  //     ['XC', 90],
  //     ['L', 50],
  //     ['XL', 40],
  //     ['X', 10],
  //     ['IX', 9],
  //     ['V', 5],
  //     ['IV', 4],
  //     ['I', 1],
  //   ];
  //   if (match) {
  //     for (let j = 0; j < len - 1; j++) {
  //       let number = parseInt(match[0]);

  //       let result = '';
  //       for (let i = 0; i < romanNumerals.length; i++) {
  //         while (number >= +romanNumerals[i][1]) {
  //           result += romanNumerals[i][0];
  //           number -= +romanNumerals[i][1];
  //         }
  //       }

  //       str = str.replace(regex, result);
  //       // return str.replace(regex, result);
  //     }

  //     // } else {
  //   }
  //   return str;
  // }

  convertToNumber(str: string): string {
    const match = str.match(/([IVXLCDM]+)$/);

    const romanNumerals: { [key: string]: number } = {
      M: 1000,
      CM: 900,
      D: 500,
      CD: 400,
      C: 100,
      XC: 90,
      L: 50,
      XL: 40,
      X: 10,
      IX: 9,
      V: 5,
      IV: 4,
      I: 1,
    };

    if (!match) return str;

    let roman = match[0];
    let num = 0;
    let i = 0;
    while (i < roman.length) {
      const twoChar = roman[i] + (roman[i + 1] || '');
      if (romanNumerals[twoChar]) {
        num += romanNumerals[twoChar];
        i += 2;
      } else {
        num += romanNumerals[roman[i]];
        i++;
      }
    }

    return str.replace(/[IVXLCDM]+$/, num.toString());
  }

  //converts all natural numbers at the end of the string even with underscores
  convertToRoma2(str: string): string {
    //const regex = /(\d+)(?=_\d+$)/;// /(\d+)$/;
    let match: any;
    // if (str.indexOf('_') > 0) {
    //   var regex = /(\d+)(?=_\d+$)/;// /(\d+)$/;
    //   //match = str.match(regex);
    // }
    // else {
    //   var regex = /(\d+)$/;

    // }

    return str.replace(/\d+/g, (match) => this.toRoman(parseInt(match, 10)));
  }
  toRoman(num: number): string {
    const romanNumerals: { value: number; numeral: string }[] = [
      { value: 1000, numeral: 'M' },
      { numeral: 'CM', value: 900 },
      { numeral: 'D', value: 500 },
      { numeral: 'CD', value: 400 },
      { numeral: 'C', value: 100 },
      { numeral: 'XC', value: 90 },
      { numeral: 'L', value: 50 },
      { numeral: 'XL', value: 40 },
      { numeral: 'X', value: 10 },
      { numeral: 'IX', value: 9 },
      { numeral: 'V', value: 5 },
      { numeral: 'IV', value: 4 },
      { numeral: 'I', value: 1 },
    ];

    let result = '';

    for (const { value, numeral } of romanNumerals) {
      while (num >= value) {
        result += numeral;
        num -= value;
      }
    }

    return result;
  }

  checkForAccents(input: string): boolean {
    const accentRegex = /[\u00C0-\u017F]/;
    return accentRegex.test(input);
  }

  findDuplicateColumnAndGenerateNew(columns: ColumnNameDatatypeName[]) {
    var listOfColumnNames = columns.map((x) => x.ColumnName).sort();

    let results = [];
    for (let i = 0; i < listOfColumnNames.length - 1; i++) {
      if (listOfColumnNames[i + 1] == listOfColumnNames[i]) {
        results.push(listOfColumnNames[i]);
        // results.push(this.columnDatatype.find(x => x.ColumnName === listOfColumnNames[i]));
      }
    }

    results.forEach((duplicateName) => {
      //find the columns which are similar
      let foundColumns = columns.filter(
        (x) => x.ColumnName === duplicateName
      );
      //console.log(results, foundColumns);
      foundColumns.forEach((c, i) => {
        if (i > 0) {
          c.ColumnName = `${c.ColumnName}_${i}`;
          c.isDuplicateColumn = true;
        }
        c.DbColumnName = c.ColumnName;
      });
    });
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

  sortRuleSet(excelFileRule: ExcelRule[]) {
    excelFileRule.sort((a, b) => {

      //prioritize ruleTypeId !== 6
      if (a.ruleTypeId === 6 && b.ruleTypeId !== 6) return 1;
      if (a.ruleTypeId !== 6 && b.ruleTypeId === 6) return -1;

      if (a.ruleSetType === 1 && b.ruleSetType !== 1) return -1;
      if (a.ruleSetType !== 1 && b.ruleSetType === 1) return 1;

      if (a.isGlobal && !b.isGlobal) return -1;
      if (!a.isGlobal && b.isGlobal) return 1;



      //if both have same ruleTypeId, sort by id
      return a.id - b.id;
    });
  }



  /**
 * Validates each file name against a list of regex strings.
 * For each file, checks regexes in order and stops at the first match (short-circuit).
 *
 * @param regexValues Array of regex strings (supports raw like '^INV_\\d+$' or '/^INV_\\d+$/i' if your compilePattern supports that)
 * @param options defaultFlags applied if the pattern provides no inline flags (optional)
 *
 * @returns {
 *   matches: Array<{ file: File; filename: string; matchedIndex: number | null }>;
 *   invalidRegexes: Array<{ pattern: string; error: string }>;
 * }
 *
 * - matchedIndex is the index in `regexValues` that matched first, or null if none matched.
 */
  validateFileNamesWithRegex(
    selectedFiles: SelectedFiles[],
    regexValuesInput: string[] | string
  ) {



    // 0) Normalize regexValues to a clean string[]
    const regexValues: string[] = Array.isArray(regexValuesInput)
      ? regexValuesInput
      : typeof regexValuesInput === 'string'
        // Split on newlines or commas; trim and drop empties
        ? regexValuesInput
          .split(/\r?\n|,/)
          .map(s => s.trim())
          .filter(Boolean)
        : [];


    // 1) Compile patterns ONCE, preserving order
    const compiled: (RegExp | null)[] = [];
    const invalidRegexes: { pattern: string; error: string }[] = [];

    for (const raw of regexValues) {
      const res = this.compilePattern(raw);
      if (res.ok) {
        compiled.push(res.regex);
      } else {
        compiled.push(null); // keep slot to preserve index alignment
        invalidRegexes.push({ pattern: raw, error: res.error });
      }
    }

    // 2) Validate files with per-file short-circuiting
    const matches: { file: File; filename: string; matchedIndex: number | null }[] = [];

    for (const f of selectedFiles) {
      const name = f.File.name.substring(0, f.File.name.lastIndexOf('.'));
      let matchedIndex: number | null = null;

      for (let i = 0; i < compiled.length; i++) {
        const rx = compiled[i];
        if (!rx) continue; // skip invalid pattern slots

        if (rx.test(name)) {
          matchedIndex = i; // first one wins
          break;            // short-circuit for THIS file
        }
      }
      //f.status = matchedIndex !== null ? 'valid' : 'false';
      matches.push({ file: f.File, filename: name, matchedIndex });
    }

    return { matches, invalidRegexes };
  }

  /**
  * Compiles a single regex pattern string.
  * Returns ok:false + error message if invalid.
  */
  compilePattern(
    raw: string,

  ): CompilePatternResult {
    const pattern = (raw ?? '').trim();

    if (!pattern) {
      return { ok: false, error: 'Pattern is empty.', regex: null };
    }


    try {
      const rx = new RegExp(pattern);
      return { ok: true, error: '', regex: rx };
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : String(e);
      return { ok: false, error: message, regex: null };
    }

  }

  updateSelectedFileStatuses(
    selectedFiles: SelectedFiles[],
    regexValues: RegexItem[] | null,
    selectedExtensions: string[] | null
  ) {
    // Normalize inputs
    const hasRegex = regexValues !== null && regexValues.length > 0;
    const hasExt = Array.isArray(selectedExtensions) && selectedExtensions.length > 0;

    // Prepare regex results only if we have regex patterns
    let regexMap = new Map<File, number | null>();

    if (hasRegex) {
      const { matches } =
        this.validateFileNamesWithRegex(selectedFiles, regexValues!.map(r => r.regex));

      for (const m of matches) {
        regexMap.set(m.file, m.matchedIndex);
      }
    }

    // NOW VALIDATE FILES
    for (const sf of selectedFiles) {
      const file = sf.File;
      const fileName = file.name;

      /** --------------------------
       *  EXTENSION VALIDATION
       * -------------------------- */
      let extValid = true; // default to true if extensions NOT provided

      if (hasExt) {
        const ext = fileName.split('.').pop()?.toLowerCase() || '';
        extValid = selectedExtensions!.includes(ext);
      }

      /** --------------------------
       *  REGEX VALIDATION
       * -------------------------- */
      let regexValid = true; // default to true if regex NOT provided

      if (hasRegex) {
        const matchedIndex = regexMap.get(file) ?? null;
        regexValid = matchedIndex !== null;
      }

      /** --------------------------
       *  FINAL DECISION
       * -------------------------- */
      if (hasRegex && hasExt) {
        sf.status = (regexValid || extValid) ? 'valid' : 'invalid';
      }
      else if (hasRegex) {
        sf.status = regexValid ? 'valid' : 'invalid';
      }
      else if (hasExt) {
        sf.status = extValid ? 'valid' : 'invalid';
      }
      else {
        // NO rules provided → you decide the default      
        sf.status = 'valid';  // or allow all
      }


    }
  }

  toRegexArray(input: unknown): RegexItem[] {
    // If it's already an array
    if (Array.isArray(input)) {
      return input
        .map((item: any) => {
          // Case: simple string → regex only
          if (typeof item === 'string' && item.length > 0) {
            return { regex: item };
          }

          // Case: object with regex
          if (item && typeof item.regex === 'string' && item.regex.length > 0) {
            return {
              regex: item.regex,
              description:
                typeof item.description === 'string'
                  ? item.description
                  : undefined,
            };
          }

          return undefined;
        })
        .filter(
          (v): v is RegexItem =>
            typeof v?.regex === 'string'
        );
    }

    // If it's a JSON string
    if (typeof input === 'string') {
      try {
        const parsed = JSON.parse(input);
        return this.toRegexArray(parsed);
      } catch {
        return [];
      }
    }

    return [];
  }

  escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

}

export function requiredArrayValidatorTemp(control: AbstractControl): Validators | null {
  return control.value && control.value.length > 0 ? null : { required: true }
}

export function noWhitespaceValidator(control: AbstractControl): Validators | null {

  const group = control.parent as FormGroup;
  if (!group) {
    return null;
  }
  let ctrlName: string | null = null;
  Object.keys(group.controls).forEach(key => {
    if (group.get(key) === control) {
      ctrlName = key;
    }
  })
  let isWhitespace: boolean = false;
  if (ctrlName === 'deltaSource' || ctrlName === 'blobSourcePath' || ctrlName === 'sourceFolderLocation') {
    isWhitespace = (control.value?.replaceAll('/', ''))?.trim().length === 0;
  } else {
    isWhitespace = (control.value || '').trim().length === 0;
  }
  return isWhitespace ? { whitespace: true } : null;
}

//use backward slash
export function cleanSourceLocation(src: string): string {

  // //replace multiple slashes surrounding spaces with a single slash
  // let cleaned = src
  //   .trim()
  //   .replace(/\/\s*/g, '/') //collapse slashes and right-side spaces
  //   .replace(/\s*\/+/g, '/') //remove spaces before slashes

  //   if(!cleaned.endsWith('/')){
  //     cleaned += '/';
  //   }

  // return cleaned;

  //delete a regex of invalid fodler characcters on windows
  const invalidChars = /[<>:"/\\|?*\x00-\x1F]/g;
  //reserved windows names to avoid (eg. con prn etc.)
  const reserved = /^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$/i;

  //normalized slashes and remove extra ones
  const normalized = src.replace(/[\/\\]+/g, '\\')

  const segments = normalized
    .split('\\') //break path into folders
    .filter(segment => segment.trim() !== '') //remove emtpy segments
    .map(folder => {
      //remove trailing dot or space (not allowed in windows folder names)
      const cleaned = folder.replace(/^[. ]+|[. ]+$/g, '');
      if (!cleaned) return null; //skip if it's empty after cleaning


      //replace invalid characster with a dash or empty string
      const safe = cleaned.replace(invalidChars, '');
      //return reserved.test(safe) ? `${safe}_folder` : safe;
      return safe;
    }).filter(Boolean); //remove nulls

  return segments.join('\\') + (segments.length ? '\\' : ''); // rebuild sanitized path
}

//use forward slash
export function cleanBlobSourceLocation(src: string): string {
  //replace multiple slashes surrounding spaces with a single slash
  let cleaned = src
    .trim() //remove leading/trailing spaces
    .replace(/\\+/g, '/') //replace backslashes with forward slashes
    .replace(/\s*\/\s*\.(\w+)/g, '/$1') //remove slash + option space + dot (e.g., "/ ." -> "/")
    .replace(/\s*\/\s*\./g, '/') //remove segements like "/ ." -> "/"
    .replace(/\s*\/\s*/g, '/') //trim spaces around slashes
    .replace(/\/+/g, '/') //collapse multiple slashes into one
    .replace(/\/+$/, '') + '/' //remove trailing slashes, then add one clean

  return cleaned;
}

export function formatWithOrAnd(input: string[], isCombination: boolean, ruleType: RuleTypeNames): string {
  if (input.length === 0) return input.toString();

  const items = input.map(item => item.trim()).filter(Boolean);
  //v1 const conjunction = subRule === SubRuleTypes.All ? 'and' : 'or';
  let conjunction = '';
  if (ruleType === RuleTypeNames.Unique) {
    conjunction = 'and';
  } else {
    conjunction = isCombination ? 'or' : 'and';
  }
  let result = '';

  switch (items.length) {
    case 0:
      result = '';
      break;
    // return '';
    case 1:
      // return items[0];
      result = items[0];
      break;
    case 2:
      result = `${items[0]} ${conjunction} ${items[1]}`;
      break;
    default:
      result = `${items.slice(0, -1).join(', ')} ${conjunction} ${items[items.length - 1]}`;
      break;
  }



  if (ruleType === RuleTypeNames.Unique) {
    result = isCombination ? result + ' combined' : result;
  }
  return result;


}




//v1 export function formatWithOrAnd(input: string[], subRule: number): string {
export function formatWithOrAndBk(input: string[], isCombination: boolean, ruleType: RuleTypeNames): string {
  if (input.length === 0) return input.toString();

  const items = input.map(item => item.trim()).filter(Boolean);
  //v1 const conjunction = subRule === SubRuleTypes.All ? 'and' : 'or';
  let conjunction = '';
  if (ruleType === RuleTypeNames.Unique) {
    conjunction = 'and';
  } else {
    conjunction = isCombination ? 'or' : 'and';
  }


  switch (items.length) {
    case 0:
      return '';
    case 1:
      return items[0];
    case 2:
      return `${items[0]} ${conjunction} ${items[1]}`;
    default:
      return `${items.slice(0, -1).join(', ')} ${conjunction} ${items[items.length - 1]}`;
  }
}

function toNullableNumber(value: any): number | null {
  return value === '' || value === null ? null : Number(value);
}

export function getInvalidFields(formGroup: FormGroup | FormArray): string[] {
  const invalidFields: string[] = [];
  // const invalidFields: { field: string, value: any }[] = [];

  Object.keys(formGroup.controls).forEach((key) => {
    const control = formGroup.get(key);
    var value = control?.value;

    if (control instanceof FormGroup || control instanceof FormArray) {
      // Recursively handle nested groups or arrays
      invalidFields.push(...getInvalidFields(control));

    } else if (control?.invalid) {
      //console.log(`Checking nested control: ${key}, invalid fields: ${invalidFields}`);
      invalidFields.push(key);
    }
  });
  return invalidFields;
}

export function generateGUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = Math.random() * 16 | 0;
    const v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

export function formatDate(dateInput: Date) {
  let date = new Date(dateInput);

  let dd = date.getDate();
  let mm = date.getMonth() + 1;
  let yyyy = date.getFullYear();

  return yyyy + '-' + mm + '-' + dd;
}

//checks if an array is empty
export function isNullUndefinedEmptyArrays(val: any): boolean {
  return !val || (Array.isArray(val) && val.length === 0);
}

export function findDuplicateAndGenerateNew(input: string[]): string[] {

  // const nameCount = new Map<string, number>();
  // const finalResults: string[] = [];

  // input.forEach(item => {
  //   const count = nameCount.get(item) || 0;
  //   nameCount.set(item, count + 1);

  //   if (count === 0) {
  //     finalResults.push(item); // first occurrence
  //   } else {
  //     finalResults.push(`${item}_${count}`); // subsequent duplicates
  //   }
  // });

  // return finalResults;

  const nameCount = new Map<string, number>();
  const usedNames = new Set<string>();
  const nameCounters = new Map<string, number>();
  const finalResults: string[] = [];

  input.forEach(original => {
    let base = original;
    let name = base;

    // If name already used, generate a new one based on base name
    while (usedNames.has(name)) {
      const count = nameCounters.get(base) || 1;
      name = `${base}_${count}`;
      nameCounters.set(base, count + 1);
      base = original; // always use original as base
    }

    usedNames.add(name);
    nameCounters.set(base, (nameCounters.get(base) || 1));
    finalResults.push(name);
  });

  return finalResults;


}

export function arraysEqual<T>(a: T[] | null | undefined, b: T[] | null | undefined): boolean {
  if (a === b) return true;               // same reference or both null/undefined
  if (!a || !b) return false;             // one is null/undefined
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    if (a[i] !== b[i]) return false;      // shallow element comparison
  }
  return true;
}

export class ValidationHelper {
  RuleTypes = RuleTypeNames;

  // Change to a method (remove `get`) and qualify `this.RuleTypes`
  showCombination(ruleType: number): boolean {
    const excluded = new Set([
      this.RuleTypes.Custom,
      this.RuleTypes.Value,
      this.RuleTypes.Format,
      this.RuleTypes.BEValidation,
      this.RuleTypes.GenericRules,
    ]);
    return !!ruleType && !excluded.has(ruleType);
  }
}

