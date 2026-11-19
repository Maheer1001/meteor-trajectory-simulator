using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Links : MonoBehaviour
{
    public void TedTalk1()
    {
        Application.OpenURL("https://www.youtube.com/watch?v=eUznJ3LtvGc");
    }
    public void TedTalk2()
    {
        Application.OpenURL("https://www.youtube.com/watch?v=OM4tL6AM_bo");
    }

    public void RK4()
    {
        Application.OpenURL("https://www.youtube.com/watch?v=HOWJp8NV5xU");
    }

    public void DragCof()
    {
        Application.OpenURL("https://pages.mtu.edu/~fmorriso/DataCorrelationForSphereDrag2016.pdf");
    }

    public void Overpressure()
    {
        Application.OpenURL("https://response.restoration.noaa.gov/oil-and-chemical-spills/chemical-spills/resources/overpressure-levels-concern.html");
    }

    // U.S. Standard Atmosphere, 1976 (NOAA / NASA / USAF), NASA Technical Reports Server
    public void StandardAtmosphere()
    {
        Application.OpenURL("https://ntrs.nasa.gov/citations/19770009539");
    }

    // Chyba, Thomas and Zahnle (1993), "The 1908 Tunguska explosion: atmospheric disruption of a stony asteroid" (pancake model)
    public void PancakeModel()
    {
        Application.OpenURL("https://doi.org/10.1038/361040a0");
    }

    // Collins, Melosh and Marcus (2005), "Earth Impact Effects Program" (crater, blast and thermal effects)
    public void ImpactEffects()
    {
        Application.OpenURL("https://doi.org/10.1111/j.1945-5100.2005.tb00157.x");
    }

    // Meteor ablation and luminous flight: Ceplecha et al. (1998), "Meteor phenomena and bodies"
    public void Ablation()
    {
        Application.OpenURL("https://doi.org/10.1023/A:1005069928850");
    }

    // NASA Earth Observatory / Visible Earth (Blue Marble and Black Marble imagery used for the Earth)
    public void NasaImagery()
    {
        Application.OpenURL("https://visibleearth.nasa.gov/");
    }
}
