using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EarthBehaviour : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        transform.Rotate(0f, 0f * Time.deltaTime, 0f, Space.Self);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate (0f, 1/14400f*Time.deltaTime, 0f, Space.Self);
    }
}