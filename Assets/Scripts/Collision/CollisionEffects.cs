using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class CollisionEffects : MonoBehaviour
{
    // Public properties
    float[] overpressure = { 275.8f, 1034.2f, 4826.3f, 13789.5f, 20684.3f, 34473.8f, 48263.3f, 68947.6f, 199948.0f };
    public static double[] R_impact;
    public static double CraterRadius;
    public TMP_Text T1;
    public TMP_Text T2;
    public TMP_Text T3;
    public TMP_Text T4;
    public TMP_Text T5;
    public TMP_Text T6;
    public TMP_Text T7;
    public TMP_Text T8;
    public TMP_Text T9;
    public TMP_Text T10;

    // Called properties
    double fMass = Meteor_Force.mass;
    double fVelocity = Meteor_Force.velocity.magnitude;
    double fRadius = Meteor_Force.meteor_radius;
    double Pmeteor = DensityRangeInput.Density;

    // Private constants 
    private TMP_Text[] textArray;
    private const float G = 6.67430e-11f;
    private const float EarthMass = 5.972e24f;
    private const float EarthRadius = 6371e3f;
    private const double EarthDensity = 5515.0f;
    private const double EarthGravity = 9.81f;

    void Start()
    {
        // Initialize your array of TMP_Text
        textArray = new TMP_Text[] { T1, T2, T3, T4, T5, T6, T7, T8, T9, T10 };
        R_impact = new double[10];
        CraterRadius = CalculateCraterSize();
        for (int i = 0; i <= 9; i++)
        {
            if (i == 9)
            {
                R_impact[i] = CraterRadius;
            }
            else
            {
                R_impact[i] = ImpactRadius(overpressure[i]);
                Debug.Log(R_impact[i] + "This is R_Impact" + i);
            }
            // Use the textArray to set the text of TMP_Text
            textArray[i].text = R_impact[i].ToString();
        }
    }

    // Calculates kinetic energy
    public double KE()
    {
        double KineticEnergy = 0.5 * fMass * Math.Pow(fVelocity, 2);
        return KineticEnergy;
    }

    // Calculates crater size
    private double CalculateCraterSize()
    {
        double KineticEnergy = KE();
        double CraterDiameterCubed = (2 * KineticEnergy) / (Math.PI * Meteor_Force.meteor_radius * EarthDensity * EarthGravity);
        double CraterDiameter = Math.Pow(CraterDiameterCubed, (1.0f / 3.0f));
        double radius = CraterDiameter / 2;
        return radius;
    }

    // Calculates explosive energy
    private double ExplosiveEnergyCalculation()
    {
        float Cd = Atmosphere_Force.DragCoffiecient;
        Debug.Log(Cd + " Drag cof");
        float Airdensity = 1.225f;
        float CSA = (float)Math.PI * (float)Math.Pow(Meteor_Force.meteor_radius, 2f);
        Debug.Log(CSA + " CSA");
        float v_mra = Meteor_Force.velocity.magnitude / 15;
        Debug.Log(v_mra + " v_mra");
        double ExplosiveEnergy = (double)(0.5 * Cd * Airdensity * CSA * Math.Pow(v_mra, 3.0f));
        return ExplosiveEnergy;
    }

    // Calculates impact radius
    private double ImpactRadius(float overpressure)
    {
        double ExplosiveEnergy = ExplosiveEnergyCalculation();
        double CubeRootEE = Math.Pow(ExplosiveEnergy, 1.0f / 3.0f);
        double ConsDivOver = Math.Pow((2.83e5 / overpressure), 5.0f / 3.0f);
        double Impact_Radius = CubeRootEE * ConsDivOver;
        return Impact_Radius;
    }
}