import { HttpClient } from '@angular/common/http';
import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';

@Component({
  standalone: true,
  selector: 'app-date-range',
  templateUrl: './date-range.html',
  styleUrls: ['./date-range.css'],
  imports: [
    CommonModule,
    FormsModule,
    NgChartsModule
  ]
})
export class DateRangeComponent {
  @Output() next = new EventEmitter<void>();

  trainingPeriod = { start: '', end: '' };
  testingPeriod = { start: '', end: '' };
  simulationPeriod = { start: '', end: '' };

  validationMessage = '';
  isValidationSuccess = false;
  isValid = false;
  showValidationSummary = false;

  validationSummary = {
    training: { days: 0, start: '', end: '' },
    testing: { days: 0, start: '', end: '' },
    simulation: { days: 0, start: '', end: '' }
  };

  barChartType: any = 'bar';
  chartData: any = null;

  barChartData = {
    labels: [] as string[],
    datasets: [] as any[]
  };

  barChartOptions = {
    responsive: true,
    plugins: {
      legend: {
        position: 'top'
      },
      title: {
        display: true,
        text: 'Monthly Distribution'
      }
    },
    scales: {
      x: {
        title: {
          display: true,
          text: 'Month (Year)'
        },
        stacked: true
      },
      y: {
        title: {
          display: true,
          text: 'Volume'
        },
        stacked: true,
        beginAtZero: true
      }
    }
  };

  // ✅ Added flags
  isValidated = false;
  hasValidationError = false;

  constructor(private http: HttpClient) { }

  validateRanges() {
    if (
      !this.trainingPeriod.start || !this.trainingPeriod.end ||
      !this.testingPeriod.start || !this.testingPeriod.end ||
      !this.simulationPeriod.start || !this.simulationPeriod.end
    ) {
      this.isValid = false;
      this.isValidationSuccess = false;
      this.validationMessage = 'Please select all start and end dates.';
      this.showValidationSummary = false;
      this.isValidated = false;
      this.hasValidationError = true;
      return;
    }

    const payload = {
      TrainStart: this.trainingPeriod.start + 'T00:00:00',
      TrainEnd: this.trainingPeriod.end + 'T23:59:59',
      TestStart: this.testingPeriod.start + 'T00:00:00',
      TestEnd: this.testingPeriod.end + 'T23:59:59',
      SimStart: this.simulationPeriod.start + 'T00:00:00',
      SimEnd: this.simulationPeriod.end + 'T23:59:59'
    };

    // const sessionId = 10000;

    this.http.post<any>('http://localhost:5230/api/session/ranges', payload).subscribe({
      next: (response) => {
        this.isValid = response.status === 'Valid' || response.Status === 'Valid';
        this.isValidationSuccess = this.isValid;
        this.validationMessage = this.isValid ? 'Date ranges are valid!' : (response.message || 'Date ranges are invalid.');

        this.validationSummary = {
          training: {
            days: this.getDayCount(this.trainingPeriod.start, this.trainingPeriod.end),
            start: this.trainingPeriod.start,
            end: this.trainingPeriod.end
          },
          testing: {
            days: this.getDayCount(this.testingPeriod.start, this.testingPeriod.end),
            start: this.testingPeriod.start,
            end: this.testingPeriod.end
          },
          simulation: {
            days: this.getDayCount(this.simulationPeriod.start, this.simulationPeriod.end),
            start: this.simulationPeriod.start,
            end: this.simulationPeriod.end
          }
        };

        this.chartData = {
          trainMonthly: response.trainMonthly || response.TrainMonthly,
          testMonthly: response.testMonthly || response.TestMonthly,
          simMonthly: response.simMonthly || response.SimMonthly
        };

        this.updateChart(this.chartData);
        this.showValidationSummary = true;

        
        this.isValidated = true;
        this.hasValidationError = false;
      },
      error: (err) => {
        this.isValid = false;
        this.isValidationSuccess = false;
        this.validationMessage = err?.error?.message || 'Validation failed. Please try again.';
        this.showValidationSummary = false;

        
        this.isValidated = false;
        this.hasValidationError = true;
      }
    });
}

getDayCount(start: string, end: string): number {
  const s = new Date(start);
  const e = new Date(end);
  return Math.floor((e.getTime() - s.getTime()) / (1000 * 60 * 60 * 24)) + 1;
}

getMonths(chartData: any): string[] {
  if (!chartData) return [];
  const months = new Set<string>([
    ...Object.keys(chartData.trainMonthly || {}),
    ...Object.keys(chartData.testMonthly || {}),
    ...Object.keys(chartData.simMonthly || {})
  ]);
  return Array.from(months).sort();
}

updateChart(chartData: any) {
  const months = this.getMonths(chartData);
  this.barChartData = {
    labels: months,
    datasets: [
      {
        label: 'Training',
        data: months.map(m => chartData.trainMonthly[m] || 0),
        backgroundColor: '#4CAF50'
      },
      {
        label: 'Testing',
        data: months.map(m => chartData.testMonthly[m] || 0),
        backgroundColor: '#FF9800'
      },
      {
        label: 'Simulation',
        data: months.map(m => chartData.simMonthly[m] || 0),
        backgroundColor: '#2196F3'
      }
    ]
  };
}

onNextClick() {
  this.next.emit();
}
}