using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

// This script manages the main menu buttons and their functionalities
public class MainMenuButton : MonoBehaviour
{

    public static bool InitialisationCountS = false;
    public static bool InitialisationCountA = false;

    // Load the specified scene (generally variable input, Main menu --> Variable Input)
    public void MainMenu(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // Load the specified scene and pause the simulation when in Stage 1 (Space Traversal)
    public void MainMenuS1(string sceneName)
    {
        InitialisationCountS = true;
        Meteor_Force.isPaused = true;
        // Push simulation parameters to stack
        PushToStack(Meteor_Force.time);
        PushToStack(Meteor_Force.radius);
        PushToStack(Meteor_Force.velocity.x);
        PushToStack(Meteor_Force.velocity.y);
        PushToStack(Meteor_Force.velocity.z);
        PushToStack(Meteor_Force.gravitationalAcc);
        Time.timeScale = 0;
        SceneManager.LoadScene(sceneName);
    }

    // Load the specified scene and pause the simulation when in Stage 2 (Atmospheric Traversal)
    public void MainMenu2(string sceneName)
    {
        InitialisationCountA = true;
        Atmosphere_Force.isPaused = true;
        // Push simulation parameters to stack
        PushToStack(Meteor_Force.time);
        PushToStack(Atmosphere_Force.distance);
        PushToStack(Meteor_Force.velocity.x);
        PushToStack(Meteor_Force.velocity.y);
        PushToStack(Meteor_Force.velocity.z);
        PushToStack(Atmosphere_Force.gravitationalAcc);
        PushToStack(Meteor_Force.mass);
        PushToStack(Meteor_Force.meteor_radius);
        Time.timeScale = 0;
        SceneManager.LoadScene(sceneName);
    }

    // Continue the simulation
    public void Continue()
    {
        string sceneName;
        if (Meteor_Force.Stage == 1)
        {
            // Resume simulation for stage 1
            Meteor_Force.isPaused = false;
            // Pop simulation parameters from stack
            Meteor_Force.gravitationalAcc = PopFromStack();
            Meteor_Force.velocity.z = (float)PopFromStack();
            Meteor_Force.velocity.y = (float)PopFromStack();
            Meteor_Force.velocity.x = (float)PopFromStack();
            Meteor_Force.radius = PopFromStack();
            Meteor_Force.time = (float)PopFromStack();
            sceneName = ("SpaceSimulation");
        }
        else if (Meteor_Force.Stage == 2)
        {
            // Resume simulation for stage 2
            Atmosphere_Force.isPaused = false;
            // Pop simulation parameters from stack
            Meteor_Force.meteor_radius = (float)PopFromStack();
            Meteor_Force.mass = (float)PopFromStack();
            Atmosphere_Force.gravitationalAcc = (float)PopFromStack();
            Meteor_Force.velocity.z = (float)PopFromStack();
            Meteor_Force.velocity.y = (float)PopFromStack();
            Meteor_Force.velocity.x = (float)PopFromStack();
            Atmosphere_Force.distance = (float)PopFromStack();
            Meteor_Force.time = (float)PopFromStack();
            sceneName = ("AtmosphereReached");
        }
        else
        {
            // Proceed to Earth collision scene
            sceneName = ("EarthCollision");
        }
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }

    // Restart the simulation
    public void Restart(string sceneName)
    {
        // Clear stack and reset simulation parameters
        Meteor_Force.Min.Clear();
        Meteor_Force.TenMin.Clear();
        Meteor_Force.ThirtyMin.Clear();
        Meteor_Force.valueStack.Clear();
        SceneManager.LoadScene(sceneName);
        InitialisationCountS = false;
        InitialisationCountA = false;
        Time.timeScale = 1;
        Meteor_Force.isPaused = false;
        Atmosphere_Force.isPaused = false;
    }

    // Exit the program
    public void ExitProgram()
    {
        // Check if running in Unity Editor or standalone build
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Push a value to the stack
    private void PushToStack(double value)
    {
        Meteor_Force.valueStack.Push(value);
    }

    // Pop a value from the stack
    private double PopFromStack()
    {
        if (Meteor_Force.valueStack.Count > 0)
        {
            return Meteor_Force.valueStack.Pop();
        }
        else
        {
            return 0.0f;
        }
    }
}