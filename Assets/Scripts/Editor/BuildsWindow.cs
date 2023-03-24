using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;
using UnityEditor.Build.Reporting;

public class BuildsWindow : EditorWindow
{
    [SerializeField] private List<CustomBuildSettings> _buildSettings = new List<CustomBuildSettings>();

    private readonly List<BuildTarget> _availableTargets = new List<BuildTarget>();

    private SerializedObject _buildSettingsObject;

    private void OnEnable()
    {
        GatherAvailableTargets();
    }

    [MenuItem("Tools/Builds Window")]
    public static void ShowWindow()
    {
        GetWindow<BuildsWindow>();
    }

    private void OnGUI()
    {
        if (_buildSettingsObject == null)
            _buildSettingsObject = new SerializedObject(this);
        SerializedProperty buildSettingsProperty = _buildSettingsObject.FindProperty("_buildSettings");
        EditorGUILayout.PropertyField(buildSettingsProperty, true);
        _buildSettingsObject.ApplyModifiedProperties();

        int numEnabled = 0;
        for (int i = 0; i < _buildSettings.Count; i++)
        {
            if (_buildSettings[i] != null)
                numEnabled++;
        }

        if (numEnabled > 0)
        {
            string prompt = numEnabled == 1 ? "Build 1 Platform" : $"Build {numEnabled} Platforms";
            if (GUILayout.Button(prompt))
            {
                RemoveNullSettings();
                RemoveUnavailableBuildTargets();
                EditorCoroutineUtility.StartCoroutine(PerformBuild(_buildSettings), this);
            }
        }
    }

    private void GatherAvailableTargets()
    {
        _availableTargets.Clear();

        System.Array targets = System.Enum.GetValues(typeof(BuildTarget));
        foreach (object targetValue in targets)
        {
            BuildTarget target = (BuildTarget)targetValue;
            bool isSupported = BuildPipeline.IsBuildTargetSupported(GetTargetGroupForTarget(target), target);
            if (isSupported == false)
                continue;

            _availableTargets.Add(target);
        }
    }

    private BuildTargetGroup GetTargetGroupForTarget(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneOSX => BuildTargetGroup.Standalone,
        BuildTarget.StandaloneWindows64 => BuildTargetGroup.Standalone,
        BuildTarget.Android => BuildTargetGroup.Android,
        BuildTarget.StandaloneLinux64 => BuildTargetGroup.Standalone,
        _ => BuildTargetGroup.Unknown
    };

    private void RemoveNullSettings()
    {
        for (int i = 0; i < _buildSettings.Count; i++)
        {
            if (_buildSettings[i] == null)
            {
                _buildSettings.RemoveAt(i);
                i--;
            }
        }
    }

    private void RemoveUnavailableBuildTargets()
    {
        for (int i = 0; i < _buildSettings.Count; i++)
        {
            if (_buildSettings[i] == null)
                continue;

            if (_availableTargets.Contains(_buildSettings[i].BuildTarget) == false)
            {
                Debug.LogWarning($"{_buildSettings[i].BuildTarget} is unsupported. Removing {_buildSettings[i].name} buildSettings");
                _buildSettings.RemoveAt(i);
                i--;
            }
        }
    }

    private IEnumerator PerformBuild(List<CustomBuildSettings> buildSettings)
    {
        int buildAllProgressID = Progress.Start("Build All", "Building all selected platforms", Progress.Options.Sticky);
        Progress.ShowDetails();
        yield return new EditorWaitForSeconds(1f);

        BuildTarget originalTarget = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup originalTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        int originalSubtarget = 0;
        string originalProductName = PlayerSettings.productName;
        if (originalTargetGroup == BuildTargetGroup.Standalone)
        {
            originalSubtarget = (int)EditorUserBuildSettings.standaloneBuildSubtarget;
        }

        for (int i = 0; i < buildSettings.Count; i++)
        {
            CustomBuildSettings curBuildSettings = buildSettings[i];

            Progress.Report(buildAllProgressID, i + 1, buildSettings.Count);
            int buildTaskProgressID = Progress.Start($"Build {curBuildSettings.BuildTarget}", null, Progress.Options.Sticky, buildAllProgressID);
            yield return new EditorWaitForSeconds(1f);

            if (BuildIndividualTarget(curBuildSettings) == false)
            {
                Progress.Finish(buildTaskProgressID, Progress.Status.Failed);
                Progress.Finish(buildAllProgressID, Progress.Status.Failed);

                TryRestoreOriginalSettings(originalTarget, originalTargetGroup, originalSubtarget, originalProductName);

                yield break;
            }

            Progress.Finish(buildTaskProgressID, Progress.Status.Succeeded);
            yield return new EditorWaitForSeconds(1f);
        }

        Progress.Finish(buildAllProgressID, Progress.Status.Succeeded);

        TryRestoreOriginalSettings(originalTarget, originalTargetGroup, originalSubtarget, originalProductName);

        yield return null;
    }

