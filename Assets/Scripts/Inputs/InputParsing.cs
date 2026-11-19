using System;
using System.Globalization;

// Culture-independent number parsing for the input fields.
// "2.5" and "2,5" both mean two and a half; when both marks appear ("60,000.5") the comma is a thousands separator.
public static class InputParsing
{
    public static float Parse(string s)
    {
        if (s == null)
        {
            throw new FormatException("No value entered");
        }

        string text = s.Trim().Replace(" ", "");
        if (text.Contains(",") && text.Contains("."))
        {
            text = text.Replace(",", "");
        }
        else if (text.Contains(","))
        {
            // A single comma is a decimal mark; several commas are thousands separators.
            text = text.IndexOf(',') == text.LastIndexOf(',') ? text.Replace(',', '.') : text.Replace(",", "");
        }

        float value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new FormatException("Not a finite number");
        }

        return value;
    }
}
