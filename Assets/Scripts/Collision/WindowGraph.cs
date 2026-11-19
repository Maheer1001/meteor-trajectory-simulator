using System;
using System.Collections.Generic;
using MTS.Core;
using UnityEngine;
using UnityEngine.UI;

// Plots distance travelled against time for the finished run.
public class Window_Graph : MonoBehaviour
{
    // Upper limit on plotted points, so the graph stays as light as the old minute-by-minute version.
    private const int MaxPoints = 200;

    [SerializeField] private Sprite circleSprite;
    private RectTransform GraphContainer;

    private void Awake()
    {
        GraphContainer = transform.Find("GraphContainer").GetComponent<RectTransform>();
    }

    private void Start()
    {
        SimulationResult result = SimulationSession.Instance.Result;
        if (result == null || result.Series == null)
        {
            return;
        }

        ShowGraph(result.Series);
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

    // Display the graph: x is time as a fraction of the run, y is distance as a fraction of the total distance
    private void ShowGraph(TimeSeries series)
    {
        int count = series.Count;
        if (count < 2)
        {
            return; // nothing meaningful to draw
        }

        double graphHeight = GraphContainer.sizeDelta.y;
        double graphWidth = GraphContainer.sizeDelta.x;
        double t0 = series.Time[0];
        double duration = series.Time[count - 1] - t0;
        double totalDistance = series.Distance[count - 1];
        if (!(duration > 0))
        {
            return;
        }

        int stride = Math.Max(1, (int)Math.Ceiling(count / (double)MaxPoints));
        GameObject lastCircleGameObject = null;
        for (int i = 0; i < count; i += stride)
        {
            int index = Math.Min(i, count - 1);
            if (i + stride >= count)
            {
                index = count - 1; // always finish on the last sample
            }

            double xPosition = (series.Time[index] - t0) / duration * graphWidth;
            double yPosition = totalDistance > 0 ? series.Distance[index] / totalDistance * graphHeight : 0.0;
            GameObject circleGameObject = CreateCircle(new Vector2((float)xPosition, (float)yPosition));
            if (lastCircleGameObject != null)
            {
                CreateDotConnection(lastCircleGameObject.GetComponent<RectTransform>().anchoredPosition, circleGameObject.GetComponent<RectTransform>().anchoredPosition);
            }

            lastCircleGameObject = circleGameObject;
            if (index == count - 1)
            {
                break;
            }
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