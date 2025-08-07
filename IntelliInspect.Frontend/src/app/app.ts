import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';  

import { UploadDatasetComponent } from './upload-dataset/upload-dataset';
import { DateRangeComponent } from './date-range/date-range';
import { TrainModelComponent } from './train-model/train-model';
import { NgChartsModule } from 'ng2-charts';
import { SimulationComponent } from './simulation/simulation.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    HttpClientModule,        
    UploadDatasetComponent,
    DateRangeComponent,
    NgChartsModule,
    TrainModelComponent,
    SimulationComponent
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class AppComponent {
  currentStep = 1;

  goToNextStep() {
    if (this.currentStep < 4) {
      this.currentStep++;
    }
  }
}
