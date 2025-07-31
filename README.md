
# ATC_3D
# ✈️ 3D Digital Twin of GSO Airport in Unity: First Phase Development and Future Vision

---

## 📋 Project Overview

This project represents the first phase in creating a **3D Digital Twin of Piedmont Triad International Airport (GSO)** using **Unity**.  
The purpose is to build a foundation for **future research**, such as:
- Predictive maintenance
- AI-based incident prevention
- Real-time monitoring
- Simulation-driven airport planning

This version implements:
- Parsing real-world ATC data (CSV)
- Displaying aircraft with live position updates
- Multiple camera systems (top view, chase view, etc.)
- A **HUD** for aircraft info and time tracking
- A **time scrubber** to replay events

---

## ✅ Current Features

✔ **Real-world positioning:** Converts latitude/longitude to Unity coordinates  
✔ **Flight playback system:** Aircraft movement based on ATC snapshot data  
✔ **HUD Overlay:**  
   - Aircraft ID and coordinates  
   - Altitude  
   - Switch between plane-specific cameras  
✔ **Global Simulation Clock:** Displays time based on ATC records  
✔ **Playback Controls:**  
   - Play / Pause buttons  
   - Speed slider  
   - Scrub bar for timeline navigation  

---
---

## Main Scripts and Components

The following scripts are key to the functionality of the project:

### 1. `FlightLoader.cs`
- **Purpose**: Loads aircraft flight data from CSV files and animates aircraft along predefined routes.
- **Features**:
  - Spawns aircraft prefabs based on real-world GPS data.
  - Adds visual paths using `LineRenderer`.
  - Creates HUD elements and labels to identify aircraft.
  - Optionally attaches chase cameras to each plane.

### 2. `ClearanceProbe.cs`
- **Purpose**: Attached to each aircraft to detect proximity and clearance incidents.
- **Features**:
  - Continuously monitors distances between aircraft.
  - Detects incidents based on configurable clearance thresholds (`warnClearanceM`, `incidentClearanceM`).
  - Reports incidents to `IncidentManager`.

### 3. `IncidentManager.cs`
- **Purpose**: Manages incident lifecycle (detection, recording, and cleanup).
- **Features**:
  - Receives and logs incidents with geolocation metadata.
  - Directs `CameraDirector` to focus cameras on active incidents.
  - Controls `CaptureService` to record video footage of incidents.

### 4. `CameraDirector.cs`
- **Purpose**: Manages camera rigs around the airport to automatically select and orient the best camera view for an ongoing incident.
- **Features**:
  - Automatically selects the optimal camera based on position and line-of-sight.
  - Integrates seamlessly with `CaptureService` for video recording.
  - Configurable camera behavior (filtering, cooldown, URP support).

### 5. `CaptureService.cs`
- **Purpose**: Handles video capture, buffering frames, and encoding videos of incidents.
- **Features**:
  - Captures high-quality video from Unity cameras.
  - Implements pre-roll buffering to capture frames before incidents occur.
  - Encodes captured frames into MP4 format using FFmpeg.

### 6. `GeoMapper.cs`
- **Purpose**: Converts real-world geographical coordinates (latitude, longitude, altitude) to Unity scene coordinates and vice versa.
- **Features**:
  - Performs affine transformations based on reference control points.
  - Provides precise spatial mapping crucial for accurate simulation.

### 7. `ZoneVolume.cs`
- **Purpose**: Defines sensitive zones (e.g., runways) that detect incursions.
- **Features**:
  - Triggers incidents when aircraft enter or exit predefined zones.
  - Reports zone-related incidents to the `IncidentManager`.

---

## 🛠 Tools & Resources

