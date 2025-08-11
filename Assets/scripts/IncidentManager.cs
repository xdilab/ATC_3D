using System.Collections;
using System.IO;
using UnityEngine;

public class IncidentManager : MonoBehaviour
{
    [Header("Refs")]
    public GeoMapper       geo;             // for lat/lon/alt enrichment
    public CameraDirector  cameraDirector;  // selects & aims the CCTV rig
    public CaptureService  capture;         // your single‐stream recorder

    [Header("Recording Settings")]
    [Tooltip("Folder under persistentDataPath where clips & logs go")]
    public string outputDir = "Captures";
    [Tooltip("Seconds to keep recording after Cleared")]
    public float  postRollSec = 5f;

    void Awake()
    {
        // ensure the output directory exists
        Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, outputDir));
        if (capture == null)
            Debug.LogWarning("[IncidentManager] No CaptureService assigned!");
    }

    public void Report(IncidentEvent e)
    {
        Debug.Log($"[IncidentManager] ▶ Event {e.incidentId} phase={e.phase}");

        // 1) Geo‐enrich
        if (geo != null)
        {
            Vector3 local = geo.worldRoot
                ? geo.worldRoot.InverseTransformPoint(e.worldPos)
                : e.worldPos;
            var (lat, lon, alt) = geo.UnityToLatLonAlt(local);
            e.lat = lat; e.lon = lon; e.alt = alt;
            Debug.Log($"[IncidentManager]    Enriched to lat={lat:F6}, lon={lon:F6}, alt={alt:F2}");
        }

        // 2) Persist JSONL
        string dir = Path.Combine(Application.persistentDataPath, outputDir, e.incidentId);
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "events.jsonl");
        File.AppendAllText(path, JsonUtility.ToJson(e) + "\n");
        Debug.Log($"[IncidentManager]    Logged event to {path}");

        // 3) Camera selection & hook (once per event)
        if (e.phase == IncidentPhase.Predicted || e.phase == IncidentPhase.Live)
        {
            if (cameraDirector != null)
            {
                cameraDirector.Focus(e);
                Debug.Log($"[IncidentManager]    cameraDirector.Focus({e.phase})");
            }
            else
            {
                // Fallback: pick a camera that actually sees the incident (or rotate nearest), then make it MainCamera
                Camera cam = PickBestCamera(e.worldPos, "GCam_", "Cam_");
                if (cam)
                {
                    AimCamera(cam, e.worldPos, 45f, 75f);
                    ActivateAsMain(cam);
                    Debug.Log($"[IncidentManager]    Fallback cam -> {cam.name}");
                }
                else
                {
                    Debug.LogWarning("[IncidentManager]    No suitable camera found near incident.");
                }
            }
        }

        // 4) Drive the CaptureService
        if (capture != null)
        {
            if (e.phase == IncidentPhase.Predicted || e.phase == IncidentPhase.Live)
            {
                Debug.Log($"[IncidentManager]    START capture: {e.incidentId}");
                capture.StartCapture(e.incidentId);
            }
            else if (e.phase == IncidentPhase.Cleared)
            {
                Debug.Log($"[IncidentManager]    STOP capture in {postRollSec}s: {e.incidentId}");
                StartCoroutine(StopAfterDelay(e.incidentId, postRollSec));
            }
        }
    }

    IEnumerator StopAfterDelay(string incidentId, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"[IncidentManager]    STOP capture: {incidentId}");
        capture.StopCapture(incidentId);
    }

    // ──────────────────────────────── Fallback camera helpers ────────────────────────────────
    // Minimal, self-contained (no external dependencies). Uses name prefixes only.

    Camera PickBestCamera(Vector3 focusWorld, string groundPrefix, string chasePrefix)
    {
        Camera[] all = FindAllCamerasIncludeInactive();
        Camera best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < all.Length; i++)
        {
            var cam = all[i];
            if (!cam || !cam.gameObject.scene.IsValid()) continue;

            string n = cam.name;
            bool candidate = n.StartsWith(groundPrefix) || n.StartsWith(chasePrefix);
            if (!candidate) continue;

            float score = ScoreCameraForPoint(cam, focusWorld);
            if (score > bestScore) { bestScore = score; best = cam; }
        }

        // If the best camera doesn't currently see the point, rotate it to look at the focus.
        if (best && bestScore < 0f)
        {
            var t = best.transform;
            t.rotation = Quaternion.LookRotation((focusWorld - t.position).normalized, Vector3.up);
            // re-evaluate (optional)
            bestScore = ScoreCameraForPoint(best, focusWorld);
        }

        return best;
    }

    void AimCamera(Camera cam, Vector3 focus, float minVFov, float maxVFov)
    {
        if (!cam) return;
        cam.transform.LookAt(focus);
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minVFov, maxVFov);
    }

    void ActivateAsMain(Camera cam)
    {
        if (!cam) return;

        // Disable all cameras & their audio, then enable the chosen one and tag it MainCamera.
        var all = FindAllCamerasIncludeInactive();
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (!c || !c.gameObject.scene.IsValid()) continue;
            c.enabled = false;
            c.tag = "Untagged";
            var al = c.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }

        cam.enabled = true;
        cam.tag = "MainCamera";
        var myAL = cam.GetComponent<AudioListener>() ?? cam.gameObject.AddComponent<AudioListener>();
        myAL.enabled = true;
    }

    float ScoreCameraForPoint(Camera cam, Vector3 world)
    {
        // Positive score = on screen (higher is closer to center). Negative = off-screen/behind.
        Vector3 v = cam.WorldToViewportPoint(world);
        if (v.z <= 0f) return -1000f; // behind

        float dx = v.x - 0.5f;
        float dy = v.y - 0.5f;
        bool onScreen = (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f);
        float centerBias = -(dx * dx + dy * dy); // 0 at center, negative outward

        // Slight preference for closer cameras (in XZ)
        Vector3 cp = cam.transform.position; cp.y = 0f;
        Vector3 wp = world; wp.y = 0f;
        float proximity = -Vector3.Distance(cp, wp) * 0.001f;

        return (onScreen ? 10f : -10f) + centerBias + proximity;
    }

    static Camera[] FindAllCamerasIncludeInactive()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<Camera>(true);
#endif
    }
}
