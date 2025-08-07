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

  isLoading: boolean = false;

  selectedFile: File | null = null;
  metadata: any = null;

  constructor(private http: HttpClient) { }

onFileSelected(event: any) {
  this.isLoading = true;

  const file = event.target.files[0];
  if (file) {
    this.selectedFile = file;
    this.upload(); // 
  } else {
    this.isLoading = false;
  }
}


async processFile(file: File) {
  // Your actual file processing logic
  // This is just an example:
  return new Promise<void>((resolve) => {
    setTimeout(() => {
      // Simulate file processing
      this.metadata = { name: file.name, size: file.size };
      resolve();
    }, 2000); // Simulate 2 second delay
  });
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
    const formData = new FormData();
    formData.append('file', this.selectedFile);

    this.http.post<any>(`http://localhost:5230/api/dataset/upload`, formData).subscribe({
      next: (res) => {
        this.metadata = res.metadata;
      },
      error: (err) => {
        console.error('Upload failed', err);
      }
    });
  }
  resetFileInput() {
    this.metadata = null;
    this.selectedFile = null;
    // If you have a file input reference, reset it:
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

  downloadCSV(event: Event) {
    event.preventDefault();
    if (!this.selectedFile) return;

    const url = URL.createObjectURL(this.selectedFile);
    const a = document.createElement('a');
    a.href = url;
    a.download = this.selectedFile.name;
    a.click();
    URL.revokeObjectURL(url);
  }
  getMonths(data: any): string[] {
    const months = new Set<string>();

    if (data.trainMonthly) {
      Object.keys(data.trainMonthly).forEach(month => months.add(month));
    }

    if (data.testMonthly) {
      Object.keys(data.testMonthly).forEach(month => months.add(month));
    }

    if (data.simMonthly) {
      Object.keys(data.simMonthly).forEach(month => months.add(month));
    }

    return Array.from(months).sort(); // optional: sort chronologically
  }

}
