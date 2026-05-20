import { Injectable } from '@angular/core';
import { renderAsync } from 'docx-preview';
import { init as initPptxPreview } from 'pptx-preview';
import readXlsxFile from 'read-excel-file/browser';
import Papa from 'papaparse';

export type RichPreviewMode = 'pdf' | 'docx' | 'excel' | 'csv' | 'pptx';

@Injectable({ providedIn: 'root' })
export class SharePointFilePreviewService {
  private readonly objectUrls = new Set<string>();
  private pptxDestroy: (() => void) | null = null;

  revokeObjectUrls(): void {
    for (const u of this.objectUrls) {
      URL.revokeObjectURL(u);
    }
    this.objectUrls.clear();
  }

  teardown(): void {
    this.revokeObjectUrls();
    if (this.pptxDestroy) {
      try {
        this.pptxDestroy();
      } catch {
        /* ignore */
      }
      this.pptxDestroy = null;
    }
  }

  async validateBlob(blob: Blob, kind: RichPreviewMode): Promise<void> {
    if (!blob?.size) {
      throw new Error('File is empty or could not be downloaded.');
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
          throw new Error('Server returned an error instead of file data. Check API connection.');
        }
      }
    }

    if (kind === 'pdf') {
      const head = await blob.slice(0, 5).text();
      if (!head.startsWith('%PDF')) {
        throw new Error('Downloaded data is not a PDF. The API may have returned an error page.');
      }
    }

    if (kind === 'pptx') {
      const sig = new Uint8Array(await blob.slice(0, 2).arrayBuffer());
      if (sig[0] !== 0x50 || sig[1] !== 0x4b) {
        throw new Error('Downloaded data is not a valid PPTX file.');
      }
    }
  }

  /** PDF via browser built-in viewer (reliable for large files; no PDF.js worker). */
  async renderPdf(container: HTMLElement, blob: Blob): Promise<void> {
    await this.validateBlob(blob, 'pdf');
    container.innerHTML = '';
    const url = URL.createObjectURL(blob);
    this.objectUrls.add(url);
    const iframe = document.createElement('iframe');
    iframe.className = 'sp-viewer__iframe sp-viewer__iframe--pdf';
    iframe.title = 'PDF preview';
    iframe.src = url;
    container.appendChild(iframe);
  }

  async renderPptx(container: HTMLElement, blob: Blob): Promise<void> {
    await this.validateBlob(blob, 'pptx');
    container.innerHTML = '';
    const width = Math.max(container.clientWidth || 0, 720);
    const height = Math.round((width * 9) / 16);
    const previewer = initPptxPreview(container, { width, height });
    this.pptxDestroy = () => previewer.destroy();
    const buf = await blob.arrayBuffer();
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
    const rows = sheets[0]?.data ?? [];
    const table = document.createElement('table');
    table.className = 'sp-sheet-table';
    for (const row of rows) {
      const tr = document.createElement('tr');
      for (const cell of row) {
        const td = document.createElement('td');
        td.textContent = cell === null || cell === undefined ? '' : String(cell);
        tr.appendChild(td);
      }
      table.appendChild(tr);
    }
    container.appendChild(table);
  }

  renderCsvTable(container: HTMLElement, text: string): void {
    container.innerHTML = '';
    const parsed = Papa.parse<string[]>(text, { skipEmptyLines: true });
    if (!parsed.data.length) {
      container.innerHTML = '<p class="sp-preview-error">Empty or invalid CSV.</p>';
      return;
    }

    const data = parsed.data as string[][];
    const limit = Math.min(data.length, 5000);
    const table = document.createElement('table');
    table.className = 'sp-sheet-table';
    const tbody = document.createElement('tbody');
    for (let i = 0; i < limit; i++) {
      const row = data[i];
      if (!row) continue;
      const tr = document.createElement('tr');
      for (const cell of row) {
        const td = document.createElement('td');
        td.textContent = cell ?? '';
        tr.appendChild(td);
      }
      tbody.appendChild(tr);
    }
    table.appendChild(tbody);
    container.appendChild(table);

    if (data.length > limit) {
      const hint = document.createElement('p');
      hint.className = 'sp-hint';
      hint.textContent = `Showing first ${limit} rows (${data.length} total). Download for full file.`;
      container.appendChild(hint);
    }
  }
}
