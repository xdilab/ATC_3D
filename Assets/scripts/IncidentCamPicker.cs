using UnityEngine;

public static class IncidentCamPicker
{
    // Pick the camera that currently sees the focus point (best), else nearest.
    // If rotateIfNeeded=true and nothing sees it, we'll rotate the nearest to look at the focus.
    public static Camera SelectBest(Vector3 focusWorld, Transform preferParent = null,
                                    string groundPrefix = "GCam_", string chasePrefix = "Cam_",
                                    bool rotateIfNeeded = true)
    {
        Camera best = null;
        float bestScore = float.NegativeInfinity;

        var all = FindAllCameras();
        foreach (var cam in all)
        {
            if (!cam || !cam.gameObject.scene.IsValid()) continue;
            if (preferParent && !cam.transform.IsChildOf(preferParent)) continue;

            // Only consider our ground/chase naming to avoid scene/utility cams
            string n = cam.name;
            bool isCandidate = n.StartsWith(groundPrefix) || n.StartsWith(chasePrefix);
            if (!isCandidate) continue;

            float score = Score(cam, focusWorld);
            if (score > bestScore) { bestScore = score; best = cam; }
        }

        // If no one "sees" the focus and rotation is allowed, aim the nearest one.
        if (best && bestScore < 0f && rotateIfNeeded)
        {
            best.transform.rotation = Quaternion.LookRotation((focusWorld - best.transform.position).normalized, Vector3.up);
            bestScore = Score(best, focusWorld); // should now be >= 0
        }

        return best;
    }

    // Aim at point and keep FOV within bounds
    public static void Aim(Camera cam, Vector3 focus, float minVFov = 45f, float maxVFov = 75f)
    {
        if (!cam) return;
        cam.transform.LookAt(focus);
        cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, minVFov, maxVFov);
    }

    // Make this the camera that CaptureService follows (by tagging MainCamera).
    // Ensures exactly one AudioListener.
    public static void ActivateAsMain(Camera cam)
    {
        if (!cam) return;

        var all = FindAllCameras();
        foreach (var c in all)
        {
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

    // --- helpers ---
    static float Score(Camera cam, Vector3 world)
    {
        // Returns positive if point is on-screen; larger is better (closer to center).
        // Negative means off-screen or behind; smaller (more negative) is worse.
        Vector3 v = cam.WorldToViewportPoint(world);
        if (v.z <= 0f) return -1000f; // behind camera

        // distance from screen center (0.5,0.5)
        float dx = v.x - 0.5f;
        float dy = v.y - 0.5f;

        bool onScreen = v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        float centerBias = -(dx * dx + dy * dy); // 0 at center, negative outward

        // Prefer on-screen strongly, then prefer more centered, then prefer closer to focus
        float distXZ = Vector3.Distance(new Vector3(cam.transform.position.x, 0, cam.transform.position.z),
                                        new Vector3(world.x, 0, world.z));
        float proximity = -distXZ * 0.001f; // small tie-breaker

        return (onScreen ? 10f : -10f) + centerBias + proximity;
    }

    static Camera[] FindAllCameras()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<Camera>(true);
#endif
    }
}
