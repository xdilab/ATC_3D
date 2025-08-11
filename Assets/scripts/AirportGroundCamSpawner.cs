// AirportGroundCamSpawner.cs
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AirportGroundCamSpawner : MonoBehaviour
{
    [Header("Spawn Area (choose one)")]
    public BoxCollider areaBounds;
    public Vector3 areaCenter = Vector3.zero;
    public Vector2 areaSizeXZ = new Vector2(4000, 4000);

    [Header("Spawn Settings")]
    [Range(1, 500)] public int cameraCount = 100;
    [Tooltip("Maximum world Y for cams (will clamp to this)")]
    public float maxY = 1f;
    public bool randomYaw = true;
    public string namePrefix = "GCam_";

    [Header("Parenting")]
    [Tooltip("If set, spawned cameras become children of this transform")]
    public Transform parentForCams;
    [Tooltip("If no parent is provided, a child GameObject with this name will be created under THIS object")]
    public string parentName = "GroundCams";

    [Header("Camera Defaults")]
    public float fieldOfView = 60f;
    public float nearClip = 0.1f;
    public float farClip = 5000f;

#if UNITY_EDITOR
    [ContextMenu("Generate Ground Cameras")]
    public void Generate()
    {
        Transform parent = ResolveParent();

        // Cleanup any previous batch under the parent
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyImmediate(parent.GetChild(i).gameObject);

        for (int i = 0; i < cameraCount; i++)
        {
            Vector3 pos = SampleXZ();
            pos.y = maxY;

            var go = new GameObject($"{namePrefix}{i:000}");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            if (randomYaw) go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.depth = 0;

            var al = go.AddComponent<AudioListener>();
            al.enabled = false;
        }

        Debug.Log($"[AirportGroundCamSpawner] Generated {cameraCount} cameras at Y={maxY} under '{parent.name}'.");
    }

    [ContextMenu("Group Existing GCam_* Under Parent")]
    public void GroupExisting()
    {
        Transform parent = ResolveParent();
        var all = FindAllCamerasIncludeInactive();
        int moved = 0;
        foreach (var c in all)
        {
            if (!c || !c.gameObject.scene.IsValid()) continue;
            if (!c.name.StartsWith(namePrefix)) continue;
            if (c.transform.parent == parent) continue;
            Undo.SetTransformParent(c.transform, parent, "Reparent Ground Cameras");
            moved++;
        }
        Debug.Log($"[AirportGroundCamSpawner] Grouped {moved} cameras under '{parent.name}'.");
    }

    static Camera[] FindAllCamerasIncludeInactive()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Object.FindObjectsOfType<Camera>(true);
#endif
    }
#endif

    Transform ResolveParent()
    {
        if (parentForCams) return parentForCams;
        var t = transform.Find(parentName);
        if (t) return t;
        var go = new GameObject(parentName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    Vector3 SampleXZ()
    {
        if (areaBounds)
        {
            var b = areaBounds.bounds;
            float x = Random.Range(b.min.x, b.max.x);
            float z = Random.Range(b.min.z, b.max.z);
            return new Vector3(x, 0f, z);
        }
        else
        {
            float x = areaCenter.x + Random.Range(-areaSizeXZ.x * 0.5f, areaSizeXZ.x * 0.5f);
            float z = areaCenter.z + Random.Range(-areaSizeXZ.y * 0.5f, areaSizeXZ.y * 0.5f);
            return new Vector3(x, 0f, z);
        }
    }
}
