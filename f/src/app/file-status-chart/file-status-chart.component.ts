import { Component, Input, OnInit, ViewChild } from '@angular/core';
import {
  ApexGrid,
  ApexPlotOptions,
  ChartComponent,
  ApexStroke,
} from 'ng-apexcharts';

import {
  ApexNonAxisChartSeries,
  ApexResponsive,
  ApexChart,
  ApexLegend,
  ApexDataLabels,
} from 'ng-apexcharts';

export type ChartOptions = {
  series: ApexNonAxisChartSeries;
  chart: ApexChart;
  responsive: ApexResponsive[];
  labels: any;
  legend: ApexLegend;
  dataLabels: ApexDataLabels;
  color: any;
  plotOptions: ApexPlotOptions;
  grid: ApexGrid;
  stroke: ApexStroke;
};

@Component({
    selector: 'app-file-status-chart',
    templateUrl: './file-status-chart.component.html',
    styleUrl: './file-status-chart.component.css',
    standalone: false
})
export class FileStatusChartComponent implements OnInit {
  @ViewChild('chart', { static: true }) chart: ChartComponent;
  @Input() series!: number[];
  public chartOptions: Partial<ChartOptions>;

  ngOnInit(): void {
    if (this.series) {
      this.bindChart(this.series);
    }
  }
  bindChart(data: number[]) {
    if (data) {
      this.chartOptions = {
        series: [],
        chart: {
          // height: '80%',
          // width: '95%',
          type: 'donut',
          parentHeightOffset: 0,
          redrawOnParentResize: true,
          offsetX: 0,
        },
        labels: ['Success', 'Failure'],
        color: ['#04b469', '#ff8f00'],
        plotOptions: {
          pie: {
            startAngle: -90,
            endAngle: 90,
            offsetY: 10,
            // donut: {
            //   size: '75%',
            // },
          },
        },
        grid: {
          padding: {
            bottom: -80,
          },
        },
        legend: {
          show: false,
        },
        stroke: {
          show: true,
          curve: 'smooth',
          lineCap: 'round',
          colors: undefined,
          width: 2,
          dashArray: 2,
        },
        responsive: [
          {
            breakpoint: 480,
            options: {
              chart: {
                // width: 200,
                height: '80%',
                width: '95%',
              },
              // legend: {
              //   position: 'bottom',
              //   show: false,
              // },
            },
          },
        ],
      };
    }
  }
}
