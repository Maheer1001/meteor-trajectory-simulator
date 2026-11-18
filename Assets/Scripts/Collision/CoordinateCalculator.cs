// Import necessary namespaces
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

// Main script for calculating coordinates and running the simulation
public class CoordinateCalculator : MonoBehaviour
{
    private Vector3 position;
    public static double Latitude;
    public static double Longitude;

    // Reference to the TMP_Text component
    public TMP_Text JsonParseText;

    // Start method is called when the script is first run
    void Start()
    {
        // Calculate initial position in Cartesian coordinates based on some calculations
        position.x = Xcord.X * (6371000 / (float)Meteor_Force.Oradius) * 4050;
        position.y = Ycord.Y * (6371000 / (float)Meteor_Force.Oradius) * 4050;
        position.z = Zcord.Z * (6371000 / (float)Meteor_Force.Oradius) * 4050;

        // Call the Main method to set up the simulation
        Main();
    }

    // Main method to set up and run the simulation
    async void Main()
    {
        // Create initial coordinates using Cartesian coordinates
        ICoordinates initialCoordinates = new CartesianCoordinates(position.x, position.y, position.z);

        // Create a motion simulation instance with parameters from a Dictionary
        Dictionary<string, double> motionParameters = new Dictionary<string, double>
        {
            { "RotationRate", 7.2722e-5 },
            { "AxialTilt", Math.PI * 23.5 / 180 },
            { "OrbitalInclination", Math.PI * 0.00005 },
            { "OrbitalPeriod", 365.25 * 24 * 3600 },
            { "PrecessionRate", Math.PI * 0.000014 },
            { "NutationAmplitude", Math.PI * 0.000005 },
            { "NutationFrequency", Math.PI * 0.000001 }
        };

        IMotionSimulation motionSimulation = new MotionSimulation(motionParameters);

        // Create an Earth object with initial coordinates and motion simulation
        Earth earth = new Earth(initialCoordinates, motionSimulation);

        // Use the actual time from Meteor_Force.time (in seconds) for the simulation
        double timeInSeconds = Meteor_Force.time;

        // Update coordinates based on the simulated motion
        MotionUpdateResult updateResult = earth.UpdateCoordinates(timeInSeconds);

        Latitude = updateResult.NewLatitude;
        Longitude = updateResult.NewLongitude;

        // Pass the updated latitude and longitude to ReverseGeocoding
        await ReverseGeocoding.ReverseGeocode(updateResult.NewLatitude, updateResult.NewLongitude, JsonParseText);
    }

