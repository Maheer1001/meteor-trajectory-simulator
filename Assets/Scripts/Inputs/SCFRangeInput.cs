using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

// This script manages the SCF (Shape Complexity Factor) range input value and ensures it stays within predefined bounds
public class SCFRangeInput : MonoBehaviour
{
    // Static variable to hold the SCF value
    public static float SCF;

    // Method to parse and validate the input string representing the SCF value
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string to float
            SCF = float.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (SCF < 0.0f)
            {
                SCF = 0.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (SCF > 1.0f)
            {
                SCF = 1.0f;
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
            SCF = 0.5f;
            print("Value entered is not valid for defined parameters, value has been set to 0.5 instead");
        }
    }
}