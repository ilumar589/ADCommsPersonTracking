#!/usr/bin/env python3
"""
Script to download YOLO11n model and export it to ONNX format.
This runs as an init container to prepare the model for the API.
"""

import os
import sys
from pathlib import Path

def export_model():
    """Download YOLO11n PyTorch model and export to ONNX format."""
    try:
        from ultralytics import YOLO
    except ImportError:
        print("Error: ultralytics package not found.")
        sys.exit(1)
    
    # Define paths
    output_dir = Path("/models")
    output_path = output_dir / "yolo11n.onnx"
    
    # Create output directory if it doesn't exist
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Check if model already exists
    if output_path.exists():
        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(f"Model already exists at {output_path} ({size_mb:.1f} MB)")
        print("✓ Model export complete")
        return
    
    print("=" * 60)
    print("YOLO11n Model Export to ONNX")
    print("=" * 60)
    print(f"Output directory: {output_dir}")
    print(f"Output file: {output_path}")
    print()
    print("Downloading YOLO11n model and exporting to ONNX...")
    print("This may take a few minutes...")
    print()
    
    # Load model (will download if not present)
    print("Step 1/2: Loading YOLO11n model...")
    model = YOLO('yolo11n.pt')
    print("✓ Model loaded successfully")
    print()
    
    # Export to ONNX format
    print("Step 2/2: Exporting to ONNX format...")
    model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
    print("✓ Model exported successfully")
    print()
    
    # Move the exported model to the output directory
    exported_path = Path("yolo11n.onnx")
    if exported_path.exists():
        exported_path.rename(output_path)
        size_mb = output_path.stat().st_size / (1024 * 1024)
        print("=" * 60)
        print(f"✓ Model successfully exported to {output_path}")
        print(f"  Size: {size_mb:.1f} MB")
        print("=" * 60)
    else:
        print("✗ Failed to export model")
        sys.exit(1)

if __name__ == "__main__":
    export_model()
