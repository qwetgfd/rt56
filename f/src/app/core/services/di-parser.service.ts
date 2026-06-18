import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import * as XLSX from 'xlsx';
import * as Papa from 'papaparse';
import { PaparseConfig } from '../models/papaparseConfig';
import { EnglishOnlyCharacters } from '../models/englistOnlyCharacters';
import JSZip from 'jszip';
import { BusyService } from './busy.service';
import { DOMParser } from 'xmldom';
import xpath from 'xpath';
type AOA = any[][];

interface KeyValuePair {
  sheetName: string;
  workBook: AOA;
  selected: boolean;
  ignoreSheet: boolean;
  maxRowCount: number;
  newSheet: boolean;
  missingSheet: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class DiParserService {




  data: AOA = [[1, 2], [3, 4]]
  wk: { key: string, value: AOA }[] = [
    // {key: 'first', value: 'one'},
    // {key: 'second', value: 'two'}
  ];

  wk2: KeyValuePair[] = [];
  constructor(
    private busyService: BusyService
  ) { }

  extractSharedStrings(xmlText: string): string[] {

    const matches = [...xmlText.matchAll(/<t[^>]*>(.*?)<\/t>/g)];

    return matches.map(m => m[1].trim());//.filter(Boolean);
  }

  createSyntheticSheet(string: string[]): XLSX.WorkSheet {
    const sheet: XLSX.WorkSheet = {};
    const limited = string.slice(0, 20);
    limited.forEach((str, i) => {
      const cellRef = `A${i + 1}`;
      sheet[cellRef] =
      {
        v: str, //raw value
        t: 's' //explicitly set type to string
      };
    })

    sheet['!ref'] = `A1:A${limited.length}`;
    return sheet;
  }

  buildSheetFromXML(sheetXML: string, sharedStrings: string[] = []): XLSX.WorkSheet {
    const sheet: XLSX.WorkSheet = {};
    const cellRegex = /<c\s+[^>]*r="([^"]+)"[^>]*>(.*?)<\/c/gs;
    let maxRow = 1;
    let maxCol = 1;

