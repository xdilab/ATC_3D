using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [Header("Waypoints in Travel Order")]
    public Transform[] waypoints;     // Assign your 5 waypoints here in the Inspector
    public float speed = 10f;         // General speed for taxiing/runway
    public float rotationSpeed = 2f;  // How quickly the plane rotates toward the waypoint
    
    private int currentWaypointIndex = 0;
    private bool isTakingOff = false;
    
    void Start()
    {
        // Optional: place the plane at the first waypoint position at the start
        transform.position = waypoints[0].position;
    }

    void Update()
    {
        // If we've reached the last waypoint, do "takeoff" logic or flight logic
        if (currentWaypointIndex >= waypoints.Length)
        {
            // We can either continue flying or stop
            if (!isTakingOff)
            {
                // Trigger takeoff once we reach the final waypoint
                isTakingOff = true;
                Debug.Log("Begin takeoff / flight logic here...");
            }
            // Example: simple forward flight after final waypoint
            MoveForward(speed * 2f); 
            return;
        }

        // Move toward the current waypoint
        MoveTowardsWaypoint(waypoints[currentWaypointIndex], speed);

        // Check if we're close enough to move on
        float distance = Vector3.Distance(transform.position, waypoints[currentWaypointIndex].position);
        if (distance < 1f)
        {
            // Advance to next waypoint
            currentWaypointIndex++;
            // If you want different speeds for runway/takeoff, you can adjust speed here
        }
    }

    private void MoveTowardsWaypoint(Transform waypoint, float moveSpeed)
    {
        // Direction to the waypoint
        Vector3 direction = (waypoint.position - transform.position).normalized;
        
        // Smooth rotation
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        
        // Move forward
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }

    private void MoveForward(float moveSpeed)
    {
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }
}
