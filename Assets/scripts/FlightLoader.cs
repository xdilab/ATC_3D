using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Replays aircraft tracks from a five-column CSV and spawns:
/// <list type="bullet">
///   <item>an animated plane prefab per selected flight</item>
///   <item>a world-space text label above each plane</item>
///   <item>a HUD row (button) that can switch to a chase-camera view</item>
///   <item>a fixed yellow LineRenderer path for visual reference</item>
/// </list>
/// Expected CSV columns (no header order change) (done through script that takes in raw csv):
/// <code>flight_id , seconds_since_midnight , latitude_deg , longitude_deg , altitude_m</code>
/// </summary>
public class FlightLoader : MonoBehaviour
{
    /* ───────────────────────── Public inspector fields ───────────────────────── */

    [Header("Prefabs & Data")]
    [Tooltip("Aircraft model prefab (must face +Z)")]
    public Transform planePrefab;

    [Tooltip("CSV file inside StreamingAssets")]
    public string csvFileName = "flights2.csv";

    [Header("Airport anchor (°)")]
    [Tooltip("Reference latitude for Unity (0,0) origin")]
    public double refLat = 36.0920;
    [Tooltip("Reference longitude for Unity (0,0) origin")]
    public double refLon = -79.9357;

    [Header("UI Prefabs")]
    [Tooltip("World-space label prefab (TextMeshProUGUI)")]
    public GameObject planeLabelPrefab;
    [Tooltip("HUD row prefab (Button + TMP)")]
    public GameObject hudEntryPrefab;
    [Tooltip("Parent VerticalLayoutGroup / ScrollRect Content transform")]
    public Transform hudPanel;

    /* ───────────────────────── Flight-selection list ─────────────────────────── */

    /// <summary>Checkbox entry shown in the Inspector.</summary>
    [Serializable]
    public class FlightSel { public string id; public bool spawn; }

    [SerializeField] public List<FlightSel> selections = new();

    /* ───────────────────────────── internal storage ───────────────────────────── */

    double metersPerLat, metersPerLon;

    struct Point { public float t; public double lat, lon, alt; }

    readonly Dictionary<string, List<Point>> tracks   = new();
    readonly Dictionary<string, Camera>      chaseCam = new();
    readonly Dictionary<string, TMP_Text>    hudLines = new();
    readonly Dictionary<string, Transform>   planeTf  = new();
    readonly Dictionary<string, int>         segIndex = new();

    float earliest = float.MaxValue, latest = float.MinValue;

    /* ────────────────── Edit-mode: rebuild tick-box list ────────────────── */
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) BuildChecklist();
    }

    /// <summary>Reads the CSV in Edit-mode and refreshes the flight ID list.</summary>
    void BuildChecklist()
    {
        string path = Path.Combine(Application.streamingAssetsPath, csvFileName);
        if (!File.Exists(path)) return;

        var ids = new HashSet<string>();
        using var sr = new StreamReader(path);
        bool skipHeader = true;
        while (!sr.EndOfStream)
        {
            var ln = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(ln) || ln.StartsWith("#")) continue;
            if (skipHeader) { skipHeader = false; continue; }
            ids.Add(ln.Split(',')[0]);
        }

        /* preserve previous check states where possible */
        var old = selections.ToDictionary(s => s.id, s => s.spawn);
        selections = ids.OrderBy(id => id)
                        .Select(id => new FlightSel {
                            id = id,
                            spawn = old.ContainsKey(id) && old[id] })
                        .ToList();
    }
