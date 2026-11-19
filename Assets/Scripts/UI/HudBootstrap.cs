using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace MTS.Hud
{
    /// <summary>
    /// Attaches the HUD to the simulation scenes at runtime, so no scene file has to change:
    /// when SpaceSimulation or AtmosphereReached loads it hides the legacy UI (<see cref="LegacyUiHider"/>), makes
    /// sure an EventSystem exists and instantiates Resources/UI/SpaceHud or Resources/UI/AtmosphereHud
    /// (built by Tools > HUD > Build HUD prefabs). If a prefab is missing the HUD is built from code instead.
    /// </summary>
    public static class HudBootstrap
    {
        public const string SpaceScene = "SpaceSimulation";
        public const string AtmosphereScene = "AtmosphereReached";
        public const string SpacePrefab = "UI/SpaceHud";
        public const string AtmospherePrefab = "UI/AtmosphereHud";

        /// <summary>Set false (e.g. from a test) to stop the HUD from attaching itself.</summary>
        public static bool AutoAttach = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialise()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SimulationSignals.Toast -= HudToasts.PostSignal;
            SimulationSignals.Toast += HudToasts.PostSignal;
        }

        public static bool TryGetStage(string sceneName, out HudStage stage)
        {
            switch (sceneName)
            {
                case SpaceScene:
                    stage = HudStage.Space;
                    return true;
                case AtmosphereScene:
                    stage = HudStage.Atmosphere;
                    return true;
                default:
                    stage = HudStage.Space;
                    return false;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!AutoAttach || !TryGetStage(scene.name, out HudStage stage))
            {
                return;
            }

            Attach(scene, stage);
        }

        /// <summary>Hides the legacy UI of the scene and instantiates the HUD for the stage.</summary>
        public static SimulationHud Attach(Scene scene, HudStage stage)
        {
            LegacyUiHider.Hide(scene);
            EnsureEventSystem(scene);

            GameObject prefab = Resources.Load<GameObject>(stage == HudStage.Space ? SpacePrefab : AtmospherePrefab);
            GameObject hud;
            if (prefab != null)
            {
                hud = Object.Instantiate(prefab);
                hud.name = prefab.name;
            }
            else
            {
                Debug.LogWarning("[HUD] Prefab missing; building the HUD from code (run Tools > HUD > Build HUD prefabs).");
                hud = HudFactory.Build(stage, HudAssets.LoadRuntime()).gameObject;
            }

            if (hud.scene != scene && scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(hud, scene);
            }

            return hud.GetComponent<SimulationHud>();
        }

        public static void EnsureEventSystem(Scene scene)
        {
            if (EventSystem.current != null || Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
        }
    }
}
