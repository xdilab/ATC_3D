using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cameraMove : MonoBehaviour
{
    public float mouseSensitivity = 400f;
    public Transform playerBody;
    float xRotation = 0f;
    bool canFreeMouse = false;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!canFreeMouse){
            Cursor.lockState = CursorLockMode.Locked;
        }
        else{
            Cursor.lockState = CursorLockMode.None;
        }
        
        if (Input.GetKey(KeyCode.Escape)){
            canFreeMouse = true;
        }
        else{
            canFreeMouse = false;
        }


        float MouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float MouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        xRotation -= MouseY;
        xRotation = Math.Clamp(xRotation, -89f, 89f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * MouseX); 
    }
}
