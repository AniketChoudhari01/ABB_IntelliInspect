import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';

@Component({
  selector: 'app-upload-dataset',
  standalone: true,
  imports: [CommonModule, HttpClientModule],
  templateUrl: './upload-dataset.html',
  styleUrls: ['./upload-dataset.css']
})
export class UploadDatasetComponent {
  @Output() next = new EventEmitter<void>();

  selectedFile: File | null = null;
  metadata: any = null;
  isLoading = false; // Simple loading state

  constructor(private http: HttpClient) {}

  onFileSelected(event: any) {
    this.selectedFile = event.target.files[0];
    this.upload();
  }

  allowDrop(event: DragEvent) {
    event.preventDefault();
  }

  onDropFile(event: DragEvent) {
    event.preventDefault();
    if (event.dataTransfer?.files.length) {
      this.selectedFile = event.dataTransfer.files[0];
      this.upload();
    }
  }

  upload() {
    if (!this.selectedFile) return;
    
    this.isLoading = true; // Show loader
    this.metadata = null; // Reset previous data

    const formData = new FormData();
    formData.append('file', this.selectedFile);

    //const tempId = 10000; // Replace with actual logic if needed
    
    this.http.post<any>(`http://localhost:5230/api/dataset/upload`, formData)
      .subscribe({
        next: (res) => {
          this.metadata = res.metadata;
          this.isLoading = false; // Hide loader
          const card = document.querySelector('.upload-card');
          if (card) card.classList.add('success');
        },
        error: (err) => {
          console.error('Upload failed', err);
          this.isLoading = false; // Hide loader
          // Optionally show error message
        }
      });
  }

  resetFileInput() {
    this.metadata = null;
    this.selectedFile = null;
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    if (fileInput) fileInput.value = '';
  }

  get passRate(): string {
    return this.metadata ? `${(this.metadata.passRate * 100).toFixed(1)}%` : '';
  }

  get dateRange(): string {
    if (!this.metadata) return '';
    return `${this.metadata.startTimeStamp} to ${this.metadata.endTimeStamp}`;
  }

  onNextClick() {
    this.next.emit();
  }

  getMonths(data: any): string[] {
    const months = new Set<string>();
    if (data?.trainMonthly) Object.keys(data.trainMonthly).forEach(m => months.add(m));
    if (data?.testMonthly) Object.keys(data.testMonthly).forEach(m => months.add(m));
    if (data?.simMonthly) Object.keys(data.simMonthly).forEach(m => months.add(m));
    return Array.from(months).sort();
  }
}