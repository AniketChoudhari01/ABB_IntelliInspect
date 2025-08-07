import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { NgChartsModule } from 'ng2-charts';
import { ChartOptions, ChartData } from 'chart.js';

@Component({
  selector: 'app-train-model',
  standalone: true,
  imports: [CommonModule, NgChartsModule],
  templateUrl: './train-model.html',
  styleUrls: ['./train-model.css']
})
export class TrainModelComponent {
  @Output() next = new EventEmitter<void>();

  modelPerformance: ModelPerformance | null = null;
  trainingMetrics: any = null;
  confusionMatrix: any = null;

  constructor(private http: HttpClient) { }

  donutChartType: 'doughnut' = 'doughnut';

  // Line chart: training metrics
  lineChartData: ChartData<'line'> = {
    labels: [],
    datasets: [
      {
        label: 'Accuracy',
        data: [],
        borderColor: '#4caf50',
        backgroundColor: 'rgba(76, 175, 80, 0.2)',
        fill: true,
        tension: 0.3,
      },
      {
        label: 'Log Loss',
        data: [],
        borderColor: '#f44336',
        backgroundColor: 'rgba(244, 67, 54, 0.2)',
        fill: true,
        tension: 0.3,
      }
    ]
  };

  // Donut chart: confusion matrix
  donutChartLabels = ['True Positive', 'True Negative', 'False Positive', 'False Negative'];
  donutChartData: ChartData<'doughnut'> = {
    labels: this.donutChartLabels,
    datasets: [{
      data: [],
      backgroundColor: ['#4caf50', '#2196f3', '#ff9800', '#f44336']
    }]
  };

  donutChartOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    cutout: '70%',
    plugins: {
      legend: { position: 'bottom' as const }
    }
  };

  isTrained: boolean = false;
  isTraining: boolean = false;

  trainModel() {
    console.log("Training model...");
    this.isTraining = true;
    this.http.get<any>('http://localhost:5230/api/session/train-model').subscribe({
      next: (res) => {
        console.log('✅ Training completed:', res);

        if (!res.metrics) {
          alert("Training did not return valid metrics. Please try again.");
          return;
        }

        try {
          const metrics = JSON.parse(res.metrics);

          // ✅ Extract model performance
          const perf = metrics.model_performance;
          this.modelPerformance = perf as ModelPerformance;

          // ✅ Extract confusion matrix
          const cm = metrics.confusion_matrix;
          this.confusionMatrix = [
            cm?.true_positive || 0,
            cm?.true_negative || 0,
            cm?.false_positive || 0,
            cm?.false_negative || 0
          ];

          // ✅ Extract training accuracy and loss for plotting
          const trainingMetrics = metrics.training_metrics;
          const accuracyHistory = trainingMetrics?.train_accuracy_history || [];
          const logLossHistory = trainingMetrics?.train_logloss_history || [];

          // ✅ Corrected: Add return type for map function
          this.lineChartData.labels = accuracyHistory.map((_: number, idx: number): string => `Epoch ${idx + 1}`);
          this.lineChartData.datasets[0].data = accuracyHistory;
          this.lineChartData.datasets[1].data = logLossHistory;

          // ✅ Update donut chart data (TP, TN, FP, FN)
          this.donutChartData.datasets[0].data = this.confusionMatrix;

          this.isTrained = true;
          this.isTraining = false;
          // this.nextStep();
        } catch (parseErr) {
          console.error("❌ Failed to parse metrics.json:", parseErr);
          alert("Invalid metrics format. Try training again.");
        }
      },
      error: (err) => {
        console.error('❌ Training failed:', err);
        alert("Training failed. Check backend logs.");
      }
    });
  }

  nextStep() {
    this.next.emit();

  }
}

// ✅ Type for model performance metrics
interface ModelPerformance {
  precision: number;
  recall: number;
  f1_score: number;
  accuracy: number;
}