    for (const match of sheetXML.matchAll(cellRegex)) {
      const ref = match[1];
      const cellBlock = match[2];

      const typeMatch = cellBlock.match(/t="(\w+)"/);
      const valMatch = cellBlock.match(/<v>(.*?)<\/v>/);
      const type = typeMatch?.[1];
      let value = valMatch?.[1];
      if (type === 's' && sharedStrings.length) {
        value = sharedStrings[+value];
      }
      sheet[ref] = { v: value ?? '', t: 's' };
      const { r, c } = XLSX.utils.decode_cell(ref);
      maxRow = Math.max(maxRow, r + 1);

      maxCol = Math.max(maxCol, c + 1);

    }
    sheet['!ref'] = `A1:${XLSX.utils.encode_cell({ r: maxRow - 1, c: maxCol - 1 })}`;
    return sheet;
  }


  parseExcel(dataSource: File, hasHeader: number = 1, skipHeaderRows: number = 0, blankrows: boolean, mappingType: string = 'sheetName'): Observable<any> {



    this.wk2 = [];
    const reader: FileReader = new FileReader();
    const chunkSize = 100 * 1024 * 1024;
    reader.readAsArrayBuffer(dataSource);

    return new Observable((o: any) => {



      reader.onload = async (e: any) => {

        if (dataSource.size < 100 * 1024 * 1024) {
          const bstr: string = e.target.result;
          const workBook: XLSX.WorkBook = XLSX.read(bstr, { type: 'array', cellText: false, cellNF: true });

          //count number of sheets
          var numberOfWorkSheet = workBook.SheetNames;

          for (var i = 0; i < workBook.SheetNames.length; i++) {
            var wsname: string = workBook.SheetNames[i];
            const ws: XLSX.WorkSheet = workBook.Sheets[wsname];

            const isEmpty = !ws['!ref'] || XLSX.utils.decode_range(ws['!ref']).e.c === -1;
            const hasData = Object.keys(ws).some(key => key[0] !== '!' && ws[key].v !== undefined && ws[key].v !== '');
            if (!isEmpty) {
              Object.keys(ws).forEach(cell => {
                const cellValue = ws[cell];

                if (cellValue.t === 'n' && cellValue.z === 'm/d/yy') {
                  const excelDate = cellValue.v;
                  const jsDate = new Date((excelDate - (25567 + 2)) * 86400 * 1000);
                  const formattedDate = `${String(jsDate.getMonth() + 1)}/${String(jsDate.getDate())}/${jsDate.getFullYear()}`;
                  ws[cell].v = formattedDate;
                  ws[cell].t = 's';
                }

                if (cellValue.t === 'n' && cellValue.z === 'm/d/yy h:mm') {
                  const excelDate = cellValue.v;
                  // const jsDate = new Date((excelDate - (25567 +2)) * 86400 * 1000);
                  const jsDate = new Date((new Date(1899, 11, 30)).getTime() + excelDate * 24 * 60 * 60 * 1000);
                  // Log the date as a moment.

                  const excelEpoch = new Date(1899, 11, 30);
                  const millisecondsInDay = 24 * 60 * 60 * 1000;


                  const isoDate = new Date((excelDate - 25569) * millisecondsInDay).toISOString();
                  const newFormattedDate = formatISODate(isoDate);
                  const formattedDate = `${String(jsDate.getMonth() + 1)}/${String(jsDate.getDate())}/${jsDate.getFullYear()} ${jsDate.getHours()}:${jsDate.getMinutes()}`;
                  ws[cell].v = newFormattedDate;
                  ws[cell].t = 's';
                }
              })

              this.data = <AOA>(XLSX.utils.sheet_to_json(ws, { header: 1, raw: false, blankrows: !blankrows, defval: "" })); //, blankrows: false
              wsname = mappingType == 'sheetName' ? wsname : `Sheet${i}`; //workBook.SheetNames[i];
              // this.wk2.push({ sheetName: wsname, workBook: this.data, maxRowCount: this.data.length - 1 });
              this.wk2.push({ sheetName: wsname, workBook: this.data, selected: true, ignoreSheet: false, maxRowCount: this.data.length - 1, newSheet: false, missingSheet: false }); //.slice(0,21)
            }
          }
        } else {
          this.busyService.busy();

          const zip = await JSZip.loadAsync(reader.result as ArrayBuffer);
          const sharedXml = await zip.file('xl/sharedStrings.xml')?.async('text');
          const stylesXml = await zip.file('xl/styles.xml')?.async('string');

          const cellXfs = [...stylesXml.matchAll(/<cellXfs[^>]*>([\S\s]*?)<\/cellXfs>/g)];
          const customFormats = [...stylesXml.matchAll(/numFmt[^>]*numFmtId=["'](\d+)["'][^>]*formatCode=["']([^"']+)["']/g)];

          const workbookXml = await zip.file('xl/workbook.xml')?.async('string');
          const workbookDoc = new DOMParser().parseFromString(workbookXml!, 'application/xml');
          const select = xpath.useNamespaces({ main: 'http://schemas.openxmlformats.org/spreadsheetml/2006/main' });

          const sheetNodes = select('//main:sheet', workbookDoc) as Element[];
          const sheetDataMap: Record<string, AOA> = {};

          for (let i = 0; i < sheetNodes.length; i++) {
            const sheetName = sheetNodes[i].getAttribute('name');
            const sheetPath = `xl/worksheets/sheet${i + 1}.xml`;
            const sheetXmlBuffer = await zip.file(sheetPath)?.async('arraybuffer'); //sheet data
            if (!sheetXmlBuffer) continue;
            const decoder = new TextDecoder();
            const textChunk = decoder.decode(sheetXmlBuffer.slice(0, 10 * 1024 * 1024));
            const dimensions = textChunk.match(/<dimension[^>]*ref="([^"]+)"/);
            //#region - removed logic to determine dimensions from <dimension> element 
            // - some files are missing this element and we can infer dimensions by scanning cell refs

            //if (!dimensions) continue;
            // const ref = dimensions[1];
            // const [, endRef] = ref.split(':');
            // const colLetters = endRef.replace(/\d+$/, '');
            // const maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
            // const colIndex = XLSX.utils.decode_col(colLetters);
            // const columnCount = colIndex + 1;
            //#endregion

            const textChunk2 = decoder.decode(sheetXmlBuffer.slice(0, chunkSize), { stream: true });
            //#region - logic to determine dimensions from <dimension> element or by scanning cell refs if <dimension> is missing
            let maxRowCount: number = 0;
            let columnCount: number = 0;

            if (dimensions) {
              const ref = dimensions[1];
              const endRef = ref.includes(':') ? ref.split(':')[1] : ref;
              const colLetters = endRef.replace(/\d+$/, '');
              maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
              columnCount = XLSX.utils.decode_col(colLetters) + 1;
            } else {
              // No <dimension> element — scan cell refs to determine bounds
              const cellRefs = [...textChunk2.matchAll(/<c\b[^>]*\br="([A-Z]+)(\d+)"/g)];
              if (cellRefs.length === 0) continue;
              let maxCol = 0;
              maxRowCount = 0;
              for (const m of cellRefs) {
                const col = XLSX.utils.decode_col(m[1]);
                const row = parseInt(m[2], 10);
                if (col > maxCol) maxCol = col;
                if (row > maxRowCount) maxRowCount = row;
              }
              columnCount = maxCol + 1;
            }
            //#endregion

            const siMatches = [...sharedXml.matchAll(/<si>([\s\S]*?)<\/si>/g)];
            const sharedStrings = siMatches.map(si => {
              const tMatches = [...si[1].matchAll(/<t[^>]*>(.*?)<\/t>/g)];
              return tMatches.map(t => t[1]).join('');
            });

            const sheet: XLSX.WorkSheet = {};
            const cellMatches = [...textChunk2.matchAll(/<c\b[^>]*\/>|<c\b[^>]*>[\s\S]*?<\/c>/g)];

            const MAX_ROWS = 20000;
            const rowSet = new Set();
            let maxRow = 0;
            let maxCol = 0;

            for (const match of cellMatches) {
              const xml = match[0];
              const cellRef = xml.match(/r="([^"]+)"/)?.[1] ?? null;
              const rowMatch = cellRef?.match(/\d+/);
              const rowNumber = rowMatch ? rowMatch[0] : null;
              if (rowNumber) rowSet.add(rowNumber);
              if (rowSet.size >= MAX_ROWS) break;

              const type = xml.match(/t="(\w+)"/)?.[1];
              const styleId = xml.match(/s="(\d+)"/) ? parseInt(xml.match(/s="(\d+)"/)![1], 10) : null;
              const rawValue = xml.match(/<v>(.*?)<\/v>/)?.[1];
              if (!rawValue) continue;

              let value = '';
              let cellType: 's' | 'n' | 'd' = 's';

              if (type === 's') {
                value = sharedStrings[+rawValue] ?? '';
                cellType = 's';
              } else {
                const num = parseFloat(rawValue);
                if (!isNaN(num) && styleId !== null) {
                  value = getFormattedDate(num, styleId, cellXfs, customFormats);
                  cellType = 'd';
                } else {
                  value = rawValue;
                  cellType = 'n';
                }
              }

              sheet[cellRef!] = { v: value, t: cellType };
              const { r, c } = XLSX.utils.decode_cell(cellRef!);
              maxRow = Math.max(maxRow, r + 1);
              maxCol = Math.max(maxCol, c + 1);
            }

            sheet['!ref'] = `A1:${XLSX.utils.encode_cell({ r: maxRow - 1, c: maxCol - 1 })}`;
            this.data = XLSX.utils.sheet_to_json(sheet, { header: 1, raw: false, blankrows: !blankrows, defval: "" });
            this.wk2.push({ sheetName: sheetName, workBook: this.data, selected: true, ignoreSheet: false, maxRowCount: maxRowCount - 1, newSheet: false, missingSheet: false }); //.slice(0,21)
          }
        }
        // // else {
        // //   this.busyService.busy();
        // //   //console.log(`parsing start: ${performance.now()}`);
        // //   const chunkSize = 100 * 1024 * 1024;
        // //   const zip = await JSZip.loadAsync(reader.result as ArrayBuffer);

        // //   //to get cell values
        // //   const sharedXml = await zip.file('xl/sharedStrings.xml')?.async('text');

        // //   //to get actual cell data
        // //   const sheetXml = await zip.file('xl/worksheets/sheet1.xml')?.async('arraybuffer');

        // //   const stylesXml = await zip.file('xl/styles.xml')?.async('string');
        // //   const cellXfs = [...stylesXml.matchAll(/<cellXfs[^>]*>([\S\s]*?)<\/cellXfs>/g)];
        // //   const customFormats = [...stylesXml.matchAll(/numFmt[^>]*numFmtId=["'](\d+)["'][^>]*formatCode=["']([^"']+)["']/g)];
        // //   //to get the sheetnames
        // //   const xmlString = await zip.file('xl/workbook.xml')?.async('string');
        // //   const doc = new DOMParser().parseFromString(xmlString!, 'application/xml');
        // //   //register the namespace used in workbook.xml
        // //   const select = xpath.useNamespaces({
        // //     main: 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
        // //   });
        // //   //use the prefix in your xpath query      
        // //   const sheetNodes = select('//main:sheet', doc) as Element[];
        // //   const sheetNames = sheetNodes.map(sheet => sheet.getAttribute('name'));


        // //   //console.log('sheetnames: ', sheetNames)

        // //   const textChunk = new TextDecoder().decode(sheetXml.slice(0, 10 * 1024 * 1024)); //1mb

        // //   const dimensions = textChunk.match(/<dimension[^>]*ref="([^"]+)"/);
        // //   //const tempRows = await streamSheetCells(zip);
        // //   // tempRows.forEach((rowcells, i) => {
        // //   //   console.log(`row ${i + 1}:`, rowcells);
        // //   // })


        // //   if (!dimensions) {
        // //     console.warn('no dimensions');
        // //     return null;
        // //   }

        // //   const decoder = new TextDecoder();

        // //   const textChunk2 = decoder.decode(sheetXml.slice(0, chunkSize), { stream: true });
        // //   //const textChunk2 = decoder.decode(sheetXml, { stream: true });
        // //   // const uint8 = new Uint8Array(sheetXml);
        // //   // const textChunk2 = decoder.decode(uint8);

        // //   const ref = dimensions[1]; // eg a1:q10
        // //   const [, endRef] = ref.split(':');
        // //   const colLetters = endRef.replace(/\d+$/, '');
        // //   const maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
        // //   const colIndex = XLSX.utils.decode_col(colLetters); //q = 16 0 base
        // //   const columnCount = colIndex + 1;
        // //   if (!sharedXml) {
        // //     console.warn('sharedStrings.xml not found');
        // //     return null;
        // //   }


        // //   const siMatches = [...sharedXml.matchAll(/<si>([\s\S]*?)<\/si>/g)];
        // //   const sharedStrings = siMatches.map(si => {
        // //     const tMatches = [...si[1].matchAll(/<t[^>]*>(.*?)<\/t>/g)];
        // //     return tMatches.map(t => t[1]).join('');
        // //   });


        // //   //process cells from sheetXML
        // //   const sheet1: XLSX.WorkSheet = {};
        // //   //const cellmMatches = [...textChunk2.matchAll(/<c[^>]*r="([^"]+)"[^>]*>(.*?)<\/c>/gs)];
        // //   const cellmMatches = [...textChunk2.matchAll(/<c\b[^>]*\/>|<c\b[^>]*>[\s\S]*?<\/c>/g)];

        // //   const MAX_ROWS = 20000;
        // //   const rowSet = new Set();

        // //   let maxRow = 0;
        // //   let maxCol = 0;


        // //   for (const match of cellmMatches) {
        // //     const xml = match[0]; //G2
        // //     const cellRef = xml.match(/r="([^"]+)"/)?.[1] ?? null;
        // //     //check if we are in MAX_ROWS
        // //     const rowMatch = cellRef.match(/\d+/);
        // //     const rowNumber = rowMatch ? rowMatch[0] : null;
        // //     if (rowNumber) rowSet.add(rowNumber);
        // //     if (rowSet.size >= MAX_ROWS) break;

        // //     //const inner = match[2]; //raw cell inner XML
        // //     const tMatch = xml.match(/t="(\w+)"/); //inner.match(/t="(\w+)"/);
        // //     const sMatch = xml.match(/s="(\d+)"/);// inner.match(/s="(\d+)"/);
        // //     const vmatch = xml.match(/<v>(.*?)<\/v>/);//inner.match(/<v>(.*?)<\/v>/);
        // //     if (!vmatch) continue;
        // //     const type = xml.match(/t="(\w+)"/)?.[1];;//match[0].match(/t="(\w+)"/)?.[1];//  tMatch?.[1];
        // //     const styleId = sMatch ? parseInt(sMatch[1], 10) : null;
        // //     const rawValue = vmatch[1];

        // //     let value: string = '';
        // //     let cellType: 's' | 'n' | 'd' = 's';

        // //     if (type === 's') {
        // //       value = sharedStrings[+rawValue] ?? '';
        // //       cellType = 's';
        // //     } else {
        // //       const num = parseFloat(rawValue);
        // //       if (!isNaN(num) && styleId !== null) {
        // //         value = getFormattedDate(num, styleId, cellXfs, customFormats);
        // //         cellType = 'd';
        // //       } else {
        // //         value = rawValue;
        // //         cellType = 'n';
        // //       }
        // //     }

        // //     sheet1[cellRef] = { v: value, t: cellType };

        // //     const { r, c } = XLSX.utils.decode_cell(cellRef);

        // //     maxRow = Math.max(maxRow, r + 1);
        // //     maxCol = Math.max(maxCol, c + 1);
        // //   }


        // //   sheet1['!ref'] = `A1:${XLSX.utils.encode_cell({ r: maxRow - 1, c: maxCol - 1 })}`;

        // //   this.data = <AOA>(XLSX.utils.sheet_to_json(sheet1, { header: 1, raw: false, blankrows: !blankrows, defval: "" })); //, blankrows: false

        // //   // var html = XLSX.utils.sheet_to_html(ws);
        // //   // var csvtemp = XLSX.utils.sheet_to_csv(ws);
        // //   // this.wk.push({ key: wsname, value: temp });
        // //   wsname = mappingType == 'sheetName' ? sheetNames[0] : `Sheet${i}`; //workBook.SheetNames[i];
        // //   // this.wk2.push({ sheetName: wsname, workBook: this.data, maxRowCount: maxRowCount - 1 }); //.slice(0,21)
        // //   this.wk2.push({ sheetName: wsname, workBook: this.data, selected: true, ignoreSheet: false, maxRowCount: this.data.length - 1, newSheet: false, missingSheet: false });


        // // }
        this.busyService.idle();
        //console.log(`parsing done: ${performance.now()}`);
        o.next(this.wk2);
        o.complete();

      };

    })

    //reader.readAsBinaryString(target)
  }

  asyncParseExcel(dataSource: File, hasHeader: number = 1, skipHeaderRows: number = 0, blankrows: boolean, mappingType: string = 'sheetName'): Promise<any> {



    this.wk2 = [];
    const reader: FileReader = new FileReader();
    const chunkSize = 100 * 1024 * 1024;
    reader.readAsArrayBuffer(dataSource);

    return new Promise((resolve, reject) => {



      reader.onload = async (e: any) => {
        try {
          if (dataSource.size < 100 * 1024 * 1024 && dataSource.name.split('.').pop() !== 'xlsx') {
            //xls and xlsb only
            const bstr: string = e.target.result;
            const workBook: XLSX.WorkBook = XLSX.read(bstr, { type: 'array', cellText: false, cellNF: true });

            //count number of sheets
            var numberOfWorkSheet = workBook.SheetNames;

            for (var i = 0; i < workBook.SheetNames.length; i++) {
              var wsname: string = workBook.SheetNames[i];
              const ws: XLSX.WorkSheet = workBook.Sheets[wsname];

              const isEmpty = !ws['!ref'] || XLSX.utils.decode_range(ws['!ref']).e.c === -1;
              const hasData = Object.keys(ws).some(key => key[0] !== '!' && ws[key].v !== undefined && ws[key].v !== '');
              if (!isEmpty) {
                Object.keys(ws).forEach(cell => {
                  const cellValue = ws[cell];

                  if (cellValue.t === 'n' && cellValue.z === 'm/d/yy') {
                    const excelDate = cellValue.v;
                    const jsDate = new Date((excelDate - (25567 + 2)) * 86400 * 1000);
                    const formattedDate = `${String(jsDate.getMonth() + 1)}/${String(jsDate.getDate())}/${jsDate.getFullYear()}`;
                    ws[cell].v = formattedDate;
                    ws[cell].t = 's';
                  }

                  if (cellValue.t === 'n' && cellValue.z === 'm/d/yy h:mm') {
                    const excelDate = cellValue.v;
                    // const jsDate = new Date((excelDate - (25567 +2)) * 86400 * 1000);
                    const jsDate = new Date((new Date(1899, 11, 30)).getTime() + excelDate * 24 * 60 * 60 * 1000);
                    // Log the date as a moment.

                    const excelEpoch = new Date(1899, 11, 30);
                    const millisecondsInDay = 24 * 60 * 60 * 1000;


                    const isoDate = new Date((excelDate - 25569) * millisecondsInDay).toISOString();
                    const newFormattedDate = formatISODate(isoDate);
                    const formattedDate = `${String(jsDate.getMonth() + 1)}/${String(jsDate.getDate())}/${jsDate.getFullYear()} ${jsDate.getHours()}:${jsDate.getMinutes()}`;
                    ws[cell].v = newFormattedDate;
                    ws[cell].t = 's';
                  }
                })

                //this.data = <AOA>(XLSX.utils.sheet_to_json(ws, { header: 1, raw: false, blankrows: !blankrows, defval: "" })); //, blankrows: false



                const rawData = XLSX.utils.sheet_to_json(ws, {
                  header: 1,
                  raw: false,
                  blankrows: !blankrows,
                  defval: ""
                }) as any[][];

                this.data = rawData.map(row =>
                  row.map(cell =>
                    typeof cell === 'string' ? decodeHtml(cell) : cell
                  )
                ) as AOA;


                wsname = mappingType == 'sheetName' ? wsname : `Sheet${i}`; //workBook.SheetNames[i];
                // this.wk2.push({ sheetName: wsname, workBook: this.data, maxRowCount: this.data.length - 1 });
                this.wk2.push({ sheetName: wsname, workBook: this.data, selected: true, ignoreSheet: false, maxRowCount: this.data.length - 1, newSheet: false, missingSheet: false }); //.slice(0,21)
              }
            }
          } else {


            //only for xlsx
            this.busyService.busy();

            const zip = await JSZip.loadAsync(reader.result as ArrayBuffer);
            const sharedXml = await zip.file('xl/sharedStrings.xml')?.async('text');
            const stylesXml = await zip.file('xl/styles.xml')?.async('string');

            const cellXfs = [...stylesXml.matchAll(/<cellXfs[^>]*>([\S\s]*?)<\/cellXfs>/g)];
            const customFormats = [...stylesXml.matchAll(/numFmt[^>]*numFmtId=["'](\d+)["'][^>]*formatCode=["']([^"']+)["']/g)];

            const workbookXml = await zip.file('xl/workbook.xml')?.async('string');
            const workbookDoc = new DOMParser().parseFromString(workbookXml!, 'application/xml');
            const select = xpath.useNamespaces({ main: 'http://schemas.openxmlformats.org/spreadsheetml/2006/main' });

            const sheetNodes = select('//main:sheet', workbookDoc) as Element[];
            const sheetDataMap: Record<string, AOA> = {};

            for (let i = 0; i < sheetNodes.length; i++) {
              const sheetName = sheetNodes[i].getAttribute('name');
              const sheetPath = `xl/worksheets/sheet${i + 1}.xml`;
              const sheetXmlBuffer = await zip.file(sheetPath)?.async('arraybuffer'); //sheet data
              if (!sheetXmlBuffer) continue;
              const decoder = new TextDecoder();
              const textChunk = decoder.decode(sheetXmlBuffer.slice(0, 10 * 1024 * 1024));
              const dimensions = textChunk.match(/<dimension[^>]*ref="([^"]+)"/);
              //if (!dimensions) continue;


              const textChunk2 = decoder.decode(sheetXmlBuffer.slice(0, chunkSize), { stream: true });

              // const ref = dimensions[1];
              // const endRef = ref.includes(':') ? ref.split(':')[1] : ref;
              // //const [, endRef] = ref.split(':');
              // const colLetters = endRef.replace(/\d+$/, '');
              // const maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
              // const colIndex = XLSX.utils.decode_col(colLetters);
              // const columnCount = colIndex + 1;

              //#region - logic to determine dimensions from <dimension> element or by scanning cell refs if <dimension> is missing
              let maxRowCount: number = 0;
              let columnCount: number = 0;

              if (dimensions) {
                const ref = dimensions[1];
                const endRef = ref.includes(':') ? ref.split(':')[1] : ref;
                const colLetters = endRef.replace(/\d+$/, '');
                maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
                columnCount = XLSX.utils.decode_col(colLetters) + 1;
              } else {
                // No <dimension> element — scan cell refs to determine bounds
                const cellRefs = [...textChunk2.matchAll(/<c\b[^>]*\br="([A-Z]+)(\d+)"/g)];
                if (cellRefs.length === 0) continue;
                let maxCol = 0;
                maxRowCount = 0;
                for (const m of cellRefs) {
                  const col = XLSX.utils.decode_col(m[1]);
                  const row = parseInt(m[2], 10);
                  if (col > maxCol) maxCol = col;
                  if (row > maxRowCount) maxRowCount = row;
                }
                columnCount = maxCol + 1;
              }
              //#endregion

              const siMatches = [...sharedXml.matchAll(/<si>([\s\S]*?)<\/si>/g)];
              const sharedStrings = siMatches.map(si => {
                const tMatches = [...si[1].matchAll(/<t[^>]*>(.*?)<\/t>/g)];
                return tMatches.map(t => t[1]).join('');
              });

              const sheet: XLSX.WorkSheet = {};
              const cellMatches = [...textChunk2.matchAll(/<c\b[^>]*\/>|<c\b[^>]*>[\s\S]*?<\/c>/g)];

              const MAX_ROWS = 20000;
              const rowSet = new Set();
              let maxRow = 0;
              let maxCol = 0;

              for (const match of cellMatches) {
                const xml = match[0];
                const cellRef = xml.match(/r="([^"]+)"/)?.[1] ?? null;
                const rowMatch = cellRef?.match(/\d+/);
                const rowNumber = rowMatch ? rowMatch[0] : null;
                if (rowNumber) rowSet.add(rowNumber);
                if (rowSet.size >= MAX_ROWS) break;

                const type = xml.match(/t="(\w+)"/)?.[1];
                const styleId = xml.match(/s="(\d+)"/) ? parseInt(xml.match(/s="(\d+)"/)![1], 10) : null;
                const rawValue = xml.match(/<v>(.*?)<\/v>/)?.[1];
                if (!rawValue) continue;

                let value = '';
                let cellType: 's' | 'n' | 'd' = 's';

                if (type === 's') {
                  value = sharedStrings[+rawValue] ?? '';
                  cellType = 's';
                } else {
                  const num = parseFloat(rawValue);
                  if (!isNaN(num) && styleId !== null) {
                    value = getFormattedDate(num, styleId, cellXfs, customFormats);
                    cellType = 'd';
                  } else {
                    value = rawValue;
                    cellType = 'n';
                  }
                }

                sheet[cellRef!] = { v: value, t: cellType };
                const { r, c } = XLSX.utils.decode_cell(cellRef!);
                maxRow = Math.max(maxRow, r + 1);
                maxCol = Math.max(maxCol, c + 1);
              }

              sheet['!ref'] = `A1:${XLSX.utils.encode_cell({ r: maxRow - 1, c: maxCol - 1 })}`;
              //this.data = XLSX.utils.sheet_to_json(sheet, { header: 1, raw: false, blankrows: !blankrows, defval: "" });

              const rawData = XLSX.utils.sheet_to_json(sheet, { header: 1, raw: false, blankrows: !blankrows, defval: "" }) as any[][];

              this.data = rawData.map(row =>
                row.map(cell =>
                  typeof cell === 'string' ? decodeHtml(cell) : cell
                )
              ) as AOA;
              this.wk2.push({ sheetName: sheetName, workBook: this.data, selected: true, ignoreSheet: false, maxRowCount: maxRowCount - 1, newSheet: false, missingSheet: false }); //.slice(0,21)
            }
          }
          this.busyService.idle();
          //console.log(`parsing done: ${performance.now()}`);
          resolve(this.wk2);
        }
        catch (error) {

          console.error('Error parsing Excel file:', error);
          reject(error);

        } finally {
          this.busyService.idle();
        }

      };
      reader.onerror = (err) => {
        reject(err);
      }

    })


  }

  scavengeSheetStrings(workBook: XLSX.WorkBook): XLSX.WorkSheet | null {
    const sheetName = workBook.SheetNames?.[0];
    const sheet = workBook.Sheets[sheetName];


    // if(!sheet || Object.keys(sheet).length === 0){
    //   console.warn('sheet appears empty or lacks structured cells');
    //   return null;
    // }
    const cellAddress = Object.keys(sheet).filter(k => !k.startsWith('!'));
    if (cellAddress.length === 0) {
      console.warn('no data cells found in the sheet');
      return null;
    }

    const stringCells: Record<string, string> = {};
    cellAddress.forEach(addr => {
      const cell = sheet[addr];
      if (typeof cell?.v === 'string') {
        stringCells[addr] = cell.v;
      }
    });
    if (Object.keys(stringCells).length === 0) {
      console.warn('no string values found');
    } else {
      console.log('string cells found');

      Object.entries(stringCells).forEach(([addr, value]) => {

      });
    }
    return sheet;

  }



  getHeaderRowForExcel(sheet: any, skipHeaderRows: number) {
    const headers = [];
    const range = XLSX.utils.decode_range(sheet["!ref"]);
    let C;
    range.s.r = skipHeaderRows;
    const R = range.s.r;
    /* start in the first row */
    for (C = range.s.r; C <= range.e.c; ++C) {
      /* walk every column in the range */
      const cell = sheet[XLSX.utils.encode_cell({ c: C, r: R })];
      /* find the cell in the first row */
      let hdr = "UNKNOWN " + C; // <-- replace with your desired default
      if (cell && cell.t) hdr = XLSX.utils.format_cell(cell);
      headers.push(hdr);
    }
    return headers;
  }

  parseCSVTXT_test(dataSource: any, config: PaparseConfig): Promise<any[]> {
    var newLine = '';
    //let seen = new Set();
    let seen: string[] = [];
    var processedColumnNames: { name: string; index: number; isDuplicate: boolean }[] = [];
    let processedIndex = new Set();

    return new Promise((resolve, reject) => {
      Papa.parse(dataSource, {
        delimiter: config.delimiter == "\\t" ? "\t" : config.delimiter,
        header: config.hasHeader,
        skipEmptyLines: config.skipEmptyLines,
        quoteChar: config.quoteCharacter,
        encoding: "ASCII",
        //newline: "\n",
        //worker:true,
        // chunkSize: 1024 * 1024 * 1, //1mb
        // chunk : function(results, parser){
        //   console.log(results);
        //   e.next(results.data.slice(0,10));
        // },
        transformHeader: function (h, i) {

          if (processedIndex.has(String(i))) {
            //if(processedColumnNames.findIndex(x => x.index === i) >= 0) {
            let foundColmn = processedColumnNames.find(x => x.index === i);

            if (config.useOnlyRomanNumerals) {


              return convertToRoman(foundColmn.name, foundColmn.isDuplicate);

            }

            return foundColmn.name;
          }
          else {
            processedIndex.add(i);
            //processedColumnNames.push({name: h, index : i, isDuplicate : false});
          };

          if (processedColumnNames.findIndex(x => x.name === h) >= 0) {
            let counter = 1;
            let newName = h + '_' + counter;

            while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
              counter++;
              newName = h + '_' + counter;
            }

            processedColumnNames.push({ name: newName, index: +i, isDuplicate: true });

            return newName;

          } else {
            if (h.trim().length === 0) {
              h = `COL${i}`;
            }

            processedColumnNames.push({ name: h, index: +i, isDuplicate: false });
            return h;
          }



        },
        complete:
          //e.next(result.data.slice(0,10));

          (result: any) => {
            if (config.skipEmptyLines) {
              result.data = result.data.filter(row => row.some(cell => cell.trim() !== ''))
            }
            resolve(result.data)
          },

        error: (error) => reject(error)
      });
    });

  }

  detectNewLine(text) {
    const counts = {

      '\r\n': (text.match(/\r\n/g) || []).length,
      '\n': (text.match(/[^\r]\n/g) || []).length,
      '\r': (text.match(/\r[^\n]/g) || []).length
    };

    // Return the most frequent newline
    return Object.entries(counts).sort((a, b) => b[1] - a[1])[0][0];

  }


  parseCSVTXT(dataSource: any, config: PaparseConfig): Observable<any> {

    var newLine = '';
    //let seen = new Set();
    let seen: string[] = [];
    var processedColumnNames: { name: string; index: number; isDuplicate: boolean }[] = [];
    let processedIndex = new Set();
    let convertedWord: string = '';

    // if(config.delimiter === "\\t"){
    //   config.delimiter = "\t";

    // }

    // if(config.delimiter === "none") {
    //   config.delimiter = "\r\n";
    //   newLine = "\n";
    // }


    return new Observable((e: any) => {
      Papa.parse(dataSource, {
        delimiter: config.delimiter === "\\t" ? "\t" : config.delimiter === "none" ? "^~#" : config.delimiter,
        header: config.hasHeader,
        skipEmptyLines: config.skipEmptyLines,
        quoteChar: config.quoteCharacter,
        encoding: "ASCII",
        newline: config.delimiter === "none" ? '\n' : '\r\n',
        //newline: "\n",
        //worker:true,
        // chunkSize: 1024 * 1024 * 1, //1mb
        // chunk : function(results, parser){
        //   console.log(results);
        //   e.next(results.data.slice(0,10));
        // },
        transformHeader: function (h, i) {

          if (processedIndex.has(String(i))) {
            //if(processedColumnNames.findIndex(x => x.index === i) >= 0) {
            let foundColmn = processedColumnNames.find(x => x.index === i);
            convertedWord = foundColmn.name;
            if (config.useOnlyEnglishLetters) {

              //findDuplicateColumnAndGenerateNew(foundColmn, processedColumnNames, config.englishOnlyCharacters);

              let hasNewName = false;

              Array.from(convertedWord).forEach(char => {
                var equivalent = config.englishOnlyCharacters.find(c => c.charToConvert === char);
                if (equivalent) {
                  convertedWord = convertedWord.replace(char, equivalent.englishEquivalent);
                  hasNewName = true;
                }
              });

              convertedWord = cleanColumnName(convertedWord);

              if (hasNewName) {
                if (processedColumnNames.findIndex(x => x.name === convertedWord) >= 0) {
                  let counter = 1;
                  let newName = convertedWord + '_' + counter;

                  while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
                    counter++;
                    newName = convertedWord + '_' + counter;
                  }

                  foundColmn.name = newName;
                  foundColmn.isDuplicate = true;
                } else {
                  processedColumnNames.find(x => x.index === i).name = convertedWord;
                }
              }

            }

            if (config.useOnlyRomanNumerals) {


              return convertToRoman(foundColmn.name, foundColmn.isDuplicate);

            }

            return foundColmn.name;
          }
          else {
            processedIndex.add(i);
            //processedColumnNames.push({name: h, index : i, isDuplicate : false});
          };

          convertedWord = h;

          if (config.useOnlyEnglishLetters) {

            let hasNewName = false;
            Array.from(convertedWord).forEach(char => {
              var equivalent = config.englishOnlyCharacters.find(c => c.charToConvert === char);
              if (equivalent) {
                convertedWord = convertedWord.replace(char, equivalent.englishEquivalent);
                hasNewName = true;
              }
            });

            convertedWord = cleanColumnName(convertedWord);
            if (hasNewName) {
              if (processedColumnNames.findIndex(x => x.name === convertedWord) >= 0) {
                let counter = 1;
                let newName = convertedWord + '_' + counter;

                while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
                  counter++;
                  newName = convertedWord + '_' + counter;
                }

                processedColumnNames.push({ name: newName, index: +i, isDuplicate: true });
              } else {
                processedColumnNames.push({ name: h, index: +i, isDuplicate: false });
                return h;
              }
            }
          }

          if (config.useOnlyRomanNumerals) {
            h = cleanColumnName(convertToRoman(h, false));
            //processedColumnNames.push({name : h, index: +i, isDuplicate : false});
            //return h;            
          } else {
            h = cleanColumnName(h);
          }


          if (processedColumnNames.findIndex(x => x.name === h) >= 0) {
            let counter = 1;
            let newName = h + '_' + counter;

            while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
              counter++;
              newName = h + '_' + counter;
            }

            processedColumnNames.push({ name: newName, index: +i, isDuplicate: true });

            return newName;

          } else {
            if (h.trim().length === 0) {
              h = `COL${i}`;
            }

            processedColumnNames.push({ name: h, index: +i, isDuplicate: false });
            return h;
          }



        },
        complete: (result: any, parser) => {
          //e.next(result.data.slice(0,10));
          if (config.skipEmptyLines) {
            result.data = result.data.filter(row =>
              Object.values(row).some(
                cell => String(cell).trim() !== ""
              )
            );
          }
          e.next(result);
          e.complete();
        },
        error: function (error) {
          console.log("error!!!! " + error);
        }
      })
    });
  }


  private readFileAsText(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsText(file);
    });
  }


  private makeUniqueHeaders(headers: string[], config: PaparseConfig): string[] {
    const used = new Map<string, number>();
    return headers.map((h, i) => {
      let name = (h ?? '').replace(/\r/g, '').trim();
      if (!name) name = `COL_${i}`; // fill empty header
      name = cleanColumnName(name);
      if (config.useOnlyEnglishLetters) {
        Array.from(name).forEach(char => {
          var equivalent = config.englishOnlyCharacters.find(c => c.charToConvert === char);
          if (equivalent) {
            name = name.replace(char, equivalent.englishEquivalent);
          }
        });
      }

      if (config.useOnlyRomanNumerals) {
        name = convertToRoman(name, false);
      }

      const count = used.get(name) ?? 0;
      used.set(name, count + 1);
      return count === 0 ? name : `${name}_${count}`; // suffix duplicates
    });
  }

  /**
   * Normalize text to make parsing deterministic:
   * - Convert CRLF -> LF
   * - Convert smart quotes -> straight quotes
   * - Replace NBSP with normal space
   */
  normalize(text: string): string {
    return text
      .replace(/\uFEFF/g, '')                        // BOM
      .replace(/\r\n/g, '\n')                        // CRLF -> LF
      .replace(/\r/g, '\n')                          // lone CR -> LF
      .replace(/[\u201C\u201D\u201E\u201F\u2033]/g, '"') // smart quotes -> "
      .replace(/\u00A0/g, ' ');                      // NBSP -> space
  }

  /**
   * Peek header synchronously (no worker), reading only the first line.
   * This avoids the "everything is a header" problem if newline detection fails.
   */
  private peekHeadersSync(cleaned: string, cfg: PaparseConfig): string[] {
    // Read only the first line to guarantee we're peeking the header row.
    const firstLine = cleaned.split('\n', 1)[0] ?? '';
    const preview = Papa.parse<string[]>(firstLine + '\n', {
      delimiter: cfg.delimiter,
      header: false,
      skipEmptyLines: false,
      preview: 1,
      fastMode: false,
      quoteChar: cfg.quoteCharacter,
      worker: false, // IMPORTANT: synchronous peek
    });
    return (preview.data[0] ?? []).map(h => String(h));
  }

  async asyncParseCSVTXT(dataSource: any, config: PaparseConfig): Promise<any> {

    const text = await this.readFileAsText(dataSource);
    const newline = this.detectNewLine(text) as "\r\n" | "\n" | "\r";

    //var newLine = '';
    //let seen = new Set();
    let seen: string[] = [];
    var processedColumnNames: { name: string; index: number; isDuplicate: boolean }[] = [];
    let processedIndex = new Set();
    let convertedWord: string = '';

    const cleaned = this.normalize(text);
    const rawHeaders = this.peekHeadersSync(cleaned, config);
    const uniqueHeaders = this.makeUniqueHeaders(rawHeaders, config);

    return new Promise((resolve, e: any) => {
      Papa.parse(text, {
        delimiter: config.delimiter === "\\t" ? "\t" : config.delimiter === "none" ? "^~#" : config.delimiter,
        header: config.hasHeader,
        skipEmptyLines: config.skipEmptyLines,
        quoteChar: config.quoteCharacter,
        encoding: "ASCII",
        newline: newline as "\r\n" | "\n" | "\r", //config.delimiter === "none" ? '\n' : '\r\n',
        transformHeader: (h, i) => uniqueHeaders[i],
        // transformHeader: function (h, i) {

        //   if (processedIndex.has(String(i))) {
        //     //if(processedColumnNames.findIndex(x => x.index === i) >= 0) {
        //     let foundColmn = processedColumnNames.find(x => x.index === i);
        //     convertedWord = foundColmn.name;
        //     if (config.useOnlyEnglishLetters) {

        //       //findDuplicateColumnAndGenerateNew(foundColmn, processedColumnNames, config.englishOnlyCharacters);

        //       let hasNewName = false;

        //       Array.from(convertedWord).forEach(char => {
        //         var equivalent = config.englishOnlyCharacters.find(c => c.charToConvert === char);
        //         if (equivalent) {
        //           convertedWord = convertedWord.replace(char, equivalent.englishEquivalent);
        //           hasNewName = true;
        //         }
        //       });

        //       convertedWord = cleanColumnName(convertedWord);

        //       if (hasNewName) {
        //         if (processedColumnNames.findIndex(x => x.name === convertedWord) >= 0) {
        //           let counter = 1;
        //           let newName = convertedWord + '_' + counter;

        //           while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
        //             counter++;
        //             newName = convertedWord + '_' + counter;
        //           }

        //           foundColmn.name = newName;
        //           foundColmn.isDuplicate = true;
        //         } else {
        //           processedColumnNames.find(x => x.index === i).name = convertedWord;
        //         }
        //       }

        //     }

        //     if (config.useOnlyRomanNumerals) {


        //       return convertToRoman(foundColmn.name, foundColmn.isDuplicate);

        //     }

        //     return foundColmn.name;
        //   }
        //   else {
        //     processedIndex.add(i);
        //     //processedColumnNames.push({name: h, index : i, isDuplicate : false});
        //   };

        //   convertedWord = h;

        //   if (config.useOnlyEnglishLetters) {

        //     let hasNewName = false;
        //     Array.from(convertedWord).forEach(char => {
        //       var equivalent = config.englishOnlyCharacters.find(c => c.charToConvert === char);
        //       if (equivalent) {
        //         convertedWord = convertedWord.replace(char, equivalent.englishEquivalent);
        //         hasNewName = true;
        //       }
        //     });

        //     convertedWord = cleanColumnName(convertedWord);
        //     if (hasNewName) {
        //       if (processedColumnNames.findIndex(x => x.name === convertedWord) >= 0) {
        //         let counter = 1;
        //         let newName = convertedWord + '_' + counter;

        //         while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
        //           counter++;
        //           newName = convertedWord + '_' + counter;
        //         }

        //         processedColumnNames.push({ name: newName, index: +i, isDuplicate: true });
        //       } else {
        //         processedColumnNames.push({ name: h, index: +i, isDuplicate: false });
        //         return h;
        //       }
        //     }
        //   }

        //   if (config.useOnlyRomanNumerals) {
        //     h = cleanColumnName(convertToRoman(h, false));
        //     //processedColumnNames.push({name : h, index: +i, isDuplicate : false});
        //     //return h;            
        //   } else {
        //     h = cleanColumnName(h);
        //   }


        //   if (processedColumnNames.findIndex(x => x.name === h) >= 0) {
        //     let counter = 1;
        //     let newName = h + '_' + counter;

        //     while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
        //       counter++;
        //       newName = h + '_' + counter;
        //     }

        //     processedColumnNames.push({ name: newName, index: +i, isDuplicate: true });

        //     return newName;

        //   } else {
        //     if (h.trim().length === 0) {
        //       h = `COL${i}`;
        //     }

        //     processedColumnNames.push({ name: h, index: +i, isDuplicate: false });
        //     return h;
        //   }



        // },
        complete: (result: any, parser) => {
          //e.next(result.data.slice(0,10));
          if (config.skipEmptyLines) {
            result.data = result.data.filter(row =>
              Object.values(row).some(
                cell => String(cell).trim() !== ""
              )
            );
          }
          resolve(result);
        },

        error: function (error) {
          console.log("error!!!! " + error);
        }
      })
    });
  }




}

