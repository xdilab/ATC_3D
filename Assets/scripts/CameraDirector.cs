using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CameraRig
{
    public string name;
    public Camera camera;
    public Transform pivot;   // optional PTZ pivot
    public float fov = 60f;
    public float weight = 1f; // manual bias
}

public class CameraDirector : MonoBehaviour
{
    public List<CameraRig> rigs = new();
    public LayerMask occluders = 0; // optional; leave 0 to skip LOS checks

    [Header("Safety Defaults")]
    public bool forceCameraDefaults = true;
    public LayerMask defaultCullingMask = ~0;                // Everything
    public CameraClearFlags defaultClearFlags = CameraClearFlags.Skybox;
    public Color defaultBackgroundColor = Color.black;
    public bool skipUrpOverlayCameras = true;

    [Header("Base camera (URP)")]
    public Camera baseCamera;                // keep this enabled if you use overlays
    public bool alwaysEnableBaseCamera = true;

    [Header("Filtering (ignore plane cams; use only marked airport cams)")]
    public bool ignoreCamerasUnderAircraft = true;   // skip any camera parented under an aircraft
    public bool requireAirportMarker = true;         // only use cams with AirportCameraMarker
    public bool ignoreTag = true;                    // skip cams with this tag (optional)
    public string planeCameraTag = "PlaneCamera";    // tag your chase cams if you like
    public string ignoreNameContains = "Chase";      // skip if camera name contains this (optional)
    public List<Camera> explicitIgnore = new();      // drag any specific cams to ignore

    [Header("Capture hookup (directly set the recorder camera)")]
    public CaptureService capture;                   // drag IncidentSystem's CaptureService here

    [Header("Integration")]
    public bool tagSelectedAsMainCamera = false;     // default OFF since we drive CaptureService directly
    public float selectionCooldownSec = 0.05f;       // very short cooldown to ensure we catch incidents

    
    [Header("Debug")]
    public bool logSelections = false;

    Camera _activeCam;
    float _nextSwitchTime = 0f;

    // Automatically rebuilds the rig list from all cameras with the spawner prefix
    public void RefreshRigs()
    {
        rigs.Clear();
        var cams = GameObject.FindObjectsOfType<Camera>(true); // include inactive
        foreach (var cam in cams)
        {
            if (!cam.name.StartsWith("GCam_")) continue;   // match spawner prefix
            rigs.Add(new CameraRig {
                name   = cam.name,
                camera = cam,
                pivot  = cam.transform,
                fov    = cam.fieldOfView,
                weight = 1f
            });
        }
    }

    void Awake()
    {
        RefreshRigs();     // auto-populate at startup
    }

