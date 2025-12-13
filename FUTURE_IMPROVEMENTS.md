# Future Improvements for Accessory Detection

## Code Quality Enhancements

### 1. Extract Shared IoU Calculation
**Current State**: `CalculateIoU` method is duplicated in:
- `ObjectDetectionService.cs` (line 334-339)
- `AccessoryDetectionService.cs` (line 147-156)

**Recommendation**: Create a shared utility class:
```csharp
// ADCommsPersonTracking.Api/Helpers/GeometricUtils.cs
public static class GeometricUtils
{
    public static float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        var x1 = Math.Max(box1.X, box2.X);
        var y1 = Math.Max(box1.Y, box2.Y);
        var x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        var y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        var intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var box1Area = box1.Width * box1.Height;
        var box2Area = box2.Width * box2.Height;
        var unionArea = box1Area + box2Area - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }
}
```

**Benefits**:
- Eliminates code duplication
- Ensures consistency across services
- Easier to maintain and test

### 2. Centralize COCO Class Mappings
**Current State**: COCO class definitions scattered across:
- `ObjectDetectionService.cs` (lines 25-32)
- `AccessoryDetectionService.cs` (lines 28-34)

**Recommendation**: Create a shared constants class:
```csharp
// ADCommsPersonTracking.Api/Constants/CocoClasses.cs
public static class CocoClasses
{
    public const int Person = 0;
    public const int Backpack = 24;
    public const int Handbag = 26;
    public const int Tie = 27;
    public const int Suitcase = 28;
    
    public static readonly Dictionary<int, string> ClassNames = new()
    {
        { Person, "person" },
        { Backpack, "backpack" },
        { Handbag, "handbag" },
        { Tie, "tie" },
        { Suitcase, "suitcase" }
    };
    
    public static readonly HashSet<int> AccessoryClassIds = new() 
    { 
        Backpack, Handbag, Tie, Suitcase 
    };
    
    public static readonly HashSet<string> AccessoryTypes = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "backpack", "handbag", "suitcase" 
    };
    
    public static readonly HashSet<string> ClothingTypes = new(StringComparer.OrdinalIgnoreCase) 
    { 
        "tie" 
    };
}
```

**Benefits**:
- Single source of truth for COCO classes
- Easier to add new classes
- Reduces risk of inconsistencies
- Better maintainability

## Feature Enhancements

### 3. Additional COCO Classes Support
Consider adding support for:
- Class 1: bicycle
- Class 31: skis
- Class 36: snowboard
- Class 38: frisbee
- Class 39: umbrella

### 4. Configurable Association Thresholds
Make spatial association parameters configurable via `appsettings.json`:
```json
{
  "AccessoryDetection": {
    "MinIouThreshold": 0.01,
    "ExtendedBoxLeftRightFactor": 0.2,
    "ExtendedBoxTopFactor": 0.1,
    "ExtendedBoxWidthMultiplier": 1.4,
    "ExtendedBoxHeightMultiplier": 1.2
  }
}
```

### 5. Multi-Frame Temporal Tracking
Track accessories across multiple frames to improve detection confidence:
- Reduce false positives
- Handle temporary occlusions
- Build accessory history per person track

### 6. Machine Learning-Based Association
Replace geometric rules with ML-based association model:
- Train on person-accessory pairs
- Learn context (e.g., backpacks typically on back/shoulders)
- Improve accuracy for edge cases

## Testing Enhancements

### 7. Integration Tests with Mock Model
Create integration tests that don't require actual YOLO model:
- Mock ONNX inference session
- Test end-to-end with synthetic detections
- Verify all code paths

### 8. Performance Benchmarks
Add performance tests to track:
- Inference time vs. number of objects
- Memory usage
- Association algorithm efficiency

## Documentation

### 9. API Documentation
Add XML documentation comments for all public APIs:
- Method descriptions
- Parameter descriptions
- Return value descriptions
- Usage examples

### 10. Architecture Diagrams
Create visual diagrams showing:
- System component interactions
- Data flow for accessory detection
- Decision trees for association logic

## Priority Recommendation

**High Priority** (Technical Debt):
1. Extract shared IoU calculation (#1)
2. Centralize COCO class mappings (#2)

**Medium Priority** (Quality):
3. Add XML documentation (#9)
4. Integration tests with mock model (#7)

**Low Priority** (Future Features):
5. Additional COCO classes (#3)
6. Configurable thresholds (#4)
7. Temporal tracking (#5)
8. ML-based association (#6)
