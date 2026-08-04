using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.CodeEditor;
using UnityEditor.SceneManagement;
using LxyDemo.UIFramework.Editor;
using VGame.GameLogic.Editor.LuaUI;

namespace LuaObjectBind.Editor
{
    [CustomEditor(typeof(ObjectBinder))]
    public class ObjectBinderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            ObjectBinder binder = (ObjectBinder)target;

            // 收集所有验证信息
            List<string> warnings = new List<string>();

            // 检查bindValues中的重复键和空键
            if (binder.bindValues != null && binder.bindValues.Binds != null)
            {
                var bindValueNames = binder.bindValues.Binds.Select(b => b?.Name).ToList();
                // 重复Bind组件
                var duplicateBindObjectNames = binder.bindValues.Binds
                    .GroupBy((x) => x.GetObjectValue)
                    .Where(g => g.Count() > 1)
                    .Select(g =>
                    {
                        if(g.Key == null) return "";
                        string names = string.Join("\n", g.Select(s => s.Name));
                        return $"{g.Key.name}({g.Key.GetType().ToString()})=>\n{names}";
                    })
                    .ToList();

                var duplicateNames = bindValueNames
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var emptyNames = bindValueNames
                    .Where(name => string.IsNullOrEmpty(name))
                    .ToList();

                if (duplicateNames.Count > 0 || emptyNames.Count > 0 || duplicateBindObjectNames.Count > 0)
                {
                    warnings.Add("绑定值异常: " +
                                 (duplicateNames.Count > 0 ? $"\n重复键[{string.Join(",", duplicateNames)}] " : "") +
                                 (emptyNames.Count > 0 ? $"\n空键[{emptyNames.Count}]" : "") +
                                 (duplicateBindObjectNames.Count > 0
                                     ? $"\n重复绑定组件:\n-------------------{string.Join("\n-------------------", duplicateBindObjectNames)}"
                                     : ""));
                }
            }

            // 检查fieldBindValues中的重复键和空键
            if (binder.fieldBindValues != null && binder.fieldBindValues.Binds != null)
            {
                var fieldBindValueNames = binder.fieldBindValues.Binds.Select(b => b?.Name).ToList();
                var duplicateNames = fieldBindValueNames
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var emptyNames = fieldBindValueNames
                    .Where(name => string.IsNullOrEmpty(name))
                    .ToList();

                if (duplicateNames.Count > 0 || emptyNames.Count > 0)
                {
                    warnings.Add("字段绑定: " +
                                 (duplicateNames.Count > 0 ? $"重复键[{string.Join(",", duplicateNames)}] " : "") +
                                 (emptyNames.Count > 0 ? $"空键[{emptyNames.Count}]" : ""));
                }
            }

            // 检查pathBindValues中的重复键和空键
            if (binder.pathBindValues != null && binder.pathBindValues.Binds != null)
            {
                var pathBindValueNames = binder.pathBindValues.Binds.Select(b => b?.Name).ToList();
                var duplicateNames = pathBindValueNames
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var emptyNames = pathBindValueNames
                    .Where(name => string.IsNullOrEmpty(name))
                    .ToList();

                if (duplicateNames.Count > 0 || emptyNames.Count > 0)
                {
                    warnings.Add("路径绑定: " +
                                 (duplicateNames.Count > 0 ? $"重复键[{string.Join(",", duplicateNames)}] " : "") +
                                 (emptyNames.Count > 0 ? $"空键[{emptyNames.Count}]" : ""));
                }
            }
            