    public void Focus(IncidentEvent e)
    {
        if (rigs.Count == 0) return;

        // Cooldown to avoid thrash selecting different rigs multiple times per second
        if (Time.time < _nextSwitchTime && _activeCam) return;

        float bestScore = float.NegativeInfinity;
        CameraRig best = null;

        foreach (var r in rigs)
        {
            if (!r.camera) continue;

            // --- IGNORE plane/undesired cameras ---
            if (explicitIgnore.Contains(r.camera)) continue;
            if (ignoreTag && r.camera.CompareTag(planeCameraTag)) continue;
            if (!string.IsNullOrEmpty(ignoreNameContains) &&
                r.camera.name.IndexOf(ignoreNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (ignoreCamerasUnderAircraft && r.camera.GetComponentInParent<ClearanceProbe>()) continue; // plane-mounted
            if (requireAirportMarker && !r.camera.GetComponentInParent<AirportCameraMarker>()) continue;  // not marked CCTV

            // Skip URP Overlay cameras if requested
            if (skipUrpOverlayCameras && IsUrpOverlay(r.camera)) continue;

            // Improved scoring: prioritize proximity and clear LOS
            Vector3 to = e.worldPos - r.camera.transform.position;
            float dist = to.magnitude;
            float score = 0f;
            // Stronger proximity weight
            score += -dist * 1.5f;
            // Manual weight
            score += r.weight * 10f;
            // Line of sight bonus
            if (occluders != 0 && !Physics.Raycast(r.camera.transform.position, to.normalized, dist, occluders))
                score += 50f;

            if (score > bestScore) { bestScore = score; best = r; }
        }

        // Fallback if all filtered out
        if (best == null)
        {
            foreach (var r in rigs)
            {
                if (!r.camera) continue;
                if (explicitIgnore.Contains(r.camera)) continue;
                if (ignoreTag && r.camera.CompareTag(planeCameraTag)) continue;
                if (ignoreCamerasUnderAircraft && r.camera.GetComponentInParent<ClearanceProbe>()) continue;
                if (skipUrpOverlayCameras && IsUrpOverlay(r.camera)) continue;
                best = r; break;
            }
            if (best == null) return;
        }

        // Enable best, disable others
        foreach (var r in rigs) if (r.camera) r.camera.enabled = (r == best);

        // Keep a Base camera alive in URP (prevents overlay-only green screens)
        if (alwaysEnableBaseCamera && baseCamera) baseCamera.enabled = true;

        // Safe defaults to avoid green screens / missing world
        if (forceCameraDefaults && best.camera)
        {
            best.camera.clearFlags = defaultClearFlags;
            best.camera.backgroundColor = defaultBackgroundColor;
            best.camera.cullingMask = defaultCullingMask;
            best.camera.targetTexture = null;
        }

        // Optional: tag the selected rig as MainCamera (not required when using capture hook)
        if (tagSelectedAsMainCamera)
        {
            foreach (var r in rigs) if (r.camera) r.camera.tag = (r == best) ? "MainCamera" : "Untagged";
            if (baseCamera && baseCamera != best.camera) baseCamera.tag = "Untagged";
        }

        // PTZ aim - center the incident in camera view
        var pivot = best.pivot ? best.pivot : best.camera.transform;
        Vector3 incidentPos = e.worldPos;
        
        // Add height offset for better viewing angle (looking slightly down)
        Vector3 cameraToIncident = incidentPos - pivot.position;
        float distance = cameraToIncident.magnitude;
        
        // Elevate the target point slightly for better framing
        Vector3 targetLookPos = incidentPos + Vector3.up * 2f;
        
        // Point camera directly at the incident
        Vector3 lookDirection = (targetLookPos - pivot.position).normalized;
        if (lookDirection.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            pivot.rotation = Quaternion.Slerp(pivot.rotation, targetRotation, 0.5f); // Faster rotation
        }

        // Adjust field of view based on distance to incident for optimal framing
        float optimalFov = CalculateOptimalFov(distance);
        best.camera.fieldOfView = Mathf.Lerp(best.camera.fieldOfView, optimalFov, 0.3f);

        // ---- Capture hookup: record THIS CCTV camera directly ----
        if (capture)
        {
            capture.sourceCamera = best.camera;
            capture.followMainCamera = false; // don't follow MainCamera; record the selected rig
        }

        _activeCam = best.camera;
        _nextSwitchTime = Time.time + selectionCooldownSec;

        if (logSelections) Debug.Log($"[CameraDirector] Selected rig: {best.name ?? best.camera.name}");
    }

    // Calculate optimal field of view based on distance to incident
    float CalculateOptimalFov(float distance)
    {
        // Close incidents need wider FOV to see more context
        // Far incidents need narrower FOV to zoom in
        if (distance < 50f) return 75f;        // Wide view for close incidents
        else if (distance < 150f) return 60f;  // Medium view for medium distance
        else if (distance < 300f) return 45f;  // Narrow view for far incidents
        else return 35f;                       // Very narrow for very far incidents
    }

    // Detect URP Overlay without a hard URP dependency (reflection)
    static bool IsUrpOverlay(Camera cam)
    {
        var t = System.Type.GetType(
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
        if (t == null) return false;
        var extra = cam.GetComponent(t);
        if (extra == null) return false;
        var prop = t.GetProperty("renderType");
        if (prop == null) return false;
        var val = prop.GetValue(extra, null);
        return val != null && val.ToString().Contains("Overlay");
    }
}
