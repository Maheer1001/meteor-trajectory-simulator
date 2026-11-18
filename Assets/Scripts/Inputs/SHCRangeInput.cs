using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the SHC (Specific Heat Capacity) range input value and ensures it stays within predefined bounds
public class SHCRangeInput : MonoBehaviour
{
    // Static variable to hold the SHC value
    public static float SHC;

    // Method to parse and validate the input string representing the SHC value
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            SHC = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (SHC < 10.0f)
            {
                SHC = 10.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (SHC > 100.0f)
            {
                SHC = 100.0f;
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
            SHC = 55.0f;
            print("Value entered is not valid for defined parameters, value has been set to 55 instead");
        }
    }
}