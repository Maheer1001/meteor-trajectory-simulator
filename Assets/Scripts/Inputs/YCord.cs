using UnityEngine;

// This script manages the Y coordinate value and ensures it stays within predefined bounds
public class Ycord : MonoBehaviour
{
    // Static variable to hold the Y coordinate value
    public static float Y = 25.0f; // default used when the field is left blank

    // Method to parse and validate the input string representing the Y coordinate
    public void StringReader(string s)
    {
        try
        {
            // Parse the input string (culture-independent; accepts "." or "," as the decimal mark)
            Y = InputParsing.Parse(s);
            // Check if the parsed value is within predefined bounds
            if (Y < -50.0f)
            {
                Y = -50.0f;
                print("Value entered exceeds minimum, value has been set to lower bound");
            }
            else if (Y > 50.0f)
            {
                Y = 50.0f;
                print("Value entered exceeds maximum, value has been set to upper bound");
            }
            else if ((-1.0f < Y) & (1.0f > Y))
            {
                Y = 25.0f;
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
            Y = 25.0f;
            print("Value entered is not valid for defined parameters, value has been set to 25 instead");
        }
    }
}