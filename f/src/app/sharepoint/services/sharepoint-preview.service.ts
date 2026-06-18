/// <reference types="vite/client" />
import { Injectable } from '@angular/core';
import { renderAsync } from 'docx-preview';
import { init as initPptxPreview } from 'pptx-preview';
import readXlsxFile from 'read-excel-file/browser';
import Papa from 'papaparse';
import type { PDFDocumentProxy } from 'pdfjs-dist';
import { SP_PREVIEW } from '../core/sharepoint.messages';
import type { RichPreviewMode } from '../core/sharepoint.types';

type PdfJsModule = typeof import('pdfjs-dist');

export type PreviewPageReadyCallback = () => void;

const MAX_PDF_PREVIEW_PAGES = 50;
const MAX_SPREADSHEET_ROWS = 5000;

@Injectable({ providedIn: 'root' })
export class SharePointFilePreviewService {
  private static pdfWorkerConfigured = false;
  private static pdfJsModule: PdfJsModule | null = null;

  private readonly objectUrls = new Set<string>();
  private pptxDestroy: (() => void) | null = null;
  private renderGeneration = 0;

  revokeObjectUrls(): void {
    for (const u of this.objectUrls) {
      URL.revokeObjectURL(u);
    }
    this.objectUrls.clear();
  }

  teardown(): void {
    this.renderGeneration++;
    this.revokeObjectUrls();
    if (this.pptxDestroy) {
      try {
        this.pptxDestroy();
      } catch {}
      this.pptxDestroy = null;
    }
  }

  async validateBlob(blob: Blob, kind: RichPreviewMode): Promise<void> {
    if (!blob?.size) {
      throw new Error(SP_PREVIEW.emptyFile);
    }

    const type = (blob.type || '').toLowerCase();
    if (type.includes('json') || (type.includes('text') && !type.includes('csv'))) {
      const snippet = await blob.slice(0, 300).text();
      if (snippet.trimStart().startsWith('{')) {
        try {
          const j = JSON.parse(snippet) as { message?: string };
          throw new Error(j.message ?? snippet.slice(0, 120));
        } catch (e) {
          if (e instanceof Error && e.message !== snippet.slice(0, 120)) throw e;
          throw new Error(SP_PREVIEW.serverErrorResponse);
        }
      }
    }

    if (kind === 'pdf') {
      const head = await blob.slice(0, 5).text();
      if (!head.startsWith('%PDF')) {
        throw new Error(SP_PREVIEW.invalidPdf);
      }
    }

    if (kind === 'pptx') {
      const sig = new Uint8Array(await blob.slice(0, 2).arrayBuffer());
      if (sig[0] !== 0x50 || sig[1] !== 0x4b) {
        throw new Error(SP_PREVIEW.invalidPptx);
      }
    }
  }

  async renderPdf(
    container: HTMLElement,
    blob: Blob,
    onFirstPageReady?: PreviewPageReadyCallback,
  ): Promise<void> {
    const generation = this.renderGeneration;
    await this.validateBlob(blob, 'pdf');
    const { getDocument } = await this.loadPdfJs();
    container.innerHTML = '';
    const viewer = document.createElement('div');
    viewer.className = 'sp-pdf-viewer';
    container.appendChild(viewer);

    const data = await blob.arrayBuffer();
    if (generation !== this.renderGeneration) return;

    const pdf = await getDocument({ data, useSystemFonts: true }).promise;
    if (generation !== this.renderGeneration) return;

    const pageCount = pdf.numPages;
    const pagesToRender = Math.min(pageCount, MAX_PDF_PREVIEW_PAGES);
    const width = Math.max(container.clientWidth - 48, 320);

    for (let pageNum = 1; pageNum <= pagesToRender; pageNum++) {
      if (generation !== this.renderGeneration) return;
      await this.renderPdfPage(pdf, pageNum, width, viewer);
      if (pageNum === 1) onFirstPageReady?.();
    }

    if (generation !== this.renderGeneration) return;

    if (pageCount > pagesToRender) {
      const note = document.createElement('p');
      note.className = 'sp-pdf-more';
      note.textContent = SP_PREVIEW.pdfPageLimit(pagesToRender, pageCount);
      viewer.appendChild(note);
    }
  }

  async renderPptx(container: HTMLElement, blob: Blob): Promise<void> {
    const generation = this.renderGeneration;
    await this.validateBlob(blob, 'pptx');
    container.innerHTML = '';
    const width = Math.max(container.clientWidth || 0, 720);
    const height = Math.round((width * 9) / 16);
    const previewer = initPptxPreview(container, { width, height });
    this.pptxDestroy = () => previewer.destroy();
    const buf = await blob.arrayBuffer();
    if (generation !== this.renderGeneration) return;
    await previewer.preview(buf);
  }

  async renderDocx(container: HTMLElement, blob: Blob): Promise<void> {
    container.innerHTML = '';
    await renderAsync(blob, container, undefined, {
      className: 'sp-docx-docx-preview',
      inWrapper: true,
    });
  }

  async renderExcelTable(container: HTMLElement, blob: Blob): Promise<void> {
    container.innerHTML = '';
    const sheets = await readXlsxFile(blob);
    const rows = (sheets[0]?.data ?? []) as unknown[][];
    container.appendChild(this.buildSpreadsheet(rows, SP_PREVIEW.workbookLabel));
  }

