#!/bin/bash
# Script to download YOLO11n ONNX model from Ultralytics

set -e

MODEL_URL="https://github.com/ultralytics/assets/releases/download/v8.3.0/yolo11n.onnx"
MODEL_DIR="models"
MODEL_PATH="$MODEL_DIR/yolo11n.onnx"

echo "Downloading YOLO11n ONNX model..."

# Create models directory if it doesn't exist
mkdir -p "$MODEL_DIR"

# Download the model
if [ -f "$MODEL_PATH" ]; then
    echo "Model already exists at $MODEL_PATH"
else
    echo "Downloading from $MODEL_URL to $MODEL_PATH..."
    curl -L -o "$MODEL_PATH" "$MODEL_URL"
    echo "Model downloaded successfully!"
fi

# Verify the file exists and has reasonable size
if [ -f "$MODEL_PATH" ]; then
    SIZE=$(stat -f%z "$MODEL_PATH" 2>/dev/null || stat -c%s "$MODEL_PATH" 2>/dev/null)
    echo "Model file size: $SIZE bytes"
    if [ "$SIZE" -gt 1000000 ]; then
        echo "✓ Model downloaded and verified successfully!"
        exit 0
    else
        echo "✗ Downloaded file is too small, may be corrupted"
        exit 1
    fi
else
    echo "✗ Failed to download model"
    exit 1
fi
