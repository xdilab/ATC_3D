using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Affine geo-mapper: fits XZ_unity = A · EN_geo + t
/// where EN_geo is local East/North meters from (refLat, refLon).
/// Needs 3+ control points mapping (lat,lon) → Unity (x,z) under the same WorldRoot.
/// </summary>
public class GeoMapper : MonoBehaviour
{
    [Header("Geodetic origin (degrees)")]
    public double refLat = 36.0920;     // near airport center
    public double refLon = -79.9357;

    [Header("Scene root (WorldRoot)")]
    public Transform worldRoot;         // everything (planes/map) should be parented here

    [Header("Altitude mapping")]
    public float yOffset = 0f;          // add this to Y
    public float yScale  = 1f;          // multiply (alt_m * yScale) + yOffset

    [Header("Legacy knobs (for tools/tests only)")]
    [Tooltip("Kept for GeoTestHarness compatibility; not used by the affine fit.")]
    public float northAlignmentDeg = 0f;
    [Tooltip("Kept for GeoTestHarness compatibility; not used by the affine fit.")]
    public float enuScale = 1f;
    [Tooltip("Kept for GeoTestHarness compatibility; not used by the affine fit.")]
    public float refAltM = 0f;

    [Serializable]
    public class Control
    {
        public string name = "CP";
        public double lat, lon;     // IRL
        public Vector2 unityXZ;     // Unity coordinates (under WorldRoot), in scene units
    }

    [Header("Control points (3 or more)")]
    public List<Control> controls = new();

    [Header("Solve status")]
    public bool solved = false;
    public float rmsError = -1f;   // meters (scene units)
    public Matrix4x4 debugA;       // 2x2 (in 4x4 slot)
    public Vector2 debugT;

    double mPerLat, mPerLon;
    double a11, a12, a21, a22, tx, tz;  // affine params

    void Awake() { UpdateMeters(); TrySolve(); }
#if UNITY_EDITOR
    void OnValidate() { UpdateMeters(); TrySolve(); }
#endif

    void UpdateMeters()
    {
        double φ = refLat * Mathf.Deg2Rad;
        mPerLat = 111132.92 - 559.82 * Math.Cos(2*φ) + 1.175 * Math.Cos(4*φ) - 0.0023 * Math.Cos(6*φ);
        mPerLon = (111412.84 * Math.Cos(φ)) - 93.5 * Math.Cos(3*φ) + 0.118 * Math.Cos(5*φ);
    }

    public bool IsSolved => solved;

    /// <summary>Geodetic → Unity. Uses affine if solved; else falls back to flat EN→XZ with origin.</summary>
    public Vector3 LatLonAltToUnity(double lat, double lon, double alt)
    {
        double e = (lon - refLon) * mPerLon;
        double n = (lat - refLat) * mPerLat;

        float x, z;
        if (solved)
        {
            x = (float)(a11 * e + a12 * n + tx);
            z = (float)(a21 * e + a22 * n + tz);
        }
        else
        {
            x = (float)e;
            z = (float)n;
        }

        float y = (float)(alt * yScale) + yOffset;
        return new Vector3(x, y, z);
    }

    /// <summary>Unity → Geodetic (inverse). Requires solved (falls back to flat if not).</summary>
    public (double lat, double lon, double alt) UnityToLatLonAlt(Vector3 pos)
    {
        double x = pos.x, z = pos.z;

        if (!solved)
        {
            double lonOut = refLon + x / mPerLon;                         // renamed to avoid CS0136
            double latOut = refLat + z / mPerLat;
            double altOut = (pos.y - yOffset) / Math.Max(1e-6, yScale);
            return (latOut, lonOut, altOut);
        }

        // Invert 2x2
        double det = a11 * a22 - a12 * a21;
        if (Math.Abs(det) < 1e-9) det = 1e-9;
        double i11 =  a22 / det, i12 = -a12 / det;
        double i21 = -a21 / det, i22 =  a11 / det;

        double e = i11 * (x - tx) + i12 * (z - tz);
        double n = i21 * (x - tx) + i22 * (z - tz);

        double lonOut2 = refLon + e / mPerLon;                            // renamed to avoid CS0136
        double latOut2 = refLat + n / mPerLat;
        double altOut2 = (pos.y - yOffset) / Math.Max(1e-6, yScale);
        return (latOut2, lonOut2, altOut2);
    }

