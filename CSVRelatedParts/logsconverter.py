#!/usr/bin/env python3
"""
snapshot_final_24hr.xlsx  ➜  convertedatc.csv   (Unity-ready)

• Expects sheet name “in” (the default in your file).  
• Writes: flight_id,timestamp_s,latitude_deg,longitude_deg,altitude_m  
• Converts feet → metres (Altitude Geometric is in feet).  
"""

import ast, csv, re, sys, pathlib
import pandas as pd

SRC  = pathlib.Path("snapshot_final_24hr.xlsx")
DEST = pathlib.Path("convertedatc.csv")

# ------------------------------------------------------------
def hhmmssZ_to_sec(txt: str) -> int:
    """'000230Z' → 150 s since midnight"""
    m = re.fullmatch(r"(\d{2})(\d{2})(\d{2})Z", txt.strip())
    if not m:
        raise ValueError(f"Bad time string: {txt}")
    h, m_, s_ = map(int, m.groups())
    return h*3600 + m_*60 + s_

try:
    df = pd.read_excel(SRC, sheet_name="in", engine="openpyxl")
except Exception as e:
    sys.exit(f"✗ Couldn’t open {SRC}: {e}")

rows_out = []
for _, row in df.iterrows():
    cell = row.get("Aircraft(Obj)", "[]")
    if not isinstance(cell, str) or cell.strip() in ("[]", ""):
        continue

    try:
        aircraft_list = ast.literal_eval(cell)
    except Exception:
        continue

    for ac in aircraft_list:
        try:
            fid  = ac.get("Flight Number", ac["Hex"]).strip()
            t    = hhmmssZ_to_sec(ac["Time"])
            lat  = float(ac["Latitude"])
            lon  = float(ac["Longitude"])

            # altitude is *feet* — convert → metres
            alt_ft = float(ac.get("Altitude Geometric", ac.get("Altitude Barometric", 0)))
            alt_m  = round(alt_ft * 0.3048, 1)

            rows_out.append((fid, t, lat, lon, alt_m))
        except (KeyError, ValueError):
            continue

if not rows_out:
    sys.exit("✗ No rows extracted — check column names / sheet.")

with DEST.open("w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["flight_id", "timestamp_s",
                "latitude_deg", "longitude_deg", "altitude_m"])
    w.writerows(rows_out)

print(f"✓ Wrote {len(rows_out):,} rows → {DEST}")
