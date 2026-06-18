import { Component, Input, OnInit } from '@angular/core';
import { DBView, EIB, BusinessProcessFileUrlMapping, EIBConfigurationDetails, EIBConfigurationDetailsWithFileUrl } from '../../core/models/EIB/dbView';
import { EibService } from '../../core/services/eib/eib.service';
import { APIResponse } from '../../core/models/apiResponse';
import { lastValueFrom } from 'rxjs';
import { HttpClient, HttpResponse } from '@angular/common/http';

@Component({
    selector: 'app-eib-download',
    templateUrl: './eib-download.component.html',
    styleUrl: './eib-download.component.css',
    standalone: false
})
export class EibDownloadComponent implements OnInit {

  @Input() EIB: EIB;
  eibConfiguration: EIBConfigurationDetailsWithFileUrl;

  /**
   *
   */
  constructor(
    private eibService: EibService,
    private http: HttpClient,
  ) {


  }

  ngOnInit(): void {
    if (this.EIB) {
      this.eibService.GetEIBByEIBId(this.EIB.eibId).subscribe({
        next: (response: APIResponse<EIBConfigurationDetails>) => {
          if (response?.responseCode === 200 && response.result) {
            this.eibConfiguration = response.result as unknown as EIBConfigurationDetailsWithFileUrl;


            const map = new Map<string, { processName: string; dbView: DBView[] }>();
            for (const item of this.eibConfiguration.businessProcessDBViewMapping) {
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
                    processName: item.businessProcessName,
                    dbView: [dbViewEntry]
                  });
              }
            }



          }
          else {
            console.warn(`Unexpected response code: ${response?.responseCode}`);
            // Optionally show a message to the user or handle fallback logic here
          }
        }
      });
    }
  }


  private sanitizeSasUrl(rawUrl: string): string {
    // Handle accidental doubleashes after the container path,
    // and ensure spaces are encoded (Azure works with %20).
    // Also, ensure the query string uses '&' not HTML-escaped '&amp;'.
    // If your data source already provides a valid URL, this is a safe no-op.
    try {
      // First unescape any accidental HTML entities like &amp;
      const fixed = rawUrl.replace(/&amp;/g, '&');

      // Use URL API to normalize
      const u = new URL(fixed);

      // Remove any duplicate slashes in pathname (keep leading '/')
      u.pathname = u.pathname.replace(/\/{2,}/g, '/');

      // Re-encode spaces if present in pathname
      u.pathname = u.pathname.split('/').map(seg => encodeURIComponent(decodeURIComponent(seg))).join('/');

      return u.toString();
    } catch {
      // Fallback: best-effort replacements
      return rawUrl.replace(/&amp;/g, '&').replace(/ /g, '%20').replace(/\/{2,}/g, '/');
    }
  }


  private addContentDisposition(url: string, downloadName: string) {
    // Fix HTML-escaped ampersands
    const fixed = url.replace(/&amp;/g, '&');

    const u = new URL(fixed);
    // Ensure the path segments are properly encoded
    u.pathname = u.pathname
      .split('/')
      .map(seg => encodeURIComponent(decodeURIComponent(seg)))
      .join('/');

    // Build rscd value: 'attachment; filename="..."'
    const rscdValue = `attachment; filename="${downloadName}"`;
    u.searchParams.set('rscd', rscdValue); // URL will auto-encode the value

    return u.toString();
  }



  downloadFile(eib: BusinessProcessFileUrlMapping) {
    // Create a hidden download link

    // const downloadLink = document.createElement('a');    
    // downloadLink.href = eib.fileUrl;   
    // document.body.appendChild(downloadLink);
    // downloadLink.click();

    const sasUrl = eib.fileUrl; // your SAS URL
    this.eibService.downloadFile(sasUrl).subscribe(response => {
      const pdfUrl = URL.createObjectURL(response);
      const link = document.createElement('a');
      link.href = pdfUrl;
      const fileName = this.getFileNameFromUrl(sasUrl);
      link.download = fileName; // Set the desired file name
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(pdfUrl);
      // const blob = new Blob([response.body!], {
      //   type: response.headers.get('Content-Type') || 'application/octet-stream'
      // });

      // const fileName = this.getFileName(response);

      // const link = document.createElement('a');
      // link.href = window.URL.createObjectURL(blob);
      // link.download = fileName;
      // link.click();
    });
  }

  getFileNameFromUrl(url: string): string {
    const path = url.split('?')[0]; // Remove the query string
    return path.substring(path.lastIndexOf('/') + 1); // Extract the filename
  }

  downloadAllViaAnchorStaggered() {

    const items = this.eibConfiguration?.businessProcessFileUrlMapping ?? [];
    if (!items.length) {
      console.warn('No files available to download.');
      return;
    }


    const DELAY_MS = 150; // tweak as needed

    items.forEach((eib, idx) => {
      setTimeout(() => {
        this.downloadFile(eib);
      }, idx * DELAY_MS);
    });


  }







}
