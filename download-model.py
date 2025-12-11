#!/usr/bin/env python3
"""
Script to download and export YOLO11n ONNX model from Ultralytics.
This script uses the Ultralytics Python package to download the PyTorch model
and export it to ONNX format.
"""

import os
import sys
from pathlib import Path

def download_and_export_model():
    """Download YOLO11n PyTorch model and export to ONNX format."""
    try:
        from ultralytics import YOLO
    except ImportError:
        print("Error: ultralytics package not found.")
        print("Please install it with: pip install ultralytics")
        sys.exit(1)
    
    # Define paths
    model_dir = Path("models")
    model_path = model_dir / "yolo11n.onnx"
    
    # Create models directory if it doesn't exist
    model_dir.mkdir(exist_ok=True)
    
    # Check if model already exists
    if model_path.exists():
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"Model already exists at {model_path} ({size_mb:.1f} MB)")
        return
    
    print("Downloading YOLO11n model and exporting to ONNX...")
    print("This may take a few minutes...")
    
    # Load model (will download if not present)
    model = YOLO('yolo11n.pt')
    
    # Export to ONNX format
    model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
    
    # Move the exported model to the models directory
    exported_path = Path("yolo11n.onnx")
    if exported_path.exists():
        exported_path.rename(model_path)
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"✓ Model successfully exported to {model_path} ({size_mb:.1f} MB)")
    else:
        print("✗ Failed to export model")
        sys.exit(1)

if __name__ == "__main__":
    download_and_export_model()
