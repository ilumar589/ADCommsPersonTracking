#!/usr/bin/env python3
"""Export YOLO11m model to ONNX format."""

from pathlib import Path
import shutil
from ultralytics import YOLO

model_dir = Path("/models")
model_path = model_dir / "yolo11m.onnx"

model_dir.mkdir(parents=True, exist_ok=True)

if model_path.exists():
    size_mb = model_path.stat().st_size / (1024 * 1024)
    print(f"Model already exists at {model_path} ({size_mb:.1f} MB)")
else:
    print("Downloading and exporting YOLO11m to ONNX...")
    model = YOLO("yolo11m.pt")
    model.export(format="onnx", simplify=True, dynamic=False, imgsz=640)
    
    exported = Path("yolo11m.onnx")
    if exported.exists():
        shutil.move(str(exported), str(model_path))
        size_mb = model_path.stat().st_size / (1024 * 1024)
        print(f"Model exported to {model_path} ({size_mb:.1f} MB)")
    else:
        print("Export failed!")
        exit(1)

print("Model export complete!")
