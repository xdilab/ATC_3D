using UnityEngine;
using System;

public class GeoTestHarness : MonoBehaviour
{
    [Tooltip("GeoOrigin with GeoMapper attached")]
    public GeoMapper geo;

    // Your two known runway threshold points (IRL)
    [Header("IRL Points (GSO 5R/23L)")]
    public double A_lat = 36.0907778;      // 23L threshold
    public double A_lon = -79.9450556;
    public double A_altM = 270.0;          // meters MSL (23L)

    public double B_lat = 36.1104722;      // 5R threshold
    public double B_lon = -79.9199722;
    public double B_altM = 279.4;          // meters MSL (5R)

    [Header("Marker visuals")]
    public float markerSize = 8f;

    void Start()
    {
        if (!geo)
        {
            Debug.LogError("[GeoTestHarness] Assign GeoMapper (geo).");
            return;
        }

        // 1) Spawn markers at A/B using GeoMapper
        Vector3 pA = geo.LatLonAltToUnity(A_lat, A_lon, A_altM);
        Vector3 pB = geo.LatLonAltToUnity(B_lat, B_lon, B_altM);

        CreateMarker("A_23L", pA, Color.green);
        CreateMarker("B_5R",  pB, Color.red);

        // 2) Draw a line between them
        var line = new GameObject("AB_Line").AddComponent<LineRenderer>();
        line.transform.SetParent(geo.transform, true);
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startWidth = line.endWidth = 2f;
        line.startColor = line.endColor = Color.cyan;
        line.positionCount = 2;
        line.SetPosition(0, pA);
        line.SetPosition(1, pB);

        // 3) Compute real-world distance (Haversine) and compare to Unity
        double realDistM = HaversineMeters(A_lat, A_lon, B_lat, B_lon);
        float unityDist = Vector3.Distance(pA, pB);
        double scale = unityDist / realDistM;

        // 4) Bearing alignment check
        float trueBrg = TrueBearingDeg(A_lat, A_lon, B_lat, B_lon);
        Vector3 dir = (pB - pA); dir.y = 0f;
        float unityYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        unityYaw = (unityYaw + 360f) % 360f;

        // Because GeoMapper already applies northAlignment, unityYaw should match (trueBrg + northAlignment)
        float expectedYaw = (trueBrg + geo.northAlignmentDeg + 360f) % 360f;
        float bearingErr = Mathf.DeltaAngle(unityYaw, expectedYaw);

        // 5) Origin snap sanity: refLat/refLon should map to geo.transform.position
        Vector3 pOrigin = geo.LatLonAltToUnity(geo.refLat, geo.refLon, geo.refAltM);
        float originErr = Vector3.Distance(pOrigin, geo.transform.position);

        Debug.LogFormat(
            "[GeoTestHarness]\n" +
            "Real AB Distance: {0:N2} m | Unity AB Distance: {1:N2} u | scale (u/m): {2:F6}\n" +
            "True Bearing A->B: {3:F3}° | Unity heading: {4:F3}° | Expected: {5:F3}° | Δbearing: {6:F3}°\n" +
            "Origin snap error: {7:F3} m (should be < 0.1)\n" +
            "(Tip) If runway overlay is offset: move GeoOrigin. If rotated: tweak northAlignmentDeg. If length off: tweak enuScale.",
            realDistM, unityDist, scale, trueBrg, unityYaw, expectedYaw, bearingErr, originErr
        );
    }

    void CreateMarker(string name, Vector3 pos, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(geo.transform, true);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * markerSize;
        var r = go.GetComponent<Renderer>();
        if (r && r.sharedMaterial) r.sharedMaterial.color = c;
    }

    // Geodesy helpers
    static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // Earth radius (m)
        double φ1 = lat1 * Math.PI / 180.0;
        double φ2 = lat2 * Math.PI / 180.0;
        double dφ = (lat2 - lat1) * Math.PI / 180.0;
        double dλ = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dφ/2)*Math.Sin(dφ/2) + Math.Cos(φ1)*Math.Cos(φ2)*Math.Sin(dλ/2)*Math.Sin(dλ/2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
        return R * c;
    }

    static float TrueBearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        double φ1 = lat1 * Math.PI / 180.0;
        double φ2 = lat2 * Math.PI / 180.0;
        double Δλ = (lon2 - lon1) * Math.PI / 180.0;
        double y = Math.Sin(Δλ) * Math.Cos(φ2);
        double x = Math.Cos(φ1)*Math.Sin(φ2) - Math.Sin(φ1)*Math.Cos(φ2)*Math.Cos(Δλ);
        double θ = Math.Atan2(y, x);
        return (float)((θ * 180.0 / Math.PI + 360.0) % 360.0);
    }
}
