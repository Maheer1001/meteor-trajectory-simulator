using System;
using UnityEngine;
using UnityEngine.Rendering;

public class CollisionUiOutput : MonoBehaviour
{
    private float pi = (float)Math.PI;
    private double[] xScaledR;
    private double[] yScaledR;
    private double xCradius;
    private double yCradius;
    private double NewLatitude;
    private double NewLongitude;

    void Start()
    {
        xScaledR = new double[10];
        yScaledR = new double[10];
        Rconversion();
        Create();
    }

    // Converts impact radii to scaled coordinates and calculates latitude and longitude values
    void Rconversion()
    {
        double[] impactArray = CollisionEffects.R_impact;

        if (impactArray != null && impactArray.Length >= 10)
        {
            float temp = (878 / (2 * pi * 6371000));
            float temp2 = (520 / (2 * pi * 6371000));
            for (int i = 0; i <= 9; i++)
            {
                xScaledR[i] = temp * impactArray[i];
                yScaledR[i] = temp2 * impactArray[i];
                Debug.Log(xScaledR[i] + "This is xScaledR " + i);
                Debug.Log(yScaledR[i] + "This is yScaledR " + i);
            }
        }
        else
        {
            Debug.LogError("R_impact array is null or doesn't have enough elements.");
        }

        float Temp = 180 * (float)CoordinateCalculator.Latitude;
        NewLatitude = Temp / 439;
        float Temp2 = 180 * (float)CoordinateCalculator.Longitude; //should be 90
        NewLongitude = Temp2 / 260;
    }

    // Creates collision visualization circles
    void Create()
    {
        GameObject circleSpriteMaskPrefab = Resources.Load<GameObject>("CircleSpriteMask");
        GameObject baseCircle = Instantiate(Resources.Load("CirclePrefab"), new Vector3((float)NewLongitude, (float)NewLatitude, 0), Quaternion.identity) as GameObject;

        for (int i = 0; i <= 9; i++)
        {
            GameObject circle = Instantiate(baseCircle, new Vector3((float)NewLongitude, (float)NewLatitude, 0), Quaternion.identity) as GameObject;

            Renderer circleRenderer = circle.GetComponent<Renderer>();
            if (circleRenderer != null)
            {
                Material ringMaterial = Resources.Load<Material>("Materials/Material" + (i + 1));
                circleRenderer.material = ringMaterial;
            }

            SpriteRenderer spriteRenderer = circle.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                Sprite ringSprite = Resources.Load<Sprite>("Sprites/Sprite" + (i + 1));
                spriteRenderer.sprite = ringSprite;
            }

            circle.transform.localScale = new Vector3((float)xScaledR[i], (float)yScaledR[i], 1.0f);

            // Add SortingGroup component to the circle
            SortingGroup sortingGroup = circle.AddComponent<SortingGroup>();
            sortingGroup.sortingOrder = i + 1;

            GameObject circleMask = Instantiate(circleSpriteMaskPrefab, circle.transform.position, Quaternion.identity);

            circleMask.transform.parent = circle.transform;

            // Set the local scale of the circleMask independently of its parent
            circleMask.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

            // Check if SortingGroup exists on the circle mask, and create one if not
            SortingGroup maskSortingGroup = circleMask.GetComponent<SortingGroup>();
            if (maskSortingGroup == null)
            {
                maskSortingGroup = circleMask.AddComponent<SortingGroup>();
            }

            // Disable SortingGroup on the circle mask
            maskSortingGroup.enabled = false;
        }

        // Destroy the base circle as it was used only for reference
        Destroy(baseCircle);
    }
}