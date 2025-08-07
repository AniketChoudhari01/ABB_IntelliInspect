import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';

@Component({
  standalone: true,
  selector: 'app-model-performance',
  templateUrl: './model-performance.html',
  styleUrls: ['./model-performance.css'],
  imports: [CommonModule, NgChartsModule]
})
export class ModelPerformanceComponent {
  @Output() next = new EventEmitter<void>();

  modelPerformance = {
    accuracy: 92.98,
    precision: 1.83,
    recall: 21.29,
    f1_score: 3.38
  };

  confusionMatrix = {
    true_positive: 106,
    true_negative: 80229,
    false_positive: 5673,
    false_negative: 392
  };

  trainingMetrics = {
    train_accuracy_history: [
      0.0765, 0.3028, 0.2991, 0.2923, 0.1836, 0.1899, 0.1995,
      0.2173, 0.2484, 0.2752, 0.3530, 0.5065
    ],
    train_logloss_history: [
      2.5013, 1.2482, 1.2653, 1.4173, 1.3605, 1.2058, 1.1494,
      1.1150, 1.0754, 1.0442, 1.0164, 0.9887
    ]
  };

  epochs = Array.from({ length: this.trainingMetrics.train_accuracy_history.length }, (_, i) => i + 1);

  lineChartData = {
    labels: this.epochs,
    datasets: [
      {
        label: 'Training Accuracy',
        data: this.trainingMetrics.train_accuracy_history,
        borderColor: 'green',
        yAxisID: 'y',
        tension: 0.4
      },
      {
        label: 'Training Loss',
        data: this.trainingMetrics.train_logloss_history,
        borderColor: 'red',
        yAxisID: 'y1',
        tension: 0.4
      }
    ]
  };

  lineChartOptions = {
    responsive: true,
    plugins: {
      legend: {
        position: 'top'
      },
      title: {
        display: true,
        text: 'Training Metrics'
      }
    },
    scales: {
      y: {
        position: 'left',
        title: { display: true, text: 'Accuracy' },
        min: 0,
        max: 1
      },
      y1: {
        position: 'right',
        title: { display: true, text: 'Loss' },
        grid: { drawOnChartArea: false }
      }
    }
  };

  donutChartData = {
    labels: ['True Positive', 'True Negative', 'False Positive', 'False Negative'],
    datasets: [
      {
        data: [
          this.confusionMatrix.true_positive,
          this.confusionMatrix.true_negative,
          this.confusionMatrix.false_positive,
          this.confusionMatrix.false_negative
        ],
        backgroundColor: ['#4CAF50', '#2196F3', '#FF5722', '#E91E63']
      }
    ]
  };

  donutChartOptions = {
    responsive: true,
    plugins: {
      legend: {
        position: 'right'
      },
      title: {
        display: true,
        text: 'Model Performance'
      }
    }
  };

  onNextClick() {
    this.next.emit();
  }
}