using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using StateControl.Runtime;
//using Yoka.ResourceCheck.OfficeOpenXml;
//using Yoka.ResourceCheck.OfficeOpenXml.Style;

namespace StateControl.Editor
{
    /// <summary>
    /// 编辑器工具：检查Prefab中StateController的ModifierRecordGroup是否存在丢失引用
    /// </summary>
    public class MissingReferenceChecker : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<MissingInfo> results = new List<MissingInfo>();
        private bool hasChecked = false;
        private bool isProcessing = false;
        private string searchPath = "Assets/GameResources/UIRes/Prefabs";

        private struct MissingInfo
        {
            public string PrefabPath;
            public string NodePath;
            public string Description;
        }

        [MenuItem("Tools/StateController/检查丢失引用")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissingReferenceChecker>("StateController丢失引用检查");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("StateController 丢失引用检查工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "检查所有Prefab中StateController组件的ModifierRecordGroups，查找：\n" +
                "• Records列表中丢失（null）的Record\n" +
                "• Target为null或Target.TargetObject丢失\n" +
                "点击结果项可定位到对应Prefab。",
                MessageType.Info
            );

            EditorGUILayout.Space(5);
            searchPath = EditorGUILayout.TextField("搜索路径", searchPath);
            EditorGUILayout.Space(5);

            GUI.enabled = !isProcessing;
            if (GUILayout.Button("开始检查", GUILayout.Height(35)))
            {
                CheckAllPrefabs();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            if (hasChecked)
            {
                if (results.Count == 0)
                {
                    EditorGUILayout.HelpBox("未发现丢失引用，一切正常！", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField($"发现 {results.Count} 个问题：", EditorStyles.boldLabel);

                    if (GUILayout.Button("导出为xlsx", GUILayout.Height(25)))
                    {
                        ExportToXlsx();
                    }

                    EditorGUILayout.Space(5);

                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                    foreach (var info in results)
                    {
                        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField(info.PrefabPath, EditorStyles.miniLabel);
                        EditorGUILayout.LabelField($"节点: {info.NodePath}");
                        EditorGUILayout.LabelField(info.Description, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.EndVertical();

                        if (GUILayout.Button("定位", GUILayout.Width(50), GUILayout.Height(40)))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<GameObject>(info.PrefabPath);
                            if (obj != null)
                            {
                                EditorGUIUtility.PingObject(obj);
                                Selection.activeObject = obj;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Space(2);
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void CheckAllPrefabs()
        {
            results.Clear();
            hasChecked = false;
            isProcessing = true;

            try
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchPath });
                int total = guids.Length;

                for (int i = 0; i < total; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    if (EditorUtility.DisplayCancelableProgressBar(
                        "检查丢失引用",
                        $"{path} ({i + 1}/{total})",
                        (float)i / total))
                    {
                        break;
                    }

                    try
                    {
                        CheckPrefab(path);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"检查Prefab失败: {path}, 错误: {ex.Message}");
                    }
                }

                EditorUtility.ClearProgressBar();
                hasChecked = true;

                if (results.Count > 0)
                    Debug.LogWarning($"[StateController丢失引用检查] 发现 {results.Count} 个问题，请在窗口中查看详情。");
                else
                    Debug.Log("[StateController丢失引用检查] 未发现丢失引用。");
            }
            finally
            {
                isProcessing = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void CheckPrefab(string prefabPath)
        {
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabInstance == null) return;

            try
            {
                StateController[] controllers = prefabInstance.GetComponentsInChildren<StateController>(true);
                foreach (var controller in controllers)
                {
                    // 跳过嵌套Prefab中的节点，避免同一问题在多个Prefab中重复报告
                    if (IsNestedPrefabNode(controller.gameObject, prefabInstance))
                        continue;

                    string nodePath = GetGameObjectPath(controller.gameObject, prefabInstance);
                    var errors = StateControllerCheckUtil.GetDetailedErrors(controller);
                    foreach (var error in errors)
                    {
                        results.Add(new MissingInfo
                        {
                            PrefabPath = prefabPath,
                            NodePath = nodePath,
                            Description = error
                        });
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabInstance);
            }
        }

        private bool IsNestedPrefabNode(GameObject go, GameObject prefabRoot)
        {
            if (go == prefabRoot) return false;

            if (PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.Connected)
            {
                GameObject nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (nearestPrefabRoot != null && nearestPrefabRoot != prefabRoot)
                    return true;
            }

            return false;
        }

        private void ExportToXlsx()
        {
            //string path = EditorUtility.SaveFilePanel("导出丢失引用报告", "", "StateController丢失引用报告", "xlsx");
            //if (string.IsNullOrEmpty(path)) return;

            //using (var package = new ExcelPackage())
            //{
            //    var sheet = package.Workbook.Worksheets.Add("丢失引用");

            //    // 表头
            //    sheet.Cells[1, 1].Value = "Prefab路径";
            //    sheet.Cells[1, 2].Value = "节点路径";
            //    sheet.Cells[1, 3].Value = "问题描述";

            //    // 表头样式
            //    for (int c = 1; c <= 3; c++)
            //    {
            //        sheet.Cells[1, c].Style.Font.Bold = true;
            //        sheet.Cells[1, c].Style.Fill.PatternType = ExcelFillStyle.Solid;
            //        sheet.Cells[1, c].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(220, 220, 220));
            //    }

            //    // 数据
            //    for (int i = 0; i < results.Count; i++)
            //    {
            //        int row = i + 2;
            //        sheet.Cells[row, 1].Value = results[i].PrefabPath;
            //        sheet.Cells[row, 2].Value = results[i].NodePath;
            //        sheet.Cells[row, 3].Value = results[i].Description;
            //    }

            //    // 自动列宽
            //    sheet.Cells.AutoFitColumns();

            //    package.SaveAs(new FileInfo(path));
            //}

            //Debug.Log($"[StateController丢失引用检查] 已导出到: {path}");
            //EditorUtility.DisplayDialog("导出成功", $"已导出 {results.Count} 条记录到:\n{path}", "确定");
        }

        private string GetGameObjectPath(GameObject go, GameObject root)
        {
            var sb = new StringBuilder(go.name);
            Transform current = go.transform.parent;
            while (current != null && current.gameObject != root)
            {
                sb.Insert(0, current.name + "/");
                current = current.parent;
            }
            if (root != null && go != root)
            {
                sb.Insert(0, root.name + "/");
            }
            return sb.ToString();
        }
    }
}
