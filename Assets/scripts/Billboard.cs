using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera cameraToFace;        // can be left null

    void LateUpdate()
    {
        // fall back to whichever camera is tagged MainCamera
        var cam = cameraToFace != null ? cameraToFace : Camera.main;
        if (cam) transform.forward = cam.transform.forward;
    }
}
