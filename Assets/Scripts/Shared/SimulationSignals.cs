using System;

/// <summary>
/// Tiny decoupling hub between the HUD and the visuals, so neither references the other's types.
/// HUD raises requests (camera switch); visuals report capabilities (camera names). Both may raise
/// <see cref="Toast"/> for one-off events. Static and scene-independent; listeners must unsubscribe in OnDisable.
/// </summary>
public static class SimulationSignals
{
    /// <summary>Raised when the user asks for camera <c>index</c> (0-based; keys 1..n).</summary>
    public static event Action<int> CameraRequested;

    /// <summary>Raised by the visuals after switching camera (index, display name).</summary>
    public static event Action<int, string> CameraChanged;

    /// <summary>One-off event message for the HUD's toast area (e.g. "Atmospheric entry").</summary>
    public static event Action<string> Toast;

    /// <summary>Camera display names published by the active scene's visuals; empty when it has none.</summary>
    public static string[] CameraNames { get; private set; } = Array.Empty<string>();

    public static void RequestCamera(int index) => CameraRequested?.Invoke(index);

    public static void PublishCameras(string[] names) => CameraNames = names ?? Array.Empty<string>();

    public static void ReportCameraChanged(int index, string name) => CameraChanged?.Invoke(index, name);

    public static void RaiseToast(string message) => Toast?.Invoke(message);
}
