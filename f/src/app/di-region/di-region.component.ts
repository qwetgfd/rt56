import { Component, OnInit, ViewChild } from '@angular/core';
import {
  ApexAxisChartSeries,
  ApexChart,
  ChartComponent,
  ApexDataLabels,
  ApexXAxis,
  ApexPlotOptions,
} from 'ng-apexcharts';
import { DashboardService } from '../core/services/dashboard.service';
import { APIResponse } from '../shared/models/apiResponse';
import { UtilizationRegion } from '../shared/models/dashboard';

export type ChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  dataLabels: ApexDataLabels;
  plotOptions: ApexPlotOptions;
  xaxis: ApexXAxis;
  colors: any;
};

@Component({
    selector: 'app-di-region',
    templateUrl: './di-region.component.html',
    styleUrl: './di-region.component.css',
    standalone: false
})
export class DiRegionComponent implements OnInit {
  @ViewChild('chart', { static: false }) chart: ChartComponent;
  public chartOptions: Partial<ChartOptions>;
  series: number[];
  categories: string[];
  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.getUtilizationByRegions();
  }
  getUtilizationByRegions() {
    this.series = [];
    this.categories = [];
    this.dashboardService.getUtilizationByRegions().subscribe({
      next: (response: APIResponse<UtilizationRegion[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              response.result.forEach((x) => {
                this.categories.push(x.regionName);
                this.series.push(x.totalFileCount);
              });
              if (this.series) this.getUtilizationChartData();
            } else {
            }
        } else {
          console.warn('not got any file upload status data!');
        }
      },
      error: (error) => {
        console.log(error);
      },
    });
  }
  getUtilizationChartData() {
    try {
      if (this.series)
        this.chartOptions = {
          series: [
            {
              name: 'utilization',
              data: this.series,
            },
          ],
          chart: {
            type: 'bar',
            height: 375,
            toolbar: {
              show: false,
            },
          },
          plotOptions: {
            bar: {
              horizontal: true,
            },
          },

          dataLabels: {
            enabled: false,
          },
          xaxis: {
            categories: this.categories,
          },
          colors: ['#00AF9B'],
        };
    } catch (error) {
      console.log(error);
    }
  }
}
