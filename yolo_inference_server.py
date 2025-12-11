#!/usr/bin/env python3
"""
Simple Flask server for YOLO11 ONNX inference.
Provides HTTP endpoints for object detection using YOLO11.
"""

import os
import io
import json
import numpy as np
from flask import Flask, request, jsonify
from PIL import Image
import onnxruntime as ort

app = Flask(__name__)

# Load YOLO11 model
MODEL_PATH = os.environ.get('MODEL_PATH', '/app/models/yolo11n.onnx')
session = None

def load_model():
    """Load the ONNX model."""
    global session
    if os.path.exists(MODEL_PATH):
        session = ort.InferenceSession(MODEL_PATH)
        print(f"Model loaded successfully from {MODEL_PATH}")
    else:
        print(f"Warning: Model not found at {MODEL_PATH}")

def preprocess_image(image_bytes, input_size=640):
    """Preprocess image for YOLO11 inference."""
    # Load image
    image = Image.open(io.BytesIO(image_bytes))
    original_size = image.size  # (width, height)
    
    # Convert to RGB if needed
    if image.mode != 'RGB':
        image = image.convert('RGB')
    
    # Resize to model input size
    image_resized = image.resize((input_size, input_size))
    
    # Convert to numpy array and normalize
    img_array = np.array(image_resized).astype(np.float32) / 255.0
    
    # Transpose to (C, H, W) format
    img_array = img_array.transpose(2, 0, 1)
    
    # Add batch dimension
    img_array = np.expand_dims(img_array, axis=0)
    
    return img_array, original_size

def convert_coordinates(cx, cy, w, h, original_size, input_size=640):
    """Convert bounding box from model coordinates to original image coordinates."""
    scale_x = original_size[0] / input_size
    scale_y = original_size[1] / input_size
    
    x = (cx - w / 2) * scale_x
    y = (cy - h / 2) * scale_y
    width = w * scale_x
    height = h * scale_y
    
    return float(x), float(y), float(width), float(height)

def extract_detections(output, confidence_threshold=0.45):
    """Extract person detections from YOLO11 output."""
    detections = []
    
    # YOLO11 output shape: [1, 84, 8400]
    # 84 = 4 (bbox) + 80 (classes)
    output = output[0]  # Remove batch dimension
    num_detections = output.shape[1]
    
    for i in range(num_detections):
        # Get class scores (skip first 4 bbox coordinates)
        class_scores = output[4:, i]
        max_score = float(np.max(class_scores))
        max_class = int(np.argmax(class_scores))
        
        # Check if it's a person (class 0) and meets confidence threshold
        if max_class == 0 and max_score >= confidence_threshold:
            # Get bounding box coordinates (center format)
            cx, cy, w, h = output[0:4, i]
            
            detections.append({
                'coords': (cx, cy, w, h),
                'confidence': max_score,
                'class_id': max_class
            })
    
    return detections

def postprocess_output(output, original_size, confidence_threshold=0.45, iou_threshold=0.5, input_size=640):
    """Process YOLO11 output to extract person detections."""
    # Extract raw detections
    raw_detections = extract_detections(output, confidence_threshold)
    
    # Convert coordinates to original image space
    detections = []
    for det in raw_detections:
        cx, cy, w, h = det['coords']
        x, y, width, height = convert_coordinates(cx, cy, w, h, original_size, input_size)
        
        detections.append({
            'x': x,
            'y': y,
            'width': width,
            'height': height,
            'confidence': det['confidence'],
            'label': 'person',
            'class_id': det['class_id']
        })
    
    # Apply Non-Maximum Suppression
    detections = apply_nms(detections, iou_threshold)
    
    return detections

def apply_nms(boxes, iou_threshold):
    """Apply Non-Maximum Suppression to remove overlapping boxes."""
    if not boxes:
        return []
    
    # Sort by confidence
    boxes = sorted(boxes, key=lambda x: x['confidence'], reverse=True)
    
    result = []
    while boxes:
        current = boxes.pop(0)
        result.append(current)
        
        # Remove boxes with high IoU
        boxes = [box for box in boxes if calculate_iou(current, box) < iou_threshold]
    
    return result

def calculate_iou(box1, box2):
    """Calculate Intersection over Union (IoU) between two boxes."""
    x1 = max(box1['x'], box2['x'])
    y1 = max(box1['y'], box2['y'])
    x2 = min(box1['x'] + box1['width'], box2['x'] + box2['width'])
    y2 = min(box1['y'] + box1['height'], box2['y'] + box2['height'])
    
    intersection_area = max(0, x2 - x1) * max(0, y2 - y1)
    box1_area = box1['width'] * box1['height']
    box2_area = box2['width'] * box2['height']
    
    # Handle edge cases: zero-area boxes or no overlap
    if box1_area <= 0 or box2_area <= 0:
        return 0.0
    
    union_area = box1_area + box2_area - intersection_area
    
    return intersection_area / union_area if union_area > 0 else 0.0

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint."""
    return jsonify({
        'status': 'healthy',
        'model_loaded': session is not None,
        'model_path': MODEL_PATH
    })

@app.route('/detect', methods=['POST'])
def detect():
    """
    Detect persons in an image.
    Expects a POST request with image bytes in the body.
    Returns JSON with detections.
    """
    if session is None:
        return jsonify({'error': 'Model not loaded'}), 500
    
    try:
        # Get image from request
        image_bytes = request.data
        if not image_bytes:
            return jsonify({'error': 'No image data provided'}), 400
        
        # Get optional parameters
        confidence_threshold = float(request.args.get('confidence', 0.45))
        iou_threshold = float(request.args.get('iou', 0.5))
        
        # Preprocess image
        input_data, original_size = preprocess_image(image_bytes)
        
        # Run inference
        input_name = session.get_inputs()[0].name
        output_name = session.get_outputs()[0].name
        outputs = session.run([output_name], {input_name: input_data})
        
        # Postprocess output
        detections = postprocess_output(
            outputs[0], 
            original_size, 
            confidence_threshold, 
            iou_threshold
        )
        
        return jsonify({
            'detections': detections,
            'count': len(detections),
            'original_size': original_size
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/info', methods=['GET'])
def info():
    """Get model information."""
    if session is None:
        return jsonify({'error': 'Model not loaded'}), 500
    
    inputs = session.get_inputs()
    outputs = session.get_outputs()
    
    return jsonify({
        'model_path': MODEL_PATH,
        'inputs': [{
            'name': inp.name,
            'shape': inp.shape,
            'type': str(inp.type)
        } for inp in inputs],
        'outputs': [{
            'name': out.name,
            'shape': out.shape,
            'type': str(out.type)
        } for out in outputs]
    })

if __name__ == '__main__':
    load_model()
    app.run(host='0.0.0.0', port=5000, debug=False)