    private bool BuildIndividualTarget(CustomBuildSettings buildSettings)
    {
        BuildTarget target = buildSettings.BuildTarget;
        BuildTargetGroup targetGroup = GetTargetGroupForTarget(target);
        string subtargetLog = string.Empty;

        BuildPlayerOptions options = new BuildPlayerOptions();

        if (targetGroup == BuildTargetGroup.Standalone)
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = buildSettings.StandaloneBuildSubtarget;
            int subtarget = (int)buildSettings.StandaloneBuildSubtarget;
            subtargetLog = buildSettings.StandaloneBuildSubtarget.ToString();
            options.subtarget = subtarget;
        }
        else if (targetGroup == BuildTargetGroup.Android)
        {
            subtargetLog = EditorUserBuildSettings.androidBuildSubtarget.ToString();
        }

        List<string> scenesPaths = new List<string>();
        foreach (var sceneObject in buildSettings.ScenesToBuild)
            scenesPaths.Add(AssetDatabase.GetAssetPath(sceneObject));

        options.scenes = scenesPaths.ToArray();
        options.target = target;
        options.targetGroup = targetGroup;
        Debug.Log($"Making build with options: target is {options.target}; " +
                  $"subtarget is {subtargetLog}; " +
                  $"targetGroup is {options.targetGroup}");

        if (string.IsNullOrEmpty(buildSettings.ProductName) == false)
            PlayerSettings.productName = buildSettings.ProductName;

        string fileName = PlayerSettings.productName + GetExtensionForTarget(target);
        string locationPathName;
        if (string.IsNullOrEmpty(buildSettings.BuildPath))
        {
            locationPathName = System.IO.Path.Combine("Builds", target.ToString(), fileName);
        }
        else
        {
            locationPathName = System.IO.Path.Combine(buildSettings.BuildPath, fileName);
        }
        options.locationPathName = locationPathName;

        if (BuildPipeline.BuildCanBeAppended(target, options.locationPathName) == CanAppendBuild.Yes)
        {
            options.options = BuildOptions.AcceptExternalModificationsToPlayer;
        }
        else
        {
            options.options = BuildOptions.None;
        }

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build for {target} completed in {report.summary.totalTime.TotalSeconds:0.##} seconds");
            return true;
        }

        Debug.LogError($"Build for {target} failed");
        return false;
    }

    private string GetExtensionForTarget(BuildTarget target) => target switch
    {
        BuildTarget.StandaloneWindows64 => ".exe",
        BuildTarget.StandaloneLinux64 => ".x86_64",
        BuildTarget.Android => ".apk",
        _ => string.Empty
    };

    private void ChangeActiveBuildTarget(BuildTarget target, BuildTargetGroup targetGroup, int subtarget)
    {
        if (targetGroup == BuildTargetGroup.Standalone)
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)subtarget;
        }

        EditorUserBuildSettings.SwitchActiveBuildTargetAsync(targetGroup, target);
    }

    private void TryRestoreOriginalSettings(BuildTarget origTarget, BuildTargetGroup origTargetGroup, int origSubtarget, string origProductName)
    {
        if (EditorUserBuildSettings.activeBuildTarget != origTarget)
            ChangeActiveBuildTarget(origTarget, origTargetGroup, origSubtarget);

        PlayerSettings.productName = origProductName;
    }
}
