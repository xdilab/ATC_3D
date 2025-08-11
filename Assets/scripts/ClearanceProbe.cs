using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ClearanceProbe : MonoBehaviour
{
    [Header("Identity")]
    public string flightId = "UNKNOWN";       // set by FlightLoader at spawn

    [Header("Geometry")]
    public Transform leftWingTip;
    public Transform rightWingTip;
    public float bodyRadiusM = 10f;            // approximate fuselage radius

    [Header("Detection (no layers required)")]
    public float queryRadiusM = 80f;          // search radius around aircraft
    public float warnClearanceM = 60f;       // WARNING below this
    public float incidentClearanceM = 50f;   // INCIDENT below this
    public float predictTtcSec = 6.0f;        // Predicted if TTC < this

    [Header("Filtering")]
    public bool requireMarker = true;         // only consider objects with ClearanceTarget
    public bool useTags = false;              // optional: filter by tags instead of marker
    public string aircraftTag = "Aircraft";
    public string obstacleTag = "Obstacle";

    [Header("Routing")]
    public IncidentManager manager;

    readonly Collider[] hits = new Collider[64];
    Rigidbody rb;

    // Track active incident per counterpart
    class Active { public string id; public bool live; }
    readonly Dictionary<Collider, Active> active = new();

    // NEW: Shared incident IDs so both aircraft write to the SAME folder/clip
    static readonly Dictionary<string, string> pairToIncidentId = new();

    void Awake() { rb = GetComponent<Rigidbody>(); }

    void FixedUpdate()
    {
        // Query all layers; we’ll filter by marker/tag
        int n = Physics.OverlapSphereNonAlloc(transform.position, queryRadiusM, hits, ~0, QueryTriggerInteraction.Ignore);
        Vector3 vSelf = rb ? rb.linearVelocity : Vector3.zero;

        for (int i = 0; i < n; i++)
        {
            var other = hits[i];
            if (!other || other.attachedRigidbody == rb) continue;

            // Filter: marker or tag
            if (requireMarker && !other.GetComponentInParent<ClearanceTarget>()) continue;
            if (useTags && !(other.CompareTag(obstacleTag) ||
                (other.attachedRigidbody && other.attachedRigidbody.CompareTag(aircraftTag)))) continue;

            // Clearance from wingtips & body
            float cL = MinDistanceTo(other, leftWingTip ? leftWingTip.position : transform.position);
            float cR = MinDistanceTo(other, rightWingTip ? rightWingTip.position : transform.position);
            float cB = MinDistanceTo(other, transform.position) - bodyRadiusM;
            float clearance = Mathf.Min(cL, cR, cB);

            // Closing speed approx → TTC
            Vector3 pSelf = ClosestPointOn(other, transform.position);
            Vector3 relDir = (ClosestPointOn(other, pSelf + vSelf * Time.fixedDeltaTime) - pSelf);
            float closingSpeed = Vector3.Dot(relDir / Mathf.Max(Time.fixedDeltaTime, 1e-4f),
                                             (pSelf - transform.position).normalized);
            float ttc = (closingSpeed > 0.05f) ? clearance / closingSpeed : Mathf.Infinity;

            // Severity
            Severity sev = clearance <= incidentClearanceM ? Severity.Critical :
                           clearance <= warnClearanceM     ? Severity.Warning  : Severity.Info;

            // Track per counterpart
            if (!active.TryGetValue(other, out var a))
                active[other] = a = new Active { id = BuildIncidentId(other), live = false };

            Vector3 focal = transform.position;

            if (clearance <= warnClearanceM && manager)
            {
                // Predicted (preemptive)
                if (ttc < predictTtcSec && !a.live)
                    manager.Report(new IncidentEvent {
                        incidentId = a.id,
                        type = IncidentType.WingClearance,
                        phase = IncidentPhase.Predicted,
                        severity = Severity.Warning,
                        tSim = Time.time,
                        aId = flightId,
                        bId = GetOtherId(other),
                        worldPos = focal,
                        minClearanceM = clearance,
                        ttcSec = ttc
                    });

                // Live breach
                if (clearance <= incidentClearanceM)
                {
                    a.live = true;
                    manager.Report(new IncidentEvent {
                        incidentId = a.id,
                        type = IncidentType.WingClearance,
                        phase = IncidentPhase.Live,
                        severity = sev,
                        tSim = Time.time,
                        aId = flightId,
                        bId = GetOtherId(other),
                        worldPos = focal,
                        minClearanceM = clearance,
                        ttcSec = ttc
                    });
                }
                else if (a.live) // still risky; keep updating as Live
                {
                    manager.Report(new IncidentEvent {
                        incidentId = a.id,
                        type = IncidentType.WingClearance,
                        phase = IncidentPhase.Live,
                        severity = sev,
                        tSim = Time.time,
                        aId = flightId,
                        bId = GetOtherId(other),
                        worldPos = focal,
                        minClearanceM = clearance,
                        ttcSec = ttc
                    });
                }
            }
            else
            {
                // Cleared
                if (a.live && manager)
                {
                    a.live = false;
                    manager.Report(new IncidentEvent {
                        incidentId = a.id,
                        type = IncidentType.WingClearance,
                        phase = IncidentPhase.Cleared,
                        severity = Severity.Info,
                        tSim = Time.time,
                        aId = flightId,
                        bId = GetOtherId(other),
                        worldPos = focal,
                        minClearanceM = clearance,
                        ttcSec = ttc
                    });
                }
            }
        }
    }

    // ---------- Shared ID builder (makes A↔B share the SAME incident folder) ----------
    string BuildIncidentId(Collider other)
    {
        string a = string.IsNullOrEmpty(flightId) ? "UNKNOWN" : flightId;
        string b = GetOtherId(other);

        // Canonicalize the pair (sorted) so A_vs_B == B_vs_A
        string k1 = a, k2 = b;
        if (string.CompareOrdinal(k1, k2) > 0) { var tmp = k1; k1 = k2; k2 = tmp; }
        string pairKey = $"{k1}_vs_{k2}";

        // Reuse the same incidentId for this pair within the session
        if (!pairToIncidentId.TryGetValue(pairKey, out var id))
        {
            id = $"{IncidentType.WingClearance}_{pairKey}_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}";
            pairToIncidentId[pairKey] = id;
        }
        return id;
    }

    // Prefer the other aircraft's flightId; fall back to collider name
    string GetOtherId(Collider other)
    {
        var p = other ? other.GetComponentInParent<ClearanceProbe>() : null;
        if (p && !string.IsNullOrEmpty(p.flightId)) return p.flightId;
        return other ? other.name : "UNKNOWN";
    }

    float MinDistanceTo(Collider c, Vector3 point)
    {
        Vector3 cp = ClosestPointOn(c, point);
        return Vector3.Distance(cp, point);
    }

    Vector3 ClosestPointOn(Collider c, Vector3 p) => c ? c.ClosestPoint(p) : p;
}
