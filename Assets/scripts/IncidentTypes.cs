using System;
using UnityEngine;

public enum IncidentType { WingClearance, GateContact, TaxiIncursion, RunwayIncursion }
public enum IncidentPhase { Predicted, Live, Cleared }
public enum Severity { Info, Warning, Critical }

[Serializable]
public struct IncidentEvent
{
    public string incidentId;      // unique id (timestamp-guid)
    public IncidentType type;
    public IncidentPhase phase;
    public Severity severity;
    public double tSim;            // sim seconds
    public string aId;             // primary aircraft id
    public string bId;             // other actor/zone
    public Vector3 worldPos;       // Unity focal point
    public double lat, lon, alt;   // filled by IncidentManager via GeoMapper
    public float minClearanceM;    // proximity
    public float ttcSec;           // predicted time-to-contact
    public string zoneName;        // for zone incidents
}
