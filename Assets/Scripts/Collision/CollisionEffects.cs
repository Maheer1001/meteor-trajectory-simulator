using System.Globalization;
using MTS.Core;
using TMPro;
using UnityEngine;

/// <summary>
/// Results screen: shows the blast radii for the nine overpressure rings (T1-T9) and the final crater radius (T10),
/// all in metres. The numbers come from MTS.Core.ImpactModel (Collins, Melosh and Marcus 2005).
/// </summary>
public class CollisionEffects : MonoBehaviour
{
    // Radii in metres: [0..8] overpressure rings (0.04 ... 29 psi), [9] final crater radius.
    public static double[] R_impact;
    public static double CraterRadius;
    public TMP_Text T1;
    public TMP_Text T2;
    public TMP_Text T3;
    public TMP_Text T4;
    public TMP_Text T5;
    public TMP_Text T6;
    public TMP_Text T7;
    public TMP_Text T8;
    public TMP_Text T9;
    public TMP_Text T10;

    void Start()
    {
        TMP_Text[] textArray = { T1, T2, T3, T4, T5, T6, T7, T8, T9, T10 };
        double[] radii = CurrentRadii();
        for (int i = 0; i < textArray.Length; i++)
        {
            if (textArray[i] != null)
            {
                textArray[i].text = radii[i].ToString("F1", CultureInfo.InvariantCulture);
            }
        }

        SimulationResult result = SimulationSession.Instance.Result;
        ImpactAssessment impact = SimulationSession.Instance.Impact;
        if (result != null && impact != null)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "Run ended: {0} ({1}) t={2:F1} s, speed {3:F1} m/s, angle {4:F1} deg, mass {5:F1} kg, altitude {6:F0} m, energy {7:G4} kt TNT, {8}, crater D={9:F1} m",
                result.Reason, result.Message, result.EndTime, result.ImpactSpeed, result.ImpactAngleDegrees,
                result.MassAtEnd, result.EndAltitude, impact.EnergyKilotons, impact.Regime, impact.FinalCraterDiameter));
        }
    }

    /// <summary>The radii for the current session (ten values, zeros if there is no finished run).</summary>
    public static double[] CurrentRadii()
    {
        double[] radii = new double[10];
        ImpactAssessment impact = SimulationSession.Instance.Impact;
        if (impact != null)
        {
            for (int i = 0; i < 9 && i < impact.BlastRadii.Length; i++)
            {
                radii[i] = impact.BlastRadii[i];
            }

            radii[9] = impact.FinalCraterRadius;
        }

        R_impact = radii;
        CraterRadius = radii[9];
        return radii;
    }
}
