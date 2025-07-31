// Assets/Scripts/CaptureForceStopHotkey.cs
using UnityEngine;

public class CaptureForceStopHotkey : MonoBehaviour
{
    [Tooltip("Drag your CaptureService component here")]
    public CaptureService captureService;

    [Tooltip("Key to press to force‐stop the active capture")]
    public KeyCode forceStopKey = KeyCode.Space;

    void Update()
    {
        if (captureService != null && Input.GetKeyDown(forceStopKey))
        {
            captureService.ForceStopActive();
        }
    }
}
