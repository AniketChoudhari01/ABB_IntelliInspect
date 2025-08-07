# ABB IntelliInspect – Predictive Maintenance Platform

---

## Project Overview

**IntelliInspect** is an end-to-end predictive analytics solution for industrial systems. The platform empowers engineers and analysts to:
- Upload and process time-series equipment data
- Define training, testing, and simulation periods
- Train machine learning models on historical data
- Stream live predictions for real-time monitoring

This solution is fully containerized and orchestrated using Docker Compose, ensuring seamless setup and reproducibility across environments.

---

## Team Members

- [Aniket Choudhari](https://github.com/AniketChoudhari01)
- [Aditya Ranganathan](https://github.com/Adi2302)
- [Mikhila Mohan](https://github.com/Mikhilavinnakota)
- [Chenchu Niranjan](https://github.com/niranjan:chenchu4622niran)

---

## Table of Contents

1. [Setup & Deployment (Docker)](#setup--deployment-docker)
2. [Usage Guide](#usage-guide)
3. [System Architecture & Design Docs](#system-architecture--design-docs)
4. [Demo Video](#demo-video)
5. [Repository Structure](#repository-structure)

---

## Setup & Deployment (Docker)

### Prerequisites

- Docker
- Docker Compose

### Step-by-step Instructions

1. **Clone the Repository**

   ```bash
   git clone https://github.com/your-org/abb-intelliinspect.git
   cd abb-intelliinspect

### System Architecture & Design Docs
[`Design_Document.pdf`](https://github.com/user-attachments/files/21661557/ABB_DOCUMENT.pdf) contains:

- **System Architecture Diagram**  
- **Data Flow Diagram**  
- **API Contracts and Payload Schemas**

### Repository Structure
/

├── frontend-angular/           # Angular 16 UI

├── backend-dotnet/             # .NET Core Web API

├── ml-service-python/          # FastAPI ML microservice

├── docker-compose.yaml         # Orchestrates services

├── Design_Document.pdf         # Architecture, flows, API specs

├── demo.mp4                    # Video walkthrough

└── README.md                   # This file
