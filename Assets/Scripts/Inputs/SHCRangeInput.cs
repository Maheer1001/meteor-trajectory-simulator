using UnityEngine;

// This script manages the SHC (Specific Heat Capacity, J/(kg K)) range input value and ensures it stays within predefined bounds
public class SHCRangeInput : MonoBehaviour
{
    // Static variable to hold the SHC value
    public static float SHC = 800.0f; // J/(kg K); mid-range default used when the field is left blank

    // Method to parse and validate the input string representing the SHC value
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string (culture-independent; accepts "." or "," as the decimal mark)
            SHC = InputParsing.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (SHC < 400.0f)
            {
                SHC = 400.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (SHC > 1500.0f)
            {
                SHC = 1500.0f;
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
            SHC = 800.0f;
            print("Value entered is not valid for defined parameters, value has been set to 800 instead");
        }
    }
}