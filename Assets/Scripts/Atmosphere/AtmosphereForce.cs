using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Specialized;
using Unity.VisualScripting;

// This script defines a class named Atmosphere_Force responsible for handling the physics and simulation 
// of a meteor's descent through the Earth's atmosphere.

public class Atmosphere_Force : MonoBehaviour
{
    // Public Meteor Properties
    public static Vector3 position;
    public static float DragCoffiecient; 
    public TMP_Text Timestamp;
    public TMP_Text DistanceToEarth;
    public TMP_Text MeteorVelocity;
    public TMP_Text MeteorAcceleration;
    public TMP_Text MeteorMass;
    public TMP_Text MeteorRadius;
    public TMP_Text AtmosphereTemperature;
    public TMP_Text AtmospherePressure;
    public UnityEngine.UI.Slider slider;
    public UnityEngine.UI.Slider slider2;
    public UnityEngine.UI.Slider slider3;
    public static float distance = 6456000.0f;
    public static float gravitationalAcc;
    public static bool isPaused = false;
    public string sceneName1;
    public string sceneName2;

    // Private Meteor Properties
    private float SHC = Meteor_Force.SHC;
    private float altitude = 85000.0f;
    private float Gas_Constant = 287.1f;
    private float StepSize = 1.0f / 60.0f;
    private float xcord;
    private float ycord;
    private float zcord;
    
    private float pi = (float)Math.PI;
    private float v_air = 15;
    private float radius;

    private void Start()
    {
        if (MainMenuButton.InitialisationCountA == false)
        {
            Meteor_Force.Stage = 2;
            InitialisePosition();
        }
    }
    void FixedUpdate()
    {
        if (isPaused == false)
        {
            Debug.Log("start cycle");
            TimeScale();
            DragCoffiecient = DragCoefficient();
            Windspeed();
            Wind_Direction();
            RK4withDrag();
            TempRateATM(); // update temp
            MassRateChange(); // update mass
            UpdatePosition();
            UpdateUI();
            ListElementUpdate();
            DisintegratorChecker(sceneName1);
            Collisionchecker(sceneName2);
        }
    }

    // Initialize the meteor's position
    private void InitialisePosition()
    {
        position.x = Xcord.X * (6546000 / (float)Meteor_Force.Oradius) * 4050f;
        position.y = Ycord.Y * (6546000 / (float)Meteor_Force.Oradius) * 4050f;
        position.z = Zcord.Z * (6546000 / (float)Meteor_Force.Oradius) * 4050f;
        xcord = position.x;
        ycord = position.y;
        zcord = position.z;
        transform.position = position;
        radius = position.magnitude * 1000;
    }

    // Update the time scale based on the slider value
    private void TimeScale() //Works 100%
    {
        float sliderValue = slider.value;
        StepSize = 1.0f / 60.0f;
        StepSize = sliderValue * StepSize;
    }

