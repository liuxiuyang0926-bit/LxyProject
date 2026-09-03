using UnityEngine;
using UnityEditor;
using StateControl.Runtime;

namespace StateControl.Editor
{
    /// <summary>
    /// 编辑器工具：修复所有Prefab中StateController的StateGroup索引
    /// </summary>
    public class PrefabStateIndexFixer : EditorWindow
    {
        private int totalPrefabsCount = 0;
        private int processedCount = 0;
        private int fixedCount = 0;
        private bool isProcessing = false;

        /// <summary>
        /// 执行显示窗口相关逻辑。
        /// </summary>
        [MenuItem("Tools/StateController/修复Prefab状态索引")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabStateIndexFixer>("Prefab状态索引修复工具");
            window.Show();
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Prefab StateController 索引修复工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "此工具将遍历所有Prefab资源，修复StateController组件中StateGroup的currentState索引。\n" +
                "只处理非嵌套Prefab节点。",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            GUI.enabled = !isProcessing;
            if (GUILayout.Button("开始修复", GUILayout.Height(40)))
            {
                FixAllPrefabs();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            if (isProcessing)
            {
                EditorGUILayout.LabelField("处理中...", EditorStyles.boldLabel);
            }

            if (totalPrefabsCount > 0)
            {
                EditorGUILayout.LabelField($"总Prefab数: {totalPrefabsCount}");
                EditorGUILayout.LabelField($"已处理: {processedCount}");
                EditorGUILayout.LabelField($"已修复: {fixedCount}");
            }

        }

        /// <summary>
        /// 执行FixAllPrefabs相关逻辑。
        /// </summary>
        private void FixAllPrefabs()
        {
            processedCount = 0;
            fixedCount = 0;
            isProcessing = true;

            try
            {
                // 查找所有.prefab文件
                string[] allPrefabGuids = AssetDatabase.FindAssets("t:Prefab");
                totalPrefabsCount = allPrefabGuids.Length;

                for (int i = 0; i < allPrefabGuids.Length; i++)
                {
                    string guid = allPrefabGuids[i];
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    // 显示进度条
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "修复Prefab状态索引",
                        $"处理中: {path} ({i + 1}/{totalPrefabsCount})",
                        (float)i / totalPrefabsCount))
                    {
                        break; // 用户取消
                    }

                    try
                    {
                        if (ProcessPrefab(path))
                        {
                            fixedCount++;
                        }
                        processedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"处理Prefab失败: {path}, 错误: {ex.Message}");
                    }
                }

                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Prefab索引修复完成！总数: {totalPrefabsCount}, 已修复: {fixedCount}");
                EditorUtility.DisplayDialog("完成", $"Prefab索引修复完成！\n总数: {totalPrefabsCount}\n已修复: {fixedCount}", "确定");
            }
            finally
            {
                isProcessing = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        /// <summary>
        /// 执行流程预制体相关逻辑。
        /// </summary>
        private bool ProcessPrefab(string prefabPath)
        {
            // 检查是否是Prefab变体，变体不需要修改
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset != null)
            {
                PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);
                if (assetType == PrefabAssetType.Variant)
                {
                    // 跳过Prefab变体
                    return false;
                }
            }

