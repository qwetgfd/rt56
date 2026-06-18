import { Component, EventEmitter, inject, OnInit, Output, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { firstValueFrom } from 'rxjs';
import { SP_INTEGRATION } from '../../sharepoint/core/sharepoint.messages';
import { SharepointIconComponent } from '../../sharepoint/core/sharepoint.ui';
import { SharePointItemDto } from '../../sharepoint/core/sharepoint.types';
import { formatFileSize } from '../../sharepoint/core/sharepoint.utils';
import { SharePointApiService } from '../../sharepoint/services/sharepoint-api.service';
import { SharePointUserApiService, SharePointUserAuthService } from '../../sharepoint/services/sharepoint-user.service';
import { SharepointWorkspaceComponent } from '../../sharepoint/sharepoint-workspace/sharepoint-workspace.component';

@Component({
  selector: 'app-file-first-sharepoint-attach',
  standalone: true,
  imports: [
    CommonModule,
    SharepointWorkspaceComponent,
    SharepointIconComponent,
  ],
  templateUrl: './file-first-sharepoint-attach.component.html',
  styleUrls: [
    '../../sharepoint/sharepoint-tokens.css',
    './file-first-sharepoint-attach.component.css',
    '../../sharepoint/sharepoint.styles.css',
  ],
  host: {
    '[class.ff-viewer-open]': 'viewerOpen',
  },
})
export class FileFirstSharepointAttachComponent implements OnInit {
  private readonly api = inject(SharePointApiService);
  private readonly userApi = inject(SharePointUserApiService);
  private readonly auth = inject(SharePointUserAuthService);

  readonly m = SP_INTEGRATION.fileFirst;

  @Output() fileSelected = new EventEmitter<File>();
  @Output() back = new EventEmitter<void>();

  @ViewChild(SharepointWorkspaceComponent)
  protected workspace!: SharepointWorkspaceComponent;

  protected fileLoading = false;
  protected error = '';
  private _viewerOpen = false;

  async ngOnInit(): Promise<void> {
    if (this.auth.isConfigured) await this.auth.initialize();
  }

  protected get selectedItem(): SharePointItemDto | null {
    return this.workspace?.selectedItem ?? null;
  }

  protected get hasFileSelected(): boolean {
    const item = this.selectedItem;
    return item != null && !item.isFolder && !this.fileLoading;
  }

  protected get viewerOpen(): boolean {
    return this._viewerOpen;
  }

  protected onViewerOpenChange(open: boolean): void {
    this._viewerOpen = open;
  }

  protected formatSize(bytes: number): string {
    return formatFileSize(bytes);
  }

  protected async useSelectedFile(): Promise<void> {
    const item = this.workspace?.selectedItem;
    if (!item || item.isFolder) return;

    this.workspace?.closeViewer();
    this.fileLoading = true;
    this.error = '';

    const filePath = item.path ?? item.name;

    try {
      const blob = this.workspace?.isModeUser
        ? await firstValueFrom(this.userApi.fetchFileBlob(this.workspace.selectedUserLibraryDriveId(), filePath))
        : await firstValueFrom(this.api.fetchFileBlob(this.workspace.workspaceConnection, filePath));
      this.fileSelected.emit(new File([blob], item.name));
    } catch {
      this.error = this.m.downloadFailed;
    } finally {
      this.fileLoading = false;
    }
  }

  protected goBack(): void {
    this.back.emit();
  }
}
