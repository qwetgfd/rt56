import { Component, OnInit, ViewChild } from '@angular/core';
import {
  ApexAxisChartSeries,
  ApexChart,
  ChartComponent,
  ApexDataLabels,
  ApexPlotOptions,
  ApexLegend,
  ApexTitleSubtitle,
} from 'ng-apexcharts';
import { DashboardService } from '../core/services/dashboard.service';
import { APIResponse } from '../core/models/apiResponse';
import { DiUtilization } from '../shared/models/dashboard';

export type ChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  dataLabels: ApexDataLabels;
  title: ApexTitleSubtitle;
  plotOptions: ApexPlotOptions;
  legend: ApexLegend;
  color: any;
};
export interface ChartSeries {
  x: string;
  y: number;
}
@Component({
    selector: 'app-di-utilization',
    templateUrl: './di-utilization.component.html',
    styleUrl: './di-utilization.component.css',
    standalone: false
})
export class DiUtilizationComponent implements OnInit {
  @ViewChild('chart', { static: false }) chart: ChartComponent;
  chartSeries!: ChartSeries[];
  diUtilization!: DiUtilization[];
  categories!: string[];
  public chartOptions: Partial<ChartOptions>;

  constructor(private dashboardService: DashboardService) {}
  ngOnInit(): void {
    this.getDIFrameworkUtilization();
  }
  getDIFrameworkUtilization() {
    this.chartSeries = [];
    this.diUtilization = [];
    this.categories = [];
    this.dashboardService.getDIFrameworkUtilization().subscribe({
      next: (response: APIResponse<DiUtilization[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.diUtilization = response.result;
              this.diUtilization.forEach((x) => {
                x.monthName = this.dashboardService.getMonthName(x.month);
                let val = { x: x.clientName, y: x.totalFileCount };
                this.chartSeries.push(val);
                //this.categories.push(x.monthName);
              });
              this.chartSeries = this.chartSeries.filter(
                (item, index, self) =>
                  index === self.findIndex((t) => t.x === item.x)
              );
              if (this.chartSeries) this.getUtilizationChartData();
            } else {
              //this.apiErrorMessage = response.responseMessage[0];
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
      if (this.chartSeries.length > 0)
        this.chartOptions = {
          series: [
            {
              name: '',
              data: this.chartSeries,
            },
          ],
          legend: {
            show: false,
          },
          chart: {
            // width: '80%',
            type: 'treemap',
            toolbar: { show: false },
            redrawOnParentResize: true,
            // parentHeightOffset: 0,
            // offsetX: 0,
          },
          dataLabels: {
            enabled: true,
          },
          color: [
            '#780096',
            '#E3008A',
            '#FF8F00',
            '#F5D200',
            '#00AF9B',
            '#8BE4BF',
            '#3047B0',
            '#0087FF',
          ],
          // title: {
          //   text: 'DI Framework Utilization Overview',
          //   align: 'center',
          // },
          plotOptions: {
            treemap: {
              distributed: true,
              enableShades: false,
            },
          },
        };
    } catch (error) {
      console.log(error);
    }
  }
  // getUtilizationChartData() {
  //   try {
  //     this.chartOptions = {
  //       series: this.chartSeries,
  //       chart: {
  //         type: 'bar',
  //         height: 350,
  //         stacked: true,
  //         toolbar: {
  //           show: false,
  //         },
  //         zoom: {
  //           enabled: true,
  //         },
  //       },
  //       responsive: [
  //         {
  //           breakpoint: 480,
  //           options: {
  //             legend: {
  //               position: 'bottom',
  //               offsetX: -10,
  //               offsetY: 0,
  //             },
  //           },
  //         },
  //       ],
  //       plotOptions: {
  //         bar: {
  //           horizontal: false,
  //         },
  //       },
  //       xaxis: {
  //         type: 'category',
  //         categories: [...new Set(this.categories)],
  //       },
  //       legend: {
  //         position: 'right',
  //         offsetY: 40,
  //       },
  //       fill: {
  //         opacity: 1,
  //       },
  //     };
  //   } catch (error) {
  //     console.log(error);
  //   }
  // }
}
