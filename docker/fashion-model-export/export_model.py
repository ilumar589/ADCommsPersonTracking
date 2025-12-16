#!/usr/bin/env python3
"""
Export fashion detection YOLO model to ONNX format.

This script downloads a Fashionpedia-trained YOLOv8 model from HuggingFace
and exports it to ONNX format for clothing item detection.

Model: keremberke/yolov8m-fashion-detection
Dataset: Fashionpedia (clothing and fashion items)
"""

from pathlib import Path
import shutil
import sys
from ultralytics import YOLO

model_dir = Path("/models")
model_path = model_dir / "fashion-yolo.onnx"

model_dir.mkdir(parents=True, exist_ok=True)

if model_path.exists():
    size_mb = model_path.stat().st_size / (1024 * 1024)
    print(f"Fashion model already exists at {model_path} ({size_mb:.1f} MB)")
else:
    print("=" * 60)
    print("Fashionpedia YOLO Model Export")
    print("=" * 60)
    print()
    print("Downloading YOLOv8m fashion detection model from HuggingFace...")
    print("Model: keremberke/yolov8m-fashion-detection")
    print("Dataset: Fashionpedia")
    print()
    
    try:
        # Load model from HuggingFace (will download if not present)
        print("Downloading model from HuggingFace...")
        model = YOLO("keremberke/yolov8m-fashion-detection")
        
        # Export to ONNX format
        print("Exporting to ONNX format...")
        model.export(format="onnx", simplify=True, dynamic=False, imgsz=640)
        
        # Find and move the exported model
        possible_exports = [
            Path("yolov8m-fashion-detection.onnx"),
            Path("best.onnx"),
            Path("model.onnx")
        ]
        
        exported = None
        for path in possible_exports:
            if path.exists():
                exported = path
                break
        
        if exported and exported.exists():
            shutil.move(str(exported), str(model_path))
            size_mb = model_path.stat().st_size / (1024 * 1024)
            print()
            print(f"✓ Fashion model exported successfully to {model_path} ({size_mb:.1f} MB)")
            print()
            print("Model classes (Fashionpedia):")
            print("  - shirt, t-shirt, jacket, coat, sweater, hoodie, vest, blazer")
            print("  - pants, jeans, shorts, skirt, dress, jumpsuit")
            print("  - hat, glasses, bag, tie, scarf")
        else:
            print("✗ Export failed - could not find exported model file")
            print("Attempted paths:", [str(p) for p in possible_exports])
            sys.exit(1)
            
    except Exception as e:
        print(f"✗ Error downloading or exporting model: {e}")
        sys.exit(1)

print("Fashion model export complete!")
