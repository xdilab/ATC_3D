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
        if (cameraDirector != null && 
           (e.phase == IncidentPhase.Predicted || e.phase == IncidentPhase.Live))
        {
            cameraDirector.Focus(e);
            Debug.Log($"[IncidentManager]    cameraDirector.Focus({e.phase})");
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
}
