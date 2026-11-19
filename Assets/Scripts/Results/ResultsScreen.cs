using System;
using MTS.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>
    /// Results screen controller (EarthCollision scene). The static layout is built by the editor tool
    /// MTS.Results.Editor.ResultsSceneBuilder; this component fills it from <see cref="SimulationSession"/>:
    /// header, tabs (Summary, Map, Charts, Physics) and their content.
    /// </summary>
    public sealed class ResultsScreen : MonoBehaviour
    {
        public enum Tab
        {
            Summary,
            Map,
            Charts,
            Physics,
        }

        public ResultsAssets Assets = new ResultsAssets();
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI HeadlineText;
        public TextMeshProUGUI LocationText;
        public Button[] TabButtons = new Button[4];
        public GameObject[] Pages = new GameObject[4];
        public RectTransform SummaryLeft;
        public RectTransform SummaryRight;
        public WorldMapView MapView;
        public RectTransform[] ChartSlots = new RectTransform[4];
        public RectTransform PhysicsLeft;
        public RectTransform PhysicsRight;

        /// <summary>Scenario run when the scene is opened in the editor without a finished run.</summary>
        public static Func<MeteorProperties> PreviewScenario = () => MeteorProperties.FromInputs(60000, 7800, 800, 0.5, 1, 1, 1);

        public OutcomeReport Report { get; private set; }
        public Tab Current { get; private set; }

        private UnityWebRequest geocodeRequest;
        private string placeName = "";

        private void Start()
        {
            EnsurePreviewRun();
            Populate();
            ShowTab(Tab.Summary);
            if (Report.HasLocation)
            {
                StartCoroutine(ResultsGeocoder.Lookup(Report.Location.Lat, Report.Location.Lon, SetPlaceName, r => geocodeRequest = r));
            }
        }

        private void OnDestroy()
        {
            // Leaving the scene cancels a lookup that is still in flight.
            if (geocodeRequest != null)
            {
                geocodeRequest.Abort();
                geocodeRequest = null;
            }
        }

        /// <summary>In the editor, runs the preview scenario to the end if the session has no finished run.</summary>
        public static void EnsurePreviewRun()
        {
            SimulationSession session = SimulationSession.Instance;
            if (session.Result != null || !Application.isEditor)
            {
                return;
            }

            session.Begin(PreviewScenario()).RunToEnd();
            Debug.Log("Results screen preview: ran a default scenario (" + session.Result.Reason + ").");
        }

        /// <summary>Builds every page from the current session. Safe to call in edit mode (used by the capture tool).</summary>
        public void Populate()
        {
            SimulationSession session = SimulationSession.Instance;
            Report = OutcomeReport.Build(session.Result, session.Impact, session.Properties);
            Canvas.ForceUpdateCanvases();

            ApplyPixelFont();
            TitleText.text = Report.Title;
            string headline = Report.Headline;
            if (Report.Kind == OutcomeKind.Disintegrated)
            {
                headline = ResultsFormat.Tnt(Report.Energy) + " deposited in the air";
            }

            if (!string.IsNullOrEmpty(Report.Comparison) && Report.Energy > 0)
            {
                headline += "  <color=#B8B8CC>·</color>  <color=#FFCC47>" + Report.Comparison + "</color>";
            }

            HeadlineText.text = headline;
            SetPlaceName(Report.HasLocation ? "" : "");

            for (int i = 0; i < TabButtons.Length; i++)
            {
                if (TabButtons[i] == null) continue;
                Tab tab = (Tab)i;
                TabButtons[i].onClick.RemoveAllListeners();
                TabButtons[i].onClick.AddListener(() => ShowTab(tab));
            }

            // Pages must be active while they are laid out and filled.
            foreach (GameObject page in Pages)
            {
                if (page != null) page.SetActive(true);
            }

            Canvas.ForceUpdateCanvases();
            ResultsPages.BuildSummary(this, session);
            MapView.Show(Report, session.Impact?.FinalCraterDiameter ?? 0.0, Assets);
            ResultsPages.BuildCharts(this, session);
            ResultsPages.BuildPhysics(this);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Makes sure the title, tab and button labels use the pixel font (the menu's style).</summary>
        private void ApplyPixelFont()
        {
            if (Assets.PixelFont == null)
            {
                return;
            }

            if (TitleText != null)
            {
                TitleText.font = Assets.PixelFont;
            }

            foreach (Button b in GetComponentsInChildren<Button>(true))
            {
                Transform label = b.transform.Find("Label");
                if (label != null) label.GetComponent<TextMeshProUGUI>().font = Assets.PixelFont;
            }
        }

        public void SetPlaceName(string name)
        {
            placeName = name ?? "";
            string coords = Report != null && Report.HasLocation ? ResultsFormat.LatLon(Report.Location.Lat, Report.Location.Lon) : "";
            if (LocationText != null)
            {
                LocationText.text = placeName.Length > 0 && coords.Length > 0
                    ? placeName + "  <color=#B8B8CC>·  " + coords + "</color>"
                    : (coords.Length > 0 ? coords : placeName);
            }

            if (MapView != null)
            {
                MapView.SetPlaceName(placeName.Length > 0 ? placeName : "Ground zero");
            }
        }

        public void ShowTab(Tab tab)
        {
            Current = tab;
            for (int i = 0; i < Pages.Length; i++)
            {
                bool on = i == (int)tab;
                if (Pages[i] != null)
                {
                    Pages[i].SetActive(on);
                }

                if (i < TabButtons.Length && TabButtons[i] != null)
                {
                    StyleTab(TabButtons[i], on);
                }
            }

            if (tab == Tab.Map && MapView != null)
            {
                MapView.Redraw();
                MapView.PlayRingGrowth();
            }
        }

        private static void StyleTab(Button button, bool selected)
        {
            Transform fill = button.transform.Find("Fill");
            Transform outline = button.transform.Find("Outline");
            Transform label = button.transform.Find("Label");
            if (fill != null) fill.GetComponent<Image>().color = selected ? ResultsTheme.Red : ResultsTheme.TabIdleFill;
            if (outline != null) outline.GetComponent<Image>().color = selected ? ResultsTheme.DarkRed : ResultsTheme.PanelOutline;
            if (label != null) label.GetComponent<TextMeshProUGUI>().color = selected ? Color.white : new Color(1f, 0.62f, 0.6f);
        }

        private void Update()
        {
            // Keyboard shortcuts: 1-4 switch tabs.
            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ShowTab((Tab)i);
                }
            }
        }
    }
}
