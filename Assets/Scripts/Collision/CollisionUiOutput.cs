using System;
using MTS.Core;
using UnityEngine;
using UnityEngine.Rendering;

public class CollisionUiOutput : MonoBehaviour
{
    // The world map spans 878 x 520 units centred on the origin (equirectangular: 360 x 180 degrees).
    private const double MapHalfWidth = 439.0;
    private const double MapHalfHeight = 260.0;

    private double[] xScaledR;
    private double[] yScaledR;
    private double NewLatitude;
    private double NewLongitude;

    void Start()
    {
        xScaledR = new double[10];
        yScaledR = new double[10];
        Rconversion();
        Create();
    }

    // Converts impact radii (m) to map scale and the impact latitude/longitude (degrees) to map coordinates
    void Rconversion()
    {
        double[] impactArray = CollisionEffects.CurrentRadii();
        double unitsPerMetreX = 2.0 * MapHalfWidth / (2.0 * Math.PI * PhysicalConstants.EarthRadius);
        double unitsPerMetreY = 2.0 * MapHalfHeight / (Math.PI * PhysicalConstants.EarthRadius);
        for (int i = 0; i < impactArray.Length; i++)
        {
            xScaledR[i] = unitsPerMetreX * impactArray[i];
            yScaledR[i] = unitsPerMetreY * impactArray[i];
        }

        CoordinateCalculator.ComputeImpactLocation();
        NewLatitude = CoordinateCalculator.Latitude / 90.0 * MapHalfHeight;
        NewLongitude = CoordinateCalculator.Longitude / 180.0 * MapHalfWidth;
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