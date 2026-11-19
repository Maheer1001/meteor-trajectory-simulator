using System;
using System.Collections;
using System.Globalization;
using System.IO;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// Works out where the run ended on the Earth's surface and looks up a place name for it.
public class CoordinateCalculator : MonoBehaviour
{
    // Latitude and longitude of the end point in degrees (north and east positive).
    public static double Latitude;
    public static double Longitude;

    // Reference to the TMP_Text component
    public TMP_Text JsonParseText;

    private UnityWebRequest request;

    void Start()
    {
        ComputeImpactLocation();
        StartCoroutine(ReverseGeocoding.ReverseGeocode(Latitude, Longitude, JsonParseText, r => request = r));
    }

    void OnDestroy()
    {
        // Leaving the scene cancels a lookup that is still in flight.
        if (request != null)
        {
            request.Abort();
            request = null;
        }
    }

    /// <summary>
    /// Sets <see cref="Latitude"/> / <see cref="Longitude"/> from the session's end state. The inertial end position is
    /// rotated into the Earth-fixed frame using the elapsed time (Earth's sidereal rotation; at t = 0 both frames
    /// coincide). See MTS.Core.Geo for the axis convention (Unity +Y is the rotation axis).
    /// </summary>
    public static void ComputeImpactLocation()
    {
        SimulationSession session = SimulationSession.Instance;
        if (session.Runner == null)
        {
            Latitude = 0;
            Longitude = 0;
            return;
        }

        MeteorState end = session.Result != null ? session.Result.FinalState : session.Runner.State;
        Geo.LatitudeLongitude(end.Position, end.Time, out Latitude, out Longitude);
    }
}

// Shape of Assets/StreamingAssets/secrets.json (see secrets.example.json)
[Serializable]
class Secrets
{
    public string geocodeMapsCoApiKey;
}

// Loads the geocode.maps.co API key without it being stored in the code
static class ApiKeyLoader
{
    const string EnvironmentVariable = "GEOCODE_MAPS_CO_API_KEY";
    const string Placeholder = "YOUR_GEOCODE_MAPS_CO_API_KEY";

    // Returns the API key, or null if none has been set up
    public static string GetGeocodeApiKey()
    {
        // 1. Environment variable
        string key = Environment.GetEnvironmentVariable(EnvironmentVariable);

        // 2. Fall back to Assets/StreamingAssets/secrets.json (gitignored)
        if (string.IsNullOrWhiteSpace(key))
        {
            string path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
            try
            {
                if (File.Exists(path))
                {
                    Secrets secrets = JsonUtility.FromJson<Secrets>(File.ReadAllText(path));
                    if (secrets != null)
                    {
                        key = secrets.geocodeMapsCoApiKey;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not read " + path + ": " + e.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(key) || key.Trim() == Placeholder)
        {
            return null;
        }

        return key.Trim();
    }
}


// Reverse geocoding through geocode.maps.co
static class ReverseGeocoding
{
    [Serializable]
    private class ReverseResult
    {
        public string display_name;
        public string error;
    }

    // Looks up a place name for the given latitude/longitude (degrees) and writes it into the text field.
    public static IEnumerator ReverseGeocode(double latitude, double longitude, TMP_Text output, Action<UnityWebRequest> onRequest)
    {
        // Without an API key the rest of the simulation still works, just without a place name
        string apiKey = ApiKeyLoader.GetGeocodeApiKey();
        if (apiKey == null)
        {
            Debug.LogWarning("No geocode.maps.co API key found. Set GEOCODE_MAPS_CO_API_KEY or create Assets/StreamingAssets/secrets.json (see the README).");
            SetText(output, "Location lookup unavailable - add an API key");
            yield break;
        }

        string apiUrl = string.Format(CultureInfo.InvariantCulture,
            "https://geocode.maps.co/reverse?lat={0:F6}&lon={1:F6}&api_key={2}",
            latitude, longitude, Uri.EscapeDataString(apiKey));

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            onRequest?.Invoke(request);
            yield return request.SendWebRequest();
            onRequest?.Invoke(null);

            if (output == null)
            {
                yield break; // scene was unloaded
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogWarning("Reverse geocoding request failed: " + request.error);
                SetText(output, "Location lookup unavailable - check your internet connection");
                yield break;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Error: {request.responseCode} - {request.error}");
                if (body.Contains("Unable to geocode"))
                {
                    SetText(output, "Location is in the sea");
                }
                else if (request.responseCode == 401 || request.responseCode == 403)
                {
                    SetText(output, "Location lookup unavailable - API key was rejected");
                }
                else
                {
                    SetText(output, "Location lookup unavailable");
                }

                yield break;
            }

            ReverseResult parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<ReverseResult>(body);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not parse reverse geocoding response: " + e.Message);
            }

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.display_name))
            {
                // geocode.maps.co answers with an error (or no address) for points in the sea
                SetText(output, "Location is in the sea");
                yield break;
            }

            // The display name runs from the most to the least specific place; show it country first.
            string[] parts = parsed.display_name.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            Array.Reverse(parts);
            SetText(output, string.Join(", ", parts));
        }
    }

    private static void SetText(TMP_Text output, string text)
    {
        if (output != null)
        {
            output.text = text;
        }
    }
}
