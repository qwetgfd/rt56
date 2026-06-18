import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { NavigateService } from '../core/services/navigate.service';
import { DataSourceType } from '../shared/enum';


@Component({
  selector: 'app-datasources',
  templateUrl: './datasources.component.html',
  styleUrl: './datasources.component.css',
  standalone: false
})
export class DatasourcesComponent {

  uploadData: boolean = true;
  configureProcess: boolean = false;

  constructor(private router: Router,
    private navigateService: NavigateService
  ) { }

  onDataSource(event: number) {
    switch (event) {
      case 1:
        this.navigateService.dataSource = DataSourceType.Default;
        break;
      case 2:
        this.navigateService.dataSource = DataSourceType.DataBricks;
        break;
      case 3:
        this.navigateService.dataSource = DataSourceType.LandingLayer;
        break;
    }
    this.router.navigate(['/file-upload']);
  }

  onConfiguration(event: number) {
    switch (event) {
      case 1:
        this.navigateService.configurationProcess = DataSourceType.Default;
        break;
      case 2:
        this.navigateService.configurationProcess = DataSourceType.DataBricks;
        break;
      case 3:
        this.navigateService.configurationProcess = DataSourceType.LandingLayer;
        break;
    }
    this.router.navigate(['/add-process']);
  }
}
