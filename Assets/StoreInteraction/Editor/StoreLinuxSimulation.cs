#if UNITY_EDITOR_LINUX
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Management;

// OpenXR 1.17.1 ships no Linux native UnityOpenXR library. Play in the XRI
// simulator on this host; leave the project's build settings assets untouched.
public static class StoreLinuxSimulation
{
    private static XRGeneralSettings original;
    private static XRGeneralSettings simulation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void PrepareSimulation()
    {
        original = XRGeneralSettings.Instance;
        if (original == null) return;
        simulation = Object.Instantiate(original);
        simulation.name = "Store Linux Editor Simulation";
        simulation.hideFlags = HideFlags.HideAndDontSave;
        simulation.InitManagerOnStart = false;
        XRGeneralSettings.Instance = simulation;
        EditorApplication.playModeStateChanged -= Restore;
        EditorApplication.playModeStateChanged += Restore;
    }

    private static void Restore(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        XRGeneralSettings.Instance = original;
        if (simulation != null) Object.DestroyImmediate(simulation);
        simulation = null;
        original = null;
        EditorApplication.playModeStateChanged -= Restore;
    }
}
#endif
