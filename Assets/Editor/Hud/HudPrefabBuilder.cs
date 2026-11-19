using System.Collections.Generic;
using System.Linq;
using MTS.Hud;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace MTS.Hud.Editor
{
    /// <summary>
    /// Rebuilds the HUD prefabs (Assets/Resources/UI/SpaceHud.prefab and AtmosphereHud.prefab) from
    /// <see cref="HudFactory"/>, so the prefabs are reproducible from code.
    /// Menu: Tools > HUD > Build HUD prefabs. Batch: -executeMethod MTS.Hud.Editor.HudPrefabBuilder.BuildAll
    /// </summary>
    public static class HudPrefabBuilder
    {
        public const string Folder = "Assets/Resources/UI";

        [MenuItem("Tools/HUD/Build HUD prefabs")]
        public static void BuildAll()
        {
            HudAssets assets = LoadAssets();
            CheckGlyphs(assets);
            Save(HudStage.Space, assets, Folder + "/SpaceHud.prefab");
            Save(HudStage.Atmosphere, assets, Folder + "/AtmosphereHud.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[HUD] Prefabs built in " + Folder);
        }

        public static HudAssets LoadAssets()
        {
            var assets = new HudAssets
            {
                LabelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudTheme.LabelFontPath),
                ValueFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudTheme.LabelFontPath),
                DisplayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudTheme.DisplayFontPath),
                Disc = AssetDatabase.LoadAssetAtPath<Sprite>(HudTheme.DiscSpritePath),
                Ring = AssetDatabase.LoadAssetAtPath<Sprite>(HudTheme.RingSpritePath),
                Panel = AssetDatabase.LoadAssetAtPath<Sprite>(HudTheme.PanelSpritePath),
                Outline = AssetDatabase.LoadAssetAtPath<Sprite>(HudTheme.OutlineSpritePath),
            };
            if (assets.LabelFont == null || assets.DisplayFont == null || assets.Panel == null || assets.Outline == null || assets.Disc == null || assets.Ring == null)
            {
                Debug.LogError("[HUD] Missing HUD asset(s): label font " + (assets.LabelFont != null) + ", display font " + (assets.DisplayFont != null) +
                               ", panel sprite " + (assets.Panel != null) + ", outline sprite " + (assets.Outline != null));
            }

            return assets;
        }

        private static void Save(HudStage stage, HudAssets assets, string path)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "UI");
            }

            SimulationHud hud = HudFactory.Build(stage, assets);
            PrefabUtility.SaveAsPrefabAsset(hud.gameObject, path, out bool ok);
            Object.DestroyImmediate(hud.gameObject);
            if (!ok)
            {
                Debug.LogError("[HUD] Could not save " + path);
            }
        }

        /// <summary>Logs characters the value font cannot draw (static font atlases have no fallback glyphs).</summary>
        private static void CheckGlyphs(HudAssets assets)
        {
            const string needed = "0123456789,.:+-%/()<>| ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz°";
            foreach (TMP_FontAsset font in new[] { assets.DisplayFont, assets.LabelFont }.Where(f => f != null))
            {
                var missing = new List<char>();
                foreach (char c in needed)
                {
                    if (!font.HasCharacter(c, true, false)) missing.Add(c);
                }

                Debug.Log("[HUD] Font " + font.name + " missing: " + (missing.Count == 0 ? "none" : "'" + new string(missing.ToArray()) + "'"));
            }
        }
    }
}
