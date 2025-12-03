# IT-Sensing Prototype - Project Overview

## Project Description
A Unity desktop application prototype based on the IT Sensing website. This is a map-based application with drawing and measurement tools.

## Core Features

### ✅ Implemented Features
1. **Interactive Map System**
   - Tile-based map rendering (slippy map)
   - Drag and scroll navigation
   - Zoom controls (range: 2-19)
   - Multiple map styles (OSM, Google Roadmap, Terrain, Satellite, Hybrid)
   - Proxy support for blocked connections (Cloudflare Workers)

2. **Drawing Tools**
   - Point/dot placement on map
   - Line drawing between points
   - Polygon drawing mode
   - Distance measurement mode

3. **UI System**
   - Toolbar with map controls
   - Button-based tool selection
   - Distance text labels
   - Map style switching buttons

## Project Structure

### Main Scripts

#### Map Controllers
- **`SlippyMapController.cs`** - Main map controller with style switching
  - Handles tile loading, drag, zoom
  - Supports OSM, Terrain, Roadmap styles
  - 5x5 tile grid system
  
- **`SlippyMapController_proxy.cs`** - Proxy version for blocked connections
  - Uses Cloudflare Workers proxy: `https://itsensing.hanhandityagw.workers.dev`
  - Supports 6 map styles: OSM, Roadmap, TerrainGoogle, TerrainTopo, Satellite, Hybrid
  
- **`SlippyMapController_noproxy.cs`** - Direct connection version
  
- **`MapController.cs`** - Simpler map implementation (3x3 grid, OpenStreetMap only)

#### Drawing Tools
- **`Pentool.cs`** - Main drawing tool controller
  - Handles mouse clicks within input area
  - Creates dots and lines
  - Manages dot/line prefabs
  - Clears all drawings on deactivation
  
- **`LineController.cs`** - Manages line rendering and distance labels
  - Uses LineRenderer for visual lines
  - Calculates cumulative distances
  - Shows/hides distance labels based on DistanceMode
  - Updates labels dynamically
  
- **`DistanceButton.cs`** - Controls distance measurement mode
  - Toggles DistanceMode on/off
  - Activates/deactivates pentool
  - Changes button color (green when active, white when inactive)
  - Clears drawings when deactivated

#### UI Components
- **`ToggleButton.cs`** - Simple toggle for multiple GameObjects
  - Toggles active state of target objects
  - Uses first target's state to determine toggle direction
  
- **`ButtonTooltip.cs`** - Tooltip system for buttons
  - Shows tooltip on hover (with delay)
  - Uses TooltipManager singleton
  - Positioned below button
  
- **`TooltipManager.cs`** - Singleton manager for tooltips
  - Shows/hides tooltip messages
  - Manages tooltip display

### Key Components

#### Prefabs
- `Dot Prefabs.prefab` - Point markers on map
- `Line Prefabs.prefab` - Line renderer objects
- `Distance Text.prefab` - Text labels showing distances
- `Button/*` - Various UI buttons
- `WindowPref.prefab` - Window UI elements

#### Scenes
- `Prototype 1.unity`
- `Prototype 2.unity`
- `Prototype 3.unity`
- `Prototype redesign.unity`
- `SampleScene.unity`

## Technical Details

### Map System Architecture

**Tile System:**
- Uses slippy map tile coordinates (x, y, z)
- Standard tile size: 256x256 pixels
- Grid size: 5x5 tiles (main controller) or 3x3 (simple controller)
- Tiles loaded via UnityWebRequest

**Coordinate Conversion:**
- Latitude/Longitude ↔ Tile coordinates
- Mercator projection math
- Center tile tracking for navigation

**Map Styles:**
- **OSM**: `https://tile.openstreetmap.org/{z}/{x}/{y}.png`
- **Google Roadmap**: `https://mt0.google.com/vt/lyrs=m&hl=en&x={x}&y={y}&z={z}`
- **Google Terrain**: `https://mt0.google.com/vt/lyrs=p&hl=en&x={x}&y={y}&z={z}`
- **Satellite/Hybrid**: Google Maps API variants

### Drawing System

**Input Handling:**
- Mouse click detection within `inputAreaImage` RectTransform
- Screen-to-world coordinate conversion
- Supports multiple canvas render modes (Overlay, Camera, World Space)

**Distance Calculation:**
- Cumulative distance from first point
- Euclidean distance between consecutive points
- Displayed in 2 decimal places (e.g., "123.45")
- Labels positioned above dots with vertical offset

**State Management:**
- `DistanceMode`: Controls whether distance labels are shown
- `DrawPolygonMode`: Future polygon drawing feature
- `isActive`: Button state (active/inactive)

### Dependencies

**Unity Packages:**
- Unity Input System (1.16.0) - For mouse/keyboard input
- Unity Visual Scripting (1.9.8)
- TextMesh Pro - For distance text labels
- Unity UI (2.0.0) - For UI elements

## Known Issues / TODO

From README.md:
- ⚠️ Style "Terrain" still not accessible
- ⚠️ Proxy needed if Google blocks connections
- UI improvements needed

## Code Patterns

### Common Patterns Used:
1. **Coroutine-based async loading** - For tile downloads
2. **Dictionary-based tile management** - `Dictionary<Vector2Int, RawImage>`
3. **State flags** - `dragging`, `loading`, `mapDirty` for optimization
4. **Button helper methods** - `SetStyleOSM()`, `SetStyleTerrain()`, etc.
5. **FindObjectOfType caching** - In LineController for DistanceButton reference

### Input Handling:
- Uses Unity Input System (`Mouse.current`)
- Checks if mouse is over input area before processing
- Drag detection with `wasPressedThisFrame` / `wasReleasedThisFrame`

## Future Development Notes

- Polygon drawing mode is declared but not fully implemented
- Distance calculation is currently 2D (screen space), may need geodetic conversion for real-world distances
- Map tile caching could be improved
- Multiple map controller variants exist - may need consolidation

