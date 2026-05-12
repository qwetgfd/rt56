import { Injectable } from '@angular/core';
import { AzureConfig } from '../models/sharepoint.models';

@Injectable({
  providedIn: 'root'
})
export class ConfigService {
  private readonly STORAGE_KEY = 'sp_config';
  private config: AzureConfig | null = null;

  getConfig(): AzureConfig | null {
    if (this.config) return this.config;

    const stored = localStorage.getItem(this.STORAGE_KEY);
    if (stored) {
      try {
        this.config = JSON.parse(stored);
        return this.config;
      } catch {
        return null;
      }
    }
    return null;
  }

  saveConfig(config: AzureConfig): void {
    this.config = config;
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(config));
  }

  clearConfig(): void {
    this.config = null;
    localStorage.removeItem(this.STORAGE_KEY);
  }

  isConfigured(): boolean {
    const cfg = this.getConfig();
    return !!cfg && !!cfg.tenantId && !!cfg.clientId && !!cfg.clientSecret && !!cfg.hostName && !!cfg.sitePath;
  }

  getHeaders(): Record<string, string> {
    const cfg = this.getConfig();
    if (!cfg) return {};

    return {
      'tenantId': cfg.tenantId,
      'clientId': cfg.clientId,
      'clientSecret': cfg.clientSecret,
      'hostName': cfg.hostName,
      'sitePath': cfg.sitePath,
      'driveName': cfg.driveName || 'Documents',
      'Content-Type': 'application/json',
      'api-version': '1.0'
    };
  }
}
