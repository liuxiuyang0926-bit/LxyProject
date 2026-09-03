#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace LxyDemo.UIFramework.Editor
{
    [CustomEditor(typeof(UICodeBinder))]
    public sealed class UICodeBinderEditor : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制 UICodeBinder 的 Inspector 界面。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "命名前缀自动绑定：btn_、tgl_、slider_、input_、" +
                "dropdown_、img_、txt_、tmp_、scroll_、cg_、" +
                "rt_、tf_、go_。",
                MessageType.Info);

            UICodeBinder binder = (UICodeBinder)target;
            if (binder.ScriptType == UIScriptType.Lua)
            {
                EditorGUILayout.HelpBox(
                    "Lua 类型不生成 C# / Lua Auto 脚本。" +
                    "请在同一根节点的 ObjectBinder 组件中配置 Lua " +
                    "文件和控件绑定。",
                    MessageType.Info);
                return;
            }

            if (GUILayout.Button("按节点命名收集绑定"))
            {
                Undo.RecordObject(binder, "Collect UI Bindings");
                int count =
                    CSharpUIGenerator.AutoCollectBindings(binder);
                EditorUtility.SetDirty(binder);
                SavePrefabIfNeeded(binder);
                Debug.Log(
                    $"[UI Generator] 新增 {count} 个绑定。");
            }

            using (new EditorGUI.DisabledScope(
                       string.IsNullOrWhiteSpace(
                           binder.AutoScriptPath)))
            {
                if (GUILayout.Button("重新生成 Auto 脚本"))
                {
                    try
                    {
                        string prefabPath =
                            GetPrefabAssetPath(binder);
                        CSharpUIGenerator.RegenerateAutoCode(
                            prefabPath,
                            false);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog(
                            "Auto 代码生成失败",
                            exception.Message,
                            "确定");
                    }
                }

                if (GUILayout.Button("收集绑定并重新生成 Auto 脚本"))
                {
                    try
                    {
                        string prefabPath =
                            GetPrefabAssetPath(binder);
                        SavePrefabIfNeeded(binder);
                        CSharpUIGenerator.RegenerateAutoCode(
                            prefabPath,
                            true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog(
                            "Auto 代码生成失败",
                            exception.Message,
                            "确定");
                    }
                }
            }
        }

        /// <summary>
        /// 获取预制体资源路径。
        /// </summary>
        private static string GetPrefabAssetPath(
            UICodeBinder binder)
        {
            string path =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    binder.gameObject);
            if (string.IsNullOrEmpty(path))
            {
                path = AssetDatabase.GetAssetPath(
                    binder.gameObject);
            }

            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "请在 Prefab 资源或 Prefab Mode 中生成代码。");
            }

            return path;
        }

        /// <summary>
        /// 保存预制体IfNeeded。
        /// </summary>
        private static void SavePrefabIfNeeded(
            UICodeBinder binder)
        {
            EditorUtility.SetDirty(binder);
            if (PrefabUtility.IsPartOfPrefabInstance(
                    binder.gameObject))
            {
                PrefabUtility.ApplyPrefabInstance(
                    binder.gameObject,
                    InteractionMode.UserAction);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif
