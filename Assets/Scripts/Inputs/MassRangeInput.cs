using MTS.Menus;
using UnityEngine;

// This script manages the mass input value (kg) and keeps it within 1,000 kg to 1e16 kg.
// Accepts plain numbers ("30000", "30,000"), scientific notation ("1.2e7") and units ("12 kt"); see MTS.Menus.MassInput.
public class MassRangeInput : MonoBehaviour
{
    public const float MinMass = (float)MassInput.MinMass;
    public const float MaxMass = (float)MassInput.MaxMass;

    // Static variable to hold the mass value (kg)
    public static float Mass = (float)MassInput.DefaultMass;

    // Method to parse and validate the input string representing the mass value
    public void StringReader(string s)
    {
        TrySet(s, out string message);
        print(message);
    }

    /// <summary>
    /// Parses and clamps a mass. Invalid text leaves the current value unchanged. Returns false (with a message)
    /// when the text was invalid or the value had to be clamped.
    /// </summary>
    public static bool TrySet(string s, out string message)
    {
        if (!MassInput.TryParse(s, out double kg))
        {
            message = "Not a valid mass; kept " + MassInput.FormatKg(Mass).Replace("<sup>", "^").Replace("</sup>", "");
            return false;
        }

        double clamped = MassInput.Clamp(kg);
        Mass = (float)clamped;
        if (clamped > kg)
        {
            message = "Below the minimum; set to 1,000 kg";
            return false;
        }

        if (clamped < kg)
        {
            message = "Above the maximum; set to 1e16 kg";
            return false;
        }

        message = "Value entered exists within parameters";
        return true;
    }
}
