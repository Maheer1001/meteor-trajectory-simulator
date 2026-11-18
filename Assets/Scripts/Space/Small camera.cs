using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmallCamera : MonoBehaviour
{
    // Variables for object radius and distance from the camera
    public float objectRadius = 2.5e-7f;  // Set the object radius here
    public float distanceFromCamera = 1.0f;  // Set the distance here

    void Start()
    {
        // Calculate the FOV using the formula
        float fov = 2.0f * Mathf.Atan((objectRadius / 2.0f) / distanceFromCamera) * Mathf.Rad2Deg;

        // Set the calculated FOV to the camera's field of view
        Camera.main.fieldOfView = fov;
    }
}