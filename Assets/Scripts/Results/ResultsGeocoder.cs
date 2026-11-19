using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace MTS.Results
{
    /// <summary>
    /// Loads the geocode.maps.co API key without it being stored in the code: the GEOCODE_MAPS_CO_API_KEY environment
    /// variable, else Assets/StreamingAssets/secrets.json (gitignored; see secrets.example.json).
    /// </summary>
    public static class GeocodeApiKey
    {
        public const string EnvironmentVariable = "GEOCODE_MAPS_CO_API_KEY";
        private const string Placeholder = "YOUR_GEOCODE_MAPS_CO_API_KEY";

        [Serializable]
        private class Secrets
        {
            public string geocodeMapsCoApiKey;
        }

        /// <summary>The API key, or null if none has been set up.</summary>
        public static string Load()
        {
            string key = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(key))
            {
                string path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
                try
                {
                    if (File.Exists(path))
                    {
                        Secrets secrets = JsonUtility.FromJson<Secrets>(File.ReadAllText(path));
                        key = secrets?.geocodeMapsCoApiKey;
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

    /// <summary>Reverse geocoding of the event location through geocode.maps.co.</summary>
    public static class ResultsGeocoder
    {
        [Serializable]
        private class ReverseResult
        {
            public string display_name;
            public string error;
        }

        /// <summary>
        /// Looks up a place name and reports it through <paramref name="onName"/> (country first). Points in the sea get
        /// an approximate ocean name. <paramref name="onRequest"/> receives the live request (null when finished) so the
        /// caller can abort it when the scene unloads.
        /// </summary>
        public static IEnumerator Lookup(double latitude, double longitude, Action<string> onName, Action<UnityWebRequest> onRequest)
        {
            string apiKey = GeocodeApiKey.Load();
            if (apiKey == null)
            {
                Debug.LogWarning("No geocode.maps.co API key found. Set " + GeocodeApiKey.EnvironmentVariable +
                                 " or create Assets/StreamingAssets/secrets.json (see the README).");
                onName?.Invoke(""); // coordinates only; the log explains how to add a key
                yield break;
            }

            string url = string.Format(CultureInfo.InvariantCulture,
                "https://geocode.maps.co/reverse?lat={0:F6}&lon={1:F6}&api_key={2}",
                latitude, longitude, Uri.EscapeDataString(apiKey));

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 15;
                onRequest?.Invoke(request);
                yield return request.SendWebRequest();
                onRequest?.Invoke(null);

                string sea = OceanNames.Guess(latitude, longitude);
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.LogWarning("Reverse geocoding request failed: " + request.error);
                    onName?.Invoke("Place lookup failed - check your internet connection");
                    yield break;
                }

                string body = request.downloadHandler != null ? request.downloadHandler.text : "";
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Reverse geocoding error: " + request.responseCode + " - " + request.error);
                    if (body.Contains("Unable to geocode"))
                    {
                        onName?.Invoke(sea);
                    }
                    else if (request.responseCode == 401 || request.responseCode == 403)
                    {
                        onName?.Invoke("Place lookup unavailable - API key was rejected");
                    }
                    else
                    {
                        onName?.Invoke("Place lookup unavailable");
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
                    onName?.Invoke(sea);
                    yield break;
                }

                onName?.Invoke(CountryFirst(parsed.display_name));
            }
        }

        /// <summary>"Street, Town, County, Country" becomes "Country, County, Town" (at most three parts, no numbers).</summary>
        public static string CountryFirst(string displayName)
        {
            string[] parts = displayName.Split(',');
            var kept = new System.Collections.Generic.List<string>();
            for (int i = parts.Length - 1; i >= 0 && kept.Count < 3; i--)
            {
                string p = parts[i].Trim();
                if (p.Length == 0 || char.IsDigit(p[0]))
                {
                    continue; // postcodes and house numbers
                }

                kept.Add(p);
            }

            return string.Join(", ", kept);
        }
    }
}