| Tool / Software      | Version       | Purpose                                   | Link |
|----------------------|-------------|------------------------------------------|------|
| Unity               | 2022.3 LTS  | 3D environment & simulation engine      | [Unity](https://unity.com/) |
| Blender             | 4.0         | 3D modeling of airport layout           | [Blender.org](https://www.blender.org/) |
| BlenderGIS Addon    | 2.1.9       | Importing geospatial OSM data           | [BlenderGIS](https://github.com/domlysz/BlenderGIS) |
| Overpass Turbo      | Web         | Query OpenStreetMap airport data        | [Overpass Turbo](https://overpass-turbo.eu/) |

---

## 📚 Datasets Used

- **snapshot_final_24hr(in).csv**: Original ATC dataset (all 24hr traffic)  
- **convertedatc.csv**: Cleaned dataset with selected columns for Unity playback  

---

## ⚙️ Setup Instructions

### ✅ 1. Install Dependencies
- **Unity 2022.3 LTS**
- **Visual Studio** (with Unity workload)
- **TextMeshPro** (included in Unity)
- **Python to handle raw atc logs** (`pip install pandas openpyxl`)
- **Install FFmpeg**  
   Required for video encoding.
   - Windows:  
     ```
     winget install --id=FFmpeg.FFmpeg --source=winget
     ```
   - macOS:  
     ```
     brew install ffmpeg
     ```
   - Linux (Debian/Ubuntu):  
     ```
     sudo apt install ffmpeg
     ```


### ✅ 2. Open Project in Unity
1. Clone this repository or download the ZIP.
2. Open the `ATC_3D` folder in **Unity Hub**.
3. Load the main scene.
▶️ [Watch the Demo Video on YouTube](https://youtu.be/mtaoOmualhk)

### ✅ 3. Configure Prefabs & UI
**Should come preconfigured**
- Assign `planePrefab`, `planeLabelPrefab`, and `hudEntryPrefab` in **FlightLoader**.
- Ensure **HUD Canvas** contains:
  - Scroll View for aircraft list
  - Playback Controls (Play, Pause, Speed Slider, Time Scrubber)
  - Clock Label

### ✅ 4. Import ATC Data
- Place `convertedatc.py` inside `StreamingAssets` folder.
- Format:
```
FlightID, Time(sec), Latitude, Longitude, Altitude
```
### ✅ How to Convert Raw ATC Data → Unity CSV
Use `CSVRelatedParts/logsconverter.py` for converting.

1. Place `snapshot_final_24hr.xlsx` and `logsconverter.py` in the same folder (`CSVRelatedParts`).
2. Run:
```bash
python logsconverter.py
```
3. Output: `convertedatc.csv`
4. Make sure it is placed in **Unity → Assets → StreamingAssets/**

snapshot_final_24hr(in).csv is already converted and ready to go labeled as `convertedatc.py`
---
---

## ▶ How to Run Simulation
1. **Play** in Unity.
2. Aircraft will appear at real-world positions and move based on CSV timeline.
3. Use:
   - **Play / Pause** to control simulation.
   - **Slider** to adjust playback speed.
   - **Scrub bar** to move through the day.

---

## 🔍 Expected Output
- Aircraft move realistically across the GSO airport map.
- Labels show **Flight ID + Coordinates**.
- Time updates according to ATC data.
- Clicking a plane in HUD switches to its camera.

---

---

## Running the Simulation

- Place your flight data CSV in `StreamingAssets`.
- Configure the desired parameters in `FlightLoader`, `ClearanceProbe`, and other scripts via the Unity Inspector.
- Press **Play** in Unity to start the simulation.
- Check the Unity Console for incident reports and debug logs.
- Recorded incident videos will appear in your project's persistent data directory (`AppData`/`Application.persistentDataPath`).

---

## Troubleshooting

- Ensure FFmpeg is correctly installed and accessible from your PATH.
- Verify aircraft tags (`Aircraft`) and markers (`ClearanceTarget`) for proper collision detection.

---

### Notes

- Adjust clearance thresholds and other parameters in the Inspector or scripts directly to fine-tune incident sensitivity.

---

---

# 🚀 Towards a real-time, predictive digital twin for smarter airports!
