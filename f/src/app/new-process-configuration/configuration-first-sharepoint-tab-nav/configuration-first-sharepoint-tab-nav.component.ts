import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MODULE_BRANDING, SP_INTEGRATION } from '../../sharepoint/core/sharepoint.messages';
import {
  SHAREPOINT_PROCESS_TYPE_ID,
  SHAREPOINT_SETTINGS_TAB_ID,
  SHAREPOINT_SETTINGS_TAB_PANE_ID,
} from '../../sharepoint/integration/configuration-first.sharepoint';

@Component({
  selector: 'app-configuration-first-sharepoint-tab-nav',
  templateUrl: './configuration-first-sharepoint-tab-nav.component.html',
  styleUrl: './configuration-first-sharepoint-tab-nav.component.css',
  standalone: false,
})
export class ConfigurationFirstSharepointTabNavComponent {
  readonly sharePointProcessTypeId = SHAREPOINT_PROCESS_TYPE_ID;
  readonly sharePointSettingsTabId = SHAREPOINT_SETTINGS_TAB_ID;
  readonly sharePointSettingsTabPaneId = SHAREPOINT_SETTINGS_TAB_PANE_ID;
  readonly navLabel = MODULE_BRANDING.browseNavTitle;
  readonly navTitle = SP_INTEGRATION.configurationFirst.navTitle;

  @Input() processType: string | number = '';
  @Input() activeTab = '';
  @Output() tabChanged = new EventEmitter<boolean>();

  get isNavActive(): boolean {
    return this.activeTab === this.sharePointSettingsTabId;
  }

  onTabClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.tabChanged.emit(false);
  }
}
