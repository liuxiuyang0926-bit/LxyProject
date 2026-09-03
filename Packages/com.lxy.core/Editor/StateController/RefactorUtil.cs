using System.Collections.Generic;
using System.IO;
using StateControl.Runtime;
using UnityEditor;
using UnityEngine;

namespace StateControl.Editor
{
    public static class RefactorUtil
    {
        /// <summary>
        /// 执行Refactor修饰器相关逻辑。
        /// </summary>
        [MenuItem("Assets/StateControl/重构prefab的StateController的Modifier", false, 19)]
        public static void RefactorModifier()
        {
            GameObject selectedPrefab = Selection.activeGameObject;
            if (selectedPrefab != null && PrefabUtility.IsPartOfAnyPrefab(selectedPrefab))
            {
                RefactorModifier(selectedPrefab);
            }
            else
            {
                Debug.LogError("当前没有选中预制体！");
            }
        }

        /// <summary>
        /// 执行Refactor状态相关逻辑。
        /// </summary>
        [MenuItem("Assets/StateControl/重构prefab的StateController的State", false, 19)]
        public static void RefactorState()
        {
            GameObject selectedPrefab = Selection.activeGameObject;
            if (selectedPrefab != null && PrefabUtility.IsPartOfAnyPrefab(selectedPrefab))
            {
                RefactorState(selectedPrefab);
            }
            else
            {
                Debug.LogError("当前没有选中预制体！");
            }
        }


        /// <summary>
        /// 执行Refactor状态相关逻辑。
        /// </summary>
        static void RefactorState(GameObject selectedPrefab)
        {
            var controllers = selectedPrefab.GetComponentsInChildren<StateControl.Runtime.StateController>(true);
            int migratedCount = 0;
            foreach (var controller in controllers)
            {
                // 检查是否为嵌套prefab的内容，如果是则恢复为引用状态
                if (PrefabUtility.IsPartOfPrefabInstance(controller.gameObject) &&
                    !PrefabUtility.IsPartOfPrefabAsset(controller.gameObject))
                {
                    PrefabUtility.RevertObjectOverride(controller, InteractionMode.AutomatedAction);
                    continue;
                }
            }

            if (migratedCount > 0)
            {
                EditorUtility.SetDirty(selectedPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"{selectedPrefab.name}已迁移完成，迁移了{migratedCount}个State->Modifier到ModifierRecord");
            }
            else
            {
                Debug.LogWarning("未发现可迁移的内容。");
            }
        }

        /// <summary>
        /// 执行Refactor修饰器相关逻辑。
        /// </summary>
        static void RefactorModifier(GameObject selectedPrefab)
        {
            // 获取所有StateController组件
            var controllers = selectedPrefab.GetComponentsInChildren<StateControl.Runtime.StateController>(true);
            int changedCount = 0;
            foreach (var controller in controllers)
            {
                // 检查是否为嵌套prefab的内容，如果是则恢复为引用状态
                if (PrefabUtility.IsPartOfPrefabInstance(controller.gameObject))
                {
                    PrefabUtility.RevertObjectOverride(controller, InteractionMode.AutomatedAction);
                    PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(controller);
                    if (modifications != null)
                    {
                        foreach (PropertyModification mod in modifications)
                        {
                            if (mod.target is StateController)
                            {
                                SerializedProperty property = new SerializedObject(controller).FindProperty(mod.propertyPath);
                                if(property != null)
                                    PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                            }
                        }
                    }
                    continue;
                }
            }

            if (changedCount > 0)
            {
                EditorUtility.SetDirty(selectedPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"{selectedPrefab.name}已重构完成，修改了{changedCount}个ModifierType");
            }
            else
            {
                Debug.LogWarning("未发现可重构的内容。");
            }
        }

        /// <summary>
        /// 执行RefactorAll相关逻辑。
        /// </summary>
        [MenuItem("Assets/StateControl/重构所有prefab", false, 19)]
        public static void RefactorAll()
        {
            var prefabs = AssetDatabase.FindAssets("t:prefab", new[] { "Assets/GameResources/UIRes/Prefabs" });
            if (prefabs.Length == 0)
            {
                Debug.LogWarning("未找到需要重构的Prefab");
                return;
            }

            // 显示确认对话框
            if (!EditorUtility.DisplayDialog("批量重构确认",
                    $"将重构 {prefabs.Length} 个Prefab，此操作不可撤销。是否继续？",
                    "开始重构", "取消"))
            {
                return;
            }

            int totalModified = 0;
            int currentIndex = 0;

            foreach (var prefab in prefabs)
            {
                currentIndex++;

                // 显示进度对话框，允许取消
                if (EditorUtility.DisplayCancelableProgressBar("批量重构进度",
                        $"正在处理: {AssetDatabase.GUIDToAssetPath(prefab)} ({currentIndex}/{prefabs.Length})",
                        (float)currentIndex / prefabs.Length))
                {
                    EditorUtility.ClearProgressBar();
                    Debug.Log("用户取消了批量重构操作");
                    return;
                }

                var path = AssetDatabase.GUIDToAssetPath(prefab);
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabAsset != null)
                {
                    RefactorModifier(prefabAsset);
                    RefactorState(prefabAsset);
                    totalModified++;
                }
            }

            EditorUtility.ClearProgressBar();

            if (totalModified > 0)
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("重构完成", $"批量重构完成，共处理了 {totalModified} 个Prefab", "确定");
                Debug.Log($"批量重构完成，共处理了{totalModified}个Prefab");
            }
            else
            {
                EditorUtility.DisplayDialog("重构完成", "未发现需要重构的内容", "确定");
                Debug.LogWarning("未发现需要重构的内容");
            }
        }
    }
}