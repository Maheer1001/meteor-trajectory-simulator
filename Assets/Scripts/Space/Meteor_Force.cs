using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Properties;

public class Meteor_Force : MonoBehaviour
{
    // Public Meteor Force Properties
    public static Vector3 position;
    public static float time;
    public static Vector3 velocity;
    public static float stepSize = 1.0f / 60.0f; // Default value
    public static float mass = MassRangeInput.Mass; // User input 
    public static float SHC = SHCRangeInput.SHC; // User input
    public static float meteor_radius;
    public static float SCF = SCFRangeInput.SCF; // User input 
    public static double p_meteor = DensityRangeInput.Density; // User input
    public static double Oradius;
    public static float temperature;
    public static double DistanceTrav = 0;
    public static List<double> Min = new List<double>();
    public static List<double> TenMin = new List<double>();
    public static List<double> ThirtyMin = new List<double>();
    public static int IncrX = 1;
    public static int IncrY = 1;
    public static int IncrZ = 1;
    public static Stack<double> valueStack = new Stack<double>();
    public static int Stage = 1;
    public static bool isPaused = false;
    public static double gravitationalAcc;
    public static double radius;

    // Private Meteor Force Properties
    private float Spacetemp = 2.7f; // Kelvin 

    // Physical Constants
    private const float G = 6.67430e-11f;
    private const float EarthMass = 5.972e24f;
    private const float Boltzmann = 5.7e-8f;
    private const float EmissivityBase = 0.8f;
    private const float SCFBase = 0.01f;
    private const double pi = Math.PI;

    // UI Elements
    public TMP_Text Timestamp;
    public TMP_Text DistanceRemaining;
    public TMP_Text MeteorVelocity;
    public TMP_Text MeteorAcceleration;
    public UnityEngine.UI.Slider slider;
    public string sceneName;

    // Initialisation on Start
    void Start()
    {
        if (MainMenuButton.InitialisationCountS == false)
        {
            InitialiseMeteor();
            RK4loop();
            UpdatePosition();
            ListElementUpdate();
            InitialTemperatureCalculation();
            UpdateUI();
        }
    }

    // Physics Update in FixedUpdate
    void FixedUpdate()
    {
        if (!isPaused)
        {
            TimeScale();
            RK4loop();
            UpdatePosition();
            TempRateSpace();
            UpdateUI();
            ListElementUpdate();
            AtmosphereChecker(sceneName);
            Debug.Log(temperature);
        }
    }

    // Initialization of Meteor Properties
    private void InitialiseMeteor()
    {
        time = 0;
        meteor_radius = (float)Math.Pow(((3.0f * MassRangeInput.Mass) / (4.0f * (float)pi * p_meteor)), (1.0f / 3.0f));
        position = new Vector3(Xcord.X, Ycord.Y, Zcord.Z);
        radius = (position.magnitude * 4.05e6f);
        Oradius = radius;
        velocity = Vector3.zero;
    }

