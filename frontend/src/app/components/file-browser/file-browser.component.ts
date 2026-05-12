import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { SharePointService } from '../../services/sharepoint.service';
import { ConfigService } from '../../services/config.service';
import {
  SharePointDriveItem,
  SharePointSite,
  SharePointDrive,
  BreadcrumbItem
} from '../../models/sharepoint.models';

@Component({
  selector: 'app-file-browser',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './file-browser.component.html',
  styleUrls: ['./file-browser.component.css']
})
export class FileBrowserComponent implements OnInit, OnDestroy {
  site: SharePointSite | null = null;
  drives: SharePointDrive[] = [];
  items: SharePointDriveItem[] = [];
  breadcrumbs: BreadcrumbItem[] = [];
  currentPath = '';
  loading = false;
  error: string | null = null;
  selectedFile: SharePointDriveItem | null = null;
  filePreviewUrl: string | null = null;
  safePreviewUrl: SafeResourceUrl | null = null;
  previewText: string | null = null;
  streamStatus: string | null = null;
  streamError: string | null = null;
  streaming = false;
  streamedBlob: Blob | null = null;
  activePreviewMimeType = '';
  showNewFolderDialog = false;
  newFolderName = '';

  constructor(
    private spService: SharePointService,
    public configService: ConfigService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    if (this.configService.isConfigured()) {
      this.loadSite();
    }
  }

  ngOnDestroy(): void {
    this.clearStreamPreview();
  }

