using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the Z coordinate value and ensures it stays within predefined bounds
public class Zcord : MonoBehaviour
{
    // Static variable to hold the Z coordinate value
    public static float Z;

    // Method to parse and validate the input string representing the Z coordinate
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            Z = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (Z < -50.0f)
            {
                Z = -50.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (Z > 50.0f)
            {
                Z = 50.0f;
                print("Value entered exceeds maximum, value has been set to upper bound");
            }
            else if ((-1.0f < Z) & (1.0f > Z))
            {
                Z = 25.0f;
                print("Value entered is not valid for defined parameters, value has been set to 25 instead");
            }
            else
            {
                print("Value entered exists within parameters");
            }
        }
        catch
        {
            // Handle parsing errors by setting the default value
            Z = 25.0f;
            print("Value entered is not valid for defined parameters, value has been set to 25 instead");
        }
    }
}