    // A readonly struct representing geographic coordinates (latitude and longitude)
    readonly struct Coordinates
    {
        public double Latitude { get; }
        public double Longitude { get; }

        // Constructor to initialize coordinates
        public Coordinates(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }

    // A struct representing Cartesian coordinates and implementing ICoordinates interface
    struct CartesianCoordinates : ICoordinates
    {
        private readonly double x;
        private readonly double y;
        private readonly double z;

        // Constructor to initialize Cartesian coordinates
        public CartesianCoordinates(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        // Convert Cartesian coordinates to geographic coordinates
        public Coordinates GetCoordinates()
        {
            // Convert Cartesian coordinates to latitude and longitude on Earth's surface
            double latitude = Math.Asin(z / 6371); // Assuming Earth radius is 6371 km
            double longitude = Math.Atan2(y, x);

            return new Coordinates(latitude, longitude);
        }
    }

    // Interface for providing geographic coordinates
    interface ICoordinates
    {
        Coordinates GetCoordinates();
    }

    // A readonly struct representing the result of a motion update
    readonly struct MotionUpdateResult
    {
        public double NewLatitude { get; }
        public double NewLongitude { get; }

        // Constructor to initialize motion update result
        public MotionUpdateResult(double newLatitude, double newLongitude)
        {
            NewLatitude = newLatitude;
            NewLongitude = newLongitude;
        }
    }

    // Interface for simulating Earth's motion
    interface IMotionSimulation
    {
        MotionUpdateResult UpdateMotion(Coordinates initialCoordinates, double timeInSeconds);
    }

    // A readonly struct implementing IMotionSimulation for Earth's motion simulation
    readonly struct MotionSimulation : IMotionSimulation
    {
        // Dictionary to store motion parameters
        private readonly Dictionary<string, double> motionParameters;

        // Constructor to initialize motion parameters
        public MotionSimulation(Dictionary<string, double> parameters)
        {
            motionParameters = parameters;
        }

        // Method to update Earth's motion based on time
        public MotionUpdateResult UpdateMotion(Coordinates initialCoordinates, double timeInSeconds)
        {
            // Access parameters from the Dictionary
            double deltaLongitude = motionParameters["RotationRate"] * timeInSeconds;
            double deltaLatitude = motionParameters["AxialTilt"] * Math.Cos(motionParameters["OrbitalInclination"]) * Math.Sin(2 * Math.PI * timeInSeconds / motionParameters["OrbitalPeriod"]);

            // Update latitude and longitude based on calculated changes
            double newLongitude = initialCoordinates.Longitude + deltaLongitude;
            double newLatitude = initialCoordinates.Latitude + deltaLatitude;

            // Calculate additional changes due to precession and nutation
            double deltaPrecession = motionParameters["PrecessionRate"] * timeInSeconds;
            double deltaNutation = motionParameters["NutationAmplitude"] * Math.Sin(motionParameters["NutationFrequency"] * timeInSeconds);

            // Update longitude with precession and nutation changes
            double updatedLongitude = newLongitude + deltaPrecession + deltaNutation;

            // Return the updated motion result
            return new MotionUpdateResult(newLatitude, updatedLongitude);
        }
    }

    // Class representing Earth with coordinates and motion simulation
    class Earth
    {
        private readonly ICoordinates coordinates;
        private readonly IMotionSimulation motionSimulation;

        // Constructor to initialize Earth with coordinates and motion simulation
        public Earth(ICoordinates coordinates, IMotionSimulation motionSimulation)
        {
            this.coordinates = coordinates;
            this.motionSimulation = motionSimulation;
        }

        // Method to update Earth's coordinates based on time
        public MotionUpdateResult UpdateCoordinates(double timeInSeconds)
        {
            // Get initial coordinates
            Coordinates initialCoordinates = coordinates.GetCoordinates();

            // Update motion based on the provided time
            return motionSimulation.UpdateMotion(initialCoordinates, timeInSeconds);
        }
    }
}

// Class for reverse geocoding
class ReverseGeocoding
{
    public static TMP_Text JsonParse;
    // Asynchronous method to reverse geocode given latitude and longitude
    public static async Task<string> ReverseGeocode(double latitude, double longitude, TMP_Text JsonParseText)
    {
        using (HttpClient client = new HttpClient())
        {
            // Construct the API URL with latitude, longitude, and API key
            string apiUrl = $"https://geocode.maps.co/reverse?lat={latitude}&lon={longitude}&api_key=YOUR_GEOCODE_MAPS_CO_API_KEY";

            // Send a GET request to the API
            HttpResponseMessage response = await client.GetAsync(apiUrl);

            JsonParse = JsonParseText;

            // Check if the response is successful
            if (response.IsSuccessStatusCode)
            {
                // Read the response content as a string
                string result = await response.Content.ReadAsStringAsync();

                // Check for specific conditions indicating location not found or in the sea
                if (result.Contains("Unable to geocode"))
                {
                    JsonParse.text = ("Location is in the sea");
                    return null;
                }

                // Manually parse JSON (improved example)
                int displayIndex = result.IndexOf("\"display_name\"");

                if (displayIndex != -1)
                {
                    int colonIndex = result.IndexOf(":", displayIndex);

                    if (colonIndex != -1)
                    {
                        int commaIndex = result.IndexOf(",", colonIndex);
                        int endIndex = result.LastIndexOf('}', colonIndex);

                        if (commaIndex != -1 && endIndex != -1)
                        {
                            // Extract and trim the display name
                            string displayName = result.Substring(colonIndex + 1, endIndex - colonIndex).Trim(' ', '"', '{', '}');
                            Debug.Log("Display Name: " + displayName);

                            // Split the string into an array
                            string[] displayNameArray = displayName.Split(',');

                            // Reverse the array
                            Array.Reverse(displayNameArray);

                            // Join the elements back into a string
                            string reversedDisplayName = string.Join(", ", displayNameArray);

                            Debug.Log("Reversed Display Name: " + reversedDisplayName);

                            JsonParse.text = (reversedDisplayName);

                            return reversedDisplayName;

                        }
                    }
                }

                Debug.Log("Failed to extract Display Name from JSON.");
                return null;
            }
            else
            {
                // Log error information if the request is not successful
                Debug.Log($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
    }
}