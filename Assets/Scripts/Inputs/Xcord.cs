using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the X coordinate value and ensures it stays within predefined bounds
public class Xcord : MonoBehaviour
{
    // Static variable to hold the X coordinate value
    public static float X;

    // Method to parse and validate the input string representing the X coordinate
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            X = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (X < -50.0f)
            {
                X = -50.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (X > 50.0f)
            {
                X = 50.0f;
                print("Value entered exceeds maximum, value has been set to upper bound");
            }
            else if ((-1.0f < X) & (1.0f > X))
            {
                X = 25.0f;
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
            X = 25.0f;
            print("Value entered is not valid for defined parameters, value has been set to 25 instead");
        }
    }
}