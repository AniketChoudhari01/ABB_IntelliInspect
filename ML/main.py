import os
import json
from fastapi import FastAPI, HTTPException,Request
from fastapi.responses import JSONResponse
from train_model import train_model
import traceback
from fastapi.responses import StreamingResponse
import time
import pandas as pd
import json
import joblib

app = FastAPI(title="ML Training API", version="1.0.0")

# ✅ Define storage path globally
STORAGE_PATH = os.path.abspath('../IntelliInspect.Backend/Storage')

@app.get("/")
def root():
    """Health check endpoint"""
    return {"message": "ML Training API is running", "status": "healthy"}

@app.get("/train")
def train_model_endpoint():
    """
    Train the ML model using data from storage
    
    Expected files in storage:
    - parsed.csv: The training/test data
    - range_selection.json: Date ranges for train/test split
    
    Returns:
    - Training results and metrics
    - Saves results to metrics.json
    """
    try:
        print("=== Starting Model Training ===")
        
        # Define file paths
        session_folder = os.path.join(STORAGE_PATH)
        csv_path = os.path.join(session_folder, 'parsed.csv')
        range_path = os.path.join(session_folder, 'range_selection.json')
        
        print(f"Storage path: {session_folder}")
        print(f"CSV path: {csv_path}")
        print(f"Range selection path: {range_path}")
        
        # Check if required files exist
        if not os.path.exists(csv_path):
            raise HTTPException(
                status_code=404, 
                detail=f"CSV file not found at: {csv_path}"
            )
        
        if not os.path.exists(range_path):
            raise HTTPException(
                status_code=404, 
                detail=f"Range selection file not found at: {range_path}"
            )
        
        # Load range selection
        try:
            with open(range_path, "r") as f:
                range_selection = json.load(f)
            print(f"Range selection loaded: {range_selection}")
        except Exception as e:
            raise HTTPException(
                status_code=400, 
                detail=f"Error reading range selection file: {str(e)}"
            )
        
        # Validate range selection format
        required_keys = ['TrainStart', 'TrainEnd', 'TestStart', 'TestEnd']
        missing_keys = [key for key in required_keys if key not in range_selection]
        if missing_keys:
            raise HTTPException(
                status_code=400,
                detail=f"Missing keys in range selection: {missing_keys}"
            )
        
        # Start training
        print("Calling train_model function...")
        results = train_model(csv_path, range_selection, STORAGE_PATH)
        
        print("=== Training Completed Successfully ===")
        return JSONResponse(
            content=results,
            status_code=200
        )
        
    except HTTPException:
        # Re-raise HTTP exceptions as-is
        raise
        
    except Exception as e:
        # Log the full error for debugging
        error_trace = traceback.format_exc()
        print(f"❌ Unexpected error during training:")
        print(error_trace)
        
        # Return error response
        error_response = {
            "status": "error",
            "message": f"Training failed: {str(e)}",
            "error_type": type(e).__name__
        }
        
        # Try to save error to metrics file
        try:
            error_path = os.path.join(STORAGE_PATH, "metrics.json")
            with open(error_path, "w") as f:
                json.dump(error_response, f, indent=4)
        except:
            pass
        
        raise HTTPException(status_code=500, detail=error_response)

@app.get("/status")
def get_status():
    """Get current status and check if files exist"""
    session_folder = os.path.join(STORAGE_PATH)
    csv_path = os.path.join(session_folder, 'parsed.csv')
    range_path = os.path.join(session_folder, 'range_selection.json')
    metrics_path = os.path.join(session_folder, 'metrics.json')
    
    file_status = {
        "storage_path": session_folder,
        "files": {
            "parsed_csv": {
                "path": csv_path,
                "exists": os.path.exists(csv_path),
                "size_mb": round(os.path.getsize(csv_path) / (1024*1024), 2) if os.path.exists(csv_path) else 0
            },
            "range_selection": {
                "path": range_path,
                "exists": os.path.exists(range_path)
            },
            "metrics": {
                "path": metrics_path,
                "exists": os.path.exists(metrics_path)
            }
        }
    }
    
    # If metrics file exists, include latest results
    if os.path.exists(metrics_path):
        try:
            with open(metrics_path, "r") as f:
                latest_results = json.load(f)
            file_status["latest_training"] = latest_results.get("status", "unknown")
            if "model_performance" in latest_results:
                file_status["latest_performance"] = latest_results["model_performance"]
        except:
            file_status["latest_training"] = "error_reading_metrics"
    
    return file_status

@app.get("/metrics")
def get_latest_metrics():
    """Get the latest training metrics if available"""
    metrics_path = os.path.join(STORAGE_PATH, "metrics.json")
    
    if not os.path.exists(metrics_path):
        raise HTTPException(
            status_code=404,
            detail="No training metrics found. Train a model first."
        )
    
    try:
        with open(metrics_path, "r") as f:
            metrics = json.load(f)
        return metrics
    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Error reading metrics file: {str(e)}"
        )
        
