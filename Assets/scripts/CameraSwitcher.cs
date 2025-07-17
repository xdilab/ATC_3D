using UnityEngine;

public class CameraSwitcher : MonoBehaviour
{
    /* Call from a UI Button and pass the camera you want to see */
    public void Activate(Camera camToShow)
    {
        /* 1. Disable every camera that exists right now */
        foreach (Camera c in Camera.allCameras)
        {
            if (!c) continue;
            c.enabled = false;
            c.tag     = "Untagged";
            c.rect    = new Rect(0, 0, 1, 1);   // full-screen
        }

        /* 2. Enable + promote the requested camera */
        camToShow.enabled = true;
        camToShow.tag     = "MainCamera";
    }
}
