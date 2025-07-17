using UnityEngine;

public class CoordinateConverter : MonoBehaviour
{
    // Reference coordinate (set these in the Inspector)
    public float refLatitude = 36.0978f;
    public float refLongitude = -79.9373f;
    
    // Reference Unity position corresponding to the above coordinate
    // Initialize the field inline
    public Vector3 refPosition = new Vector3(-1.866801f, 1.232304f, -0.344962f);
    
    // Conversion constant: approximately meters per degree latitude
    private const float metersPerDegreeLat = 111320f;

    /// <summary>
    /// Converts a given latitude and longitude to Unity world coordinates.
    /// </summary>
    /// <param name="latitude">Real-world latitude</param>
    /// <param name="longitude">Real-world longitude</param>
    /// <param name="elevation">Optional elevation value (default is 0)</param>
    /// <returns>Unity world position as Vector3</returns>
    public Vector3 ConvertLatLonToUnity(float latitude, float longitude, float elevation = 0f)
    {
        // Calculate the differences between the given and reference coordinates
        float deltaLat = latitude - refLatitude;
        float deltaLon = longitude - refLongitude;
        
        // Convert these differences to meters
        float metersX = deltaLon * (111320f * Mathf.Cos(refLatitude * Mathf.Deg2Rad));
        float metersZ = deltaLat * metersPerDegreeLat;
        
        // Return the final Unity position (assigning X, Y, and Z)
        Vector3 unityPos = refPosition + new Vector3(metersX, elevation, metersZ);
        return unityPos;
    }
    
    // For testing purposes: logs a converted coordinate when the game starts.
    private void Start()
    {
        // Sample coordinate for testing (replace with your own values)
        float sampleLat = 36.1050f;
        float sampleLon = -79.9500f;
        Vector3 gatePosition = ConvertLatLonToUnity(sampleLat, sampleLon);
        Debug.Log("Gate Unity Position: " + gatePosition);
    }
}