    private void RK4withDrag() // which fulfils objective 5.2
    {
        // Runge-Kutta 4th Order Integration with Drag Calculation
        float k1velocity = (float)GravitationalAcceleration(distance);
        float k2velocity = (float)GravitationalAcceleration(distance + k1velocity * StepSize / 2);
        float k3velocity = (float)GravitationalAcceleration(distance + k2velocity * StepSize / 2);
        float k4velocity = (float)GravitationalAcceleration(distance + k3velocity * StepSize);

        // Calculate the new velocity based on the change in acceleration
        Vector3 accelerationChange = new Vector3(
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * StepSize / 6),
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * StepSize / 6),
            (float)((k1velocity + 2 * k2velocity + 2 * k3velocity + k4velocity) * StepSize / 6)
        );
        Meteor_Force.velocity += accelerationChange;

        Debug.Log(accelerationChange);
    }

    // Calculate gravitational acceleration at a given radius
    private double GravitationalAcceleration(float radius) 
    {
        float G = -6.67430e-11f; // Gravitational constant
        float earthMass = 5.972e24f; // Mass of the Earth
        float gravitationalForce = (G * earthMass * Meteor_Force.mass) / (radius * radius);
        float dragForce = DragForce(); // Calculate drag force 
        float netForce = gravitationalForce + dragForce; // drag force is negative
        float acceleration = netForce / Meteor_Force.mass; // Calculate acceleration
        return acceleration;
    }

    // Calculate drag force
    private float DragForce() 
    {
        float AirPressure = AirPressureVariation();
        float AirTemperature = AirTemperatureVariation();
        float p_air = AirPressure / (Gas_Constant * AirTemperature);
        float CSA = pi * (float)Math.Pow(Meteor_Force.meteor_radius, 2f);
        float v_mra = Meteor_Force.velocity.magnitude / v_air;
        float Drag = 0.5f * p_air * CSA * DragCoffiecient * (float)Math.Pow(v_mra, 2f);
        return Drag;
    }

    // Calculate drag coefficient
    private float DragCoefficient() //which fulfils objective 5.3
    {
        float AirPressure = AirPressureVariation();
        float AirTemperature = AirTemperatureVariation();
        float p_air = AirPressure / (Gas_Constant * AirTemperature);
        Debug.Log(Meteor_Force.temperature);
        float dynamic_viscosity = 0.03f * (float)Math.Pow((Meteor_Force.temperature / 300), 1.5) * ((300 + 110) / (Meteor_Force.temperature + 110));
        float v_arm = v_air / Meteor_Force.velocity.magnitude;
        float Re = p_air * v_arm * Meteor_Force.meteor_radius * 2f / dynamic_viscosity;
        float firstTerm = 24 / Re;
        float secondTerm = (2.6f * (Re / 5f)) / (1f + Mathf.Pow((Re / 5f), 1.52f));
        double numerator = 0.441f;
        double denominatorleft = Math.Pow((Re / 2.63e5), 7.94);
        double denominaterright = Math.Pow((Re / 2.63e5), -0.06);
        double thirdTerm = numerator / (denominatorleft + denominaterright);
        float fourthTerm = (0.25f * (Re / 1e6f)) / (1f + (Re / 1e6f));
        DragCoffiecient = firstTerm + secondTerm + (float)thirdTerm + fourthTerm;
        return DragCoffiecient;
    }

    // Calculate air pressure variation with altitude which fulfils objective 5.1
    private float AirPressureVariation() 
    {
        float AirPressure;
        if (altitude <= 12000)
        {
            AirPressure = 101325f * ((float)Math.Pow((1 - ((-0.0065f * altitude) / 288.15f)), (9.81f / (-0.0065f * Gas_Constant))));
        }
        else if (altitude <= 50000)
        {
            AirPressure = 101325f * ((float)Math.Pow((1 - ((-0.0002f * altitude) / 288.15f)), (9.81f / (-0.0002f * Gas_Constant))));
        }
        else
        {
            AirPressure = 101325f * ((float)Math.Pow((1 - ((-0.0001f * altitude) / 288.15f)), (9.81f / (-0.0001f * Gas_Constant))));
        }
        return AirPressure;
    }

    // Calculate air temperature variation with altitude which fulfils objective 5.1
    private float AirTemperatureVariation() 
    {
        float AirTemperature;
        if (altitude <= 12000)
        {
            AirTemperature = 288.15f - (0.0065f * altitude);
        }
        else if (altitude <= 50000)
        {
            AirTemperature = 210.15f - (0.0002f * (altitude - 12000));
        }
        else
        {
            AirTemperature = 134.15f - (0.0001f * (altitude - 50000));
        }
        return AirTemperature;
    }

    // Calculate the rate of temperature change in the atmosphere 
    private float TempRateATM() //which fulfils objective 5.7
    {
        float AerodynamicHeating = Aerodynamic_Heating(); 
        float ConvectiveCooling = Convective_Cooling(); 
        float RadiativeCooling = Radiative_Cooling();
        double dT_dt = (AerodynamicHeating - ConvectiveCooling - RadiativeCooling) / (Meteor_Force.mass * SHC);
        float old_temp = Meteor_Force.temperature;
        Meteor_Force.temperature += (float)(dT_dt * StepSize);
        return old_temp;
    }

    // Calculate aerodynamic heating
    private float Aerodynamic_Heating() //which fulfils objective 5.4
    {
        float AirPressure = AirPressureVariation();
        float AirTemperature = AirTemperatureVariation();
        float p_air = AirPressure / (Gas_Constant * AirTemperature);
        float n = 5; //DEGREES OF FREEDOM
        float Cp = (((2 * Gas_Constant) + (Gas_Constant * n)) / n);
        float AerodynamicHeating = 0.5f * p_air * (float)Math.Pow(Meteor_Force.velocity.magnitude, 2) * Cp;
        return Math.Abs(AerodynamicHeating);
    }

    // Calculate convective cooling
    private float Convective_Cooling() //which fulfils objective 5.5
    {
        float AirPressure = AirPressureVariation();
        float AirTemperature = AirTemperatureVariation();
        float p_air = AirPressure / (Gas_Constant * AirTemperature);
        float dynamic_viscosity = 0.03f * (float)Math.Pow((Meteor_Force.temperature / 288.15f), 1.5f) * ((288.15f + 110f) / (Meteor_Force.temperature + 110f));
        float v_arm = v_air / Meteor_Force.velocity.magnitude;
        float Re = p_air * v_arm * Meteor_Force.meteor_radius * 2 / dynamic_viscosity;
        float Nusselt = 0.023f * ((float)Math.Pow(Re, 0.5)) * ((float)Math.Pow(0.7, (1.0 / 3.0)));
        float Thermal_conduct = 320; //typical value for gold tbd for user input 
        float CHC = Thermal_conduct * Nusselt / (Meteor_Force.meteor_radius * 2);
        float SA = 4 * pi * (float)Math.Pow(Meteor_Force.meteor_radius, 2f);
        float Tempdiff = AirTemperature - Meteor_Force.temperature;
        float ConvectiveCooling = CHC * SA * Tempdiff;
        return Math.Abs(ConvectiveCooling);
    }

    // Calculate radiative cooling
    private float Radiative_Cooling() //which fulfils objective 5.6
    {
        float Boltzmann = 5.7e-8f;
        float emissivity = 0.8f + (0.2f * Meteor_Force.SCF) + (0.01f * (float)Math.Pow(Meteor_Force.temperature / 100f, 2f));
        float Surface_area = (4 * pi * (float)Math.Pow(Meteor_Force.meteor_radius, 2));
        float temperatureATM = AirTemperatureVariation();
        float RadiativeCooling = emissivity * Boltzmann * Surface_area * (((float)Math.Pow(Meteor_Force.temperature, 4)) - ((float)Math.Pow(temperatureATM, 4)));
        return Math.Abs(RadiativeCooling);
    }

    private void MassRateChange()
    {
        float CSA = pi * (float)Math.Pow(Meteor_Force.meteor_radius, 2);
        float AirPressure = AirPressureVariation();
        float AirTemperature = AirTemperatureVariation();
        float p_air = AirPressure / (Gas_Constant * AirTemperature);
        float dm_dt = CSA * Meteor_Force.velocity.magnitude * p_air;
        Meteor_Force.mass -= (dm_dt * StepSize);
        Meteor_Force.meteor_radius = (float)Math.Pow(((3 * Meteor_Force.mass) / (4 * pi * Meteor_Force.p_meteor)), (1.0f / 3.0f));
    }

    // Calculate the rate of mass change
    //    private void MassRateChange() //which fulfils objective 5.8
    //   {
    //        float v_mra = Meteor_Force.velocity.magnitude / v_air;
    //        float CSA = pi * (float)Math.Pow(Meteor_Force.meteor_radius, 2);
    //        float Ablation_Coefficient = AblationCoefficient();
    //        float dm_dt = ((float)Meteor_Force.p_meteor) * v_mra * CSA * Ablation_Coefficient;
    //        UnityEngine.Debug.Log("Mass" + Meteor_Force.mass);
    //        Meteor_Force.mass -= (dm_dt * StepSize);
    //        Meteor_Force.meteor_radius = (float)Math.Pow(((3 * Meteor_Force.mass) / (4 * pi * Meteor_Force.p_meteor)), (1 / 3)); //changing 1/3 to 1.0f / 3.0f breaks sim. 
    //    }

    //    private float AblationCoefficient()
    //    {
    //       float AirPressure = AirPressureVariation();
    //        float AirTemperature = AirTemperatureVariation();
    //        float p_air = AirPressure / (Gas_Constant * AirTemperature);
    //        float dynamic_viscosity = 0.03f * (float)Math.Pow((Meteor_Force.temperature / 300), 1.5) * ((300 + 110) / (Meteor_Force.temperature + 110));
    //        float v_arm = v_air / Meteor_Force.velocity.magnitude;
    //        float Re = p_air * v_arm * Meteor_Force.meteor_radius * 2 / dynamic_viscosity;
    //        float Nusselt = 0.023f * ((float)Math.Pow(Re, 0.5)) * 1 * ((float)Math.Pow(0.7, (1.0f / 3.0f)));
    //        float Thermal_conduct = 320; //typical value for gold tbd for user input 
    //        float CHC = Thermal_conduct * Nusselt / (Meteor_Force.meteor_radius * 2);
    //        float Cd = DragCoefficient();
    //        float old_temp = TempRateATM();
    //        float DeltaT = (float)(Meteor_Force.temperature - old_temp);
    //        float Q_ablation = SHC * DeltaT;
    //        float Ablation_Coefficient = (CHC / (2 * Cd * Q_ablation));
    //        return Ablation_Coefficient;
    //    }

    // Update the meteor's position based on its velocity
    private void UpdatePosition()
    {
        // Calculate DisplacementChange based on velocity
        UnityEngine.Vector3 DisplacementChange = Meteor_Force.velocity * StepSize;

        // Update the radius based on the displacement change (decrease the distance)
        distance -= DisplacementChange.magnitude;
        Meteor_Force.DistanceTrav += DisplacementChange.magnitude;
        altitude = distance - 6371000;

        // Update the position of the meteor using the velocity
        position.x = xcord * (distance / radius);
        position.y = ycord * (distance / radius);
        position.z = zcord * (distance / radius);

        // Apply the updated position to the Unity transform component
        transform.position = position;
    }

    // Update UI elements with relevant information
    void UpdateUI() 
    {
        gravitationalAcc = -(float)GravitationalAcceleration(distance);
        // Update the UI elements
        Meteor_Force.time += StepSize;
        Timestamp.text = Mathf.Round(Meteor_Force.time).ToString();
        DistanceToEarth.text = ((double)altitude).ToString("F3");
        MeteorVelocity.text = ((double)Meteor_Force.velocity.magnitude).ToString("F3");
        MeteorAcceleration.text = ((double)gravitationalAcc).ToString("F3");
        MeteorMass.text = (Meteor_Force.mass).ToString("F3");
        MeteorRadius.text = Meteor_Force.meteor_radius.ToString("F3");
        AtmosphereTemperature.text = AirTemperatureVariation().ToString("F3");
        AtmospherePressure.text = AirPressureVariation().ToString("F3");
    }

    // Update lists with new data
    private void ListElementUpdate()
    {
        if ((Math.Floor(Meteor_Force.time / 60) == Meteor_Force.IncrX && Meteor_Force.time < 12000))
        {
            Meteor_Force.Min.Add(Meteor_Force.DistanceTrav);
            Debug.Log("Added to Min list: " + Meteor_Force.DistanceTrav);
            Meteor_Force.IncrX++;
        }
        if ((Math.Floor(Meteor_Force.time / 600) == Meteor_Force.IncrY && Meteor_Force.time < 126000))
        {
            Meteor_Force.TenMin.Add(Meteor_Force.DistanceTrav);
            Debug.Log("Added to TenMin list: " + Meteor_Force.DistanceTrav);
            Meteor_Force.IncrY++;
        }
        if ((Math.Floor(Meteor_Force.time / 1800) == Meteor_Force.IncrZ))
        {
            Meteor_Force.ThirtyMin.Add(Meteor_Force.DistanceTrav);
            Debug.Log("Added to ThirtyMin list: " + Meteor_Force.DistanceTrav);
            Meteor_Force.IncrZ++;
        }
    }

    // Update windpseed based on seperate sliders for its magnitude and direction
    public void Windspeed() 
    {
        v_air = 15;
        float sliderValue = slider2.value;
        v_air = sliderValue;
    }

    public void Wind_Direction()
    {
        float radians = slider3.value * pi;
        v_air = v_air * Mathf.Cos(radians);
    }

    // Handle pause button press
    public void PauseButtonPressed()
    {
        if (!isPaused)
        {
            isPaused = true;
            PushToStack(Meteor_Force.time);
            PushToStack(distance);
            PushToStack(Meteor_Force.velocity.x);
            PushToStack(Meteor_Force.velocity.y);
            PushToStack(Meteor_Force.velocity.z);
            PushToStack(gravitationalAcc);
            PushToStack(Meteor_Force.mass);
            PushToStack(Meteor_Force.meteor_radius);
            Time.timeScale = 0;
        }
    }

    // Handle Play Button Pressed
    public void PlayButtonPressed()
    {
        if (isPaused)
        {
            isPaused = false;
            Meteor_Force.meteor_radius = (float)PopFromStack();
            Meteor_Force.mass = (float)PopFromStack();
            gravitationalAcc = (float)PopFromStack();
            Meteor_Force.velocity.z = (float)PopFromStack();
            Meteor_Force.velocity.y = (float)PopFromStack();
            Meteor_Force.velocity.x = (float)PopFromStack();
            distance = (float)PopFromStack();
            Meteor_Force.time = (float)PopFromStack();
            Time.timeScale = 1;
        }
    }

    // Push a Value to the Stack
    private void PushToStack(double value)
    {
        Meteor_Force.valueStack.Push(value);
    }

    // Pop a Value from the Stack
    private double PopFromStack()
    {
        if (Meteor_Force.valueStack.Count > 0)
        {
            return Meteor_Force.valueStack.Pop();
        }
        else
        {
            return 0.0f;
        }
    }

    // Check for meteor disintegration
    public void DisintegratorChecker(string sceneName) //which fulfils objective 5.9
    {
        if (Meteor_Force.mass <= 1f || Meteor_Force.meteor_radius <= 0.1f)
        {
            Time.timeScale = 0;
            Meteor_Force.Min.Clear();
            Meteor_Force.TenMin.Clear();
            Meteor_Force.ThirtyMin.Clear();
            Meteor_Force.valueStack.Clear();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }

    // Check for collision with Earth
    public void Collisionchecker(string sceneName) 
    {
        if (altitude <= 0)
        {
            Time.timeScale = 0;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}