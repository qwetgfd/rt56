import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfigFormComponent } from './components/config-form/config-form.component';
import { FileBrowserComponent } from './components/file-browser/file-browser.component';
import { ConfigService } from './services/config.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ConfigFormComponent, FileBrowserComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  activeTab: 'browser' | 'config' = 'browser';

  constructor(public configService: ConfigService) {}

  switchTab(tab: 'browser' | 'config'): void {
    this.activeTab = tab;
  }
}
