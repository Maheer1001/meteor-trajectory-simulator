using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MTS.Visuals
{
    /// <summary>
    /// Asset references used by the runtime visuals (materials, textures, post-processing profiles). One shared asset
    /// (Assets/Art/VisualsLibrary.asset) is built by the editor tooling and assigned to the scene controllers, so
    /// everything the visuals need is referenced and included in builds.
    /// </summary>
    [CreateAssetMenu(menuName = "MTS/Visuals Library")]
    public sealed class VisualsLibrary : ScriptableObject
    {
        [Header("Earth and sky")]
        public Material earth;
        public Material atmosphereShell;
        public Material starfield;
        public Material atmosphereSky;
        public Material ground;
        public Material cloudDeck;

        [Header("Meteor")]
        public Material meteorRock;
        public Material plasma;
        public Material glow;
        public Material ionTrail;
        public Material sparks;
        public Material smoke;
        public Material debris;

        [Header("Paths and markers")]
        public Material pathPast;
        public Material pathFuture;
        public Material marker;

        [Header("End sequence")]
        public Material shockwave;
        public Material crater;
        public Material dust;

        [Header("Post-processing")]
        public PostProcessProfile spaceProfile;
        public PostProcessProfile atmosphereProfile;
        public PostProcessProfile flashProfile;
    }
}
