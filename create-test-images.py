#!/usr/bin/env python3
"""
Script to create sample test images for YOLO11 integration tests.
Creates:
1. A simple image without any person (solid color)
2. A simple image with a person-like shape (for basic testing)
"""

from PIL import Image, ImageDraw
import os

def create_test_images():
    """Create test images for integration tests."""
    output_dir = "ADCommsPersonTracking.Tests/TestData/Images"
    os.makedirs(output_dir, exist_ok=True)
    
    # Image 1: No person - solid blue background
    print("Creating test image without person...")
    img_no_person = Image.new('RGB', (640, 480), color=(135, 206, 235))  # Sky blue
    no_person_path = os.path.join(output_dir, "no_person.jpg")
    img_no_person.save(no_person_path, 'JPEG')
    print(f"✓ Created: {no_person_path}")
    
    # Image 2: Simple person-like shape (rectangle approximating a person)
    print("Creating test image with person-like shape...")
    img_person = Image.new('RGB', (640, 480), color=(200, 200, 200))  # Light gray background
    draw = ImageDraw.Draw(img_person)
    
    # Draw a person-like shape (head + body)
    # Head (circle approximation)
    head_x, head_y = 320, 150
    head_radius = 30
    draw.ellipse([head_x - head_radius, head_y - head_radius, 
                  head_x + head_radius, head_y + head_radius], 
                 fill=(255, 220, 177))  # Skin tone
    
    # Body (rectangle)
    body_left, body_top = 280, 180
    body_right, body_bottom = 360, 380
    draw.rectangle([body_left, body_top, body_right, body_bottom], 
                   fill=(50, 100, 200))  # Blue shirt
    
    # Legs (two rectangles)
    draw.rectangle([280, 380, 315, 450], fill=(50, 50, 50))  # Left leg
    draw.rectangle([325, 380, 360, 450], fill=(50, 50, 50))  # Right leg
    
    person_path = os.path.join(output_dir, "person.jpg")
    img_person.save(person_path, 'JPEG')
    print(f"✓ Created: {person_path}")
    
    # Image 3: Empty scene (gradient background)
    print("Creating test image with gradient background...")
    img_gradient = Image.new('RGB', (640, 480))
    pixels = img_gradient.load()
    for y in range(480):
        for x in range(640):
            # Create a gradient effect
            r = int((x / 640) * 255)
            g = int((y / 480) * 255)
            b = 128
            pixels[x, y] = (r, g, b)
    
    gradient_path = os.path.join(output_dir, "empty_scene.jpg")
    img_gradient.save(gradient_path, 'JPEG')
    print(f"✓ Created: {gradient_path}")
    
    print("\n✓ All test images created successfully!")

if __name__ == "__main__":
    create_test_images()
