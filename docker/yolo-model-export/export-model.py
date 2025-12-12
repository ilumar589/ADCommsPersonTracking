#!/usr/bin/env python3
"""
YOLO Model Export Script for Init Container
Downloads YOLO11n model and exports it to ONNX format.
Writes the exported model to /models/yolo11n.onnx for use by the API.
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
    
    # Define output path
    output_dir = Path("/models")
    output_path = output_dir / "yolo11n.onnx"
    
    # Create output directory if it doesn't exist
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Check if model already exists
    if output_path.exists():
        size_mb = output_path.stat().st_size / (1024 * 1024)
        print(f"✓ Model already exists at {output_path} ({size_mb:.1f} MB)")
        print("Skipping export.")
        return 0
    
    print("=" * 60)
    print("YOLO11n Model Export for ADComms Person Tracking")
    print("=" * 60)
    print(f"Output directory: {output_dir}")
    print(f"Output file: {output_path}")
    print()
    
    try:
        print("Step 1: Downloading YOLO11n model...")
        # Load model (will download yolo11n.pt if not present)
        model = YOLO('yolo11n.pt')
        print("✓ Model downloaded successfully")
        print()
        
        print("Step 2: Exporting to ONNX format...")
        print("   - Format: ONNX")
        print("   - Simplify: True")
        print("   - Dynamic: False")
        print("   - Image size: 640")
        
        # Export to ONNX format
        # This creates yolo11n.onnx in the current directory
        model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
        print("✓ Export completed")
        print()
        
        print("Step 3: Moving model to output directory...")
        # Move the exported model to /models
        exported_path = Path("yolo11n.onnx")
        if exported_path.exists():
            exported_path.rename(output_path)
            size_mb = output_path.stat().st_size / (1024 * 1024)
            print(f"✓ Model successfully exported to {output_path}")
            print(f"  Size: {size_mb:.1f} MB")
            print()
            print("=" * 60)
            print("Export completed successfully!")
            print("=" * 60)
            return 0
        else:
            print("✗ Failed to find exported model file")
            return 1
            
    except Exception as e:
        print(f"✗ Error during export: {e}")
        import traceback
        traceback.print_exc()
        return 1

if __name__ == "__main__":
    sys.exit(export_model())
