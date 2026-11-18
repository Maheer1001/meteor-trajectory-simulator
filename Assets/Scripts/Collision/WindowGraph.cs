using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Window_Graph : MonoBehaviour
{

    [SerializeField] private Sprite circleSprite;
    private RectTransform GraphContainer;
    private List<double> final = new List<double>();

    private void Awake()
    {
        GraphContainer = transform.Find("GraphContainer").GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Determine the list of values based on the elapsed time
        if (Meteor_Force.time >= 126000)
        {
            final = Meteor_Force.ThirtyMin;
        }
        else if (Meteor_Force.time >= 12000)
        {
            final = Meteor_Force.TenMin;
        }
        else
        {
            final = Meteor_Force.Min;
        }
        // Display the graph
        ShowGraph(final);
    }

    // Create a circle GameObject at the given anchored position
    private GameObject CreateCircle(Vector2 anchoredPosition)
    {
        GameObject gameObject = new GameObject("circle", typeof(Image));
        gameObject.transform.SetParent(GraphContainer, false);
        gameObject.GetComponent<Image>().sprite = circleSprite;
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(5, 5);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        return gameObject;
    }

    // Display the graph using the provided list of values
    private void ShowGraph(List<double> valueList)
    {
        double graphHeight = GraphContainer.sizeDelta.y;
        double graphWidth = GraphContainer.sizeDelta.x;
        double xSize = graphWidth / (valueList.Count - 1);
        GameObject lastCircleGameObject = null;
        for (int i = 0; i < valueList.Count; i++)
        {
            double xPosition = i * xSize;
            double yPosition = (valueList[i] / (Meteor_Force.DistanceTrav)) * graphHeight;
            GameObject circleGameObject = CreateCircle(new Vector2((float)xPosition, (float)yPosition));
            if (lastCircleGameObject != null)
            {
                CreateDotConnection(lastCircleGameObject.GetComponent<RectTransform>().anchoredPosition, circleGameObject.GetComponent<RectTransform>().anchoredPosition);
            }
            lastCircleGameObject = circleGameObject;
        }
    }

    // Create a line connecting two dots
    private void CreateDotConnection(Vector2 DotA, Vector2 DotB)
    {
        GameObject gameObject = new GameObject("dotConnection", typeof(Image));
        gameObject.transform.SetParent(GraphContainer, false);
        gameObject.GetComponent<Image>().color = new Color(1, 1, 1, 0.5f);
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        Vector2 dir = (DotA - DotB).normalized;
        float distance = Vector2.Distance(DotA, DotB);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.sizeDelta = new Vector2(distance, 3.0f);
        rectTransform.anchoredPosition = DotA - (dir * distance * 0.5f);
        // Calculate the angle
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // Apply rotation
        rectTransform.localEulerAngles = new Vector3(0, 0, angle);
    }
}