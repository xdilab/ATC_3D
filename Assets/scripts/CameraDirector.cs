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
    public float selectionCooldownSec = 0.5f;        // avoid rapid toggles

    [Header("Debug")]
    public bool logSelections = false;

    Camera _activeCam;
    float _nextSwitchTime = 0f;

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

            // Score by distance + LOS + manual weight
            Vector3 to = e.worldPos - r.camera.transform.position;
            float dist = to.magnitude;
            float score = -dist * 0.5f + r.weight * 10f;

            if (occluders != 0 && !Physics.Raycast(r.camera.transform.position, to.normalized, dist, occluders))
                score += 25f;

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

        // PTZ aim
        var pivot = best.pivot ? best.pivot : best.camera.transform;
        Vector3 look = e.worldPos - pivot.position; look.y += 3f;
        if (look.sqrMagnitude > 0.1f)
            pivot.rotation = Quaternion.Slerp(pivot.rotation, Quaternion.LookRotation(look), 0.25f);

        best.camera.fieldOfView = Mathf.Lerp(best.camera.fieldOfView, best.fov, 0.25f);

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
