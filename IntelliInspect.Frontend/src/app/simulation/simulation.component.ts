import { Component, OnDestroy, OnInit, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartDataset, ChartOptions } from 'chart.js';
import { NgChartsModule } from 'ng2-charts';

interface SimulationData {
  id: number;
  timestamp: string;
  prediction: string;
  confidence: number;
  actual: string;
}

@Component({
  selector: 'app-simulation',
  standalone: true,
  imports: [CommonModule, NgChartsModule],
  templateUrl: './simulation.component.html',
  styleUrls: ['./simulation.component.css']
})
export class SimulationComponent implements OnInit, OnDestroy {
  constructor(private zone: NgZone) { }
  lineChartOptions: ChartOptions<'line'> = {
    responsive: true,
    plugins: {
      legend: {
        display: true,
        position: 'top'
      },
      title: {
        display: true,
        text: 'Real-Time Quality Score'
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        title: {
          display: true,
          text: 'Confidence (%)'
        }
      },
      x: {
        title: {
          display: true,
          text: 'Time'
        }
      }
    }
  };

  lineChartLabels: string[] = [];
  lineChartData: ChartDataset<'line'>[] = [
    {
      data: [],
      label: 'Quality Score',
      fill: true,
      tension: 0.4,
      borderColor: '#2196F3',
      backgroundColor: 'rgba(33,150,243,0.2)',
      pointRadius: 3,
      pointBackgroundColor: '#2196F3'
    }
  ];


  updateChart(data: SimulationData): void {
    const confidence = Number(data.confidence);
    if (isNaN(confidence)) return;

    const timeLabel = new Date(data.timestamp).toLocaleTimeString();

    if (this.lineChartLabels.length >= 20) {
      this.lineChartLabels.shift();
      (this.lineChartData[0].data as number[]).shift();
    }

    this.lineChartLabels.push(timeLabel);
    (this.lineChartData[0].data as number[]).push(confidence);
    this.lineChartLabels = [...this.lineChartLabels];
    this.lineChartData = [...this.lineChartData];
  }

  trackByFn(index: number, item: any): number {
    return item.id;
  }

  getRowClass(row: any): string {
    switch (row.predicted_label) {
      case 1:
        return 'row-success';
      case 0:
        return 'row-warning';
      default:
        return '';
    }
  }

  formatTime(timestamp: string): string {
    return new Date(timestamp).toLocaleTimeString();
  }

  simulationActive = false;
  simulationCompleted = false;
  simulationError = false;
  errorMessage = '';
  eventSource: EventSource | null = null;

  predictionData: SimulationData[] = [];

  stats = {
    total: 0,
    pass: 0,
    fail: 0,
    avgConfidence: 0,
    accuracy: 0,
    correct: 0
  };

  ngOnInit(): void { }

  startSimulation(): void {
    if (this.simulationActive) return;

    this.resetSimulation();
    this.simulationActive = true;
    console.log("Starting simulation...");

    this.eventSource = new EventSource(`http://localhost:5230/api/session/simulate`);

    this.eventSource.onmessage = (event) => {
      try {
        const raw = event.data.trim();
        const jsonString = raw.startsWith("data:") ? raw.substring(5).trim() : raw;
        const data: SimulationData & { type?: string, error?: string } = JSON.parse(jsonString);

        this.updateChart(data);
        this.zone.run(() => {
          console.log('✅ Parsed data:', data);

          switch (data.type) {
            case 'complete':
              this.simulationCompleted = true;
              this.simulationActive = false;
              this.eventSource?.close();
              break;

            case 'error':
              this.simulationError = true;
              this.errorMessage = data.error || 'Unknown error occurred';
              this.simulationActive = false;
              this.eventSource?.close();
              break;

            default:
              if (data.id && data.prediction) {
                this.predictionData.unshift(data);
                this.updateStats(data);

                if (this.predictionData.length > 100) {
                  this.predictionData = this.predictionData.slice(0, 100);
                }
              }
          }
        });
      } catch (e) {
        console.error('Error parsing simulation data:', e);
      }
    };

    this.eventSource.onerror = (error) => {
      console.error('EventSource error:', error);
      this.simulationError = true;
      this.errorMessage = 'Connection error - check if backend is running';
      this.eventSource?.close();
      this.simulationActive = false;
    };

    this.eventSource.onopen = () => {
      console.log('Connected to simulation stream');
    };
  }

  stopSimulation(): void {
    if (this.eventSource) {
      this.eventSource.close();
      this.simulationActive = false;
      console.log('Simulation stopped by user');
    }
  }

  clearResults(): void {
    this.resetSimulation();
  }

  ngOnDestroy(): void {
    this.eventSource?.close();
  }

  private resetSimulation(): void {
    this.simulationCompleted = false;
    this.simulationError = false;
    this.errorMessage = '';
    this.predictionData = [];
    this.lineChartLabels = [];
    (this.lineChartData[0].data as number[]) = [];
    this.stats = {
      total: 0,
      pass: 0,
      fail: 0,
      avgConfidence: 0,
      accuracy: 0,
      correct: 0
    };
  }

  private updateStats(data: SimulationData): void {
    this.stats.total++;

    if (data.prediction === 'Pass') {
      this.stats.pass++;
    } else if (data.prediction === 'Fail') {
      this.stats.fail++;
    }

    if (data.confidence) {
      this.stats.avgConfidence =
        (this.stats.avgConfidence * (this.stats.total - 1) + data.confidence) / this.stats.total;
    }

    if (data.actual && data.actual !== 'Unknown' && data.prediction !== 'Error') {
      if (data.prediction === data.actual) {
        this.stats.correct++;
      }
      this.stats.accuracy = (this.stats.correct / this.stats.total) * 100;
    }
  }
}