#endif

    /* ─────────────────────────── Runtime lifecycle ─────────────────────────── */

    void Awake()
    {
        metersPerLat = 111_320f;                                        // constant
        metersPerLon = Mathf.Cos((float)refLat * Mathf.Deg2Rad) * 111_320f;
    }

    void Start()
    {
        LoadCsv(Path.Combine(Application.streamingAssetsPath, csvFileName));
        PlaybackController.SetTimeBounds(earliest, latest);
        PlaybackController.simTime = earliest;                          // start clock

        var wanted = new HashSet<string>(selections.Where(s => s.spawn).Select(s => s.id));
        foreach (var kv in tracks)
        {
            if (wanted.Count > 0 && !wanted.Contains(kv.Key)) continue;
            StartCoroutine(Animate(kv.Key, kv.Value));
        }
    }

    /* ───────────────────────────── CSV loader ──────────────────────────────── */

    /// <summary>Parses CSV rows into the <see cref="tracks"/> dictionary.</summary>
    void LoadCsv(string path)
    {
        using var sr = new StreamReader(path);
        bool skipHeader = true;

        while (!sr.EndOfStream)
        {
            var ln = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(ln) || ln.StartsWith("#")) continue;
            if (skipHeader) { skipHeader = false; continue; }

            var s = ln.Split(',');
            string id = s[0];

            var p = new Point {
                t   = float.Parse (s[1], CultureInfo.InvariantCulture),
                lat = double.Parse(s[2], CultureInfo.InvariantCulture),
                lon = double.Parse(s[3], CultureInfo.InvariantCulture),
                alt = double.Parse(s[4], CultureInfo.InvariantCulture)
            };

            earliest = Mathf.Min(earliest, p.t);
            latest   = Mathf.Max(latest,   p.t);

            if (!tracks.ContainsKey(id)) tracks[id] = new List<Point>();
            tracks[id].Add(p);
        }
        foreach (var list in tracks.Values) list.Sort((a, b) => a.t.CompareTo(b.t));
    }

    /* ─────────────────────────── Plane animation ──────────────────────────── */

    /// <summary>Coroutine that animates one aircraft along its track.</summary>
    IEnumerator Animate(string id, List<Point> pts)
    {
        if (pts.Count < 2) yield break;

        var plane = Instantiate(planePrefab, ToUnity(pts[0]), Quaternion.identity);
        plane.name         = id;
        planeTf[id]        = plane;
        segIndex[id]       = 0;

        SpawnPath(id, pts);
        AttachLabel(plane, id);
        AttachHudEntry(id);
        var cam = AttachChaseCam(plane, id);

        for (int seg = 0; seg < pts.Count - 1; seg++)
        {
            var a  = pts[seg]; var b = pts[seg + 1];
            Vector3 A = ToUnity(a), B = ToUnity(b);
            float dt = b.t - a.t, t = 0f;

            while (t < dt)
            {
                if (!PlaybackController.paused)
                {
                    t += Time.deltaTime * PlaybackController.speed;
                    float α = Mathf.Clamp01(t / dt);

                    plane.position = Vector3.Lerp(A, B, α);
                    plane.forward  = (B - A).normalized;

                    UpdateHud(id,
                              Mathf.Lerp((float)a.lat,(float)b.lat,α),
                              Mathf.Lerp((float)a.lon,(float)b.lon,α),
                              Mathf.Lerp((float)a.alt,(float)b.alt,α));
                }
                yield return null;
            }
            segIndex[id] = seg + 1;
        }
    }

    /* ───────────────────────────── Scrub seeking ──────────────────────────── */

    void Update()
    {
        if (!PlaybackController.scrubbing) return;
        float tSec = PlaybackController.simTime;
        foreach (var id in tracks.Keys) Seek(id, tSec);
    }

    /// <summary>Instantly moves one plane to its position at absolute time <paramref name="tSec"/>.</summary>
    void Seek(string id, float tSec)
    {
        if (!planeTf.TryGetValue(id, out var tf)) return;
        var pts = tracks[id]; if (pts.Count < 2) return;

        if (!segIndex.ContainsKey(id)) segIndex[id] = 0;
        int i = segIndex[id];

        while (i < pts.Count - 2 && tSec > pts[i + 1].t) i++;
        while (i > 0              && tSec < pts[i].t)    i--;

        /* keep index in-bounds so pts[i+1] is always valid */
        if (i >= pts.Count - 1) i = pts.Count - 2;
        if (i < 0)              i = 0;
        segIndex[id] = i;

        var a = pts[i]; var b = pts[i + 1];
        float α = Mathf.InverseLerp(a.t, b.t, tSec);

        tf.position = Vector3.Lerp(ToUnity(a), ToUnity(b), α);
        tf.forward  = (ToUnity(b) - ToUnity(a)).normalized;

        UpdateHud(id,
                  Mathf.Lerp((float)a.lat,(float)b.lat,α),
                  Mathf.Lerp((float)a.lon,(float)b.lon,α),
                  Mathf.Lerp((float)a.alt,(float)b.alt,α));
    }

    /* ───────────────────────────── Helper builders ─────────────────────────── */

    void SpawnPath(string id, List<Point> pts)
    {
        var lr = new GameObject(id + "_Path").AddComponent<LineRenderer>();
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth    = lr.endWidth = 2f;
        lr.startColor    = lr.endColor = Color.yellow;
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++)
            lr.SetPosition(i, ToUnity(pts[i]));
    }

    Camera AttachChaseCam(Transform plane, string id)
    {
        var go  = new GameObject("Cam_" + id);
        go.transform.SetParent(plane, false);
        go.transform.localPosition = new Vector3(0, 20, -40);
        go.transform.LookAt(plane);

        var cam = go.AddComponent<Camera>();
        cam.depth   = 1;
        cam.enabled = false;
        chaseCam[id] = cam;

        if (Camera.main == null) cam.tag = "MainCamera";
        return cam;
    }

    void AttachLabel(Transform plane, string id)
    {
        var lbl = Instantiate(planeLabelPrefab, plane);
        lbl.transform.localPosition = new Vector3(0, 30, 0);
        lbl.GetComponentInChildren<TextMeshProUGUI>().text = id;
        lbl.AddComponent<Billboard>();
    }

    void AttachHudEntry(string id)
    {
        if (!hudPanel || !hudEntryPrefab) return;
        var row = Instantiate(hudEntryPrefab, hudPanel);
        var txt = row.GetComponentInChildren<TMP_Text>();
        hudLines[id] = txt;
        txt.text = $"{id}  |  …";
        row.GetComponent<UnityEngine.UI.Button>()
           .onClick.AddListener(() => SwitchCam(id));
    }

    void UpdateHud(string id, double lat, double lon, double alt)
    {
        if (hudLines.TryGetValue(id, out var txt))
            txt.text = $"{id}  |  {lat:F4}°, {lon:F4}°  |  {alt:F0} m";
    }

    void SwitchCam(string id)
    {
        if (!chaseCam.ContainsKey(id)) return;
        foreach (var kv in chaseCam) { kv.Value.enabled = false; kv.Value.tag = "Untagged"; }
        if (Camera.main) Camera.main.enabled = false;

        chaseCam[id].enabled = true;
        chaseCam[id].tag     = "MainCamera";
    }

    /* ───────────────────────── Latitude/Longitude → Unity ───────────────────── */

    Vector3 ToUnity(Point p)                    => ToUnity(p.lat, p.lon, p.alt);
    Vector3 ToUnity(double lat, double lon, double alt)
    {
        float x = (float)((lon - refLon) * metersPerLon);   // East
        float z = (float)((lat - refLat) * metersPerLat);   // North
        return new Vector3(x, (float)alt, z);
    }
}

/* ───────────── User-friendly custom Inspector for flight list ───────────── */
#if UNITY_EDITOR
[CustomEditor(typeof(FlightLoader))]
public class FlightLoaderEditor : Editor
{
    static Vector2 scroll;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var fl = (FlightLoader)target;
        if (GUILayout.Button("Select All")) foreach (var s in fl.selections) s.spawn = true;
        if (GUILayout.Button("Clear All" )) foreach (var s in fl.selections) s.spawn = false;

        EditorGUILayout.LabelField("Flights", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(200));
        foreach (var sel in fl.selections)
            sel.spawn = EditorGUILayout.ToggleLeft(sel.id, sel.spawn);
        EditorGUILayout.EndScrollView();

        if (GUI.changed) EditorUtility.SetDirty(fl);
    }
}
#endif
