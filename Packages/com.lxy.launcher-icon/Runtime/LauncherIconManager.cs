using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Lxy.LauncherIcons
{
    /// <summary>
    /// Stable logical IDs shared by game code and remote configuration.
    /// Add project-specific IDs here only when a compile-time constant is useful;
    /// <see cref="LauncherIconManager.SetIcon"/> also accepts a validated server value.
    /// </summary>
    public static class LauncherIconId
    {
        public const string Default = "default";
    }

    /// <summary>
    /// Host-side adapter used by the OpenHarmony Unity integration.
    /// The OpenHarmony exporter packages the declarations and media; the installed
    /// Unity/OpenHarmony bridge registers the runtime implementation.
    /// </summary>
    public interface IOpenHarmonyLauncherIconAdapter
    {
        bool SetIcon(string iconId);
    }

    /// <summary>
    /// Platform-independent launcher icon API. Icon IDs are logical IDs, not
    /// Android resource names, iOS file names, or Harmony media paths.
    /// </summary>
    public static class LauncherIconManager
    {
        private static IOpenHarmonyLauncherIconAdapter openHarmonyAdapter;

        /// <summary>
        /// Registers the bridge supplied by the OpenHarmony export integration.
        /// Registering null intentionally clears a previously installed adapter.
        /// </summary>
        public static void SetOpenHarmonyAdapter(IOpenHarmonyLauncherIconAdapter adapter)
        {
            openHarmonyAdapter = adapter;
        }

        /// <summary>
        /// Requests a launcher icon change. Use <see cref="LauncherIconId.Default"/>
        /// to restore the packaged default icon.
        /// </summary>
        public static bool SetIcon(string iconId)
        {
            if (!IsValidIconId(iconId))
            {
                Debug.LogWarning("[LauncherIcon] Icon ID must match [a-z][a-z0-9_]*: " + iconId);
                return false;
            }

#if UNITY_OPENHARMONY
            if (openHarmonyAdapter == null)
            {
                Debug.LogWarning("[LauncherIcon] No OpenHarmony launcher-icon adapter is registered.");
                return false;
            }

            return openHarmonyAdapter.SetIcon(iconId);
#elif UNITY_ANDROID && !UNITY_EDITOR
            return AndroidLauncherIcon.SetIcon(iconId);
#elif UNITY_IOS && !UNITY_EDITOR
            return IOSLauncherIcon.SetIcon(iconId);
#else
            Debug.LogWarning("[LauncherIcon] Launcher icon switching is not supported on this platform.");
            return false;
#endif
        }

        internal static bool IsValidIconId(string iconId)
        {
            if (string.IsNullOrEmpty(iconId) || iconId.Length > 255 || iconId[0] < 'a' || iconId[0] > 'z')
            {
                return false;
            }

            for (var index = 1; index < iconId.Length; index++)
            {
                var character = iconId[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '_')
                {
                    return false;
                }
            }

            return true;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal static class AndroidLauncherIcon
    {
        private const int GetActivities = 1;
        private const int ComponentEnabled = 1;
        private const int ComponentDisabled = 2;
        private const int DontKillApp = 1;
        private const string AliasPrefix = ".UnityLauncherIcon_";

        internal static bool SetIcon(string iconId)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                {
                    var packageName = activity.Call<string>("getPackageName");
                    using (var packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, GetActivities))
                    {
                        var activities = packageInfo.Get<AndroidJavaObject[]>("activities");
                        var targetClassName = packageName + AliasPrefix + iconId;
                        var targetExists = false;

                        if (activities != null)
                        {
                            for (var index = 0; index < activities.Length; index++)
                            {
                                using (var activityInfo = activities[index])
                                {
                                    var componentName = activityInfo.Get<string>("name");
                                    if (componentName == targetClassName)
                                    {
                                        targetExists = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!targetExists)
                        {
                            Debug.LogWarning("[LauncherIcon] The packaged Android icon is not available: " + iconId);
                            return false;
                        }

                        SetComponentState(packageManager, packageName, targetClassName, ComponentEnabled);

                        if (activities != null)
                        {
                            for (var index = 0; index < activities.Length; index++)
                            {
                                using (var activityInfo = activities[index])
                                {
                                    var componentName = activityInfo.Get<string>("name");
                                    if (componentName != targetClassName &&
                                        componentName.StartsWith(packageName + AliasPrefix, StringComparison.Ordinal))
                                    {
                                        SetComponentState(packageManager, packageName, componentName, ComponentDisabled);
                                    }
                                }
                            }
                        }

                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LauncherIcon] Android icon switch failed: " + exception.Message);
                return false;
            }
        }

        private static void SetComponentState(
            AndroidJavaObject packageManager,
            string packageName,
            string className,
            int state)
        {
            using (var componentName = new AndroidJavaObject("android.content.ComponentName", packageName, className))
            {
                packageManager.Call("setComponentEnabledSetting", componentName, state, DontKillApp);
            }
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    internal static class IOSLauncherIcon
    {
        [DllImport("__Internal")]
        private static extern bool LxyLauncherIconSet(string iconId);

        internal static bool SetIcon(string iconId)
        {
            try
            {
                return LxyLauncherIconSet(iconId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LauncherIcon] iOS icon switch failed: " + exception.Message);
                return false;
            }
        }
    }
#endif
}