            // 使用LoadPrefabContents加载并实例化Prefab，这样才能正确反序列化所有字段
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabInstance == null)
            {
                return false;
            }

            bool hasChanges = false;

            try
            {
                // 获取Prefab根节点及所有子节点
                Transform[] allTransforms = prefabInstance.GetComponentsInChildren<Transform>(true);

                foreach (Transform transform in allTransforms)
                {
                    GameObject go = transform.gameObject;

                    // 检查是否是嵌套Prefab节点
                    if (IsNestedPrefabNode(go, prefabInstance))
                    {
                        continue; // 跳过嵌套Prefab节点
                    }

                    // 获取StateController组件
                    StateController stateController = go.GetComponent<StateController>();
                    if (stateController == null)
                    {
                        continue;
                    }

                    // 处理StateController的所有StateGroup
                    foreach (StateGroup stateGroup in stateController.StateGroups)
                    {
                        if (stateGroup.States == null || stateGroup.States.Count == 0)
                        {
                            continue;
                        }

                        // 现在可以直接访问currentState，实例化后应该有正确的值
                        State oldCurrentState = stateGroup.currentState;
                        string currentStateName = oldCurrentState?.Name;

                        // 如果currentState不为空且有Name
                        if (!string.IsNullOrEmpty(currentStateName))
                        {
                            // 在States列表中查找匹配的State
                            State matchedState = null;
                            for (int i = 0; i < stateGroup.States.Count; i++)
                            {
                                if (stateGroup.States[i].Name == currentStateName)
                                {
                                    matchedState = stateGroup.States[i];
                                    break;
                                }
                            }

                            if (matchedState != null)
                            {
                                // 通过CurState属性设置，这会自动更新curStateIndex
                                stateGroup.CurState = matchedState;

                                // 清空旧字段
                                stateGroup.currentState = null;

                                hasChanges = true;
                                Debug.Log($"修复 {prefabPath} - {stateGroup.Name}: 从旧字段恢复状态 '{currentStateName}'");
                            }
                            else
                            {
                                Debug.LogWarning($"无法在 {prefabPath} - {stateGroup.Name} 中找到名为 '{currentStateName}' 的状态");
                            }
                        }
                        // 如果currentState不为空但Name是空的，尝试通过引用匹配
                        else if (oldCurrentState != null)
                        {
                            // 尝试通过对象引用在States列表中查找
                            int matchIndex = stateGroup.States.IndexOf(oldCurrentState);
                            if (matchIndex >= 0)
                            {
                                stateGroup.CurState = stateGroup.States[matchIndex];
                                stateGroup.currentState = null;
                                hasChanges = true;
                                Debug.Log($"修复 {prefabPath} - {stateGroup.Name}: 通过引用匹配恢复状态（索引 {matchIndex}）");
                            }
                            else
                            {
                                // 无法匹配，设置为第一个状态
                                Debug.LogWarning($"{prefabPath} - {stateGroup.Name}: currentState无Name且无法匹配引用，设置为第一个状态");
                                stateGroup.CurState = stateGroup.States[0];
                                stateGroup.currentState = null;
                                hasChanges = true;
                            }
                        }
                        // 如果currentState为空，但curStateIndex为-1或无效
                        else if (stateGroup.CurState == null && stateGroup.States.Count > 0)
                        {
                            // 设置为第一个状态
                            stateGroup.CurState = stateGroup.States[0];
                            hasChanges = true;
                        }
                        // 确保索引正确
                        else if (stateGroup.CurState != null)
                        {
                            // 重新设置一次，确保索引正确
                            State currentState = stateGroup.CurState;
                            stateGroup.CurState = currentState;
                            hasChanges = true;
                        }
                    }
                }

                // 如果有修改，保存Prefab
                if (hasChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    return true;
                }
            }
            finally
            {
                // 无论成功或失败，都要卸载Prefab实例
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }

            return false;
        }

        /// <summary>
        /// 检查GameObject是否是嵌套Prefab节点
        /// </summary>
        private bool IsNestedPrefabNode(GameObject go, GameObject prefabRoot)
        {
            // 如果是Prefab根节点，不是嵌套节点
            if (go == prefabRoot)
            {
                return false;
            }

            // 检查是否是Prefab资源的一部分
            // 如果该节点是另一个Prefab的实例（嵌套Prefab），则跳过
            var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(go);

            // 如果节点是嵌套Prefab的一部分
            if (prefabInstanceStatus == PrefabInstanceStatus.Connected)
            {
                // 获取最近的Prefab实例根节点
                GameObject nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);

                // 如果最近的Prefab根不是当前Prefab的根，说明是嵌套Prefab
                if (nearestPrefabRoot != null && nearestPrefabRoot != prefabRoot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