  renderCsvTable(container: HTMLElement, text: string): void {
    container.innerHTML = '';
    const parsed = Papa.parse<string[]>(text, { skipEmptyLines: true });
    if (!parsed.data.length) {
      const message = document.createElement('p');
      message.className = 'sp-preview-error';
      message.textContent = SP_PREVIEW.spreadsheetEmpty;
      container.appendChild(message);
      return;
    }
    container.appendChild(this.buildSpreadsheet(parsed.data as string[][], SP_PREVIEW.csvLabel));
  }

  private async loadPdfJs(): Promise<PdfJsModule> {
    if (!SharePointFilePreviewService.pdfJsModule) {
      SharePointFilePreviewService.pdfJsModule = await import('pdfjs-dist');
      if (!SharePointFilePreviewService.pdfWorkerConfigured) {
        SharePointFilePreviewService.pdfJsModule.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';
        SharePointFilePreviewService.pdfWorkerConfigured = true;
      }
    }
    return SharePointFilePreviewService.pdfJsModule;
  }

  private async renderPdfPage(
    pdf: PDFDocumentProxy,
    pageNum: number,
    containerWidth: number,
    viewer: HTMLElement,
  ): Promise<void> {
    const page = await pdf.getPage(pageNum);
    const base = page.getViewport({ scale: 1 });
    const scale = containerWidth / base.width;
    const viewport = page.getViewport({ scale });

    const canvas = document.createElement('canvas');
    canvas.className = 'sp-pdf-page';
    canvas.setAttribute('data-page', String(pageNum));
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error(SP_PREVIEW.pdfOpenFailed);

    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);

    await page.render({ canvas, viewport }).promise;
    viewer.appendChild(canvas);
  }

  private buildSpreadsheet(rows: unknown[][], label: string): HTMLElement {
    const wrap = document.createElement('div');
    wrap.className = 'sp-spreadsheet';

    const ribbon = document.createElement('div');
    ribbon.className = 'sp-spreadsheet__ribbon';
    ribbon.textContent = label;
    wrap.appendChild(ribbon);

    const grid = document.createElement('div');
    grid.className = 'sp-spreadsheet__grid';

    const limited = rows.slice(0, MAX_SPREADSHEET_ROWS);
    const colCount = limited.reduce((max, row) => Math.max(max, Array.isArray(row) ? row.length : 0), 0);
    const hasHeader = limited.length > 0;
    const headerRow = hasHeader ? limited[0] : [];
    const bodyRows = limited.length > 1 ? limited.slice(1) : [];

    const table = document.createElement('table');
    table.className = 'sp-spreadsheet__table';

    const thead = document.createElement('thead');

    const colRow = document.createElement('tr');
    colRow.className = 'sp-spreadsheet__col-labels';
    const corner = document.createElement('th');
    corner.className = 'sp-spreadsheet__corner';
    corner.setAttribute('scope', 'col');
    colRow.appendChild(corner);
    for (let c = 0; c < colCount; c++) {
      const th = document.createElement('th');
      th.className = 'sp-spreadsheet__col-label';
      th.textContent = columnLetter(c);
      colRow.appendChild(th);
    }
    thead.appendChild(colRow);

    if (hasHeader) {
      const headerTr = document.createElement('tr');
      headerTr.className = 'sp-spreadsheet__header-row';
      const rowLabel = document.createElement('th');
      rowLabel.className = 'sp-spreadsheet__row-label';
      rowLabel.textContent = '1';
      headerTr.appendChild(rowLabel);
      for (let c = 0; c < colCount; c++) {
        const th = document.createElement('th');
        th.textContent = formatSpreadsheetCell(headerRow[c]);
        headerTr.appendChild(th);
      }
      thead.appendChild(headerTr);
    }

    table.appendChild(thead);

    const tbody = document.createElement('tbody');
    const dataRows = hasHeader ? bodyRows : limited;
    dataRows.forEach((row, index) => {
      const tr = document.createElement('tr');
      const rowNum = document.createElement('td');
      rowNum.className = 'sp-spreadsheet__row-label';
      rowNum.textContent = String(hasHeader ? index + 2 : index + 1);
      tr.appendChild(rowNum);
      for (let c = 0; c < colCount; c++) {
        const td = document.createElement('td');
        const cells = Array.isArray(row) ? row : [];
        td.textContent = formatSpreadsheetCell(cells[c]);
        tr.appendChild(td);
      }
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);

    grid.appendChild(table);
    wrap.appendChild(grid);

    if (rows.length > MAX_SPREADSHEET_ROWS) {
      const foot = document.createElement('p');
      foot.className = 'sp-spreadsheet__foot';
      foot.textContent = SP_PREVIEW.spreadsheetRowLimit(MAX_SPREADSHEET_ROWS, rows.length);
      wrap.appendChild(foot);
    }

    return wrap;
  }
}

function formatSpreadsheetCell(value: unknown): string {
  if (value === null || value === undefined) return '';
  if (value instanceof Date) return value.toLocaleString();
  return String(value);
}

function columnLetter(index: number): string {
  let n = index;
  let label = '';
  do {
    label = String.fromCharCode(65 + (n % 26)) + label;
    n = Math.floor(n / 26) - 1;
  } while (n >= 0);
  return label;
}
