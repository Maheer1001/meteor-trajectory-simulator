using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the mass range input value and ensures it stays within predefined bounds
public class MassRangeInput : MonoBehaviour
{
    // Static variable to hold the mass value
    public static float Mass;

    // Method to parse and validate the input string representing the mass value
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            Mass = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (Mass < 5000.0f)
            {
                Mass = 5000.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (Mass > 60000.0f)
            {
                Mass = 60000.0f;
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
            Mass = 30000.0f;
            print("Value entered is not valid for defined parameters, value has been set to 30000 instead");
        }
    }
}