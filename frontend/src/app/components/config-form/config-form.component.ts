import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigService } from '../../services/config.service';
import { AzureConfig } from '../../models/sharepoint.models';

@Component({
  selector: 'app-config-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './config-form.component.html',
  styleUrls: ['./config-form.component.css']
})
export class ConfigFormComponent implements OnInit {
  config: AzureConfig = {
    tenantId: '',
    clientId: '',
    clientSecret: '',
    hostName: '',
    sitePath: '',
    driveName: 'Documents'
  };

  saved = false;
  showSecret = false;

  constructor(public configService: ConfigService) {}

  ngOnInit(): void {
    const existing = this.configService.getConfig();
    if (existing) {
      this.config = { ...existing };
    }
  }

  onSubmit(): void {
    this.configService.saveConfig({ ...this.config });
    this.saved = true;
    setTimeout(() => this.saved = false, 3000);
  }

  clearConfig(): void {
    this.configService.clearConfig();
    this.config = {
      tenantId: '',
      clientId: '',
      clientSecret: '',
      hostName: '',
      sitePath: '',
      driveName: 'Documents'
    };
  }
}
