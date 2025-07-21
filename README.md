
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
- **flights2.csv**: Cleaned dataset with selected columns for Unity playback  

---

## ⚙️ Setup Instructions

### ✅ 1. Install Dependencies
- **Unity 2022.3 LTS**
- **Visual Studio** (with Unity workload)
- **TextMeshPro** (included in Unity)
- **Python to handle raw atc logs**

### ✅ 2. Open Project in Unity
1. Clone this repository or download the ZIP.
2. Open the `ATC_3D` folder in **Unity Hub**.
3. Load the main scene.

### ✅ 3. Configure Prefabs & UI
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
Use CSVRelatedParts/logsconverter.py for converting snapshot_final_24hr(in).csv is already converted and ready to go labeled as `convertedatc.py`.

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

## 🔮 Future Enhancements
- Conflict detection (planes crossing paths).
- Add runway lighting and weather conditions.
- Support for **real-time ATC feeds**.
- VR/AR support for immersive control tower simulation.

---

# 🚀 Towards a real-time, predictive digital twin for smarter airports!
