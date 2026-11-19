using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTS.Hud.Editor
{
    /// <summary>
    /// Dumps the GameObject hierarchy of the simulation scenes (names, active state, components) to a text file.
    /// Used to keep <c>LegacyUiHider</c>'s list in sync with the scenes. Batch: -executeMethod MTS.Hud.Editor.HudSceneProbe.Dump
    /// (output path from -hudProbeOut, default Temp/hud-scene-probe.txt).
    /// </summary>
    public static class HudSceneProbe
    {
        [MenuItem("Tools/HUD/Dump simulation scene hierarchy")]
        public static void Dump()
        {
            string output = HudEditorUtil.Arg("-hudProbeOut", "Temp/hud-scene-probe.txt");
            var sb = new StringBuilder();
            foreach (string scene in new[] { "SpaceSimulation", "AtmosphereReached" })
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                sb.AppendLine("=== " + scene);
                foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                {
                    Write(sb, root.transform, 0);
                }
            }

            System.IO.File.WriteAllText(output, sb.ToString());
            Debug.Log("HUD scene probe written to " + output);
        }

        private static void Write(StringBuilder sb, Transform t, int depth)
        {
            string comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            string rect = "";
            if (t is RectTransform rt)
            {
                rect = $" amin={rt.anchorMin} amax={rt.anchorMax} pos={rt.anchoredPosition} size={rt.sizeDelta}";
            }

            var img = t.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                rect += $" img[{(img.sprite != null ? img.sprite.name : "-")} {img.type} c={img.color} ppu={img.pixelsPerUnitMultiplier}]";
            }

            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                rect += $" tmp[\"{tmp.text}\" {tmp.fontSize} c={tmp.color} {(tmp.font != null ? tmp.font.name : "-")}]";
            }

            sb.Append(new string(' ', depth * 2)).Append(t.gameObject.activeSelf ? "" : "[off] ").Append(t.name)
                .Append("  {").Append(comps).Append('}').Append(rect).AppendLine();
            foreach (Transform child in t)
            {
                Write(sb, child, depth + 1);
            }
        }
    }

    internal static class HudEditorUtil
    {
        public static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }
    }
}
