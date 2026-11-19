using MTS.Core;
using TMPro;
using UnityEngine;

namespace MTS.Menus
{
    /// <summary>
    /// Main menu and pause menu behaviour: opens and closes the More Information page (Esc closes it) and, on the pause
    /// menu, says which stage the run was paused in. Navigation itself goes through <see cref="MainMenuButton"/>.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        public GameObject informationPage;
        public GameObject mainButtons;
        /// <summary>Pause menu only: "Paused during the space stage" and so on.</summary>
        public TMP_Text stageText;

        private void Start()
        {
            ShowInformation(false);
            if (stageText != null)
            {
                stageText.text = StageLine(SimulationSession.Instance.Stage);
            }
        }

        private void Update()
        {
            if (informationPage != null && informationPage.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                ShowInformation(false);
            }
        }

        public void ShowInformation(bool show)
        {
            if (informationPage != null) informationPage.SetActive(show);
            if (mainButtons != null) mainButtons.SetActive(!show);
        }

        public void OpenInformation() => ShowInformation(true);

        public void CloseInformation() => ShowInformation(false);

        public static string StageLine(SimulationStage stage)
        {
            switch (stage)
            {
                case SimulationStage.Space: return "Paused during stage 1: space";
                case SimulationStage.Atmosphere: return "Paused during stage 2: atmosphere";
                case SimulationStage.Ended: return "The run has ended";
                default: return "No run in progress";
            }
        }
    }
}
