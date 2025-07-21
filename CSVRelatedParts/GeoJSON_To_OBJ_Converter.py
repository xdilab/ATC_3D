
import json
import os
from shapely.geometry import Polygon
import trimesh

# Configuration
GEOJSON_PATH = "gsojosm.geojson"  # Replace with your actual GeoJSON file path
OUTPUT_DIR = "GSO_Runway_Meshes"
EXTRUDE_HEIGHT = 1.0  # Meters

# Coordinate conversion function (lat/lon to Unity-style XZ)
def convert_latlon_to_unity(lat, lon, origin_lat=36.105, origin_lon=-79.940):
    scale_factor = 111000  # meters per degree approx
    x = (lon - origin_lon) * scale_factor
    z = (lat - origin_lat) * scale_factor
    return x, z

# Load GeoJSON
with open(GEOJSON_PATH, "r") as f:
    data = json.load(f)

# Prepare output folder
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Process features
for i, feature in enumerate(data.get("features", [])):
    geometry = feature.get("geometry", {})
    props = feature.get("properties", {}) or {}
    name = props.get("name", f"Feature_{i}").replace(" ", "_")

    if geometry.get("type") != "Polygon":
        continue

    coords = geometry.get("coordinates", [])[0]
    transformed = [convert_latlon_to_unity(lat, lon) for lon, lat in coords]
    polygon = Polygon(transformed)

    if not polygon.is_valid:
        print(f"Skipping invalid polygon: {name}")
        continue

    try:
        mesh = trimesh.creation.extrude_polygon(polygon, EXTRUDE_HEIGHT)
        mesh.export(os.path.join(OUTPUT_DIR, f"{name}.obj"))
        print(f"Exported: {name}.obj")
    except Exception as e:
        print(f"Error processing {name}: {e}")
