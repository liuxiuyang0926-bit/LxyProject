using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal static class TurnBasedAssetEditorUtility
    {
        public static List<T> FindAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var result = new List<T>(guids.Length);
            for (int index = 0; index < guids.Length; index++)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                if (asset != null)
                {
                    result.Add(asset);
                }
            }
            result.Sort((left, right) => string.Compare(
                left.name,
                right.name,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static T CreateAsset<T>(
            string folder,
            string suggestedName,
            Action<T> initialize = null)
            where T : ScriptableObject
        {
            EnsureFolder(folder);
            string path = EditorUtility.SaveFilePanelInProject(
                "新建战斗编辑资产",
                suggestedName,
                "asset",
                "请选择 ScriptableObject 保存位置。",
                folder);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            initialize?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }

        public static string FileNameWithoutExtension(UnityEngine.Object asset)
        {
            return asset == null
                ? string.Empty
                : Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
        }

        public static bool RemoveAssetWithConfirmation(
            UnityEngine.Object asset,
            string assetKind,
            string displayName,
            Action beforeRemove = null)
        {
            if (asset == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                !AssetDatabase.IsMainAsset(asset))
            {
                EditorUtility.DisplayDialog(
                    "无法移除",
                    "只允许移除 Assets 目录下的独立 ScriptableObject 资源。\n\n" + path,
                    "知道了");
                return false;
            }

            string safeKind = string.IsNullOrWhiteSpace(assetKind)
                ? "资源"
                : assetKind;
            string safeName = string.IsNullOrWhiteSpace(displayName)
                ? asset.name
                : displayName;
            bool confirmed = EditorUtility.DisplayDialog(
                "确认移除" + safeKind,
                $"确定要移除{safeKind}“{safeName}”吗？\n\n" +
                $"资源路径：{path}\n\n" +
                "该 ScriptableObject 会从项目中移除并移动到系统回收站，" +
                "其他资源对它的引用将会失效。",
                "确定移除",
                "取消");
            if (!confirmed)
            {
                return false;
            }

            beforeRemove?.Invoke();
            if (Selection.activeObject == asset)
            {
                Selection.activeObject = null;
            }

            if (!AssetDatabase.MoveAssetToTrash(path))
            {
                EditorUtility.DisplayDialog(
                    "移除失败",
                    "无法移除资源，请确认文件没有被占用或设为只读。\n\n" + path,
                    "知道了");
                Debug.LogError($"[TurnBasedBattle] 移除{safeKind}失败：{path}", asset);
                return false;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[TurnBasedBattle] 已移除{safeKind}“{safeName}”：{path}");
            return true;
        }

        public static void EnsureFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        public static GUIStyle SelectedListStyle(bool selected)
        {
            return GUI.skin.FindStyle(selected ? "SelectionRect" : "Label") ??
                EditorStyles.label;
        }
    }
}
