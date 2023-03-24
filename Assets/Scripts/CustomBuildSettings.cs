using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NewBuildSettings", menuName = "SO/Build Settings", order = 51), ExecuteInEditMode]
public class CustomBuildSettings : ScriptableObject
{
#if UNITY_EDITOR

    [SerializeField] private List<Object> _scenesToBuild = new List<Object>();
    [SerializeField] private AvailableBuildTargets _buildTarget;
    [SerializeField] private StandaloneBuildSubtarget _standaloneBuildSubtarget;
    [SerializeField] private string _productName;
    [SerializeField] private string _buildPath;

    public List<Object> ScenesToBuild => _scenesToBuild;
    public BuildTarget BuildTarget => GetBuildTargetByEnum(_buildTarget);
    public StandaloneBuildSubtarget StandaloneBuildSubtarget => _standaloneBuildSubtarget;
    public string ProductName => _productName;
    public string BuildPath => _buildPath;


    public enum AvailableBuildTargets
    {
        Windows64,
        Linux64,
        OSX,
        Android
    }

    public BuildTarget GetBuildTargetByEnum(AvailableBuildTargets target) => target switch
    {
        AvailableBuildTargets.Windows64 => BuildTarget.StandaloneWindows64,
        AvailableBuildTargets.Linux64 => BuildTarget.StandaloneLinux64,
        AvailableBuildTargets.OSX => BuildTarget.StandaloneOSX,
        AvailableBuildTargets.Android => BuildTarget.Android,
        _ => BuildTarget.NoTarget
    };

#endif
}