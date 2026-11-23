using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MTS.Results;
using NUnit.Framework;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MTS.SetupChecks.Tests
{
    /// <summary>
    /// Setup checks for the local environment: the Unity version, the packages the scenes depend on, and the optional
    /// geocoding key. Nothing here needs a network connection.
    /// </summary>
    public class EnvironmentChecks
    {
        [Test]
        public void EditorIsTheProjectsUnityVersion()
        {
            string wanted = Regex.Match(File.ReadAllText("ProjectSettings/ProjectVersion.txt"), @"m_EditorVersion:\s*(\S+)").Groups[1].Value;
            string running = Application.unityVersion;
            string Line(string v) => string.Join(".", v.Split('.').Take(2));
            Assert.That(Line(running), Is.EqualTo(Line(wanted)), "This project needs Unity " + wanted + "; the tests are running in " + running);
            if (running != wanted)
            {
                TestContext.WriteLine("WARNING: " + "Running Unity " + running + "; the project is pinned to " + wanted + ". It should work, but " + wanted + " is what is tested.");
            }
        }

        // Package id, a type each package must provide (the scenes and scripts use these), and the assembly it is in.
        [TestCase("com.unity.postprocessing", "UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime")]
        [TestCase("com.unity.cinemachine", "Cinemachine.CinemachineBrain, Cinemachine")]
        [TestCase("com.unity.textmeshpro", "TMPro.TextMeshProUGUI, Unity.TextMeshPro")]
        [TestCase("com.unity.ugui", "UnityEngine.UI.Button, UnityEngine.UI")]
        public void RequiredPackageResolvesAtTheManifestVersion(string packageId, string typeName)
        {
            Match pinned = Regex.Match(File.ReadAllText("Packages/manifest.json"), "\"" + Regex.Escape(packageId) + "\"\\s*:\\s*\"([^\"]+)\"");
            Assert.That(pinned.Success, Is.True, packageId + " is not in Packages/manifest.json");

            PackageInfo info = PackageInfo.FindForAssetPath("Packages/" + packageId);
            Assert.That(info, Is.Not.Null, packageId + " did not resolve. Check the Package Manager window / your network on first open.");
            Assert.That(info.version, Is.EqualTo(pinned.Groups[1].Value), packageId + " resolved to a different version than the manifest pins");

            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, typeName + " could not be loaded: " + packageId + " is installed but did not compile");
        }

        [Test]
        public void GeocodingKeyIsOptional()
        {
            // Without a key the results screen shows coordinates only, so a missing key must not throw or block.
            string key = null;
            Assert.DoesNotThrow(() => key = GeocodeApiKey.Load());
            Assert.That(File.Exists("Assets/StreamingAssets/secrets.example.json"), Is.True, "the secrets template is missing");
            TestContext.WriteLine(key == null
                ? "No geocode.maps.co key: the results screen will show coordinates only (set " + GeocodeApiKey.EnvironmentVariable + " or Assets/StreamingAssets/secrets.json to get place names)."
                : "geocode.maps.co key found: the results screen will look up place names.");
            if (key != null)
            {
                Assert.That(key, Is.Not.Empty.And.Not.Contains(" "), "the key looks malformed");
            }
        }
    }
}
