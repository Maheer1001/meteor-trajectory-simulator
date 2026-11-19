using UnityEngine;

// Spins the Earth model about its axis at the sidereal rate (one turn every 23 h 56 min 4 s of real time).
public class EarthBehaviour : MonoBehaviour
{
    private const float DegreesPerSecond = 360f / 86164.0905f;

    void Update()
    {
        transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
