using UnityEngine;

public class ZoomAndPanController : MonoBehaviour
{
    // Public variables for controlling zoom and pan behavior
    public float zoomSpeed = 70f;
    public float panSpeed = 200f;
    public float minZoom = -500f;
    public float maxZoom = 273.4232f;

    public Transform mapBounds; // Reference to the transform defining the map bounds

    void Update()
    {
        // Check for zoom input from mouse scroll wheel
        float zoomInput = Input.GetAxis("Mouse ScrollWheel");
        Zoom(zoomInput);

        // Check for pan input from arrow keys
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Pan(horizontalInput, verticalInput);
    }

    // Function to handle zooming based on delta value
    void Zoom(float delta)
    {
        Camera mainCamera = Camera.main;
        // Adjust the orthographic size of the camera to zoom in/out
        mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize - delta * zoomSpeed, minZoom, maxZoom);
    }

    // Function to handle panning based on horizontal and vertical input
    void Pan(float horizontal, float vertical)
    {
        // Move the camera based on input
        Transform cameraTransform = Camera.main.transform;
        Vector3 panDirection = new Vector3(horizontal, vertical, 0f).normalized;
        Vector3 panAmount = panDirection * panSpeed * Time.deltaTime;

        // Calculate the new position of the camera
        Vector3 newPosition = cameraTransform.position + panAmount;

        // Ensure the camera stays within the bounds defined by mapBounds
        newPosition.x = Mathf.Clamp(newPosition.x, mapBounds.position.x, mapBounds.position.x + mapBounds.localScale.x);
        newPosition.y = Mathf.Clamp(newPosition.y, mapBounds.position.y, mapBounds.position.y + mapBounds.localScale.y);

        // Update the position of the camera
        cameraTransform.position = newPosition;
    }
}