/// <reference lib="webworker" />

import * as XLSX from 'xlsx';
import { formatISODate, getFormattedDate, streamSheetCells } from '../services/di-parser.service';
import JSZip from 'jszip';
import { DOMParser } from 'xmldom';
import xpath from 'xpath';

type AOA = any[][];

//TODO process each sheet for multiple sheets
console.log('worker initialized');
addEventListener('message', async ({ data }) => {
  console.log('worker received the filed');

  let wk: { sheetName: string, workBook: AOA, maxRowCount: number }[] = [
    // {key: 'first', value: 'one'},
    // {key: 'second', value: 'two'}
  ];

  let datax: AOA = [[1, 2], [3, 4]]

  try {

    if (data.fileSize < 100 * 1024 * 1024) {
      const bstr: string = data.file;
      const workBook: XLSX.WorkBook = XLSX.read(bstr, { type: 'array', cellText: false, cellNF: true });

      //count number of sheets
      var numberOfWorkSheet = workBook.SheetNames;

      for (var i = 0; i < workBook.SheetNames.length; i++) {
        const wsname: string = workBook.SheetNames[i];
        const ws: XLSX.WorkSheet = workBook.Sheets[wsname];

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

        datax = <AOA>(XLSX.utils.sheet_to_json(ws, { header: 1, raw: false, blankrows: !data.blankrows, defval: "" })); //, blankrows: false

        wk.push({ sheetName: wsname, workBook: datax, maxRowCount: datax.length });

      }
    } else {
      const chunkSize = 100 * 1024 * 1024;
      const zip = await JSZip.loadAsync(data.file as ArrayBuffer);

      //to get cell values
      const sharedXml = await zip.file('xl/sharedStrings.xml')?.async('text');
      
      //to get actual cell data
      const sheetXml = await zip.file('xl/worksheets/sheet1.xml')?.async('arraybuffer');
      const stylesXml = await zip.file('xl/styles.xml')?.async('string');
      const cellXfs = [...stylesXml.matchAll(/<cellXfs[^>]*>([\S\s]*?)<\/cellXfs>/g)];
      const customFormats = [...stylesXml.matchAll(/numFmt[^>]*numFmtId=["'](\d+)["'][^>]*formatCode=["']([^"']+)["']/g)];
      //to get the sheetnames
      const xmlString = await zip.file('xl/workbook.xml')?.async('string');
      const doc = new DOMParser().parseFromString(xmlString!, 'application/xml');     
      //register the namespace used in workbook.xml
      const select = xpath.useNamespaces({
        main: 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
      });
      //use the prefix in your xpath query      
      const sheetNodes = select('//main:sheet', doc) as Element[];
      const sheetNames  = sheetNodes.map(sheet => sheet.getAttribute('name'));
      

      console.log('sheetnames: ', sheetNames)

      const textChunk = new TextDecoder().decode(sheetXml.slice(0, 10 * 1024 * 1024)); //1mb

      const dimensions = textChunk.match(/<dimension[^>]*ref="([^"]+)"/);
      //const tempRows = await streamSheetCells(zip);
      // tempRows.forEach((rowcells, i) => {
      //   console.log(`row ${i + 1}:`, rowcells);
      // })


      if (!dimensions) {
        console.warn('no dimensions');
        return null;
      }

      const decoder = new TextDecoder();

      const textChunk2 = decoder.decode(sheetXml.slice(0, chunkSize), { stream: true });
      //const textChunk2 = decoder.decode(sheetXml, { stream: true });
      // const uint8 = new Uint8Array(sheetXml);
      // const textChunk2 = decoder.decode(uint8);

      const ref = dimensions[1]; // eg a1:q10
      const [, endRef] = ref.split(':');
      const colLetters = endRef.replace(/\d+$/, '');
      const maxRowCount = parseInt(endRef.match(/\d+/)?.[0] ?? "1", 10);
      const colIndex = XLSX.utils.decode_col(colLetters); //q = 16 0 base
      const columnCount = colIndex + 1;
      if (!sharedXml) {
        console.warn('sharedStrings.xml not found');
        return null;
      }
      // const sheet1: XLSX.WorkSheet = {};

      // const siMatches = [...sharedXml.matchAll(/<si>([\s\S]*?)<\/si>/g)];
      // const sharedStrings = siMatches.map(si => {
      //   const tMatches = [...si[1].matchAll(/<t[^>]*>(.*?)<\/t>/g)];
      //   return tMatches.map(t => t[1]).join('');
      // });
      // let maxRow = 0;
      // let maxCol = 0;
      // for(let i = 0; i < tempRows.length; i++){
      //   const row = tempRows[i];
      //   for(let j = 0; j < row.length; j++){
      //     const cell = row[j];
      //     const ref= cell.ref;

      //     let value: string = '';
      //     let cellType: 's' | 'n' | 'd' = 's';

      //     if (cell.type === 's') {
      //       value = sharedStrings[+cell.value!] ?? '';
      //       cellType = 's';
      //     } else {
      //       const num = parseFloat(cell.value);
      //       if (!isNaN(num) && cell.style !== null) {
      //         value = getFormattedDate(num, cell.style, stylesXml);
      //         cellType = 'd';
      //       } else {
      //         value = cell.value;
      //         cellType = 'n';
      //       }
      //     }
      //     sheet1[cell.ref!] = {
      //       t: cell.type || 'n',
      //       v: value
      //     }
      //   }
      // }

      
      const siMatches = [...sharedXml.matchAll(/<si>([\s\S]*?)<\/si>/g)];
      const sharedStrings = siMatches.map(si => {
        const tMatches = [...si[1].matchAll(/<t[^>]*>(.*?)<\/t>/g)];
        return tMatches.map(t => t[1]).join('');
      });


      //process cells from sheetXML
      const sheet1: XLSX.WorkSheet = {};
      const cellmMatches = [...textChunk2.matchAll(/<c[^>]*r="([^"]+)"[^>]*>(.*?)<\/c>/gs)];
      let maxRow = 0;
      let maxCol = 0;

      for (const match of cellmMatches) {
        const cellRef = match[1]; //G2
        const inner = match[2]; //raw cell inner XML
        const tMatch = inner.match(/t="(\w+)"/);
        const sMatch = match[0].match(/s="(\d+)"/);// inner.match(/s="(\d+)"/);
        const vmatch = inner.match(/<v>(.*?)<\/v>/);
        if (!vmatch) continue;
        const type = match[0].match(/t="(\w+)"/)?.[1];//  tMatch?.[1];
        const styleId = sMatch ? parseInt(sMatch[1], 10) : null;
        const rawValue = vmatch[1];

        let value: string = '';
        let cellType: 's' | 'n' | 'd' = 's';

        if (type === 's') {
          value = sharedStrings[+rawValue] ?? '';
          cellType = 's';
        } else {
          const num = parseFloat(rawValue);
          if (!isNaN(num) && styleId !== null) {
            value = getFormattedDate(num, styleId, cellXfs,customFormats);
            cellType = 'd';
          } else {
            value = rawValue;
            cellType = 'n';
          }
        }

        sheet1[cellRef] = { v: value, t: cellType };

        const { r, c } = XLSX.utils.decode_cell(cellRef);

        maxRow = Math.max(maxRow, r + 1);
        maxCol = Math.max(maxCol, c + 1);
      }

      sheet1['!ref'] = `A1:${XLSX.utils.encode_cell({ r: maxRow - 1, c: maxCol -1 })}`;

      datax = <AOA>(XLSX.utils.sheet_to_json(sheet1, { header: 1, raw: false, blankrows: !data.blankrows, defval: "" })); //, blankrows: false

      // var html = XLSX.utils.sheet_to_html(ws);
      // var csvtemp = XLSX.utils.sheet_to_csv(ws);
      // this.wk.push({ key: wsname, value: temp });
      wk.push({ sheetName: sheetNames[0], workBook: datax, maxRowCount: maxRowCount }); //.slice(0,21)

    }
    postMessage({ success: true, wk });
  } catch (error) {
    postMessage({ success: false, error: (error as Error).message });
  }

});
