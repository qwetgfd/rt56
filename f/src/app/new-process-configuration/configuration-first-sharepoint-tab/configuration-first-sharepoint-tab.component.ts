import { Component, EventEmitter, inject, Input, OnChanges, Output, SimpleChanges, ViewChild } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { SP_INTEGRATION } from '../../sharepoint/core/sharepoint.messages';
import { ProcessConfigSharePointSelection } from '../../sharepoint/core/sharepoint.types';
import {
  isSharePointProcessType,
  SHAREPOINT_SETTINGS_TAB_ID,
  SHAREPOINT_SETTINGS_TAB_PANE_ID,
} from '../../sharepoint/integration/configuration-first.sharepoint';
import { SharepointWorkspaceComponent } from '../../sharepoint/sharepoint-workspace/sharepoint-workspace.component';

@Component({
  selector: 'app-configuration-first-sharepoint-tab',
  templateUrl: './configuration-first-sharepoint-tab.component.html',
  styleUrls: [
    '../../sharepoint/sharepoint-tokens.css',
    './configuration-first-sharepoint-tab.component.css',
  ],
  standalone: false,
  host: {
    class: 'tab-pane fade show',
    '[class.active]': 'isActive',
    '[attr.id]': 'sharePointSettingsTabPaneId',
    role: 'tabpanel',
    '[attr.aria-labelledby]': 'sharePointSettingsTabId',
    tabindex: '0',
  },
})
export class ConfigurationFirstSharepointTabComponent implements OnChanges {
  private readonly toastr = inject(ToastrService);

  readonly sharePointSettingsTabId = SHAREPOINT_SETTINGS_TAB_ID;
  readonly sharePointSettingsTabPaneId = SHAREPOINT_SETTINGS_TAB_PANE_ID;
  readonly m = SP_INTEGRATION.configurationFirst;
  readonly listDetail = SP_INTEGRATION.listDetail;

  @ViewChild('sharepointWorkspace') sharepointWorkspace?: SharepointWorkspaceComponent;

  @Input() activeTab = '';
  @Input() processType: string | number = '';
  @Input() initialApplicationId: string | null = null;
  @Input() initialApplicationSiteId: string | null = null;
  @Input() initialLibraryName: string | null = null;
  @Input() initialFolderPath: string | null = null;

  @Output() selectionChange = new EventEmitter<ProcessConfigSharePointSelection | null>();

  workspaceMounted = false;
  private cachedSelection: ProcessConfigSharePointSelection | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    if (this.shouldRender) {
      this.workspaceMounted = true;
    }

    const initialChanged = !!(
      changes['initialApplicationId']
      || changes['initialApplicationSiteId']
      || changes['initialLibraryName']
      || changes['initialFolderPath']
    );
    if (initialChanged && this.workspaceMounted) {
      void this.sharepointWorkspace?.reloadProcessConfigInitials();
    }
  }

  get isActive(): boolean {
    return this.activeTab === this.sharePointSettingsTabId;
  }

  get shouldRender(): boolean {
    return isSharePointProcessType(this.processType);
  }

  selection(): ProcessConfigSharePointSelection | null {
    return this.sharepointWorkspace?.processConfigurationSelection() ?? this.cachedSelection;
  }

  get selectionSummary(): {
    application: string;
    site: string;
    library: string;
    folder: string;
    isComplete: boolean;
  } {
    const current = this.selection();
    return {
      application: current?.sharePointApplicationId ? this.sharepointWorkspace?.selectedFilePickApplication()?.displayName ?? this.m.selectionNotSet : this.m.selectionNotSet,
      site: this.sharepointWorkspace?.selectedFilePickSite()?.siteName ?? this.m.selectionNotSet,
      library: current?.sharePointLibraryName || this.m.selectionNotSet,
      folder: current?.sharePointFolderPath?.trim()
        ? current.sharePointFolderPath
        : (current?.sharePointLibraryName ? this.m.selectionLibraryRoot : this.m.selectionNotSet),
      isComplete: this.isSelectionComplete(current),
    };
  }

  onWorkspaceSelectionChange(): void {
    const current = this.sharepointWorkspace?.processConfigurationSelection() ?? null;
    this.cachedSelection = current;
    this.selectionChange.emit(current);
  }

  validateSelection(showToast = true): boolean {
    const current = this.sharepointWorkspace?.processConfigurationSelection() ?? this.cachedSelection;
    this.cachedSelection = current;
    const valid = this.isSelectionComplete(current);
    if (!valid && showToast) {
      this.toastr.error(this.m.validateSelection);
    }
    return valid;
  }

  private isSelectionComplete(selection: ProcessConfigSharePointSelection | null | undefined): boolean {
    return !!selection?.sharePointApplicationId && !!selection?.sharePointLibraryName;
  }
}
