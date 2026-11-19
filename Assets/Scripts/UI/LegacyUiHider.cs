using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MTS.Hud
{
    /// <summary>
    /// Hides the original NEA simulation UI at runtime so the new HUD can replace it without editing the scenes.
    ///
    /// THE SINGLE LIST OF LEGACY UI (for deleting it from the scenes at integration time):
    ///
    /// SpaceSimulation and AtmosphereReached both have one Screen Space - Overlay canvas named
    /// <see cref="LegacyCanvasName"/> ("Meteor Background", the big red panel). Everything under it is legacy:
    ///   - "Pause / Play"          (Play / Pause buttons -> PlayButtonPressed / PauseButtonPressed on the adapter)
    ///   - "Meteor stats"          (Time Elapsed, Distance Remaining, Velocity, Acceleration [+ Mass, Radius in Atmosphere])
    ///   - "Time scaler"           (Time slider, 1..500 in Space / 1..50 in Atmosphere -> adapter.slider)
    ///   - "Back"                  (Back button -> MainMenuButton.MainMenuS1 / MainMenu2("MainMenu2"))
    ///   - "Wind Speed Scaler"     (Atmosphere only: Wind slider 10..105 m/s -> Atmosphere_Force.slider2)
    ///   - "Wind Direction Slider" (Atmosphere only: -0.25..0.25 -> Atmosphere_Force.slider3)
    ///   - "Atmosphere Stats"      (Atmosphere only: temperature / pressure readouts)
    ///
    /// The canvas GameObject itself is NOT deactivated: in SpaceSimulation it also carries a Meteor_Force and a
    /// MainMenuButton component. Instead its Canvas and GraphicRaycaster components are disabled, which hides and
    /// un-clicks every child while all scripts keep running. At integration: delete the children listed above,
    /// remove the Canvas/CanvasScaler/GraphicRaycaster/Image (and the redundant Meteor_Force/MainMenuButton on the
    /// Space canvas; Meteor1 and Meteor2 carry their own), then null the adapters' legacy UI fields (they are optional).
    /// </summary>
    public static class LegacyUiHider
    {
        public const string LegacyCanvasName = "Meteor Background";

        /// <summary>Children of the legacy canvas, listed for reference and for the fallback below.</summary>
        public static readonly string[] LegacyChildren =
        {
            "Pause / Play", "Meteor stats", "Time scaler", "Back", "Wind Speed Scaler", "Wind Direction Slider", "Atmosphere Stats",
        };

        /// <summary>Hides the legacy UI of a scene. Returns the names of what was hidden (for logging and tests).</summary>
        public static List<string> Hide(Scene scene)
        {
            var hidden = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != LegacyCanvasName)
                {
                    continue;
                }

                if (root.TryGetComponent(out Canvas canvas))
                {
                    canvas.enabled = false;
                    hidden.Add(root.name + " (Canvas)");
                }

                if (root.TryGetComponent(out GraphicRaycaster raycaster))
                {
                    raycaster.enabled = false;
                }

                if (canvas == null)
                {
                    // Not a canvas any more (scene partly cleaned up): hide the known children instead.
                    foreach (Transform child in root.transform)
                    {
                        if (System.Array.IndexOf(LegacyChildren, child.name) >= 0)
                        {
                            child.gameObject.SetActive(false);
                            hidden.Add(child.name);
                        }
                    }
                }
            }

            return hidden;
        }
    }
}
