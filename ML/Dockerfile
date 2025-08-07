FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Copy code to container
COPY . .

# Install dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Expose port
EXPOSE 8000

# Run FastAPI app (prod mode)
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
