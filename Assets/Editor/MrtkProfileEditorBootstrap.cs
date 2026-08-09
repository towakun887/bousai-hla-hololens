#if UNITY_EDITOR
using MixedReality.Toolkit;
using MixedReality.Toolkit.Editor;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class MrtkProfileEditorBootstrap
{
    static MrtkProfileEditorBootstrap()
    {
        AssignStandaloneProfile();
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        AssignStandaloneProfile();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode)
        {
            AssignStandaloneProfile();
        }
    }

    private static void AssignStandaloneProfile()
    {
        MRTKProfile profile = MRTKSettings.ProfileForBuildTarget(BuildTargetGroup.Standalone);
        if (profile != null)
            MRTKProfile.Instance = profile;
    }
}
#endif
