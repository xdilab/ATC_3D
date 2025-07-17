/*  CameraAutoSpawner.cs  --------------------------------------------------
 *  Drops six realistically-placed cameras for KGSO.
 *  - OverheadCam   (orthographic map, enabled at start, MainCamera)
 *  - TowerCam
 *  - Rwy23R_EndCam
 *  - Rwy05L_EndCam
 *  - IntersectionCam
 *  - ApronWestCam
 *
 *  Attach to an empty GameObject (e.g. "CameraManager"),
 *  tick Spawn-On-Play, press Play once.  All cameras stay in the scene
 *  after you stop; untick or remove component afterward.
 * -----------------------------------------------------------------------*/
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class CameraAutoSpawner : MonoBehaviour
{
    [Tooltip("Run the spawn logic next time you press Play in the editor.")]
    public bool spawnOnPlay = true;

    [Header("Reference lat/lon (same as FlightLoader)")]
    public double refLat = 36.0920;
    public double refLon = -79.9357;

    /* ------------------------------------------------------------------ */
    struct CamSpec
    {
        public string name;
        public double lat, lon, alt;     // WGS-84 centre of lens
        public bool   ortho;             // true = orthographic
        public float  sizeOrFov;         // orthoSize OR fieldOfView
    }

    readonly CamSpec[] specs =
    {
        new CamSpec{ name="OverheadCam",    lat=36.0920,  lon=-79.9357,  alt=800,
                     ortho=true,  sizeOrFov=450 },

        new CamSpec{ name="TowerCam",       lat=36.09731, lon=-79.93902, alt=40,
                     ortho=false, sizeOrFov=60  },

        new CamSpec{ name="Rwy23R_EndCam",  lat=36.116433,lon=-79.937136,alt=6,
                     ortho=false, sizeOrFov=35 },

        new CamSpec{ name="Rwy05L_EndCam",  lat=36.099256,lon=-79.959046,alt=6,
                     ortho=false, sizeOrFov=35 },

        new CamSpec{ name="IntersectionCam",lat=36.1006,  lon=-79.9422,  alt=15,
                     ortho=false, sizeOrFov=45 },

        new CamSpec{ name="ApronWestCam",   lat=36.0988,  lon=-79.9485,  alt=25,
                     ortho=true,  sizeOrFov=200 },
    };

    /* ------------------------------------------------------------------ */
    const double mPerLat = 111_320.0;
    double mPerLon => Mathf.Cos((float)refLat * Mathf.Deg2Rad) * 111_320.0;

#if UNITY_EDITOR
    void Start()
    {
        if (!spawnOnPlay || !Application.isPlaying) return;
        SpawnAll();
        spawnOnPlay = false;                     // prevent repeat spawns
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Spawn Cameras Now")]
    void SpawnAll()
    {
        foreach (var s in specs)
        {
            if (GameObject.Find(s.name)) continue;      // already exists

            /* 1 ─ anchor at world-metre position */
            var anchor = new GameObject(s.name + "_Anchor");
            anchor.transform.position = Geo2Unity(s.lat, s.lon, s.alt);

            /* 2 ─ child camera */
            var camGO = new GameObject(s.name);
            camGO.transform.SetParent(anchor.transform, false);
            camGO.transform.forward = Vector3.down;     // look straight down
            var cam = camGO.AddComponent<Camera>();

            if (s.ortho)
            {
                cam.orthographic     = true;
                cam.orthographicSize = s.sizeOrFov;
            }
            else
            {
                cam.fieldOfView      = s.sizeOrFov;
            }

            cam.rect = new Rect(0, 0, 1, 1);            // full-screen

            /* only OverheadCam active + MainCamera at boot */
            bool isOverhead = s.name == "OverheadCam";
            cam.enabled = isOverhead;
            cam.tag     = isOverhead ? "MainCamera" : "Untagged";
        }

        Debug.Log("CameraAutoSpawner: six cameras created.");
    }
#endif

    /* ------------------------------------------------------------------ */
    Vector3 Geo2Unity(double lat,double lon,double alt)
    {
        float x = (float)((lon - refLon) * mPerLon);
        float z = (float)((lat - refLat) * mPerLat);
        return new Vector3(x,(float)alt,z);
    }
}
