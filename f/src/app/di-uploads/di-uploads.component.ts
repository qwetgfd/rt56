import { Component, OnInit, ViewChild } from '@angular/core';
import {
  ApexNonAxisChartSeries,
  ApexPlotOptions,
  ApexChart,
  ChartComponent,
  ApexLegend,
  ApexResponsive,
  ApexTheme,
  ApexTitleSubtitle,
  ApexFill,
  ApexStroke,
  ApexYAxis,
  ApexDataLabels,
} from 'ng-apexcharts';
import { APIResponse } from '../core/models/apiResponse';
import { DiUploads } from '../shared/models/dashboard';
import { DashboardService } from '../core/services/dashboard.service';
export type ChartOptions = {
  series: ApexNonAxisChartSeries;
  chart: ApexChart;
  responsive: ApexResponsive[];
  labels: string[];
  plotOptions: ApexPlotOptions;
  legend: ApexLegend;
  theme: ApexTheme;
  title: ApexTitleSubtitle;
  fill: ApexFill;
  yaxis: ApexYAxis;
  stroke: ApexStroke;
  dataLabels: ApexDataLabels;
  color: any;
};
@Component({
    selector: 'app-di-uploads',
    templateUrl: './di-uploads.component.html',
    styleUrl: './di-uploads.component.css',
    standalone: false
})
export class DiUploadsComponent implements OnInit {
  @ViewChild('chart', { static: false }) chart: ChartComponent;
  categories: string[];
  public chartOptions: Partial<ChartOptions>;
  diUploads: DiUploads[] = [];
  totalUploads: number[] = [];
  totalUploadPercentage: number[] = [];
  total: number = 0;
  tot: string = '100';
  constructor(private dashboardService: DashboardService) {}
  ngOnInit(): void {
    this.getDiUploads();
  }
  getSum(x: number[]) {}
  getDiUploads() {
    this.diUploads = [];
    this.totalUploads = [];
    this.categories = [];
    this.totalUploadPercentage = [];
    this.total = 0;
    this.dashboardService.getDiUploads().subscribe({
      next: (response: APIResponse<DiUploads[] | null>) => {
        if (response) {
          if (response.responseCode === 200)
            if (response.result && response.responseMessage[0] == 'Success') {
              this.diUploads = response.result;
              this.diUploads.forEach((x) => {
                this.total = this.total + x.totalUploadedFiles;
                this.totalUploads.push(x.totalUploadedFiles);
                this.categories.push(x.processType);
              });
              //this.total = this.dashboardService.sumArray(this.totalUploads);
              this.diUploads.forEach((y) => {
                let percentage = (y.totalUploadedFiles / this.total) * 100;
                this.totalUploadPercentage.push(Number(percentage.toFixed(2)));
              });
              this.getUploads();
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

  getUploads() {
    this.chartOptions = {
      series: this.totalUploadPercentage,
      chart: {
        width: '80%',
        type: 'polarArea',
        redrawOnParentResize: true,
        parentHeightOffset: 0,
        offsetX: 0,
        offsetY: 0,
      },
      labels: this.categories,
      fill: {
        opacity: 1,
      },
      color: ['#3047b0', '#0087ff', '#e3008a'],
      stroke: {
        width: 1,
        colors: undefined,
      },
      yaxis: {
        show: false,
      },
      legend: {
        // position: 'bottom',
        //show: false,
        position: 'bottom',
        horizontalAlign: 'center',
      },
      dataLabels: {
        enabled: true,
        dropShadow: {
          enabled: true,
          left: 2,
          top: 2,
          opacity: 0.5,
        },
      },
      responsive: [
        {
          breakpoint: 480,
          options: {
            chart: {
              // width: 200,
            },
            // legend: {
            //   position: 'bottom',
            // },
          },
        },
      ],
      plotOptions: {
        polarArea: {
          rings: {
            strokeWidth: 1,
          },
        },
      },
      theme: {
        monochrome: {
          //    enabled: true,
          shadeTo: 'light',
          shadeIntensity: 0.6,
        },
      },
    };
  }

  // getUploads() {
  //   let xx = String(this.dashboardService.sumArray(this.totalUploads));
  //   this.chartOptions = {
  //     series: this.totalUploadPercentage,
  //     chart: {
  //       height: 350,
  //       type: 'radialBar',
  //     },
  //     plotOptions: {
  //       radialBar: {
  //         dataLabels: {
  //           name: {
  //             fontSize: '22px',
  //           },
  //           value: {
  //             fontSize: '16px',
  //           },
  //           total: {
  //             show: true,
  //             label: 'Total',
  //             formatter: function (w) {
  //               return xx;
  //             },
  //           },
  //         },
  //       },
  //     },
  //     legend: {
  //       position: 'right',
  //       offsetY: 40,
  //     },
  //     labels: this.categories,
  //   };
  // }
}
