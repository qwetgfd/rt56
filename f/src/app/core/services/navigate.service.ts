import { Injectable } from '@angular/core';
import { CampaignUserAccess } from '../models/userDetails';

@Injectable({
  providedIn: 'root'
})
export class NavigateService {

  _dataSourceType: number = 1;
  _configurationService: number = 1;
  

  set dataSource(value: number) {
    sessionStorage.setItem('dataSource', value.toString());
    //this._dataSourceType = value;
  }

  get dataSource(): number {
    return Number(sessionStorage.getItem('dataSource'));
    //return this._dataSourceType;
  }

  set configurationProcess(value: number) {
    sessionStorage.setItem('configurationProcess', value.toString());
    //this._configurationService = value;
  }

  get configurationProcess(): number {
    return Number(sessionStorage.getItem('configurationProcess'));
    //return this._configurationService;
  }

  constructor() { }
}