    // Runge-Kutta 4th Order Integration Loop
    private void RK4loop()
    {
        float k1velocity = (float)GravitationalAcceleration(radius);
        float k2velocity = (float)GravitationalAcceleration(radius + k1velocity * stepSize / 2);
        float k3velocity = (float)GravitationalAcceleration(radius + k2velocity * stepSize / 2);
        float k4velocity = (float)GravitationalAcceleration(radius + k3velocity * stepSize);

        Vector3 accelerationChange = new Vector3(
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * stepSize / 6),
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * stepSize / 6),
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * stepSize / 6)
        );

        velocity += accelerationChange;
    }

    // Calculate Gravitational Acceleration at Given Radius
    private double GravitationalAcceleration(double radius)
    {
        return -((G * EarthMass) / (radius * radius));
    }

    // Update Position Based on Velocity
    private void UpdatePosition()
    {
        Vector3 displacementChange = velocity * stepSize;
        radius -= displacementChange.magnitude;
        DistanceTrav += displacementChange.magnitude;

        position.x = Xcord.X * (float)(radius / Oradius);
        position.y = Ycord.Y * (float)(radius / Oradius);
        position.z = Zcord.Z * (float)(radius / Oradius);

        transform.position = position;
        gravitationalAcc = GravitationalAcceleration(radius);
    }

    private void ListElementUpdate()
    {
        // Update Min list every 60 seconds
        if ((Math.Floor(time / 60) == IncrX && time < 12000 && time != 0))
        {
            Min.Add(DistanceTrav);
            Debug.Log("Added to Min list: ");
            IncrX++;
        }

        // Update TenMin list every 10 minutes
        if ((Math.Floor(time / 600) == IncrY && time < 126000 && time != 0))
        {
            TenMin.Add(DistanceTrav);
            Debug.Log("Added to TenMin list: ");
            IncrY++;
        }

        // Update ThirtyMin list every 30 minutes
        if ((Math.Floor(time / 1800) == IncrZ && time != 0))
        {
            ThirtyMin.Add(DistanceTrav);
            Debug.Log("Added to ThirtyMin list: ");
            IncrZ++;
        }
    }

    // Initial Temperature Calculation
    private void InitialTemperatureCalculation()
    {
        float vInitial = 20.0f;
        float emissivity = EmissivityBase + (SCFBase * SCF);
        float pi = (float)Math.PI;
        float surfaceArea = (4.0f * pi * (float)Math.Pow(meteor_radius, 2));
        temperature = (((0.5f * mass * (float)Math.Pow(vInitial, 2f)) - (emissivity * Boltzmann * surfaceArea * (float)Math.Pow(Spacetemp, 4f))) / ((mass * SHC) + (emissivity * Boltzmann * surfaceArea)));
    }

    // Update Time Scale Based on Slider
    private void TimeScale()
    {
        float sliderValue = slider.value;
        stepSize = sliderValue * (1.0f / 60.0f);
    }

    // Temperature Rate Calculation in Space
    private void TempRateSpace()
    {
        float emissivity = EmissivityBase + (SCFBase * SCF) + (0.01f * (float)Math.Pow(temperature / 100f, 2f));
        float pi = (float)Math.PI;
        float surfaceArea = (4.0f * pi * (float)Math.Pow(meteor_radius, 2));
        float dT_dt = ((-emissivity * Boltzmann * surfaceArea) * ((float)Math.Pow(temperature, 4.0f) - (float)Math.Pow(Spacetemp, 4.0f)) / (mass * SHC));
        temperature += (dT_dt) * (stepSize);
    }

    // Update UI Elements
    private void UpdateUI()
    {
        time += stepSize;
        Timestamp.text = Mathf.Round(time).ToString();
        DistanceRemaining.text = ((double)(radius - 6456000)).ToString("F2");
        MeteorVelocity.text = ((double)velocity.magnitude).ToString("F9");
        MeteorAcceleration.text = ((double)-gravitationalAcc).ToString("F9");
    }

    // Check for Atmosphere Entry and Load New Scene
    private void AtmosphereChecker(string sceneName)
    {
        if (radius <= 6459000)
        {
            SavePlayerPrefs();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }

    // Save Player Preferences for Later Use
    private void SavePlayerPrefs()
    {
        PlayerPrefs.SetFloat("time", time);
        PlayerPrefs.SetFloat("velocity.x", velocity.x);
        PlayerPrefs.SetFloat("velocity.y", velocity.y);
        PlayerPrefs.SetFloat("velocity.z", velocity.z);
        PlayerPrefs.SetFloat("temperature", temperature);
        PlayerPrefs.SetFloat("mass", mass);
        PlayerPrefs.SetFloat("Density", SCF);
        PlayerPrefs.SetFloat("meteor_radius", meteor_radius);
    }

    // Handle Pause Button Pressed
    public void PauseButtonPressed()
    {
        if (!isPaused)
        {
            isPaused = true;
            PushToStack(time);
            PushToStack(radius);
            PushToStack(velocity.x);
            PushToStack(velocity.y);
            PushToStack(velocity.z);
            PushToStack(gravitationalAcc);
            Time.timeScale = 0;
        }
    }

    // Push a Value to the Stack
    private void PushToStack(double value)
    {
        valueStack.Push(value);
    }

    // Handle Play Button Pressed
    public void PlayButtonPressed()
    {
        if (isPaused)
        {
            isPaused = false;
            gravitationalAcc = PopFromStack();
            velocity.z = (float)PopFromStack();
            velocity.y = (float)PopFromStack();
            velocity.x = (float)PopFromStack();
            radius = PopFromStack();
            time = (float)PopFromStack();
            Time.timeScale = 1;
        }
    }

    // Pop a Value from the Stack
    private double PopFromStack()
    {
        if (valueStack.Count > 0)
        {
            return valueStack.Pop();
        }
        else
        {
            return 0.0f;
        }
    }
}