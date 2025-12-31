using UnityEngine;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
using System.Linq;
using System.Collections.Generic;

[InitializeOnLoad]
public class FixXRSettings
{
    static FixXRSettings()
    {
        EditorApplication.delayCall += Fix;
    }

    static void Fix()
    {
        // Only run on macOS Editor
        if (Application.platform != RuntimePlatform.OSXEditor)
            return;

        BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone;
        XRGeneralSettings settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);

        if (settings == null)
        {
            return;
        }

        XRManagerSettings manager = settings.Manager;
        if (manager == null)
        {
            return;
        }

        // Create a list copy to avoid modification during iteration issues, though TryRemoveLoader handles it
        var loaders = manager.activeLoaders;
        var openXRLoader = loaders.FirstOrDefault(l => l.GetType().FullName.Contains("OpenXRLoader"));

        if (openXRLoader != null)
        {
            Debug.Log($"[FixXRSettings] Removing OpenXRLoader from Standalone settings as it is not supported on macOS.");
            if (manager.TryRemoveLoader(openXRLoader))
            {
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssets();
                Debug.Log("[FixXRSettings] Successfully removed OpenXR Loader.");
            }
            else
            {
                Debug.LogError("[FixXRSettings] Failed to remove OpenXR Loader.");
            }
        }
    }
}
