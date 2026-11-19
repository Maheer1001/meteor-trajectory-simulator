using MTS.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

// This script manages the main menu buttons and their functionalities.
// Run state lives in SimulationSession; pausing just stops the runner, so nothing needs to be saved or restored.
public class MainMenuButton : MonoBehaviour
{
    // Kept for compatibility; true while a run is paused behind the in-simulation menu.
    public static bool InitialisationCountS = false;
    public static bool InitialisationCountA = false;

    // Load the specified scene (Main menu --> Variable Input, Variable Input --> Space Simulation, results --> menus)
    public void MainMenu(string sceneName)
    {
        // Going back to the input screen or starting from it means a fresh run with the inputs as they are now.
        if (sceneName == "VariableInput" || sceneName == "SpaceSimulation")
        {
            SimulationSession.Instance.Reset();
            InitialisationCountS = false;
            InitialisationCountA = false;
        }

        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }

    // Pause the simulation during Stage 1 (Space Traversal) and open the in-simulation menu
    public void MainMenuS1(string sceneName)
    {
        InitialisationCountS = true;
        Pause();
        SceneManager.LoadScene(sceneName);
    }

    // Pause the simulation during Stage 2 (Atmospheric Traversal) and open the in-simulation menu
    public void MainMenu2(string sceneName)
    {
        InitialisationCountA = true;
        Pause();
        SceneManager.LoadScene(sceneName);
    }

    // Continue the simulation from where it was paused
    public void Continue()
    {
        SimulationSession session = SimulationSession.Instance;
        string sceneName;
        switch (session.Stage)
        {
            case SimulationStage.Space:
                sceneName = "SpaceSimulation";
                break;
            case SimulationStage.Atmosphere:
                sceneName = "AtmosphereReached";
                break;
            case SimulationStage.Ended:
                sceneName = "EarthCollision";
                break;
            default:
                sceneName = "VariableInput";
                break;
        }

        session.IsPaused = false;
        InitialisationCountS = false;
        InitialisationCountA = false;
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }

    // Restart the simulation: discard the current run completely
    public void Restart(string sceneName)
    {
        SimulationSession.Instance.Reset();
        InitialisationCountS = false;
        InitialisationCountA = false;
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }

    // Exit the program
    public void ExitProgram()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Pause()
    {
        SimulationSession.Instance.IsPaused = true;
        Time.timeScale = 0;
    }
}