export function convertToRoman(str: string, isDuplicate: boolean): string {
  //const regex = /(\d+)(?=_\d+$)/;// /(\d+)$/;
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

export function cleanColumnName(columnName: string): string {
  //trim column name
  columnName = columnName.trim().toLowerCase();
  columnName = columnName.replaceAll('\t', '');
  columnName = columnName.replace(/[\-\.\@\,\(\{]/gm, '_');
  columnName = columnName.replace(
    /[\)\'\"\}\$\#\!\&\%\*\/\~\^\?\<\>\?\;\:\¿\¡]/gm,
    ''
  );
  columnName = columnName.replaceAll('[', "").replaceAll("]", "");
  columnName = columnName.replaceAll(' ', '_');
  // columnName = columnName.replaceAll('___', '_');
  // columnName = columnName.replaceAll('__', '_'); //TODO
  //to remove nonbreaking space
  columnName = columnName.replaceAll('\xa0', '_');
  const findAllUnderscores = /_{2,}(?<word>.*?)/g;

  columnName = columnName.replace(findAllUnderscores, '_');
  return columnName.toUpperCase();
}

function findDuplicateColumnAndGenerateNew(
  column: { name: string; index: number; isDuplicate: boolean },
  processedColumnNames: { name: string; index: number; isDuplicate: boolean }[],
  englishOnlyCharacters: EnglishOnlyCharacters[]
) {
  let hasNewName = false;

  Array.from(column.name).forEach(char => {
    var equivalent = englishOnlyCharacters.find(c => c.charToConvert === char);
    if (equivalent) {
      column.name = column.name.replace(char, equivalent.englishEquivalent);
      hasNewName = true;
    }
  });

  if (hasNewName) {
    if (processedColumnNames.findIndex(x => x.name === column.name) >= 0) {
      let counter = 1;
      let newName = column.name + '_' + counter;

      while (processedColumnNames.findIndex(x => x.name === newName) >= 0) {
        counter++;
        newName = column.name + '_' + counter;
      }

      column.name = newName;
      column.isDuplicate = true;
    }
  }

  //return column;

}

export function formatISODate(isoDate) {
  const [fullDate, time] = isoDate.split('T');
  const [year, month, day] = fullDate.split('-')
  const [hours, minutes] = time.split(':');

  return `${month}/${day}/${year} ${hours}:${minutes}`;

}

export function getFormattedDate(serial: number, styleId: number, stylesXml: any, customFormats: any) {
  //get all cellXfs to identify if a cell has a custom format
  const xfMatches = stylesXml; //[...stylesXml.matchAll(/<cellXfs[^>]*>([\S\s]*?)<\/cellXfs>/g)];//[...stylesXml.matchAll(/<xf[^>]+>/g)];
  const xfBlock = xfMatches[0]?.[1] || "";

  const xf = [...xfBlock.matchAll(/<xf[^>]*numFmtId="(\d+)"[^>]*\/?>/g)]; //xfMatches[styleId];


  if (!xf) return XLSX.SSF.format("yyyy-mm-dd", serial);


  const numFmtIds = xf.map(m => parseInt(m[1], 10)); // xf[0].match(/numFmtId="(\d+)"/);

  var numFmtId = numFmtIds[styleId];// ? parseInt(numFmtIds[styleId]) : null;
  const isCustom = numFmtId >= 164;
  var fmtStr = '';
  if (isCustom) {
    const fmt = customFormats.map(([_, id, code]) => ({ numFmtId: parseInt(id, 10), formatCode: code }));
    fmtStr = fmt.find(x => x.numFmtId === numFmtId).formatCode;
  } else {
    fmtStr = XLSX.SSF._table[numFmtId] || null;
  }


  // FIX: decode HTML entities
  fmtStr = fmtStr
    .replace(/&quot;/g, '"')
    .replace(/&#34;/g, '"')
    .replace(/\|/g, "");

  const isSerialDate = serial > 1 && serial < 2958465;
  // const isGeneral = !fmtStr || fmtStr.toLowerCase() === 'general';

  if (isSerialDate) {
    if (fmtStr === 'm/d/yy') {
      return XLSX.SSF.format("m/d/yyyy", serial);
    }
    if (fmtStr === 'm/d/yy h:mm') {
      return XLSX.SSF.format("m/d/yyyy h:mm", serial);
    }
  }

  return XLSX.SSF.format(fmtStr || "general", serial);

}

export async function streamSheetCells(zip: JSZip, sheetPath = 'xl/worksheets/sheet1.xml') {
  const buffer = await zip.file(sheetPath)?.async('arraybuffer');
  if (!buffer) throw new Error(`File not found: ${sheetPath}`);

  const CHUNK_SIZE = 2 * 1024 * 1024;
  const decoder = new TextDecoder();
  let tail = '';
  let offset = 0;
  const emitRows = [];
  while (offset < buffer.byteLength) {
    const chunk = new Uint8Array(buffer, offset, Math.min(CHUNK_SIZE, (await buffer).byteLength - offset));
    offset += CHUNK_SIZE;
    const text = decoder.decode(chunk);
    const xml = tail + text;
    let rowRegex = /<row[^>]*>([\s\S]*?)<\/row>/g;
    let match;
    while ((match = rowRegex.exec(xml)) !== null) {
      const rowXml = match[0];
      const cellMatches = [...rowXml.matchAll(/<c[^>]*>([\s\S]*?)<\/c>/g)];
      const cells = cellMatches.map(cell => {
        const outer = cell[0];
        const inner = cell[1];
        return {
          ref: outer.match(/r="([^"]+)"/)?.[1],
          type: outer.match(/t="([^"]+)"/)?.[1],
          style: outer.match(/s="(\d+)"/)?.[1] ?? null,
          value: inner.match(/<v>(.*?)<\/v>/)?.[1] ?? null,
        }
      });
      emitRows.push(cells);
    }
    //save tail buffer in cas row block was split between chunks

    const lastOpen = xml.lastIndexOf('<row');
    tail = lastOpen > -1 ? xml.slice(lastOpen) : '';
  }

  return emitRows;
}


function decodeHtml(input: string) {
  const txt = document.createElement('textarea');
  txt.innerHTML = input;
  return txt.value;
}