    [ContextMenu("Solve Now")]
    public void TrySolve()
    {
        solved = false;
        rmsError = -1f;
        if (controls == null || controls.Count < 3) return;

        // Normal equations for least squares on [a11 a12 a21 a22 tx tz]
        double[,] G = new double[6,6];
        double[]  h = new double[6];

        foreach (var c in controls)
        {
            double e = (c.lon - refLon) * mPerLon;
            double n = (c.lat - refLat) * mPerLat;
            double X = c.unityXZ.x;
            double Z = c.unityXZ.y;

            // x = a11*e + a12*n + tx
            Accumulate(G, h, idxA:0, idxB:1, idxT:4, e, n, X);
            // z = a21*e + a22*n + tz
            Accumulate(G, h, idxA:2, idxB:3, idxT:5, e, n, Z);
        }

        double[] v = SolveSymmetric6x6(G, h);
        if (v == null) return;

        a11 = v[0]; a12 = v[1]; a21 = v[2]; a22 = v[3]; tx = v[4]; tz = v[5];

        // RMS residual (scene units)
        double err2 = 0; int m = 0;
        foreach (var c in controls)
        {
            double e = (c.lon - refLon) * mPerLon;
            double n = (c.lat - refLat) * mPerLat;
            double X = a11*e + a12*n + tx;
            double Z = a21*e + a22*n + tz;
            double dx = X - c.unityXZ.x;
            double dz = Z - c.unityXZ.y;
            err2 += dx*dx + dz*dz;
            m += 2;
        }
        rmsError = (float)Math.Sqrt(err2 / Math.Max(1, m));
        solved = true;

        debugA = Matrix4x4.identity;
        debugA.m00 = (float)a11; debugA.m01 = (float)a12;
        debugA.m10 = (float)a21; debugA.m11 = (float)a22;
        debugT = new Vector2((float)tx, (float)tz);
    }

    static void Accumulate(double[,] G, double[] h, int idxA, int idxB, int idxT, double e, double n, double target)
    {
        G[idxA, idxA] += e*e;  G[idxA, idxB] += e*n;  G[idxA, idxT] += e;
        G[idxB, idxB] += n*n;  G[idxB, idxT] += n;    G[idxT, idxT] += 1.0;

        G[idxB, idxA] = G[idxA, idxB];
        G[idxT, idxA] = G[idxA, idxT];
        G[idxT, idxB] = G[idxB, idxT];

        h[idxA] += e * target;
        h[idxB] += n * target;
        h[idxT] += 1.0 * target;
    }

    static double[] SolveSymmetric6x6(double[,] G, double[] h)
    {
        int n = 6;
        double[,] A = new double[n, n+1];
        for (int r = 0; r < n; r++) { for (int c = 0; c < n; c++) A[r,c] = G[r,c]; A[r,n] = h[r]; }

        for (int p = 0; p < n; p++)
        {
            int piv = p; double best = Math.Abs(A[p,p]);
            for (int r = p+1; r < n; r++) { double v = Math.Abs(A[r,p]); if (v > best) { best = v; piv = r; } }
            if (piv != p) for (int c = p; c <= n; c++) { double tmp = A[p,c]; A[p,c] = A[piv,c]; A[piv,c] = tmp; }

            double diag = A[p,p]; if (Math.Abs(diag) < 1e-9) return null;
            for (int c = p; c <= n; c++) A[p,c] /= diag;

            for (int r = 0; r < n; r++)
            {
                if (r == p) continue;
                double f = A[r,p]; if (Math.Abs(f) < 1e-12) continue;
                for (int c = p; c <= n; c++) A[r,c] -= f * A[p,c];
            }
        }

        double[] x = new double[n]; for (int i = 0; i < n; i++) x[i] = A[i,n];
        return x;
    }
}
