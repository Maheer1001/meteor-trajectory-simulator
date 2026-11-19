using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTS.Visuals.EditorTools
{
    /// <summary>Dumps scene hierarchies (components, transforms, cameras, canvases) to text for review.</summary>
    public static class SceneInspector
    {
        public static void DumpAll()
        {
            string outDir = GetArg("-dumpDir", "Temp/SceneDump");
            Directory.CreateDirectory(outDir);
            foreach (string scene in new[] { "SpaceSimulation", "AtmosphereReached" })
            {
                var s = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                var sb = new StringBuilder();
                sb.AppendLine("Skybox: " + RenderSettings.skybox + " ambient " + RenderSettings.ambientMode + " fog " + RenderSettings.fog);
                foreach (GameObject root in s.GetRootGameObjects())
                {
                    Dump(root.transform, 0, sb);
                }

                File.WriteAllText(Path.Combine(outDir, scene + ".txt"), sb.ToString());
            }
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            var go = t.gameObject;
            sb.Append(new string(' ', depth * 2));
            sb.AppendFormat("{0} [{1}] active={2} layer={3} pos={4} lpos={5} scale={6} rot={7}", go.name, PrefabUtility.IsPartOfPrefabInstance(go) ? "prefab" : "", go.activeSelf,
                LayerMask.LayerToName(go.layer), t.position.ToString("G5"), t.localPosition.ToString("G5"), t.lossyScale.ToString("G5"), t.eulerAngles.ToString("F1"));
            sb.AppendLine();
            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine(new string(' ', depth * 2 + 2) + "- <missing script>"); continue; }
                if (c is Transform) continue;
                sb.Append(new string(' ', depth * 2 + 2) + "- " + c.GetType().Name);
                if (c is Camera cam)
                    sb.AppendFormat(" clear={0} bg={1} near={2} far={3} fov={4} ortho={5} depth={6} mask={7} hdr={8} msaa={9} tag={10}", cam.clearFlags, cam.backgroundColor, cam.nearClipPlane, cam.farClipPlane, cam.fieldOfView, cam.orthographic, cam.depth, cam.cullingMask, cam.allowHDR, cam.allowMSAA, go.tag);
                if (c is Canvas cv)
                    sb.AppendFormat(" mode={0} cam={1} plane={2} sort={3}", cv.renderMode, cv.worldCamera, cv.planeDistance, cv.sortingOrder);
                if (c is Renderer r)
                    sb.AppendFormat(" enabled={0} mats={1}", r.enabled, string.Join(",", System.Array.ConvertAll(r.sharedMaterials, m => m == null ? "null" : m.name + "(" + (m.shader != null ? m.shader.name : "") + ")")));
                if (c is MeshFilter mf) sb.Append(" mesh=" + (mf.sharedMesh != null ? mf.sharedMesh.name : "null"));
                if (c is Light l) sb.AppendFormat(" type={0} int={1} col={2}", l.type, l.intensity, l.color);
                if (c is MonoBehaviour mb && mb.GetType().Assembly.GetName().Name == "Assembly-CSharp")
                {
                    var so = new SerializedObject(mb);
                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.propertyType == SerializedPropertyType.ObjectReference)
                            sb.Append(" " + it.name + "=" + (it.objectReferenceValue != null ? it.objectReferenceValue.name : "null"));
                        else if (it.propertyType == SerializedPropertyType.String || it.propertyType == SerializedPropertyType.Float || it.propertyType == SerializedPropertyType.Boolean)
                            sb.Append(" " + it.name + "=" + (it.propertyType == SerializedPropertyType.String ? it.stringValue : it.propertyType == SerializedPropertyType.Float ? it.floatValue.ToString() : it.boolValue.ToString()));
                    }
                }

                sb.AppendLine();
            }

            foreach (Transform child in t)
            {
                Dump(child, depth + 1, sb);
            }
        }

        public static string GetArg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }

            return fallback;
        }
    }
}
