#!/usr/bin/env python3
"""Export YOLO11n model to ONNX format."""

from pathlib import Path
from ultralytics import YOLO

model_dir = Path("/models")
model_path = model_dir / "yolo11n.onnx"

model_dir.mkdir(parents=True, exist_ok=True)

if model_path.exists():
    size_mb = model_path.stat().st_size / (1024 * 1024)
    print(f"Model already exists at {model_path} ({size_mb:.1f} MB)")
else:
    print("Downloading and exporting YOLO11n to ONNX...")
    model = YOLO("yolo11n.pt")
    model.export(format="onnx", simplify=True, dynamic=False, imgsz=640)
    
    exported = Path("yolo11n.onnx")
    if exported.exists():
        exported.rename(model_path)
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"Model exported to {model_path} ({size_mb:.1f} MB)")
    else:
        print("Export failed!")
        exit(1)

print("Model export complete!")
