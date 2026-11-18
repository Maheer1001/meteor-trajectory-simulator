using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the density range input value and ensures it stays within predefined bounds
public class DensityRangeInput : MonoBehaviour
{
    // Static variable to hold the density value
    public static double Density;

    // Method to parse and validate the input string representing the density value
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            Density = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (Density < 1000.0f)
            {
                Density = 1000.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (Density > 8000.0f)
            {
                Density = 8000.0f;
                print("Value entered exceeds maximum, value has been set to upper bound");
            }
            else
            {
                print("Value entered exists within parameters");
            }
        }
        catch
        {
            // Handle parsing errors by setting the default value
            Density = 4500.0f;
            print("Value entered is not valid for defined parameters, value has been set to 4500 instead");
        }
    }
}