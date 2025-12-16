#!/usr/bin/env python3
"""
Script to download and export a fashion-trained YOLO model to ONNX format.
This script downloads a Fashionpedia-trained YOLOv8 model from HuggingFace
and exports it to ONNX format for clothing item detection.

Model: keremberke/yolov8m-fashion-detection
Dataset: Fashionpedia (clothing and fashion items)
"""

import os
import sys
from pathlib import Path

def download_and_export_model():
    """Download fashion YOLO model from HuggingFace and export to ONNX format."""
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
    print("Fashionpedia YOLO Model Download and Export")
    print("=" * 60)
    print()
    print("Downloading YOLOv8m fashion detection model from HuggingFace...")
    print("Model: keremberke/yolov8m-fashion-detection")
    print("Dataset: Fashionpedia")
    print()
    print("This model can detect:")
    print("  - Upper body: shirt, t-shirt, jacket, coat, sweater, hoodie, vest, blazer")
    print("  - Lower body: pants, jeans, shorts, skirt")
    print("  - Full body: dress, jumpsuit")
    print("  - Accessories: hat, glasses, bag, tie, scarf")
    print()
    
    try:
        # Load model from HuggingFace (will download if not present)
        # Using keremberke/yolov8m-fashion-detection - a Fashionpedia-trained model
        print("Downloading model from HuggingFace...")
        model = YOLO('keremberke/yolov8m-fashion-detection')
        
        # Export to ONNX format
        print("Exporting to ONNX format...")
        model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
        
        # Find and move the exported model
        # HuggingFace models export with their name
        possible_exports = [
            Path("yolov8m-fashion-detection.onnx"),
            Path("best.onnx"),
            Path("model.onnx")
        ]
        
        exported_path = None
        for path in possible_exports:
            if path.exists():
                exported_path = path
                break
        
        if exported_path and exported_path.exists():
            exported_path.rename(model_path)
            size_mb = model_path.stat().st_size / (1024 * 1024)
            print()
            print(f"✓ Fashion model exported successfully to {model_path} ({size_mb:.1f} MB)")
            print()
            print("Model classes (Fashionpedia):")
            print("  - shirt, t-shirt, jacket, coat, sweater, hoodie, vest, blazer")
            print("  - pants, jeans, shorts, skirt, dress, jumpsuit")
            print("  - hat, glasses, bag, tie, scarf")
            print()
            print("To enable clothing detection:")
            print("  Set ClothingDetection:Enabled=true in appsettings.json")
        else:
            print("✗ Failed to find exported model file")
            print("Attempted paths:", [str(p) for p in possible_exports])
            sys.exit(1)
            
    except Exception as e:
        print(f"✗ Error downloading or exporting model: {e}")
        print()
        print("Alternative: Download manually from HuggingFace")
        print("  https://huggingface.co/keremberke/yolov8m-fashion-detection")
        sys.exit(1)

if __name__ == "__main__":
    download_and_export_model()
