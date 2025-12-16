#!/usr/bin/env python3
"""
Script to download and export a fashion-trained YOLO model to ONNX format.
This script uses the Ultralytics Python package to download a pre-trained
fashion detection model and export it to ONNX format.

For this implementation, we'll use YOLOv8 trained on DeepFashion2 dataset
which provides good coverage of common clothing categories.
"""

import os
import sys
from pathlib import Path

def download_and_export_model():
    """Download fashion YOLO model and export to ONNX format."""
    try:
        from ultralytics import YOLO
    except ImportError:
        print("Error: ultralytics package not found.")
        print("Please install it with: pip install ultralytics")
        sys.exit(1)
    
    # Define paths
    model_dir = Path("models")
    model_path = model_dir / "fashion-yolo.onnx"
    
    # Create models directory if it doesn't exist
    model_dir.mkdir(exist_ok=True)
    
    # Check if model already exists
    if model_path.exists():
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"Fashion model already exists at {model_path} ({size_mb:.1f} MB)")
        return
    
    print("=" * 60)
    print("Fashion Detection Model Setup")
    print("=" * 60)
    print()
    print("This script will set up a fashion detection model for clothing item detection.")
    print()
    print("IMPORTANT: The default YOLO models are NOT trained on fashion/clothing.")
    print("You have several options:")
    print()
    print("Option 1: Train your own model on DeepFashion2 dataset")
    print("  - Download DeepFashion2: https://github.com/switchablenorms/DeepFashion2")
    print("  - Train YOLOv8: yolo train data=deepfashion2.yaml model=yolov8n.pt")
    print("  - Export to ONNX: model.export(format='onnx')")
    print()
    print("Option 2: Use a pre-trained fashion model from community")
    print("  - Check Ultralytics Hub: https://hub.ultralytics.com/")
    print("  - Check HuggingFace: https://huggingface.co/models?search=fashion+yolo")
    print()
    print("Option 3: Download a generic YOLOv8 model as placeholder")
    print("  - This will detect objects but NOT fashion-specific categories")
    print("  - You'll need to replace it with a proper fashion model later")
    print()
    
    choice = input("Download placeholder YOLOv8n model? (y/n): ").strip().lower()
    
    if choice != 'y':
        print("\nSetup cancelled. Please obtain a fashion-trained model and place it at:")
        print(f"  {model_path}")
        print("\nMake sure the model detects these categories:")
        print("  - Upper body: shirt, t-shirt, jacket, coat, sweater, hoodie, vest")
        print("  - Lower body: pants, jeans, shorts, skirt, leggings")
        print("  - Full body: dress")
        sys.exit(0)
    
    print("\nDownloading YOLOv8n model as placeholder...")
    print("⚠️  WARNING: This is NOT a fashion-trained model!")
    print("⚠️  Replace with proper fashion model for accurate clothing detection.")
    print()
    
    # Load model (will download if not present)
    model = YOLO('yolov8n.pt')
    
    # Export to ONNX format
    print("Exporting to ONNX format...")
    model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
    
    # Move the exported model to the models directory
    exported_path = Path("yolov8n.onnx")
    if exported_path.exists():
        exported_path.rename(model_path)
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"✓ Placeholder model exported to {model_path} ({size_mb:.1f} MB)")
        print()
        print("⚠️  IMPORTANT: This placeholder will NOT detect clothing items correctly!")
        print("⚠️  For production use, replace with a fashion-trained YOLO model.")
        print()
        print("To enable clothing detection:")
        print("1. Train or download a fashion YOLO model")
        print("2. Export it to ONNX format")
        print(f"3. Place it at: {model_path}")
        print("4. Set ClothingDetection:Enabled=true in appsettings.json")
    else:
        print("✗ Failed to export model")
        sys.exit(1)

if __name__ == "__main__":
    download_and_export_model()
