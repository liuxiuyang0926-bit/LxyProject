#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace LxyDemo.UIFramework.Editor
{
    public static class UIManagerValidationMenu
    {
        /// <summary>
        /// 校验全部设置。
        /// </summary>
        [MenuItem(
            "Tools/UI Manager/Validate All Settings",
            priority = 100)]
        public static void ValidateAllSettings()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:UIManagerSettings");
            if (guids.Length == 0)
            {
                Debug.LogWarning(
                    "[UIManager] 未找到 UIManagerSettings 资源。");
                return;
            }

            int panelCount = 0;
            var errors = new List<string>();
            foreach (string guid in guids)
            {
                string settingsPath =
                    AssetDatabase.GUIDToAssetPath(guid);
                UIManagerSettings settings =
                    AssetDatabase.LoadAssetAtPath<
                        UIManagerSettings>(settingsPath);
                if (settings == null)
                {
                    continue;
                }

                var ids =
                    new HashSet<string>(StringComparer.Ordinal);
                foreach (UIPanelConfig config in settings.Panels)
                {
                    panelCount++;
                    ValidateConfig(
                        config,
                        settingsPath,
                        ids,
                        errors);
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Debug.LogError($"[UIManager] {error}");
                }

                throw new InvalidOperationException(
                    $"UIManager 配置校验失败，共 {errors.Count} 个错误。");
            }

            Debug.Log(
                $"[UIManager] 配置校验通过：{guids.Length} 个 Settings，" +
                $"{panelCount} 个 Panel。");
        }

        /// <summary>
        /// 校验配置。
        /// </summary>
        private static void ValidateConfig(
            UIPanelConfig config,
            string settingsPath,
            HashSet<string> ids,
            List<string> errors)
        {
            if (config == null)
            {
                errors.Add($"{settingsPath} 中存在空 Panel 配置。");
                return;
            }

            try
            {
                config.Validate();
            }
            catch (Exception exception)
            {
                errors.Add($"{settingsPath}: {exception.Message}");
                return;
            }

            if (!ids.Add(config.Id))
            {
                errors.Add(
                    $"{settingsPath} 中 Panel ID 重复：{config.Id}");
            }

            if (string.Equals(
                    config.PackageName,
                    "@local",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!BundleCollectorSettingData.HasSettingAsset())
            {
                errors.Add(
                    "项目中不存在 YooAsset " +
                    "BundleCollectorSetting 配置。");
                return;
            }

            try
            {
                CollectResult collectResult =
                    BundleCollectorSettingData.Setting.BeginCollect(
                        config.PackageName,
                        true,
                        false);
                CollectAssetInfo collectedAsset =
                    collectResult.CollectAssets.FirstOrDefault(
                        item =>
                            string.Equals(
                                item.AssetInfo.AssetPath,
                                config.Location,
                                StringComparison.Ordinal) ||
                            string.Equals(
                                item.Address,
                                config.Location,
                                StringComparison.Ordinal));
                if (collectedAsset == null)
                {
                    errors.Add(
                        $"{config.Id} 的 YooAsset 资源不存在：" +
                        $"Package={config.PackageName}，" +
                        $"Location={config.Location}");
                    return;
                }

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        collectedAsset.AssetInfo.AssetPath);
                if (prefab == null)
                {
                    errors.Add(
                        $"{config.Id} 的 YooAsset Location " +
                        "不是 GameObject Prefab：" +
                        config.Location);
                    return;
                }
            }
            catch (Exception exception)
            {
                errors.Add(
                    $"{config.Id} 的 YooAsset 配置校验失败：" +
                    exception.Message);
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    config.LogicTypeName))
            {
                Type logicType =
                    Type.GetType(config.LogicTypeName, false);
                if (logicType == null ||
                    !typeof(UIPanelLogic).IsAssignableFrom(
                        logicType) ||
                    logicType.IsAbstract)
                {
                    errors.Add(
                        $"{config.Id} 的 Logic 类型无效：" +
                        config.LogicTypeName);
                }
            }
        }
    }
}
#endif