@app.get("/simulate")
def simulate():
    def event_generator():
        try:
            MODEL_PATH = os.path.join(STORAGE_PATH, "model.pkl")
            csv_path = os.path.join(STORAGE_PATH, "parsed.csv")
            range_path = os.path.join(STORAGE_PATH, "range_selection.json")

            print(f"📄 Loading data from: {csv_path}")
            print(f"🗂️ Reading range from: {range_path}")
            print(f"🤖 Loading model from: {MODEL_PATH}")

            # Check if all required files exist
            if not os.path.exists(csv_path):
                yield f"data: {json.dumps({'error': 'CSV file not found'})}\n\n"
                return
            
            if not os.path.exists(range_path):
                yield f"data: {json.dumps({'error': 'Range selection file not found'})}\n\n"
                return
                
            if not os.path.exists(MODEL_PATH):
                yield f"data: {json.dumps({'error': 'Model file not found. Train model first.'})}\n\n"
                return

            # Load the trained model
            model = joblib.load(MODEL_PATH)
            print("✅ Model loaded successfully")

            # Load data
            df = pd.read_csv(csv_path)
            print(f"📊 Loaded {len(df)} total rows")

            # Load simulation range
            with open(range_path) as f:
                ranges = json.load(f)

            # Check if SimStart and SimEnd exist
            if 'SimStart' not in ranges or 'SimEnd' not in ranges:
                yield f"data: {json.dumps({'error': 'SimStart or SimEnd not found in range selection'})}\n\n"
                return

            sim_start = pd.to_datetime(ranges["SimStart"])
            sim_end = pd.to_datetime(ranges["SimEnd"])
            print(f"⏱️ Simulating from {sim_start} to {sim_end}")

            # Filter simulation data
            df["SyntheticTimestamp"] = pd.to_datetime(df["SyntheticTimestamp"])
            sim_data = df[(df["SyntheticTimestamp"] >= sim_start) &
                          (df["SyntheticTimestamp"] <= sim_end)]

            print(f"📊 Rows in simulation window: {len(sim_data)}")
            
            if len(sim_data) == 0:
                yield f"data: {json.dumps({'error': 'No data found in simulation range'})}\n\n"
                return

            # Get feature columns (same as used in training)
            feature_columns = [col for col in df.columns if col not in ['Response', 'Id', 'SyntheticTimestamp']]
            print(f"🔧 Using features: {feature_columns}")

            # Send initial info
            yield f"data: {json.dumps({'type': 'info', 'message': f'Starting simulation with {len(sim_data)} samples'})}\n\n"

            # Simulate each row
            for i, (_, row) in enumerate(sim_data.iterrows()):
                try:
                    # Prepare features for prediction
                    input_data = row[feature_columns].values.reshape(1, -1)
                    
                    # Make prediction
                    prediction_prob = model.predict_proba(input_data)[0]
                    prediction = model.predict(input_data)[0]
                    confidence = max(prediction_prob) * 100
                    
                    # Convert prediction to Pass/Fail
                    prediction_label = "Pass" if prediction == 1 else "Fail"
                    
                    # Get actual response if available
                    actual_response = row.get("Response", "Unknown")
                    actual_label = "Pass" if actual_response == 1 else "Fail" if actual_response == 0 else "Unknown"

                    # Build event data
                    event = {
                        "id": row.get("Id", f"SAMPLE_{i:03d}"),
                        "timestamp": row["SyntheticTimestamp"].isoformat(),
                        "prediction": prediction_label,
                        "confidence": round(confidence, 2),
                        "actual": actual_label,
                        "features": {col: round(float(row[col]), 2) if pd.notna(row[col]) else 0 for col in feature_columns[:3]}  # Send first 3 features for display
                    }

                    print(f"📤 Sending event {i+1}/{len(sim_data)}: {event['id']}")
                    yield f"data: {json.dumps(event)}\n\n"
                    
                    # Add delay for real-time effect
                    time.sleep(0.5)  # Reduced delay for faster simulation

                except Exception as e:
                    print(f"⚠️ Prediction error for row {i}: {e}")
                    error_event = {
                        "id": row.get("Id", f"SAMPLE_{i:03d}"),
                        "timestamp": row["SyntheticTimestamp"].isoformat(),
                        "prediction": "Error",
                        "confidence": 0.0,
                        "error": str(e)
                    }
                    yield f"data: {json.dumps(error_event)}\n\n"

            # Send completion signal
            print("✅ Simulation completed")
            yield f"data: {json.dumps({'type': 'complete', 'message': 'Simulation completed successfully'})}\n\n"

        except Exception as e:
            print(f"❌ Simulation error: {e}")
            yield f"data: {json.dumps({'type': 'error', 'error': str(e)})}\n\n"

    return StreamingResponse(event_generator(), media_type="text/event-stream")

if __name__ == "__main__":
    import uvicorn
    print(f"Storage path set to: {STORAGE_PATH}")
    uvicorn.run(app, host="127.0.0.1", port=8000, reload=True)