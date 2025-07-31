using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ZoneVolume : MonoBehaviour
{
    public IncidentType type = IncidentType.RunwayIncursion; // or TaxiIncursion
    public string zoneName = "RWY_5R_Protected";
    public IncidentManager manager;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!manager) return;
        var probe = other.GetComponentInParent<ClearanceProbe>(); // aircraft root?
        if (!probe) return;

        var e = new IncidentEvent {
            incidentId = $"{type}_{zoneName}_{probe.flightId}_{System.DateTime.UtcNow:yyyyMMddTHHmmssfffZ}",
            type = type,
            phase = IncidentPhase.Live,
            severity = Severity.Warning,
            tSim = Time.time,
            aId = probe.flightId,
            bId = zoneName,
            worldPos = other.bounds.center,
            zoneName = zoneName
        };
        manager.Report(e);
    }

    void OnTriggerExit(Collider other)
    {
        if (!manager) return;
        var probe = other.GetComponentInParent<ClearanceProbe>(); 
        if (!probe) return;

        var e = new IncidentEvent {
            incidentId = $"{type}_{zoneName}_{probe.flightId}",
            type = type,
            phase = IncidentPhase.Cleared,
            severity = Severity.Info,
            tSim = Time.time,
            aId = probe.flightId,
            bId = zoneName,
            worldPos = other.bounds.center,
            zoneName = zoneName
        };
        manager.Report(e);
    }
}
