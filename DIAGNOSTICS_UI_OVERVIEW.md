# Diagnostics UI Overview

## Visual Layout

### Page Structure

```
┌─────────────────────────────────────────────────────────────┐
│ Inference Diagnostics                                        │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 📋 Session Information                                  │ │
│ │                                                         │ │
│ │ Session ID: diag_abc123...                             │ │
│ │ Timestamp: 2024-12-13 10:30:45                         │ │
│ │ Tracking ID: track_xyz789                              │ │
│ │ Prompt: "person with backpack"                         │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 🔍 Extracted Search Criteria                           │ │
│ │                                                         │ │
│ │ Accessories: [backpack]                                │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 📊 Processing Summary                                   │ │
│ │                                                         │ │
│ │ Images      Persons    Matching    Accessories         │ │
│ │ Processed   Detected   Persons     Detected            │ │
│ │   10          158        0           5                 │ │
│ │                         (0.0%)                          │ │
│ │                                                         │ │
│ │ Total Association    Successful      Average IoU       │ │
│ │ Attempts            Associations                        │ │
│ │    790                  0            0.0001            │ │
│ │                                                         │ │
│ │ Processing Duration: 1250 ms                           │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 🖼️ Per-Image Analysis                                  │ │
│ │                                                         │ │
│ │ ▼ Image 1                           [10 detections]    │ │
│ │   Image Dimensions: 1920 x 1080 px                     │ │
│ │                                                         │ │
│ │   All YOLO Detections:                                 │ │
│ │   ┌──────┬──────────┬───────┬─────────────────────┐  │ │
│ │   │Class │   Type   │ Conf  │   Bounding Box      │  │ │
│ │   ├──────┼──────────┼───────┼─────────────────────┤  │ │
│ │   │ [0]  │ person   │ 0.920 │ (100, 150, 80, 200) │  │ │
│ │   │ [0]  │ person   │ 0.890 │ (300, 160, 75, 195) │  │ │
│ │   │ [24] │ backpack │ 0.850 │ (450, 300, 50, 60)  │  │ │
│ │   │ [24] │ backpack │ 0.780 │ (650, 280, 48, 58)  │  │ │
│ │   └──────┴──────────┴───────┴─────────────────────┘  │ │
│ │                                                         │ │
│ │   Person Analysis:                                     │ │
│ │   ┌────────────────────────────────────────────────┐  │ │
│ │   │ 👤 Person 1              [✗ Excluded]         │  │ │
│ │   ├────────────────────────────────────────────────┤  │ │
│ │   │ Bounding Box: (100, 150, 80, 200)             │  │ │
│ │   │ Confidence: 0.920                              │  │ │
│ │   │                                                 │  │ │
│ │   │ Detected Colors:                               │  │ │
│ │   │   Upper: red, white | Lower: blue, black      │  │ │
│ │   │                                                 │  │ │
│ │   │ Accessory Association Attempts:                │  │ │
│ │   │ ┌──────────┬──────┬──────────┬─────────┬─────┐│  │ │
│ │   │ │   Type   │ Conf │   Box    │   IoU   │Res. ││  │ │
│ │   │ ├──────────┼──────┼──────────┼─────────┼─────┤│  │ │
│ │   │ │ backpack │ 0.85 │ (450,..  │ 0.0000  │ ✗   ││  │ │
│ │   │ │ backpack │ 0.78 │ (650,..  │ 0.0000  │ ✗   ││  │ │
│ │   │ └──────────┴──────┴──────────┴─────────┴─────┘│  │ │
│ │   │                                                 │  │ │
│ │   │ 🟡 backpack: IoU 0.0000 < threshold 0.01,     │  │ │
│ │   │    center (475, 330) outside extended bounds  │  │ │
│ │   │    (80, 135, 112, 240)                        │  │ │
│ │   │                                                 │  │ │
│ │   │ 🟡 backpack: IoU 0.0000 < threshold 0.01,     │  │ │
│ │   │    center (674, 309) outside extended bounds  │  │ │
│ │   │    (80, 135, 112, 240)                        │  │ │
│ │   │                                                 │  │ │
│ │   │ Criteria Matching Summary:                     │  │ │
│ │   │ ┌──────────────────────────────────────────┐  │  │ │
│ │   │ │ ✓ Colors: No criteria specified          │  │  │ │
│ │   │ │ ✗ Accessories: Searched for backpack.    │  │  │ │
│ │   │ │   Detected: none. No matches found       │  │  │ │
│ │   │ │ ✓ Physical: No criteria specified        │  │  │ │
│ │   │ │ ─────────────────────────────────────    │  │  │ │
│ │   │ │ ✗ Overall: NO MATCH                      │  │  │ │
│ │   │ └──────────────────────────────────────────┘  │  │ │
│ │   │                                                 │  │ │
│ │   │ 🔴 Exclusion Reason: accessories/clothing     │  │ │
│ │   │    don't match                                 │  │ │
│ │   └────────────────────────────────────────────────┘  │ │
│ │                                                         │ │
│ │   [Similar panels for Person 2, 3, etc...]            │ │
│ │                                                         │ │
│ │ ▶ Image 2                           [12 detections]    │ │
│ │ ▶ Image 3                           [15 detections]    │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ 📋 Log Timeline                                         │ │
│ │                                                         │ │
│ │ ▶ (Click to expand log entries)                        │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Color Coding

### Success Indicators (Green 🟢)
- ✓ Person included in results
- ✓ Accessory successfully associated
- ✓ Criteria matched
- IoU scores above threshold

### Failure Indicators (Red 🔴)
- ✗ Person excluded from results
- ✗ Accessory not associated
- ✗ Criteria not matched
- IoU scores below threshold

### Warning Indicators (Yellow/Orange 🟡)
- Association attempt details showing why association failed

### Info Indicators (Blue ℹ️)
- Neutral information
- Detected objects that are persons (ClassId 0)

## Key UI Features

### 1. Session Information
- Shows session ID, timestamp, tracking ID, and search prompt
- Provides context for the diagnostic session

### 2. Extracted Search Criteria
- Visual chips showing parsed criteria:
  - Colors (primary chips)
  - Accessories (success chips)
  - Clothing (info chips)
  - Physical attributes (secondary chips)
  - Height (warning chips)

### 3. Processing Summary
- Key metrics displayed as large numbers
- Includes percentage for matching persons
- Shows total association attempts across all persons and accessories
- Displays average IoU to understand typical overlap
- Shows processing time

### 4. Per-Image Analysis
- Expandable panels for each image
- Badge showing total detection count
- Table of all YOLO detections
  - Color-coded by class (persons vs accessories)
  - Shows confidence scores
  - Shows bounding box coordinates

### 5. Per-Person Analysis (Critical for Debugging)
Each person card includes:

#### Header
- Person index number
- Inclusion status badge (green for included, red for excluded)

#### Person Information
- Bounding box coordinates
- Detection confidence score

#### Detected Colors
- Organized by body region (upper/lower/overall)
- Comma-separated list of color names

#### Accessory Association Attempts Table
Critical debugging information showing:
- **Type**: Accessory class name (e.g., "backpack")
- **Conf**: YOLO confidence score for the accessory
- **Box**: Bounding box coordinates (abbreviated for space)
- **IoU**: Calculated Intersection over Union score
  - **Highlighted in red if below threshold**
  - Shows exactly how much overlap exists
- **Extended Bounds**: Icon showing if accessory center is within extended person bounds
  - ✓ (green checkmark) if within
  - ✗ (red X) if outside
- **Result**: Final association decision
  - Green chip "✓ Associated" if successful
  - Red chip "✗ Not Associated" if failed

#### Detailed Association Reasons
Alert boxes for each accessory showing:
- Human-readable explanation
- Specific values (IoU score, center coordinates, extended bounds)
- Example: "IoU 0.0023 < threshold 0.01, center (450, 320) outside extended bounds (100, 150, 280, 400)"

#### Successfully Associated Items
- Green chips for associated accessories
- Blue chips for associated clothing
- Shows confidence scores

#### Criteria Matching Summary
Visual breakdown with icons:
- ✓/✗ Color matching with explanation
- ✓/✗ Accessory matching with explanation
  - Shows what was searched for vs. what was detected
- ✓/✗ Physical attribute matching with explanation
- **Overall result prominently displayed**

#### Exclusion Reason
- Red alert box if person was excluded
- Clear explanation: "colors don't match", "accessories/clothing don't match", etc.

### 6. Log Timeline
- Expandable table with chronological log entries
- Color-coded by log level (debug, info, warning, error)
- Shows timestamp, category, and message
- Useful for detailed troubleshooting

## Interactive Elements

### Expandable Panels
- Images can be collapsed/expanded individually
- Persons can be expanded to see full details
- Log timeline can be collapsed when not needed

### Responsive Tables
- Tables adjust to screen size
- Data labels shown on mobile
- Horizontal scrolling when needed

### Visual Indicators
- Chips for categorization
- Icons for quick recognition
- Color coding throughout
- Badges for counts

## User Workflow Example

### Problem: "person with backpack" returns 0 matches

1. **Review Summary**
   - See 158 persons, 5 accessories, 0 matches
   - Notice 790 association attempts but 0 successful
   - See average IoU is 0.0001 (very low)

2. **Expand Image 1**
   - See YOLO detected 2 persons and 2 backpacks
   - Bounding boxes show persons and backpacks are far apart

3. **Expand Person 1**
   - See person at (100, 150, 80, 200)
   - See backpack #1 at (450, 300, 50, 60)
   - View association attempt table:
     - IoU: 0.0000 (highlighted in red)
     - Extended bounds: ✗ (red X)
     - Result: ✗ Not Associated
   - Read reason: "center (475, 330) outside extended bounds (80, 135, 112, 240)"
   - Understand: Backpack is too far from person (475 vs 180)

4. **Understand Root Cause**
   - Backpacks are correctly detected
   - Persons are correctly detected
   - But spatial positions don't overlap
   - Backpacks appear to be on the ground or elsewhere, not being carried

5. **Decision**
   - If this is correct (backpacks not being carried): No action needed
   - If thresholds too strict: Adjust `ExtendedBoxWidthMultiplier` in config
   - If IoU too strict: Adjust `MinIouThreshold` in config

## Benefits

### For Users
- **Transparency**: See exactly what the system detected and why it made decisions
- **Debuggability**: Understand failures with specific details
- **Tunability**: Adjust thresholds based on observed behavior
- **Confidence**: Trust the system's results with full visibility

### For Developers
- **Testing**: Verify association logic is working correctly
- **Optimization**: Identify threshold values that need adjustment
- **Troubleshooting**: Diagnose issues quickly with detailed data
- **Iteration**: Test changes and immediately see impact

## Technical Notes

### Performance
- Diagnostic data collected asynchronously (non-blocking)
- UI uses lazy rendering with expandable sections
- Statistics calculated on-demand
- Data retained for 30 minutes

### Accessibility
- Semantic HTML structure
- ARIA labels for screen readers
- Keyboard navigation support
- Color not the only indicator (icons + text)

### Browser Compatibility
- Modern browsers (Chrome, Firefox, Safari, Edge)
- MudBlazor components handle cross-browser compatibility
- Responsive design for mobile/tablet