  loadSite(): void {
    this.loading = true;
    this.error = null;

    this.spService.getSite().subscribe({
      next: (response) => {
        this.site = response.data;
        this.loadDrives();
      },
      error: (err) => {
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  loadDrives(): void {
    this.spService.listDrives().subscribe({
      next: (response) => {
        this.drives = response.data;
        this.breadcrumbs = [{ name: this.site?.name || 'Root', path: '' }];
        this.loadChildren('');
      },
      error: (err) => {
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  loadChildren(folderPath: string): void {
    this.loading = true;
    this.error = null;
    this.closePreview();

    this.spService.listChildren(folderPath).subscribe({
      next: (response) => {
        this.items = response.data.sort((a, b) => {
          if (a.isFolder && !b.isFolder) return -1;
          if (!a.isFolder && b.isFolder) return 1;
          return a.name.localeCompare(b.name);
        });
        this.currentPath = folderPath;
        this.updateBreadcrumbs(folderPath);
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  navigateToFolder(item: SharePointDriveItem): void {
    if (item.isFolder && item.path) {
      this.loadChildren(item.path);
    }
  }

  navigateToBreadcrumb(index: number): void {
    const crumb = this.breadcrumbs[index];
    this.loadChildren(crumb.path);
    this.breadcrumbs = this.breadcrumbs.slice(0, index + 1);
  }

  selectFile(item: SharePointDriveItem): void {
    if (item.isFolder) return;
    this.selectedFile = item;
    this.clearStreamPreview();
    this.streamError = null;

    if (this.canUseDirectBrowserStream(item)) {
      this.filePreviewUrl = this.spService.getInlineStreamUrl(item.path || item.name);
      this.streamStatus = 'Opening direct browser stream from backend...';
      if (this.canPreviewAsPdf(item)) {
        this.safePreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.filePreviewUrl);
      }
      return;
    }

    this.streaming = true;
    this.streamStatus = 'Testing StreamFile via XHR...';
    this.spService.streamFile(item.path || item.name).subscribe({
      next: async (blob) => {
        this.streaming = false;
        this.streamedBlob = blob;
        this.activePreviewMimeType = blob.type || item.mimeType || '';
        this.streamStatus = `Stream successful (${this.formatSize(blob.size)} returned).`;

        if (this.canPreviewAsText(item)) {
          this.previewText = await blob.text();
          return;
        }

        this.filePreviewUrl = URL.createObjectURL(blob);
        if (this.canPreviewAsPdf(item)) {
          this.safePreviewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.filePreviewUrl);
        }
      },
      error: (err) => {
        this.streaming = false;
        this.streamError = `Stream failed: ${err.message}`;
      }
    });
  }

  downloadFile(item: SharePointDriveItem): void {
    this.spService.downloadFile(item.path || item.name).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = item.name;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.error = `Download failed: ${err.message}`;
      }
    });
  }

  openStreamInNewTab(): void {
    if (!this.filePreviewUrl) return;
    window.open(this.filePreviewUrl, '_blank');
  }

  downloadStreamedFile(): void {
    if (!this.selectedFile || !this.streamedBlob) return;

    const url = URL.createObjectURL(this.streamedBlob);
    const a = document.createElement('a');
    a.href = url;
    a.download = this.selectedFile.name;
    a.click();
    URL.revokeObjectURL(url);
  }

  closePreview(): void {
    this.selectedFile = null;
    this.clearStreamPreview();
  }

  createFolder(): void {
    if (!this.newFolderName.trim()) return;

    this.spService.createFolder(this.newFolderName.trim(), this.currentPath || undefined).subscribe({
      next: () => {
        this.showNewFolderDialog = false;
        this.newFolderName = '';
        this.loadChildren(this.currentPath);
      },
      error: (err) => {
        this.error = `Failed to create folder: ${err.message}`;
      }
    });
  }

  goBack(): void {
    if (this.breadcrumbs.length > 1) {
      const parentIndex = this.breadcrumbs.length - 2;
      this.navigateToBreadcrumb(parentIndex);
    }
  }

  getFileIcon(item: SharePointDriveItem): string {
    if (item.isFolder) return '📁';

    const ext = item.name.split('.').pop()?.toLowerCase() || '';
    const mimeType = item.mimeType || '';

    if (mimeType.startsWith('image/')) return '🖼️';
    if (mimeType.startsWith('video/')) return '🎬';
    if (mimeType.startsWith('audio/')) return '🎵';
    if (mimeType === 'application/pdf') return '📕';
    if (['doc', 'docx'].includes(ext)) return '📘';
    if (['xls', 'xlsx'].includes(ext)) return '📗';
    if (['ppt', 'pptx'].includes(ext)) return '📙';
    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) return '📦';
    if (['js', 'ts', 'py', 'cs', 'html', 'css', 'json', 'xml'].includes(ext)) return '💻';
    if (['txt', 'md', 'log'].includes(ext)) return '📝';

    return '📄';
  }

  formatSize(bytes: number): string {
    if (bytes === 0) return '-';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let i = 0;
    let size = bytes;
    while (size >= 1024 && i < units.length - 1) {
      size /= 1024;
      i++;
    }
    return `${size.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
  }

  formatDate(dateStr?: string): string {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  markDirectStreamLoaded(): void {
    this.streamStatus = 'Direct browser stream opened successfully.';
    this.streamError = null;
  }

  markDirectStreamFailed(): void {
    this.streamStatus = null;
    this.streamError = 'Direct browser stream failed. Check backend logs and Network tab for StreamFileInline.';
  }

  canPreviewAsImage(item: SharePointDriveItem): boolean {
    return this.getEffectiveMimeType(item).startsWith('image/')
      || ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'].includes(this.getExtension(item.name));
  }

  canPreviewAsPdf(item: SharePointDriveItem): boolean {
    return this.getEffectiveMimeType(item) === 'application/pdf'
      || this.getExtension(item.name) === 'pdf';
  }

  canPreviewAsText(item: SharePointDriveItem): boolean {
    const mimeType = this.getEffectiveMimeType(item);
    const extension = this.getExtension(item.name);
    return mimeType.startsWith('text/')
      || ['application/json', 'application/xml', 'application/javascript'].includes(mimeType)
      || ['txt', 'md', 'csv', 'json', 'xml', 'html', 'css', 'js', 'ts', 'log'].includes(extension);
  }

  canPreviewAsVideo(item: SharePointDriveItem): boolean {
    return this.getEffectiveMimeType(item).startsWith('video/')
      || ['mp4', 'm4v', 'webm', 'ogg', 'mov'].includes(this.getExtension(item.name));
  }

  canPreviewAsAudio(item: SharePointDriveItem): boolean {
    return this.getEffectiveMimeType(item).startsWith('audio/')
      || ['mp3', 'wav', 'm4a', 'aac', 'oga', 'ogg'].includes(this.getExtension(item.name));
  }

  hasVisualPreview(item: SharePointDriveItem): boolean {
    return this.canPreviewAsImage(item)
      || this.canPreviewAsPdf(item)
      || this.canPreviewAsText(item)
      || this.canPreviewAsVideo(item)
      || this.canPreviewAsAudio(item);
  }

  private getEffectiveMimeType(item: SharePointDriveItem): string {
    if (this.activePreviewMimeType && this.activePreviewMimeType !== 'application/octet-stream') {
      return this.activePreviewMimeType;
    }

    return item.mimeType || this.activePreviewMimeType || '';
  }

  private getExtension(fileName: string): string {
    return fileName.split('.').pop()?.toLowerCase() || '';
  }

  private canUseDirectBrowserStream(item: SharePointDriveItem): boolean {
    return this.canPreviewAsImage(item)
      || this.canPreviewAsPdf(item)
      || this.canPreviewAsVideo(item)
      || this.canPreviewAsAudio(item);
  }

  private clearStreamPreview(): void {
    if (this.filePreviewUrl && this.filePreviewUrl.startsWith('blob:')) {
      URL.revokeObjectURL(this.filePreviewUrl);
    }

    this.filePreviewUrl = null;
    this.safePreviewUrl = null;
    this.previewText = null;
    this.streamStatus = null;
    this.streamError = null;
    this.streaming = false;
    this.streamedBlob = null;
    this.activePreviewMimeType = '';
  }

  private updateBreadcrumbs(folderPath: string): void {
    if (!folderPath) {
      this.breadcrumbs = [{ name: this.site?.name || 'Root', path: '' }];
      return;
    }

    const parts = folderPath.split('/');
    this.breadcrumbs = [{ name: this.site?.name || 'Root', path: '' }];

    let accumulatedPath = '';
    for (const part of parts) {
      accumulatedPath = accumulatedPath ? `${accumulatedPath}/${part}` : part;
      this.breadcrumbs.push({ name: part, path: accumulatedPath });
    }
  }
}
