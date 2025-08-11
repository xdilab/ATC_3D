// CameraHUDBinder.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraHUDBinder : MonoBehaviour
{
    [Header("HUD (leave empty to auto-use FlightLoader's)")]
    public Transform hudPanel;
    public GameObject hudEntryPrefab;

    [Header("Which cameras to list")]
    public bool includeGroundCams = true;   // GCam_###
    public bool includeChaseCams  = true;   // Cam_<FLIGHT_ID>
    public string groundPrefix = "GCam_";
    public string chasePrefix  = "Cam_";

    [Header("Optional parent filter for ground cams")]
    [Tooltip("If set, ground cams will be collected ONLY from this parent")]
    public Transform groundCamsParent;

    [Header("Row naming")]
    public string cameraButtonPrefix = "CAMBTN_"; // so we can clear/rebuild safely

    void Start()
    {
        if (!hudPanel || !hudEntryPrefab)
        {
            var fl = FindObjectOfType<FlightLoader>();
            if (fl)
            {
                if (!hudPanel) hudPanel = fl.hudPanel;
                if (!hudEntryPrefab) hudEntryPrefab = fl.hudEntryPrefab;
            }
        }

        if (!hudPanel || !hudEntryPrefab)
        {
            Debug.LogWarning("[CameraHUDBinder] Missing hudPanel or hudEntryPrefab.");
            return;
        }

        BuildCameraButtons();
    }

    void BuildCameraButtons()
    {
        for (int i = hudPanel.childCount - 1; i >= 0; i--)
        {
            var c = hudPanel.GetChild(i);
            if (c.name.StartsWith(cameraButtonPrefix)) Destroy(c.gameObject);
        }

        var cams = FindAllRelevantCameras();
        foreach (var cam in cams)
        {
            var row = Instantiate(hudEntryPrefab, hudPanel);
            row.name = cameraButtonPrefix + cam.name;

            var txt = row.GetComponentInChildren<TMP_Text>(true);
            if (txt) txt.text = cam.name;

            var btn = row.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => SwitchTo(cam));
        }
    }

    List<Camera> FindAllRelevantCameras()
    {
        var outList = new List<Camera>();

        if (includeGroundCams)
        {
            if (groundCamsParent)
            {
                var childCams = groundCamsParent.GetComponentsInChildren<Camera>(true);
                foreach (var c in childCams)
                {
                    if (!c || !c.gameObject.scene.IsValid()) continue;
                    if (c.name.StartsWith(groundPrefix)) outList.Add(c);
                }
            }
            else
            {
                var all = FindAllCamerasIncludeInactive();
                foreach (var c in all)
                {
                    if (!c || !c.gameObject.scene.IsValid()) continue;
                    if (c.name.StartsWith(groundPrefix)) outList.Add(c);
                }
            }
        }

        if (includeChaseCams)
        {
            var all = FindAllCamerasIncludeInactive();
            foreach (var c in all)
            {
                if (!c || !c.gameObject.scene.IsValid()) continue;
                if (c.name.StartsWith(chasePrefix)) outList.Add(c);
            }
        }

        return outList;
    }

    void SwitchTo(Camera target)
    {
        var all = FindAllCamerasIncludeInactive();
        foreach (var c in all)
        {
            if (!c || !c.gameObject.scene.IsValid()) continue;
            c.enabled = false;
            c.rect = new Rect(0, 0, 1, 1);
            c.tag = "Untagged";
            var al = c.GetComponent<AudioListener>();
            if (al) al.enabled = false;
        }

        target.enabled = true;
        target.tag = "MainCamera";
        var tal = target.GetComponent<AudioListener>();
        if (!tal) tal = target.gameObject.AddComponent<AudioListener>();
        tal.enabled = true;
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
