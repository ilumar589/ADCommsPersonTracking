FROM ultralytics/ultralytics:latest

# Set working directory
WORKDIR /app

# Copy the export script
COPY export-model.py /app/export-model.py

# Make the script executable
RUN chmod +x /app/export-model.py

# Set the entrypoint to run the export script
ENTRYPOINT ["python3", "/app/export-model.py"]