            // 检查stateControlBindValues中的重复键和空键
            if (binder.stateControlBindValues != null && binder.stateControlBindValues.Binds != null)
            {
                var stateControlBindValueNames = binder.stateControlBindValues.Binds.Select(b => b?.Name).ToList();
                var duplicateNames = stateControlBindValueNames
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var emptyNames = stateControlBindValueNames
                    .Where(name => string.IsNullOrEmpty(name))
                    .ToList();

                if (duplicateNames.Count > 0 || emptyNames.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        "状态控制绑定: " +
                        (duplicateNames.Count > 0 ? $"重复键[{string.Join(",", duplicateNames)}] " : "") +
                        (emptyNames.Count > 0 ? $"空键[{emptyNames.Count}]" : ""),
                        MessageType.Warning);
                }
            }

            // 检查staticTextBindValues中的重复键和空键
            if (binder.staticTextBindValues != null && binder.staticTextBindValues.Binds != null)
            {
                var staticTextBindValueNames = binder.staticTextBindValues.Binds.Select(b => b?.Name).ToList();
                var duplicateNames = staticTextBindValueNames
                    .Where(name => !string.IsNullOrEmpty(name))
                    .GroupBy(name => name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                var emptyNames = staticTextBindValueNames
                    .Where(name => string.IsNullOrEmpty(name))
                    .ToList();

                if (duplicateNames.Count > 0 || emptyNames.Count > 0)
                {
                    warnings.Add("静态文本绑定: " +
                                 (duplicateNames.Count > 0 ? $"重复键[{string.Join(",", duplicateNames)}] " : "") +
                                 (emptyNames.Count > 0 ? $"空键[{emptyNames.Count}]" : ""));
                }
            }

            // 始终显示一个警告区域，避免布局变化导致焦点丢失
            if (warnings.Count > 0)
            {
                string message = string.Join("\n", warnings);

                // 1. 定义样式：基于默认的 helpBox，但强制开启换行
                GUIStyle multilineStyle = new GUIStyle(EditorStyles.helpBox);
                multilineStyle.wordWrap = true;             // 关键：允许自动换行
                multilineStyle.richText = true;             // 允许富文本
                multilineStyle.alignment = TextAnchor.UpperLeft; // 左对齐
                multilineStyle.padding = new RectOffset(10, 10, 10, 10); // 增加内边距，美观
                multilineStyle.fontSize = 12;

                // 2. 获取内置的 Error 图标
                Texture2D errorIcon = EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;
                GUIContent content = new GUIContent(message, errorIcon);

                // 3. 临时修改背景色以模拟 Error 级别的红色背景 (淡红色)
                Color originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); 

                // 4. 使用 Label 绘制，它比 HelpBox 更擅长处理自动高度扩展
                GUILayout.Label(content, multilineStyle);

                // 恢复背景色
                GUI.backgroundColor = originalColor;
            }
            else
            {
                // 即使没有警告也显示一个占位空间，保持布局稳定
                GUILayout.Space(4);
            }

            EditorGUILayout.Space(2);
            if (binder.uiScriptGeneration == null)
            {
                Undo.RecordObject(
                    binder,
                    "Initialize UI Script Generation Settings");
                binder.uiScriptGeneration =
                    new UIScriptGenerationSettings();
                EditorUtility.SetDirty(binder);
            }

            DrawConditionalInspector();

            bool isLuaScript =
                binder.uiScriptGeneration.scriptType ==
                UIObjectBinderScriptType.Lua;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "UI 脚本生成",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "当前 Prefab 类型：" +
                binder.uiScriptGeneration.scriptType +
                "。类型与路径可以直接在上方 UI Script " +
                "Generation 配置中修改；创建 Prefab 时不会自动生成脚本。",
                MessageType.Info);

            if (isLuaScript)
            {
                if (GUILayout.Button(
                        "生成 Lua 脚本",
                        GUILayout.Height(30)))
                {
                    try
                    {
                        GenerateLuaScripts(binder);
                    }
                    catch (System.OperationCanceledException)
                    {
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog(
                            "Lua 脚本生成失败",
                            exception.Message,
                            "确定");
                    }
                }
            }
            else
            {
                if (GUILayout.Button(
                        "生成 C# 脚本",
                        GUILayout.Height(30)))
                {
                    try
                    {
                        GenerateCSharpScripts(binder);
                    }
                    catch (System.OperationCanceledException)
                    {
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog(
                            "C# 脚本生成失败",
                            exception.Message,
                            "确定");
                    }
                }
            }

            if (EditorGUILayout.LinkButton("前往编辑绑定缩写"))
            {
                string path = LuaObjectBindProxy.GetLuaObjectBindEditorProxyPath();
                if (!string.IsNullOrEmpty(path))
                {
                    if (!File.Exists(path))
                    {
                        EditorUtility.DisplayDialog("错误", "文件不存在: " + path, "确定");
                        return;
                    }

                    CodeEditor.CurrentEditor.OpenProject(path);
                }
            }

            if (EditorGUILayout.LinkButton("前往文档"))
            {
                Application.OpenURL("https://l00pl1t6jli.feishu.cn/docx/EAwkdaq1XoaK52xCbeTcPmLhnNb");
            }
        }

        private void DrawConditionalInspector()
        {
            serializedObject.Update();

            SerializedProperty scriptTypeProperty =
                serializedObject.FindProperty(
                    "uiScriptGeneration.scriptType");
            SerializedProperty property =
                serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(
                            property,
                            true);
                    }

                    continue;
                }

                if (property.propertyPath == "lua" &&
                    !IsLuaScriptType(scriptTypeProperty))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsLuaScriptType(
            SerializedProperty scriptTypeProperty)
        {
            return scriptTypeProperty != null &&
                   scriptTypeProperty.enumValueIndex ==
                   (int)UIObjectBinderScriptType.Lua;
        }

        private static void GenerateLuaScripts(
            ObjectBinder binder)
        {
            GameObject prefab =
                ResolvePrefabAsset(binder);
            string prefabPath =
                AssetDatabase.GetAssetPath(prefab);
            CollectBindingsAndSave(prefabPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            ObjectBinder prefabBinder =
                prefab.GetComponent<ObjectBinder>();
            UIScriptGenerationSettings settings =
                prefabBinder.uiScriptGeneration ??=
                    new UIScriptGenerationSettings();
            settings.scriptType =
                UIObjectBinderScriptType.Lua;
            settings.panelId =
                string.IsNullOrWhiteSpace(settings.panelId)
                    ? prefab.name
                    : settings.panelId;
            settings.luaOutputFolder =
                string.IsNullOrWhiteSpace(settings.luaOutputFolder)
                    ? "Lua/UI"
                    : settings.luaOutputFolder;
            // luaOutputFolder 是最终输出目录。模块名必须和实际文件路径
            // 保持一致，否则 require 会去错误的目录查找脚本。
            settings.luaModuleName = BuildLuaModuleName(
                settings.luaOutputFolder,
                prefab.name);

            var generator = new LuaUIBaseLogicGenerator();
            generator.InitializeWithTarget(prefab);
            generator.SetClassName(settings.luaModuleName);
            generator.SetOutputFolder(
                settings.luaOutputFolder);
            generator.GenerateLuaFile(false);

            prefabBinder.lua ??= new LuaFileReference();
            prefabBinder.lua.FilePath =
                settings.luaModuleName.Replace('.', '/') +
                ".lua";
            EditorUtility.SetDirty(prefabBinder);
            AssetDatabase.SaveAssets();
        }

        private static string BuildLuaModuleName(
            string outputFolder,
            string prefabName)
        {
            string relativeFolder =
                (outputFolder ?? "Lua/UI")
                .Replace('\\', '/')
                .Trim('/');
            if (relativeFolder.StartsWith(
                    "Lua/",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                relativeFolder = relativeFolder.Substring(4);
            }
            else if (string.Equals(
                         relativeFolder,
                         "Lua",
                         System.StringComparison.OrdinalIgnoreCase))
            {
                relativeFolder = string.Empty;
            }

            string modulePrefix =
                relativeFolder.Replace('/', '.').Trim('.');
            return string.IsNullOrEmpty(modulePrefix)
                ? prefabName
                : modulePrefix + "." + prefabName;
        }

        private static void GenerateCSharpScripts(
            ObjectBinder binder)
        {
            GameObject prefab =
                ResolvePrefabAsset(binder);
            string prefabPath =
                AssetDatabase.GetAssetPath(prefab);
            CollectBindingsAndSave(prefabPath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            ObjectBinder prefabBinder =
                prefab.GetComponent<ObjectBinder>();
            prefabBinder.uiScriptGeneration ??=
                new UIScriptGenerationSettings();
            prefabBinder.uiScriptGeneration.scriptType =
                UIObjectBinderScriptType.CSharp;
            EditorUtility.SetDirty(prefabBinder);
            AssetDatabase.SaveAssets();

            CSharpUIGenerator.GenerateCSharpViewScripts(
                prefabPath,
                true);
        }

        private static void CollectBindingsAndSave(
            string prefabPath)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ObjectBinder objectBinder =
                    root.GetComponent<ObjectBinder>();
                if (objectBinder == null)
                {
                    throw new System.InvalidOperationException(
                        $"Prefab {prefabPath} 没有 ObjectBinder。");
                }

                CSharpUIGenerator.AutoCollectObjectBindings(
                    objectBinder);
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject ResolvePrefabAsset(
            ObjectBinder binder)
        {
            string path =
                AssetDatabase.GetAssetPath(
                    binder.gameObject);
            if (string.IsNullOrEmpty(path))
            {
                PrefabStage stage =
                    PrefabStageUtility.GetPrefabStage(
                        binder.gameObject);
                path = stage?.assetPath;
                if (!string.IsNullOrEmpty(path))
                {
                    PrefabUtility.SaveAsPrefabAsset(
                        stage.prefabContentsRoot,
                        path);
                }
            }

            if (string.IsNullOrEmpty(path) ||
                !path.EndsWith(
                    ".prefab",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.InvalidOperationException(
                    "请先把当前对象保存为 Prefab，再生成脚本。");
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);
            if (prefab == null)
            {
                throw new System.IO.FileNotFoundException(
                    "无法加载 Prefab。",
                    path);
            }

            return prefab;
        }
    }
}
