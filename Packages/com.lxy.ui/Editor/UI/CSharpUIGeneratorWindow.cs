#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using LuaObjectBind;

namespace LxyDemo.UIFramework.Editor
{
    [Serializable]
    public sealed class CSharpUIGenerationOptions
    {
        /// <summary>
        /// 公开的面板标识数据。
        /// </summary>
        public string panelId = "UIExample";
        /// <summary>
        /// 公开的脚本类型数据。
        /// </summary>
        public UIScriptType scriptType = UIScriptType.CSharp;
        /// <summary>
        /// 公开的codeNamespace数据。
        /// </summary>
        public string codeNamespace = "LxyDemo.GameUI";
        /// <summary>
        /// 公开的logic类型名称数据。
        /// </summary>
        public string logicClassName = "UIExample";
        /// <summary>
        /// 公开的prefabFolder数据。
        /// </summary>
        public string prefabFolder =
            "Assets/GameResources/Prefabs/UIRes";
        /// <summary>
        /// 公开的脚本Folder数据。
        /// </summary>
        public string scriptFolder = "Assets/Scripts/GameUI";
        /// <summary>
        /// 公开的existingPrefab数据。
        /// </summary>
        public GameObject existingPrefab;
        /// <summary>
        /// 公开的autoCollect绑定数据。
        /// </summary>
        public bool autoCollectBindings = true;
        /// <summary>
        /// 公开的ui层级数据。
        /// </summary>
        public UILayer uiLayer = UILayer.Auto;
    }

    public readonly struct CSharpUIGenerationResult
    {
        /// <summary>
        /// 创建CSharpUIGenerationResult实例。
        /// </summary>
        public CSharpUIGenerationResult(
            string prefabPath,
            string mainScriptPath,
            string autoScriptPath,
            bool mainScriptCreated)
        {
            PrefabPath = prefabPath;
            MainScriptPath = mainScriptPath;
            AutoScriptPath = autoScriptPath;
            MainScriptCreated = mainScriptCreated;
        }

        /// <summary>
        /// 向调用方提供Prefab路径。
        /// </summary>
        public string PrefabPath { get; }
        /// <summary>
        /// 向调用方提供MainScript路径。
        /// </summary>
        public string MainScriptPath { get; }
        /// <summary>
        /// 向调用方提供AutoScript路径。
        /// </summary>
        public string AutoScriptPath { get; }
        /// <summary>
        /// 向调用方提供MainScriptCreated。
        /// </summary>
        public bool MainScriptCreated { get; }
    }

    public sealed class CSharpUIGeneratorWindow : EditorWindow
    {
            /// <summary>
            /// 执行CSharpUIGeneration选项相关逻辑。
            /// </summary>
        [SerializeField]
        private CSharpUIGenerationOptions options =
            new CSharpUIGenerationOptions();

        [SerializeField]
        private Vector2 scrollPosition;

        /// <summary>
        /// 执行显示窗口相关逻辑。
        /// </summary>
        [MenuItem(
            "工具/UI工具/创建Prefab",
            priority = 10)]
        public static void ShowWindow()
        {
            CSharpUIGeneratorWindow window =
                GetWindow<CSharpUIGeneratorWindow>(
                    "UI & Prefab Generator");
            window.minSize = new Vector2(560f, 700f);
            window.TryUseSelectedPrefab();
            window.Show();
        }

        /// <summary>
        /// 执行显示窗口从预制体相关逻辑。
        /// </summary>
        [MenuItem(
            "Assets/UI Manager/Generate UI From Prefab",
            false,
            120)]
        private static void ShowWindowFromPrefab()
        {
            CSharpUIGeneratorWindow window =
                GetWindow<CSharpUIGeneratorWindow>(
                    "UI & Prefab Generator");
            window.minSize = new Vector2(560f, 700f);
            window.SetExistingPrefab(
                Selection.activeObject as GameObject);
            window.Show();
        }

        /// <summary>
        /// 校验Show窗口From预制体。
        /// </summary>
        [MenuItem(
            "Assets/UI Manager/Generate UI From Prefab",
            true)]
        private static bool ValidateShowWindowFromPrefab()
        {
            return Selection.activeObject is GameObject selected &&
                   AssetDatabase.GetAssetPath(selected)
                       .EndsWith(
                           ".prefab",
                           StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 在组件启用时建立运行时关联。
        /// </summary>
        private void OnEnable()
        {
            if (options == null)
            {
                options = new CSharpUIGenerationOptions();
            }
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition);

            EditorGUILayout.LabelField(
                "UI + Prefab Generator",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "C# 和 Lua 类型都只创建 Prefab，不立即创建脚本。" +
                "脚本请在 Prefab 的 ObjectBinder 上手动生成。" +
                "新 Prefab 会自动添加 Canvas 和 GraphicRaycaster；" +
                "两种类型都使用 ObjectBinder 保存控件引用和生成参数。",
                MessageType.Info);
            EditorGUILayout.Space();

            DrawSourceSection();
            DrawCodeSection();
            DrawOutputPreview();
            DrawGenerateButton();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制源数据Section。
        /// </summary>
        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField(
                "Prefab",
                EditorStyles.boldLabel);
            GameObject newPrefab =
                (GameObject)EditorGUILayout.ObjectField(
                    "现有 Prefab（可选）",
                    options.existingPrefab,
                    typeof(GameObject),
                    false);
            if (newPrefab != options.existingPrefab)
            {
                SetExistingPrefab(newPrefab);
            }

            options.panelId = EditorGUILayout.TextField(
                "Panel ID / Prefab 名",
                options.panelId);
            EditorGUI.BeginChangeCheck();
            UIScriptType selectedScriptType =
                (UIScriptType)EditorGUILayout.Popup(
                    "脚本类型",
                    (int)options.scriptType,
                    new[] { "C# 脚本", "Lua 脚本" });
            if (EditorGUI.EndChangeCheck())
            {
                SwitchScriptType(selectedScriptType);
            }
            using (new EditorGUI.DisabledScope(
                       options.existingPrefab != null))
            {
                options.prefabFolder = DrawFolderField(
                    "Prefab 路径",
                    options.prefabFolder);
            }

            if (options.scriptType == UIScriptType.CSharp)
            {
                options.autoCollectBindings =
                    EditorGUILayout.Toggle(
                        "按节点前缀收集绑定",
                        options.autoCollectBindings);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Lua 类型只创建 Prefab，并在根节点挂载 " +
                    "ObjectBinder；不会自动创建 Lua 脚本。",
                    MessageType.Info);
            }
            if (options.scriptType == UIScriptType.CSharp)
            {
                options.uiLayer =
                    (UILayer)EditorGUILayout.EnumPopup(
                        "UI 层级",
                        options.uiLayer);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Lua 面板的资源路径、Logic、层级和关闭策略统一在 " +
                    "Framework.UI.UIDefine.PanelConfig 中配置。",
                    MessageType.None);
            }
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 绘制代码Section。
        /// </summary>
        private void DrawCodeSection()
        {
            EditorGUILayout.LabelField(
                options.scriptType == UIScriptType.Lua
                    ? "Lua Binding"
                    : "C# Script Defaults",
                EditorStyles.boldLabel);
            if (options.scriptType == UIScriptType.Lua)
            {
                EditorGUILayout.HelpBox(
                    "创建完成后，请在 Prefab 根节点的 ObjectBinder " +
                    "中配置 Lua 文件和控件绑定。",
                    MessageType.None);
            }
            else
            {
                options.logicClassName =
                    EditorGUILayout.TextField(
                        "View Class",
                        options.logicClassName);
                options.codeNamespace = EditorGUILayout.TextField(
                    "Namespace",
                    options.codeNamespace);
                options.scriptFolder = DrawFolderField(
                    "Output 路径",
                    options.scriptFolder);
            }
            EditorGUILayout.Space();
        }

        /// <summary>
        /// 执行切换脚本类型相关逻辑。
        /// </summary>
        private void SwitchScriptType(UIScriptType scriptType)
        {
            if (options.scriptType == scriptType)
            {
                return;
            }

            options.scriptType = scriptType;
            string panelClass =
                CSharpUIGenerator.SanitizeTypeName(options.panelId);
            if (scriptType == UIScriptType.Lua)
            {
                options.logicClassName = string.Empty;
            }
            else
            {
                options.logicClassName = panelClass;
            }
        }

        /// <summary>
        /// 绘制目录Field。
        /// </summary>
        private static string DrawFolderField(
            string label,
            string currentPath)
        {
            string result = currentPath;
            DefaultAsset currentFolder =
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(
                    currentPath);
            EditorGUI.BeginChangeCheck();
            DefaultAsset selectedFolder =
                (DefaultAsset)EditorGUILayout.ObjectField(
                    label,
                    currentFolder,
                    typeof(DefaultAsset),
                    false);
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedFolder == null)
                {
                    result = string.Empty;
                }
                else
                {
                    string selectedPath =
                        AssetDatabase.GetAssetPath(selectedFolder);
                    if (AssetDatabase.IsValidFolder(selectedPath))
                    {
                        result = selectedPath;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "请选择文件夹",
                            $"{label} 只接受 Project 窗口中的文件夹。",
                            "确定");
                    }

                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "当前路径",
                    result);
            }

            return result;
        }

        /// <summary>
        /// 绘制输出预览。
        /// </summary>
        private void DrawOutputPreview()
        {
            EditorGUILayout.LabelField(
                "Prefab / 手动脚本预览",
                EditorStyles.boldLabel);

            string prefabPath =
                CSharpUIGenerator.GetPrefabPath(options);
            string mainPath =
                CSharpUIGenerator.GetMainScriptPath(options);
            string autoPath =
                CSharpUIGenerator.GetAutoScriptPath(options);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    "Prefab",
                    prefabPath);
                if (options.scriptType == UIScriptType.CSharp)
                {
                    EditorGUILayout.TextField(
                        "C# View",
                        mainPath);
                    EditorGUILayout.TextField(
                        "Generated Base",
                        autoPath);
                }
            }

            if (options.scriptType == UIScriptType.CSharp &&
                File.Exists(
                    CSharpUIGenerator.ToAbsolutePath(mainPath)))
            {
                EditorGUILayout.HelpBox(
                    "业务主文件已存在，将保留不覆盖。",
                    MessageType.Info);
            }

            if (options.scriptType == UIScriptType.CSharp &&
                File.Exists(
                    CSharpUIGenerator.ToAbsolutePath(autoPath)))
            {
                EditorGUILayout.HelpBox(
                    "Base 文件已存在，点击 ObjectBinder 的生成按钮时会覆盖。",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// 绘制生成按钮。
        /// </summary>
        private void DrawGenerateButton()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button(
                    options.scriptType == UIScriptType.Lua
                        ? "创建 Lua Prefab"
                        : "创建 C# Prefab",
                    GUILayout.Height(38f)))
            {
                try
                {
                    CSharpUIGenerationResult result =
                        CSharpUIGenerator.Generate(
                            options,
                            true);
                    Selection.activeObject =
                        AssetDatabase.LoadAssetAtPath<GameObject>(
                            result.PrefabPath);
                    EditorGUIUtility.PingObject(
                        Selection.activeObject);
                    EditorUtility.DisplayDialog(
                        options.scriptType == UIScriptType.Lua
                            ? "Lua Prefab 创建完成"
                            : "C# Prefab 创建完成",
                        options.scriptType == UIScriptType.Lua
                            ? $"Prefab: {result.PrefabPath}\n" +
                              "已挂载 ObjectBinder，未创建 Lua 脚本。\n" +
                              "请在 ObjectBinder 上点击生成 Lua 脚本。"
                            : $"Prefab: {result.PrefabPath}\n" +
                              "已挂载 ObjectBinder，未创建 C# 脚本。\n" +
                              "请在 ObjectBinder 上点击生成 C# 脚本。",
                        "确定");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog(
                        "生成失败",
                        exception.Message,
                        "确定");
                }
            }
        }

        /// <summary>
        /// 尝试Use选中项预制体，并返回是否成功。
        /// </summary>
        private void TryUseSelectedPrefab()
        {
            if (Selection.activeObject is GameObject selected)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (path.EndsWith(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    SetExistingPrefab(selected);
                }
            }
        }

        /// <summary>
        /// 设置Existing预制体。
        /// </summary>
        private void SetExistingPrefab(GameObject prefab)
        {
            options.existingPrefab = prefab;
            if (prefab == null)
            {
                return;
            }

            options.panelId = prefab.name;
            string prefabPath =
                AssetDatabase.GetAssetPath(prefab);
            options.prefabFolder =
                Path.GetDirectoryName(prefabPath)
                    ?.Replace('\\', '/');
            UICodeBinder binder =
                prefab.GetComponent<UICodeBinder>();
            ObjectBinder objectBinder =
                prefab.GetComponent<ObjectBinder>();
            options.scriptType = binder != null
                ? binder.ScriptType
                : objectBinder != null
                    ? objectBinder.uiScriptGeneration.scriptType ==
                      UIObjectBinderScriptType.CSharp
                        ? UIScriptType.CSharp
                        : UIScriptType.Lua
                    : UIScriptType.CSharp;
            options.logicClassName =
                !string.IsNullOrWhiteSpace(binder?.LogicClassName)
                    ? binder.LogicClassName
                    : !string.IsNullOrWhiteSpace(
                        objectBinder?.uiScriptGeneration
                            .csharpClassName)
                        ? objectBinder.uiScriptGeneration
                            .csharpClassName
                    : CSharpUIGenerator.SanitizeTypeName(prefab.name) +
                      string.Empty;
            if (!string.IsNullOrWhiteSpace(
                    objectBinder?.uiScriptGeneration
                        .csharpNamespace))
            {
                options.codeNamespace =
                    objectBinder.uiScriptGeneration
                        .csharpNamespace;
            }
            if (!string.IsNullOrWhiteSpace(
                    objectBinder?.uiScriptGeneration
                        .csharpOutputFolder))
            {
                options.scriptFolder =
                    objectBinder.uiScriptGeneration
                        .csharpOutputFolder;
            }
            if (options.scriptType == UIScriptType.Lua)
            {
                options.logicClassName = string.Empty;
            }
            options.uiLayer = binder != null
                ? binder.Layer
                : objectBinder != null
                    ? (UILayer)(int)objectBinder
                        .uiScriptGeneration.uiLayer
                    : UILayer.Auto;
        }
    }

    public static class CSharpUIGenerator
    {
        private const string MainTemplatePath =
            "Packages/com.lxy.ui/Editor/UI/Templates/" +
            "CSharpUILogicTemplate.txt";
        private const string AutoTemplatePath =
            "Packages/com.lxy.ui/Editor/UI/Templates/" +
            "CSharpUILogicAutoTemplate.txt";
        private const string ViewTemplatePath =
            "Packages/com.lxy.ui/Editor/UI/Templates/" +
            "CSharpUIViewTemplate.txt";
        private const string ViewBaseTemplatePath =
            "Packages/com.lxy.ui/Editor/UI/Templates/" +
            "CSharpUIViewBaseTemplate.txt";

        private enum GeneratedEventKind
        {
            None,
            Click,
            BoolValue,
            FloatValue,
            StringValue,
            IntValue
        }

        private enum BindingSource
        {
            Object,
            LogicObject
        }

        private sealed class BindingCode
        {
            /// <summary>
            /// 公开的字段名称数据。
            /// </summary>
            public string FieldName;
            /// <summary>
            /// 公开的字段类型数据。
            /// </summary>
            public string FieldType;
            /// <summary>
            /// 公开的绑定名称数据。
            /// </summary>
            public string BindingName;
            /// <summary>
            /// 公开的Required数据。
            /// </summary>
            public bool Required;
            /// <summary>
            /// 公开的源数据数据。
            /// </summary>
            public BindingSource Source;
            /// <summary>
            /// 公开的事件Kind数据。
            /// </summary>
            public GeneratedEventKind EventKind;
            /// <summary>
            /// 公开的Handler名称数据。
            /// </summary>
            public string HandlerName;
            /// <summary>
            /// 公开的Callback名称数据。
            /// </summary>
            public string CallbackName;
        }

        private static readonly HashSet<string> CSharpKeywords =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "abstract", "as", "base", "bool", "break",
                "byte", "case", "catch", "char", "checked",
                "class", "const", "continue", "decimal",
                "default", "delegate", "do", "double", "else",
                "enum", "event", "explicit", "extern", "false",
                "finally", "fixed", "float", "for", "foreach",
                "goto", "if", "implicit", "in", "int",
                "interface", "internal", "is", "lock", "long",
                "namespace", "new", "null", "object",
                "operator", "out", "override", "params",
                "private", "protected", "public", "readonly",
                "ref", "return", "sbyte", "sealed", "short",
                "sizeof", "stackalloc", "static", "string",
                "struct", "switch", "this", "throw", "true",
                "try", "typeof", "uint", "ulong", "unchecked",
                "unsafe", "ushort", "using", "virtual", "void",
                "volatile", "while"
            };

        /// <summary>
        /// 执行Generate相关逻辑。
        /// </summary>
        public static CSharpUIGenerationResult Generate(
            CSharpUIGenerationOptions options,
            bool promptForOverwrite)
        {
            ValidateAndNormalizeOptions(options);

            string prefabPath = GetPrefabPath(options);
            string mainScriptPath = GetMainScriptPath(options);
            string autoScriptPath = GetAutoScriptPath(options);

            EnsureAssetFolder(
                Path.GetDirectoryName(prefabPath)
                    ?.Replace('\\', '/'));

            bool useExistingAsset =
                options.existingPrefab != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath) != null;

            if (useExistingAsset &&
                options.existingPrefab == null &&
                promptForOverwrite &&
                !EditorUtility.DisplayDialog(
                    "Prefab 已存在",
                    $"{prefabPath} 已存在。\n" +
                    "不会重建 Prefab，将在现有资源上配置绑定器，是否继续？",
                    "使用现有 Prefab",
                    "取消"))
            {
                throw new OperationCanceledException();
            }

            if (!useExistingAsset)
            {
                CreateNewPrefab(prefabPath, options);
            }
            else
            {
                PrepareExistingPrefab(prefabPath, options);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[UI Generator/{options.scriptType}] 生成完成\n" +
                $"Prefab: {prefabPath}\n" +
                "ObjectBinder: 已挂载\n" +
                "脚本: 未生成，请在 ObjectBinder Inspector 手动生成");

            return new CSharpUIGenerationResult(
                prefabPath,
                mainScriptPath,
                autoScriptPath,
                false);
        }

        public static CSharpUIGenerationResult
            GenerateCSharpViewScripts(
                string prefabPath,
                bool promptForOverwrite)
        {
            prefabPath = NormalizeAssetPath(prefabPath);
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
            if (prefab == null)
            {
                throw new FileNotFoundException(
                    "Prefab 不存在。",
                    prefabPath);
            }

            ObjectBinder objectBinder =
                prefab.GetComponent<ObjectBinder>();
            if (objectBinder == null)
            {
                throw new InvalidOperationException(
                    $"Prefab {prefabPath} 没有 ObjectBinder。");
            }

            objectBinder.uiScriptGeneration ??=
                new UIScriptGenerationSettings();
            UIScriptGenerationSettings settings =
                objectBinder.uiScriptGeneration;
            settings.scriptType =
                UIObjectBinderScriptType.CSharp;

            var options = new CSharpUIGenerationOptions
            {
                panelId =
                    string.IsNullOrWhiteSpace(settings.panelId)
                        ? prefab.name
                        : settings.panelId,
                scriptType = UIScriptType.CSharp,
                codeNamespace = settings.csharpNamespace,
                logicClassName =
                    string.IsNullOrWhiteSpace(
                        settings.csharpClassName)
                        ? SanitizeTypeName(prefab.name)
                        : settings.csharpClassName,
                scriptFolder = settings.csharpOutputFolder,
                prefabFolder =
                    Path.GetDirectoryName(prefabPath)
                        ?.Replace('\\', '/'),
                existingPrefab = prefab,
                uiLayer =
                    (UILayer)(int)settings.uiLayer,
            };
            ValidateAndNormalizeOptions(options);

            settings.panelId = options.panelId;
            settings.csharpClassName =
                options.logicClassName;
            settings.csharpNamespace =
                options.codeNamespace;
            settings.csharpOutputFolder =
                options.scriptFolder;
            EditorUtility.SetDirty(objectBinder);

            string mainScriptPath =
                GetMainScriptPath(options);
            string baseScriptPath =
                GetAutoScriptPath(options);
            if (File.Exists(ToAbsolutePath(baseScriptPath)) &&
                promptForOverwrite &&
                !EditorUtility.DisplayDialog(
                    "覆盖 Base 文件",
                    $"{baseScriptPath} 已存在。\n" +
                    "业务 View 文件不会覆盖，是否重新生成 Base？",
                    "重新生成",
                    "取消"))
            {
                throw new OperationCanceledException();
            }

            EnsureAssetFolder(options.scriptFolder);
            List<BindingCode> bindings =
                CollectBindingCode(prefabPath);
            string mainContent =
                BuildViewMainContent(
                    options,
                    prefabPath,
                    baseScriptPath,
                    bindings);
            string baseContent =
                BuildViewBaseContent(
                    options,
                    prefabPath,
                    bindings);

            bool mainExists =
                File.Exists(ToAbsolutePath(mainScriptPath));
            if (!mainExists)
            {
                WriteTextAsset(mainScriptPath, mainContent);
            }
            WriteTextAsset(baseScriptPath, baseContent);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[UI Generator/CSharp] 手动生成完成\n" +
                $"View: {mainScriptPath}\n" +
                $"Base: {baseScriptPath}");
            return new CSharpUIGenerationResult(
                prefabPath,
                mainScriptPath,
                baseScriptPath,
                !mainExists);
        }

        /// <summary>
        /// 执行RegenerateAuto代码相关逻辑。
        /// </summary>
        public static void RegenerateAutoCode(
            string prefabPath,
            bool autoCollectBindings)
        {
            prefabPath = NormalizeAssetPath(prefabPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath) == null)
            {
                throw new FileNotFoundException(
                    "Prefab 不存在。",
                    prefabPath);
            }

            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);
            string mainPath;
            string autoPath;
            string panelId;
            string codeNamespace;
            string logicClassName;
            UIScriptType scriptType;
            try
            {
                UICodeBinder binder =
                    root.GetComponent<UICodeBinder>();
                if (binder == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab {prefabPath} 没有 UICodeBinder。");
                }

                if (binder.ScriptType == UIScriptType.Lua)
                {
                    throw new InvalidOperationException(
                        "Lua 类型不生成 Auto 脚本，请使用 " +
                        "ObjectBinder 配置 Lua 文件与绑定。");
                }

                ObjectBinder objectBinder =
                    EnsureObjectBinder(root);
                SynchronizeObjectBinderBindings(
                    binder,
                    objectBinder);
                if (autoCollectBindings)
                {
                    AutoCollectBindings(binder);
                }
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);

                mainPath = binder.MainScriptPath;
                autoPath = binder.AutoScriptPath;
                panelId = binder.PanelId;
                codeNamespace = binder.CodeNamespace;
                logicClassName = binder.LogicClassName;
                scriptType = binder.ScriptType;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var options = new CSharpUIGenerationOptions
            {
                panelId = panelId,
                codeNamespace = codeNamespace,
                logicClassName = logicClassName,
                scriptType = scriptType,
                scriptFolder = Path.GetDirectoryName(mainPath)
                    ?.Replace('\\', '/')
            };

            GenerateScriptsFromPrefab(
                prefabPath,
                mainPath,
                autoPath,
                options,
                true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[UI Generator/{scriptType}] Auto 代码已更新：" +
                autoPath);
        }

        /// <summary>
        /// 执行Auto收集Bindings相关逻辑。
        /// </summary>
        public static int AutoCollectBindings(
            UICodeBinder binder)
        {
            if (binder == null)
            {
                return 0;
            }

            ObjectBinder objectBinder =
                EnsureObjectBinder(binder.gameObject);
            SynchronizeObjectBinderBindings(
                binder,
                objectBinder);

            List<UICodeBinding> bindings = binder.Bindings;
            List<BindValue> objectBindings =
                objectBinder.bindValues;
            var usedFields = new HashSet<string>(
                objectBindings
                    .Where(item =>
                        item != null &&
                        !string.IsNullOrWhiteSpace(item.Name))
                    .Select(item => item.Name),
                StringComparer.Ordinal);
            var usedTargets =
                new HashSet<UnityEngine.Object>(
                    objectBindings
                        .Where(item =>
                            item?.GetObjectValue != null)
                        .Select(item =>
                            item.GetObjectValue));

            int addedCount = 0;
            Transform[] transforms =
                binder.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform == binder.transform)
                {
                    continue;
                }

                if (!TryResolveConventionTarget(
                        transform.gameObject,
                        out UnityEngine.Object target,
                        out string fieldName,
                        out bool bindEvent) ||
                    target == null ||
                    usedTargets.Contains(target))
                {
                    continue;
                }

                fieldName = MakeUniqueFieldName(
                    fieldName,
                    usedFields);
                var objectBinding = new BindValue
                {
                    Name = fieldName
                };
                objectBinding.SetValue(target);
                objectBindings.Add(objectBinding);
                bindings.Add(new UICodeBinding
                {
                    fieldName = fieldName,
                    target = target,
                    required = true,
                    bindDefaultEvent = bindEvent
                });
                usedFields.Add(fieldName);
                usedTargets.Add(target);
                addedCount++;
            }

            EditorUtility.SetDirty(binder);
            EditorUtility.SetDirty(objectBinder);
            return addedCount;
        }

        /// <summary>
        /// 获取预制体路径。
        /// </summary>
        public static string GetPrefabPath(
            CSharpUIGenerationOptions options)
        {
            if (options?.existingPrefab != null)
            {
                return NormalizeAssetPath(
                    AssetDatabase.GetAssetPath(
                        options.existingPrefab));
            }

            return CombineAssetPath(
                options?.prefabFolder,
                (options?.panelId ?? string.Empty) + ".prefab");
        }

        /// <summary>
        /// 获取主入口Script路径。
        /// </summary>
        public static string GetMainScriptPath(
            CSharpUIGenerationOptions options)
        {
            if (options?.scriptType == UIScriptType.Lua)
            {
                return string.Empty;
            }

            return CombineAssetPath(
                options?.scriptFolder,
                (options?.logicClassName ?? string.Empty) + ".cs");
        }

        /// <summary>
        /// 获取AutoScript路径。
        /// </summary>
        public static string GetAutoScriptPath(
            CSharpUIGenerationOptions options)
        {
            if (options?.scriptType == UIScriptType.Lua)
            {
                return string.Empty;
            }

            return CombineAssetPath(
                options?.scriptFolder,
                (options?.logicClassName ?? string.Empty) +
                "Base.cs");
        }

        /// <summary>
        /// 执行转换为Absolute路径相关逻辑。
        /// </summary>
        public static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                NormalizeAssetPath(assetPath)));
        }

        /// <summary>
        /// 执行Sanitize类型名称相关逻辑。
        /// </summary>
        public static string SanitizeTypeName(string value)
        {
            string identifier = SanitizeIdentifier(value, true);
            return identifier.Length == 0
                ? "UIExample"
                : identifier;
        }

        /// <summary>
        /// 创建New预制体。
        /// </summary>
        private static void CreateNewPrefab(
            string prefabPath,
            CSharpUIGenerationOptions options)
        {
            var root = new GameObject(
                options.panelId,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            try
            {
                UILayerRoot.SetUILayerRecursively(root);
                RectTransform rootRect =
                    root.GetComponent<RectTransform>();
                UILayerRoot.Stretch(rootRect);
                rootRect.sizeDelta = Vector2.zero;

                ObjectBinder objectBinder =
                    EnsureObjectBinder(root);
                ConfigureObjectBinder(
                    objectBinder,
                    options);
                EnsureRootObjectBinding(objectBinder);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 准备Existing预制体。
        /// </summary>
        private static void PrepareExistingPrefab(
            string prefabPath,
            CSharpUIGenerationOptions options)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                UILayerRoot.SetUILayerRecursively(root);
                ObjectBinder objectBinder =
                    EnsureObjectBinder(root);
                UICodeBinder binder =
                    root.GetComponent<UICodeBinder>();
                if (binder != null)
                {
                    EnsureRootBinding(binder);
                    SynchronizeObjectBinderBindings(
                        binder,
                        objectBinder);
                }

                ConfigureObjectBinder(
                    objectBinder,
                    options);
                EnsureRootObjectBinding(objectBinder);
                if (options.scriptType == UIScriptType.CSharp &&
                    options.autoCollectBindings)
                {
                    AutoCollectObjectBindings(objectBinder);
                }

                if (binder != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        binder,
                        true);
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 执行配置绑定器相关逻辑。
        /// </summary>
        private static void ConfigureBinder(
            UICodeBinder binder,
            CSharpUIGenerationOptions options)
        {
            binder.ConfigureGenerator(
                options.panelId,
                options.codeNamespace,
                options.logicClassName,
                GetMainScriptPath(options),
                GetAutoScriptPath(options),
                options.uiLayer,
                options.scriptType,
                string.Empty);
            EditorUtility.SetDirty(binder);
        }

        /// <summary>
        /// 确保对象Binder。
        /// </summary>
        private static ObjectBinder EnsureObjectBinder(
            GameObject root)
        {
            ObjectBinder objectBinder =
                root.GetComponent<ObjectBinder>();
            if (objectBinder != null)
            {
                return objectBinder;
            }

            objectBinder = root.AddComponent<ObjectBinder>();
            EditorUtility.SetDirty(objectBinder);
            return objectBinder;
        }

        /// <summary>
        /// 执行配置对象绑定器相关逻辑。
        /// </summary>
        private static void ConfigureObjectBinder(
            ObjectBinder objectBinder,
            CSharpUIGenerationOptions options)
        {
            objectBinder.uiScriptGeneration ??=
                new UIScriptGenerationSettings();
            UIScriptGenerationSettings settings =
                objectBinder.uiScriptGeneration;
            settings.scriptType =
                options.scriptType == UIScriptType.CSharp
                    ? UIObjectBinderScriptType.CSharp
                    : UIObjectBinderScriptType.Lua;
            settings.panelId = options.panelId;
            settings.csharpClassName =
                string.IsNullOrWhiteSpace(options.logicClassName)
                    ? SanitizeTypeName(options.panelId)
                    : options.logicClassName;
            settings.csharpNamespace =
                string.IsNullOrWhiteSpace(options.codeNamespace)
                    ? "LxyDemo.GameUI"
                    : options.codeNamespace;
            settings.csharpOutputFolder =
                string.IsNullOrWhiteSpace(options.scriptFolder)
                    ? "Assets/Scripts/GameUI"
                    : options.scriptFolder;
            settings.luaModuleName =
                string.IsNullOrWhiteSpace(settings.luaModuleName)
                    ? "UI." + SanitizeTypeName(options.panelId)
                    : settings.luaModuleName;
            settings.luaOutputFolder =
                string.IsNullOrWhiteSpace(settings.luaOutputFolder)
                    ? "Lua/UI"
                    : settings.luaOutputFolder;
            settings.uiLayer =
                (UIObjectBinderLayer)(int)options.uiLayer;
            EditorUtility.SetDirty(objectBinder);
        }

        /// <summary>
        /// 确保根节点对象绑定。
        /// </summary>
        private static void EnsureRootObjectBinding(
            ObjectBinder objectBinder)
        {
            objectBinder.bindValues ??=
                new BindValueCollection();
            List<BindValue> bindings =
                objectBinder.bindValues;
            UnityEngine.Object rootTarget =
                objectBinder.transform is RectTransform rect
                    ? rect
                    : objectBinder.transform;
            BindValue rootBinding = bindings.FirstOrDefault(item =>
                item != null &&
                string.Equals(
                    item.Name,
                    "root",
                    StringComparison.Ordinal));
            if (rootBinding == null)
            {
                rootBinding = new BindValue
                {
                    Name = "root"
                };
                bindings.Insert(0, rootBinding);
            }

            rootBinding.SetValue(rootTarget);
            EditorUtility.SetDirty(objectBinder);
        }

        /// <summary>
        /// 执行Auto收集对象Bindings相关逻辑。
        /// </summary>
        public static int AutoCollectObjectBindings(
            ObjectBinder objectBinder)
        {
            if (objectBinder == null)
            {
                return 0;
            }

            EnsureRootObjectBinding(objectBinder);
            List<BindValue> bindings =
                objectBinder.bindValues;
            var usedFields = new HashSet<string>(
                bindings
                    .Where(item =>
                        item != null &&
                        !string.IsNullOrWhiteSpace(item.Name))
                    .Select(item => item.Name),
                StringComparer.Ordinal);
            var usedTargets =
                new HashSet<UnityEngine.Object>(
                    bindings
                        .Where(item =>
                            item?.GetObjectValue != null)
                        .Select(item =>
                            item.GetObjectValue));

            int addedCount = 0;
            Transform[] transforms =
                objectBinder.GetComponentsInChildren<Transform>(
                    true);
            foreach (Transform transform in transforms)
            {
                if (transform == objectBinder.transform)
                {
                    continue;
                }

                if (!TryResolveConventionTarget(
                        transform.gameObject,
                        out UnityEngine.Object target,
                        out string fieldName,
                        out _) ||
                    target == null ||
                    usedTargets.Contains(target))
                {
                    continue;
                }

                fieldName = MakeUniqueFieldName(
                    fieldName,
                    usedFields);
                var binding = new BindValue
                {
                    Name = fieldName
                };
                binding.SetValue(target);
                bindings.Add(binding);
                usedFields.Add(fieldName);
                usedTargets.Add(target);
                addedCount++;
            }

            EditorUtility.SetDirty(objectBinder);
            return addedCount;
        }

        /// <summary>
        /// 执行同步对象绑定器Bindings相关逻辑。
        /// </summary>
        private static void SynchronizeObjectBinderBindings(
            UICodeBinder codeBinder,
            ObjectBinder objectBinder)
        {
            if (codeBinder == null || objectBinder == null)
            {
                return;
            }

            objectBinder.bindValues ??=
                new BindValueCollection();
            List<BindValue> objectBindings =
                objectBinder.bindValues;

            foreach (UICodeBinding codeBinding in
                     codeBinder.Bindings)
            {
                if (codeBinding?.target == null)
                {
                    continue;
                }

                string bindingName = SanitizeIdentifier(
                    codeBinding.fieldName,
                    false);
                if (bindingName.Length == 0)
                {
                    continue;
                }

                BindValue objectBinding =
                    objectBindings.FirstOrDefault(item =>
                        item != null &&
                        string.Equals(
                            item.Name,
                            bindingName,
                            StringComparison.Ordinal));
                if (objectBinding == null)
                {
                    objectBinding = new BindValue
                    {
                        Name = bindingName
                    };
                    objectBindings.Add(objectBinding);
                }

                objectBinding.SetValue(codeBinding.target);
            }

            EditorUtility.SetDirty(objectBinder);
        }

        /// <summary>
        /// 确保根节点绑定。
        /// </summary>
        private static void EnsureRootBinding(UICodeBinder binder)
        {
            bool hasRoot = binder.Bindings.Any(
                item => item != null &&
                        item.target == binder.transform);
            bool hasRootRect = binder.Bindings.Any(
                item => item != null &&
                        item.target ==
                        (UnityEngine.Object)
                        (binder.transform as RectTransform));

            if (!hasRoot && !hasRootRect)
            {
                UnityEngine.Object target =
                    binder.transform is RectTransform rect
                        ? rect
                        : binder.transform;
                binder.Bindings.Insert(0, new UICodeBinding
                {
                    fieldName = "root",
                    target = target,
                    required = true
                });
            }
        }

        /// <summary>
        /// 执行Generate脚本从预制体相关逻辑。
        /// </summary>
        private static bool GenerateScriptsFromPrefab(
            string prefabPath,
            string mainScriptPath,
            string autoScriptPath,
            CSharpUIGenerationOptions options,
            bool createMainWhenMissing)
        {
            List<BindingCode> bindings =
                CollectBindingCode(prefabPath);
            string mainContent = BuildMainContent(
                options,
                prefabPath,
                autoScriptPath,
                bindings);
            string autoContent = BuildAutoContent(
                options,
                prefabPath,
                bindings);

            bool mainExists =
                File.Exists(ToAbsolutePath(mainScriptPath));
            if (!mainExists && createMainWhenMissing)
            {
                WriteTextAsset(mainScriptPath, mainContent);
            }

            WriteTextAsset(autoScriptPath, autoContent);
            return !mainExists && createMainWhenMissing;
        }

        /// <summary>
        /// 执行收集绑定代码相关逻辑。
        /// </summary>
        private static List<BindingCode> CollectBindingCode(
            string prefabPath)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                UICodeBinder binder =
                    root.GetComponent<UICodeBinder>();
                ObjectBinder objectBinder =
                    root.GetComponent<ObjectBinder>();
                if (objectBinder == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab {prefabPath} 没有 ObjectBinder。");
                }

                var result = new List<BindingCode>();
                var fields =
                    new HashSet<string>(
                        new[] { "uiObjectBinder" },
                        StringComparer.Ordinal);
                IEnumerable<BindValue> objectBindings =
                    objectBinder.bindValues?.Binds ??
                    Enumerable.Empty<BindValue>();
                foreach (BindValue objectBinding in objectBindings)
                {
                    if (objectBinding == null ||
                        objectBinding.GetObjectValue == null)
                    {
                        continue;
                    }

                    string bindingName =
                        objectBinding.Name?.Trim() ??
                        string.Empty;
                    string fieldName =
                        SanitizeIdentifier(
                            bindingName,
                            false);
                    if (fieldName.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "ObjectBinder 中存在空绑定名。");
                    }

                    if (!fields.Add(fieldName))
                    {
                        throw new InvalidOperationException(
                            $"ObjectBinder 绑定名重复或占用保留名：" +
                            fieldName);
                    }

                    GameObject targetObject;
                    Type fieldType;
                    bool isGameObject =
                        objectBinding.GetObjectValue is GameObject;
                    if (isGameObject)
                    {
                        targetObject =
                            (GameObject)objectBinding.GetObjectValue;
                        fieldType = typeof(GameObject);
                    }
                    else if (
                        objectBinding.GetObjectValue is
                        Component component)
                    {
                        targetObject = component.gameObject;
                        fieldType = component.GetType();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"字段 {fieldName} 只能绑定 GameObject 或 Component。");
                    }

                    if (targetObject != root &&
                        !targetObject.transform.IsChildOf(
                            root.transform))
                    {
                        throw new InvalidOperationException(
                            $"字段 {fieldName} 的目标不在 Prefab 内部。");
                    }

                    UICodeBinding metadata =
                        binder?.Bindings.FirstOrDefault(item =>
                            item != null &&
                            string.Equals(
                                SanitizeIdentifier(
                                    item.fieldName,
                                    false),
                                fieldName,
                                StringComparison.Ordinal));
                    bool bindDefaultEvent =
                        metadata != null
                            ? metadata.bindDefaultEvent
                            : ResolveEventKind(fieldType) !=
                              GeneratedEventKind.None;
                    GeneratedEventKind eventKind =
                        bindDefaultEvent
                            ? ResolveEventKind(fieldType)
                            : GeneratedEventKind.None;

                    result.Add(new BindingCode
                    {
                        FieldName = fieldName,
                        FieldType = GetCSharpTypeName(fieldType),
                        BindingName = bindingName,
                        Required = metadata?.required ?? true,
                        Source = BindingSource.Object,
                        EventKind = eventKind,
                        HandlerName =
                            "Handle" +
                            SanitizeIdentifier(fieldName, true) +
                            GetEventMethodSuffix(eventKind),
                        CallbackName =
                            "On" +
                            SanitizeIdentifier(fieldName, true) +
                            GetEventMethodSuffix(eventKind)
                    });
                }

                IEnumerable<BinderElement> logicBindings =
                    objectBinder.binderElements?.Binds ??
                    Enumerable.Empty<BinderElement>();
                foreach (BinderElement logicBinding in logicBindings)
                {
                    if (logicBinding == null ||
                        logicBinding.Value == null)
                    {
                        continue;
                    }

                    string bindingName =
                        logicBinding.Name?.Trim() ??
                        string.Empty;
                    string fieldName =
                        SanitizeIdentifier(
                            bindingName,
                            false);
                    if (fieldName.Length == 0)
                    {
                        throw new InvalidOperationException(
                            "ObjectBinder 的 Logic 对象中存在空绑定名。");
                    }

                    if (!fields.Add(fieldName))
                    {
                        throw new InvalidOperationException(
                            "ObjectBinder 的普通绑定与 Logic 对象" +
                            $"绑定名重复或占用保留名：{fieldName}");
                    }

                    GameObject targetObject =
                        logicBinding.Value.gameObject;
                    if (targetObject != root &&
                        !targetObject.transform.IsChildOf(
                            root.transform))
                    {
                        throw new InvalidOperationException(
                            $"Logic 对象 {fieldName} 的目标不在 Prefab 内部。");
                    }

                    result.Add(new BindingCode
                    {
                        FieldName = fieldName,
                        FieldType = GetCSharpLogicTypeName(
                            logicBinding),
                        BindingName = bindingName,
                        Required = true,
                        Source = BindingSource.LogicObject,
                        EventKind = GeneratedEventKind.None
                    });
                }

                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 获取CSharpLogic类型名称。
        /// </summary>
        private static string GetCSharpLogicTypeName(
            BinderElement logicBinding)
        {
            UIScriptGenerationSettings settings =
                logicBinding.Value.uiScriptGeneration;
            if (settings == null ||
                settings.scriptType !=
                UIObjectBinderScriptType.CSharp)
            {
                throw new InvalidOperationException(
                    $"Logic 对象 {logicBinding.Name} 未配置为 C# 类型。");
            }

            string codeNamespace =
                settings.csharpNamespace?.Trim() ??
                string.Empty;
            string className =
                settings.csharpClassName?.Trim() ??
                string.Empty;
            if (!IsValidNamespace(codeNamespace) ||
                !IsValidIdentifier(className))
            {
                throw new InvalidOperationException(
                    $"Logic 对象 {logicBinding.Name} 的 C# " +
                    "Namespace 或 Class Name 无效。");
            }

            string fullName = codeNamespace + "." + className;
            Type logicType = FindType(fullName);
            if (logicType == null)
            {
                throw new InvalidOperationException(
                    $"无法解析 Logic 对象 {logicBinding.Name} 的类型 " +
                    $"{fullName}，请先生成并编译子 Logic 脚本。");
            }

            if (!typeof(UIPanelLogic).IsAssignableFrom(logicType) ||
                logicType.IsAbstract ||
                logicType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException(
                    $"Logic 对象 {logicBinding.Name} 的类型 {fullName} " +
                    "必须是可实例化且具有无参构造函数的 UIPanelLogic。");
            }

            return "global::" + fullName;
        }

        /// <summary>
        /// 构建视图主入口内容。
        /// </summary>
        private static string BuildViewMainContent(
            CSharpUIGenerationOptions options,
            string prefabPath,
            string baseScriptPath,
            List<BindingCode> bindings)
        {
            string template = ReadTemplate(ViewTemplatePath);
            return template
                .Replace("$USERNAME$", Environment.UserName)
                .Replace(
                    "$DATETIME$",
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$PANEL_ID$", options.panelId)
                .Replace("$PREFAB_PATH$", prefabPath)
                .Replace("$BASE_SCRIPT_PATH$", baseScriptPath)
                .Replace("$NAMESPACE$", options.codeNamespace)
                .Replace("$CLASS_NAME$", options.logicClassName)
                .Replace(
                    "$BASE_CLASS_NAME$",
                    options.logicClassName + "Base")
                .Replace(
                    "$EVENT_HANDLER_STUBS$",
                    BuildViewEventHandlerStubs(bindings));
        }

        /// <summary>
        /// 构建视图Base内容。
        /// </summary>
        private static string BuildViewBaseContent(
            CSharpUIGenerationOptions options,
            string prefabPath,
            List<BindingCode> bindings)
        {
            var fields = new StringBuilder();
            var assignments = new StringBuilder();
            var subscriptions = new StringBuilder();
            var unsubscriptions = new StringBuilder();
            var releases = new StringBuilder();
            var wrappers = new StringBuilder();
            var callbacks = new StringBuilder();

            fields.AppendLine(
                "        protected global::LuaObjectBind.ObjectBinder " +
                "_ObjectBinder;");
            assignments.AppendLine(
                "            _ObjectBinder = " +
                "GameObject.GetComponent<" +
                "global::LuaObjectBind.ObjectBinder>();");
            assignments.AppendLine(
                "            if (_ObjectBinder == null)");
            assignments.AppendLine("            {");
            assignments.AppendLine(
                "                throw new " +
                "global::UnityEngine.MissingComponentException(" +
                "\"C# UI Prefab requires ObjectBinder: \" + " +
                "Config.Id);");
            assignments.AppendLine("            }");

            foreach (BindingCode binding in bindings)
            {
                string viewFieldName =
                    GetViewFieldName(binding.FieldName);
                fields.AppendLine(
                    $"        protected {binding.FieldType} " +
                    $"{viewFieldName};");
                AppendViewBindingAssignment(
                    binding,
                    viewFieldName,
                    assignments);
                if (binding.Source == BindingSource.LogicObject)
                {
                    releases.AppendLine(
                        $"            ReleaseEmbeddedLogic(" +
                        $"{viewFieldName});");
                }
                releases.AppendLine(
                    $"            {viewFieldName} = null;");

                AppendViewEventCode(
                    binding,
                    viewFieldName,
                    subscriptions,
                    unsubscriptions,
                    wrappers,
                    callbacks);
            }

            if (bindings.Count == 0)
            {
                releases.AppendLine(
                    "            // No generated bindings.");
            }
            releases.AppendLine(
                "            _ObjectBinder = null;");

            string template = ReadTemplate(ViewBaseTemplatePath);
            return template
                .Replace(
                    "$DATETIME$",
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$PANEL_ID$", options.panelId)
                .Replace("$PREFAB_PATH$", prefabPath)
                .Replace("$NAMESPACE$", options.codeNamespace)
                .Replace(
                    "$BASE_CLASS_NAME$",
                    options.logicClassName + "Base")
                .Replace(
                    "$BIND_FIELDS$",
                    fields.ToString().TrimEnd())
                .Replace(
                    "$BIND_ASSIGNMENTS$",
                    assignments.ToString().TrimEnd())
                .Replace(
                    "$EVENT_SUBSCRIPTIONS$",
                    subscriptions.ToString().TrimEnd())
                .Replace(
                    "$EVENT_UNSUBSCRIPTIONS$",
                    unsubscriptions.ToString().TrimEnd())
                .Replace(
                    "$FIELD_RELEASES$",
                    releases.ToString().TrimEnd())
                .Replace(
                    "$EVENT_WRAPPERS$",
                    wrappers.Length == 0
                        ? "        // No generated event wrappers."
                        : wrappers.ToString().TrimEnd())
                .Replace(
                    "$EVENT_CALLBACKS$",
                    callbacks.Length == 0
                        ? "        // No generated event callbacks."
                        : callbacks.ToString().TrimEnd());
        }

        /// <summary>
        /// 执行追加视图绑定Assignment相关逻辑。
        /// </summary>
        private static void AppendViewBindingAssignment(
            BindingCode binding,
            string viewFieldName,
            StringBuilder assignments)
        {
            string escapedBindingName =
                EscapeStringLiteral(binding.BindingName);
            if (binding.Source == BindingSource.LogicObject)
            {
                assignments.AppendLine(
                    $"            {viewFieldName} = " +
                    $"CreateEmbeddedLogic<{binding.FieldType}>(");
                assignments.AppendLine(
                    "                " +
                    "UIBindingUtility.GetBoundLogicObject(" +
                    "_ObjectBinder, " +
                    $"\"{escapedBindingName}\", Config.Id, " +
                    $"{ToBoolean(binding.Required)}));");
                return;
            }

            assignments.AppendLine(
                $"            {viewFieldName} = " +
                $"UIBindingUtility.GetBoundObject<" +
                $"{binding.FieldType}>(_ObjectBinder, " +
                $"\"{escapedBindingName}\", Config.Id, " +
                $"{ToBoolean(binding.Required)});");
        }

        /// <summary>
        /// 执行追加视图事件代码相关逻辑。
        /// </summary>
        private static void AppendViewEventCode(
            BindingCode binding,
            string viewFieldName,
            StringBuilder subscriptions,
            StringBuilder unsubscriptions,
            StringBuilder wrappers,
            StringBuilder callbacks)
        {
            if (binding.EventKind == GeneratedEventKind.None)
            {
                return;
            }

            string eventName =
                GetEventProperty(binding.EventKind);
            subscriptions.AppendLine(
                $"            if ({viewFieldName} != null) " +
                $"{viewFieldName}.{eventName}.AddListener(" +
                $"{binding.HandlerName});");
            unsubscriptions.AppendLine(
                $"            if ({viewFieldName} != null) " +
                $"{viewFieldName}.{eventName}.RemoveListener(" +
                $"{binding.HandlerName});");

            string valueType =
                GetEventValueType(binding.EventKind);
            if (binding.EventKind == GeneratedEventKind.Click)
            {
                wrappers.AppendLine(
                    $"        private void {binding.HandlerName}()");
                wrappers.AppendLine("        {");
                wrappers.AppendLine(
                    $"            {binding.CallbackName}();");
                wrappers.AppendLine("        }");
                callbacks.AppendLine(
                    $"        protected virtual void " +
                    $"{binding.CallbackName}() {{ }}");
            }
            else
            {
                wrappers.AppendLine(
                    $"        private void {binding.HandlerName}(" +
                    $"{valueType} value)");
                wrappers.AppendLine("        {");
                wrappers.AppendLine(
                    $"            {binding.CallbackName}(value);");
                wrappers.AppendLine("        }");
                callbacks.AppendLine(
                    $"        protected virtual void " +
                    $"{binding.CallbackName}(" +
                    $"{valueType} value) {{ }}");
            }

            wrappers.AppendLine();
            callbacks.AppendLine();
        }

        /// <summary>
        /// 构建视图事件处理器Stubs。
        /// </summary>
        private static string BuildViewEventHandlerStubs(
            List<BindingCode> bindings)
        {
            var builder = new StringBuilder();
            foreach (BindingCode binding in bindings.Where(
                         item =>
                             item.EventKind !=
                             GeneratedEventKind.None))
            {
                string valueType =
                    GetEventValueType(binding.EventKind);
                if (binding.EventKind ==
                    GeneratedEventKind.Click)
                {
                    builder.AppendLine(
                        $"        protected override void " +
                        $"{binding.CallbackName}()");
                }
                else
                {
                    builder.AppendLine(
                        $"        protected override void " +
                        $"{binding.CallbackName}(" +
                        $"{valueType} value)");
                }
                builder.AppendLine("        {");
                builder.AppendLine(
                    "            // TODO: UI event logic.");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            return builder.Length == 0
                ? "        // No generated UI event methods."
                : builder.ToString().TrimEnd();
        }

        /// <summary>
        /// 获取视图Field名称。
        /// </summary>
        private static string GetViewFieldName(
            string fieldName)
        {
            return "_" +
                   SanitizeIdentifier(fieldName, true);
        }

        /// <summary>
        /// 获取事件值类型。
        /// </summary>
        private static string GetEventValueType(
            GeneratedEventKind eventKind)
        {
            switch (eventKind)
            {
                case GeneratedEventKind.BoolValue:
                    return "bool";
                case GeneratedEventKind.FloatValue:
                    return "float";
                case GeneratedEventKind.StringValue:
                    return "string";
                case GeneratedEventKind.IntValue:
                    return "int";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 构建主入口内容。
        /// </summary>
        private static string BuildMainContent(
            CSharpUIGenerationOptions options,
            string prefabPath,
            string autoScriptPath,
            List<BindingCode> bindings)
        {
            string template = ReadTemplate(MainTemplatePath);
            return template
                .Replace("$USERNAME$", Environment.UserName)
                .Replace(
                    "$DATETIME$",
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$PANEL_ID$", options.panelId)
                .Replace("$PREFAB_PATH$", prefabPath)
                .Replace("$AUTO_SCRIPT_PATH$", autoScriptPath)
                .Replace("$NAMESPACE$", options.codeNamespace)
                .Replace("$CLASS_NAME$", options.logicClassName)
                .Replace(
                    "$EVENT_HANDLER_STUBS$",
                    BuildEventHandlerStubs(bindings));
        }

        /// <summary>
        /// 构建Auto内容。
        /// </summary>
        private static string BuildAutoContent(
            CSharpUIGenerationOptions options,
            string prefabPath,
            List<BindingCode> bindings)
        {
            var fields = new StringBuilder();
            var assignments = new StringBuilder();
            var subscriptions = new StringBuilder();
            var unsubscriptions = new StringBuilder();
            var releases = new StringBuilder();
            var wrappers = new StringBuilder();

            fields.AppendLine(
                "        protected global::LuaObjectBind.ObjectBinder " +
                "uiObjectBinder;");
            assignments.AppendLine(
                "            uiObjectBinder = " +
                "GameObject.GetComponent<" +
                "global::LuaObjectBind.ObjectBinder>();");
            assignments.AppendLine(
                "            if (uiObjectBinder == null)");
            assignments.AppendLine("            {");
            assignments.AppendLine(
                "                throw new " +
                "global::UnityEngine.MissingComponentException(" +
                "\"C# UI Prefab requires ObjectBinder: \" + " +
                "Config.Id);");
            assignments.AppendLine("            }");

            foreach (BindingCode binding in bindings)
            {
                fields.AppendLine(
                    $"        protected {binding.FieldType} " +
                    $"{binding.FieldName};");

                string escapedBindingName =
                    EscapeStringLiteral(binding.BindingName);
                if (binding.Source == BindingSource.LogicObject)
                {
                    assignments.AppendLine(
                        $"            {binding.FieldName} = " +
                        $"CreateEmbeddedLogic<{binding.FieldType}>(");
                    assignments.AppendLine(
                        "                " +
                        "UIBindingUtility.GetBoundLogicObject(" +
                        "uiObjectBinder, " +
                        $"\"{escapedBindingName}\", Config.Id, " +
                        $"{ToBoolean(binding.Required)}));");
                }
                else
                {
                    assignments.AppendLine(
                        $"            {binding.FieldName} = " +
                        $"UIBindingUtility.GetBoundObject<" +
                        $"{binding.FieldType}>(uiObjectBinder, " +
                        $"\"{escapedBindingName}\", Config.Id, " +
                        $"{ToBoolean(binding.Required)});");
                }

                if (binding.Source == BindingSource.LogicObject)
                {
                    releases.AppendLine(
                        $"            ReleaseEmbeddedLogic(" +
                        $"{binding.FieldName});");
                }
                releases.AppendLine(
                    $"            {binding.FieldName} = null;");
                AppendEventCode(
                    binding,
                    subscriptions,
                    unsubscriptions,
                    wrappers);
            }

            if (bindings.Count == 0)
            {
                releases.AppendLine(
                    "            // No generated bindings.");
            }
            releases.AppendLine(
                "            uiObjectBinder = null;");

            string template = ReadTemplate(AutoTemplatePath);
            return template
                .Replace("$PANEL_ID$", options.panelId)
                .Replace("$PREFAB_PATH$", prefabPath)
                .Replace(
                    "$DATETIME$",
                    DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$NAMESPACE$", options.codeNamespace)
                .Replace("$CLASS_NAME$", options.logicClassName)
                .Replace(
                    "$BIND_FIELDS$",
                    fields.ToString().TrimEnd())
                .Replace(
                    "$BIND_ASSIGNMENTS$",
                    assignments.ToString().TrimEnd())
                .Replace(
                    "$EVENT_SUBSCRIPTIONS$",
                    subscriptions.Length == 0
                        ? string.Empty
                        : "\n" +
                          subscriptions.ToString().TrimEnd())
                .Replace(
                    "$EVENT_UNSUBSCRIPTIONS$",
                    unsubscriptions.ToString().TrimEnd())
                .Replace(
                    "$FIELD_RELEASES$",
                    releases.ToString().TrimEnd())
                .Replace(
                    "$EVENT_WRAPPERS$",
                    wrappers.Length == 0
                        ? "        // No generated event wrappers."
                        : wrappers.ToString().TrimEnd());
        }

        /// <summary>
        /// 执行追加事件代码相关逻辑。
        /// </summary>
        private static void AppendEventCode(
            BindingCode binding,
            StringBuilder subscriptions,
            StringBuilder unsubscriptions,
            StringBuilder wrappers)
        {
            if (binding.EventKind == GeneratedEventKind.None)
            {
                return;
            }

            string eventName = GetEventProperty(binding.EventKind);
            subscriptions.AppendLine(
                $"            if ({binding.FieldName} != null) " +
                $"{binding.FieldName}.{eventName}.AddListener(" +
                $"{binding.HandlerName});");
            unsubscriptions.AppendLine(
                $"            if ({binding.FieldName} != null) " +
                $"{binding.FieldName}.{eventName}.RemoveListener(" +
                $"{binding.HandlerName});");

            switch (binding.EventKind)
            {
                case GeneratedEventKind.Click:
                    wrappers.AppendLine(
                        $"        private void {binding.HandlerName}()");
                    wrappers.AppendLine("        {");
                    wrappers.AppendLine(
                        "            OnGeneratedClick(" +
                        $"nameof({binding.FieldName}));");
                    wrappers.AppendLine("        }");
                    break;
                case GeneratedEventKind.BoolValue:
                    AppendValueWrapper(
                        binding,
                        "bool",
                        wrappers);
                    break;
                case GeneratedEventKind.FloatValue:
                    AppendValueWrapper(
                        binding,
                        "float",
                        wrappers);
                    break;
                case GeneratedEventKind.StringValue:
                    AppendValueWrapper(
                        binding,
                        "string",
                        wrappers);
                    break;
                case GeneratedEventKind.IntValue:
                    AppendValueWrapper(
                        binding,
                        "int",
                        wrappers);
                    break;
            }

            wrappers.AppendLine();
        }

        /// <summary>
        /// 执行追加值Wrapper相关逻辑。
        /// </summary>
        private static void AppendValueWrapper(
            BindingCode binding,
            string valueType,
            StringBuilder wrappers)
        {
            wrappers.AppendLine(
                $"        private void {binding.HandlerName}(" +
                $"{valueType} value)");
            wrappers.AppendLine("        {");
            wrappers.AppendLine(
                "            OnGeneratedValueChanged(" +
                $"nameof({binding.FieldName}), value);");
            wrappers.AppendLine("        }");
        }

        /// <summary>
        /// 构建事件处理器Stubs。
        /// </summary>
        private static string BuildEventHandlerStubs(
            List<BindingCode> bindings)
        {
            List<BindingCode> clicks = bindings
                .Where(item =>
                    item.EventKind == GeneratedEventKind.Click)
                .ToList();
            List<BindingCode> values = bindings
                .Where(item =>
                    item.EventKind != GeneratedEventKind.None &&
                    item.EventKind != GeneratedEventKind.Click)
                .ToList();

            if (clicks.Count == 0 && values.Count == 0)
            {
                return
                    "        // 有交互绑定后，可按需重写 " +
                    "OnGeneratedClick / OnGeneratedValueChanged。";
            }

            var builder = new StringBuilder();
            if (clicks.Count > 0)
            {
                builder.AppendLine(
                    "        protected override void " +
                    "OnGeneratedClick(string bindingName)");
                builder.AppendLine("        {");
                builder.AppendLine(
                    "            switch (bindingName)");
                builder.AppendLine("            {");
                foreach (BindingCode binding in clicks)
                {
                    builder.AppendLine(
                        $"                case nameof(" +
                        $"{binding.FieldName}):");
                    builder.AppendLine(
                        "                    break;");
                }

                builder.AppendLine("            }");
                builder.AppendLine("        }");
            }

            if (clicks.Count > 0 && values.Count > 0)
            {
                builder.AppendLine();
            }

            if (values.Count > 0)
            {
                builder.AppendLine(
                    "        protected override void " +
                    "OnGeneratedValueChanged(");
                builder.AppendLine(
                    "            string bindingName,");
                builder.AppendLine(
                    "            object value)");
                builder.AppendLine("        {");
                builder.AppendLine(
                    "            switch (bindingName)");
                builder.AppendLine("            {");
                foreach (BindingCode binding in values)
                {
                    builder.AppendLine(
                        $"                case nameof(" +
                        $"{binding.FieldName}):");
                    builder.AppendLine(
                        "                    break;");
                }

                builder.AppendLine("            }");
                builder.AppendLine("        }");
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// 解析事件Kind。
        /// </summary>
        private static GeneratedEventKind ResolveEventKind(
            Type fieldType)
        {
            if (typeof(Button).IsAssignableFrom(fieldType))
            {
                return GeneratedEventKind.Click;
            }

            if (typeof(Toggle).IsAssignableFrom(fieldType))
            {
                return GeneratedEventKind.BoolValue;
            }

            if (typeof(Slider).IsAssignableFrom(fieldType) ||
                typeof(Scrollbar).IsAssignableFrom(fieldType))
            {
                return GeneratedEventKind.FloatValue;
            }

            if (typeof(InputField).IsAssignableFrom(fieldType))
            {
                return GeneratedEventKind.StringValue;
            }

            if (typeof(Dropdown).IsAssignableFrom(fieldType))
            {
                return GeneratedEventKind.IntValue;
            }

            string fullName = fieldType.FullName;
            if (fullName == "TMPro.TMP_InputField")
            {
                return GeneratedEventKind.StringValue;
            }

            if (fullName == "TMPro.TMP_Dropdown")
            {
                return GeneratedEventKind.IntValue;
            }

            return GeneratedEventKind.None;
        }

        /// <summary>
        /// 获取事件属性。
        /// </summary>
        private static string GetEventProperty(
            GeneratedEventKind eventKind)
        {
            return eventKind == GeneratedEventKind.Click
                ? "onClick"
                : "onValueChanged";
        }

        /// <summary>
        /// 获取事件MethodSuffix。
        /// </summary>
        private static string GetEventMethodSuffix(
            GeneratedEventKind eventKind)
        {
            switch (eventKind)
            {
                case GeneratedEventKind.Click:
                    return "Click";
                case GeneratedEventKind.BoolValue:
                case GeneratedEventKind.FloatValue:
                case GeneratedEventKind.StringValue:
                case GeneratedEventKind.IntValue:
                    return "ValueChanged";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 尝试解析Convention目标，并返回是否成功。
        /// </summary>
        private static bool TryResolveConventionTarget(
            GameObject gameObject,
            out UnityEngine.Object target,
            out string fieldName,
            out bool bindEvent)
        {
            target = null;
            fieldName = null;
            bindEvent = false;

            string name = gameObject.name;
            string lowerName = name.ToLowerInvariant();
            Type targetType = null;
            bool bindGameObject = false;

            if (lowerName.StartsWith("btn_"))
            {
                targetType = typeof(Button);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("tgl_") ||
                     lowerName.StartsWith("toggle_"))
            {
                targetType = typeof(Toggle);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("sld_") ||
                     lowerName.StartsWith("slider_"))
            {
                targetType = typeof(Slider);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("scrollbar_"))
            {
                targetType = typeof(Scrollbar);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("input_"))
            {
                targetType =
                    FindType("TMPro.TMP_InputField") ??
                    typeof(InputField);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("dropdown_"))
            {
                targetType =
                    FindType("TMPro.TMP_Dropdown") ??
                    typeof(Dropdown);
                bindEvent = true;
            }
            else if (lowerName.StartsWith("img_"))
            {
                targetType = typeof(Image);
            }
            else if (lowerName.StartsWith("raw_"))
            {
                targetType = typeof(RawImage);
            }
            else if (lowerName.StartsWith("txt_") ||
                     lowerName.StartsWith("tmp_"))
            {
                targetType =
                    FindType("TMPro.TMP_Text") ??
                    typeof(Text);
            }
            else if (lowerName.StartsWith("scroll_"))
            {
                targetType = typeof(ScrollRect);
            }
            else if (lowerName.StartsWith("cg_"))
            {
                targetType = typeof(CanvasGroup);
            }
            else if (lowerName.StartsWith("rt_"))
            {
                targetType = typeof(RectTransform);
            }
            else if (lowerName.StartsWith("tf_"))
            {
                targetType = typeof(Transform);
            }
            else if (lowerName.StartsWith("go_"))
            {
                bindGameObject = true;
            }
            else
            {
                return false;
            }

            target = bindGameObject
                ? gameObject
                : gameObject.GetComponent(targetType);
            if (target == null &&
                (lowerName.StartsWith("txt_") ||
                 lowerName.StartsWith("tmp_") ||
                 lowerName.StartsWith("input_") ||
                 lowerName.StartsWith("dropdown_")))
            {
                target = FindFallbackUIComponent(gameObject);
            }

            if (target == null)
            {
                Debug.LogWarning(
                    $"[UI Generator] 节点 {name} 符合绑定命名，" +
                    "但缺少对应组件。");
                return false;
            }

            fieldName = SanitizeFieldName(name);
            return true;
        }

        /// <summary>
        /// 查找FallbackUIComponent。
        /// </summary>
        private static UnityEngine.Object FindFallbackUIComponent(
            GameObject gameObject)
        {
            return (UnityEngine.Object)
                       gameObject.GetComponent<InputField>() ??
                   gameObject.GetComponent<Dropdown>() ??
                   gameObject.GetComponent<Text>() ??
                   gameObject.GetComponents<Component>()
                       .FirstOrDefault(component =>
                           component != null &&
                           component.GetType().Namespace == "TMPro");
        }

        /// <summary>
        /// 查找类型。
        /// </summary>
        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// 校验AndNormalize选项。
        /// </summary>
        private static void ValidateAndNormalizeOptions(
            CSharpUIGenerationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.panelId =
                options.panelId?.Trim() ?? string.Empty;
            options.codeNamespace =
                options.codeNamespace?.Trim() ?? string.Empty;
            options.logicClassName =
                options.logicClassName?.Trim() ?? string.Empty;
            options.prefabFolder =
                NormalizeAssetPath(options.prefabFolder);
            options.scriptFolder =
                NormalizeAssetPath(options.scriptFolder);

            if (options.panelId.Length == 0 ||
                options.panelId.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException(
                    "Panel ID 不能为空，也不能包含文件名非法字符。");
            }

            if (options.scriptType == UIScriptType.CSharp &&
                !IsValidIdentifier(options.logicClassName))
            {
                throw new InvalidOperationException(
                    "无效的 C# Logic 类名：" +
                    options.logicClassName);
            }

            if (options.scriptType == UIScriptType.CSharp &&
                !IsValidNamespace(options.codeNamespace))
            {
                throw new InvalidOperationException(
                    $"无效的 C# Namespace：{options.codeNamespace}");
            }

            ValidateAssetFolder(
                options.prefabFolder,
                "Prefab 路径");
            if (options.scriptType == UIScriptType.CSharp)
            {
                ValidateAssetFolder(
                    options.scriptFolder,
                    "Output 路径");
            }

            if (!Enum.IsDefined(
                    typeof(UIScriptType),
                    options.scriptType))
            {
                throw new InvalidOperationException(
                    $"无效的脚本类型：{options.scriptType}");
            }

            if (!Enum.IsDefined(
                    typeof(UILayer),
                    options.uiLayer))
            {
                throw new InvalidOperationException(
                    $"无效的 UI 层级：{options.uiLayer}");
            }

            if (options.existingPrefab != null)
            {
                string path = AssetDatabase.GetAssetPath(
                    options.existingPrefab);
                if (!path.EndsWith(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "现有资源必须是 Prefab。");
                }
            }
        }

        /// <summary>
        /// 校验资源目录。
        /// </summary>
        private static void ValidateAssetFolder(
            string path,
            string label)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !(path == "Assets" ||
                  path.StartsWith(
                      "Assets/",
                      StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"{label} 必须位于 Assets 下：{path}");
            }
        }

        /// <summary>
        /// 执行判断是否ValidNamespace相关逻辑。
        /// </summary>
        private static bool IsValidNamespace(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Split('.').All(IsValidIdentifier);
        }

        /// <summary>
        /// 执行判断是否Valid标识符相关逻辑。
        /// </summary>
        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                CSharpKeywords.Contains(value) ||
                (!char.IsLetter(value[0]) &&
                 value[0] != '_'))
            {
                return false;
            }

            return value.All(character =>
                char.IsLetterOrDigit(character) ||
                character == '_');
        }

        /// <summary>
        /// 执行Sanitize字段名称相关逻辑。
        /// </summary>
        private static string SanitizeFieldName(string nodeName)
        {
            string[] parts = nodeName.Split(
                new[] { '_', '-', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "binding";
            }

            var builder = new StringBuilder(
                parts[0].ToLowerInvariant());
            for (int index = 1; index < parts.Length; index++)
            {
                string part = parts[index];
                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part.Substring(1));
                }
            }

            return SanitizeIdentifier(
                builder.ToString(),
                false);
        }

        /// <summary>
        /// 执行Sanitize标识符相关逻辑。
        /// </summary>
        private static string SanitizeIdentifier(
            string value,
            bool pascalCase)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            bool uppercaseNext = pascalCase;
            foreach (char character in value.Trim())
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '_')
                {
                    uppercaseNext = true;
                    continue;
                }

                if (builder.Length == 0 &&
                    char.IsDigit(character))
                {
                    builder.Append('_');
                }

                if (character == '_')
                {
                    uppercaseNext = true;
                    continue;
                }

                builder.Append(
                    uppercaseNext
                        ? char.ToUpperInvariant(character)
                        : character);
                uppercaseNext = false;
            }

            string result = builder.ToString();
            if (CSharpKeywords.Contains(result))
            {
                result = "_" + result;
            }

            return result;
        }

        /// <summary>
        /// 执行MakeUnique字段名称相关逻辑。
        /// </summary>
        private static string MakeUniqueFieldName(
            string fieldName,
            HashSet<string> usedFields)
        {
            if (!usedFields.Contains(fieldName))
            {
                return fieldName;
            }

            int suffix = 2;
            string candidate;
            do
            {
                candidate = fieldName + suffix;
                suffix++;
            }
            while (usedFields.Contains(candidate));

            return candidate;
        }

        /// <summary>
        /// 获取CSharp类型名称。
        /// </summary>
        private static string GetCSharpTypeName(Type type)
        {
            return "global::" +
                   (type.FullName ?? type.Name)
                   .Replace('+', '.');
        }

        /// <summary>
        /// 执行转义字符串Literal相关逻辑。
        /// </summary>
        private static string EscapeStringLiteral(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        /// <summary>
        /// 执行转换为Boolean相关逻辑。
        /// </summary>
        private static string ToBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        /// <summary>
        /// 执行读取模板相关逻辑。
        /// </summary>
        private static string ReadTemplate(string assetPath)
        {
            TextAsset template =
                AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (template != null)
            {
                return template.text;
            }

            string absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "找不到 UI 代码模板。",
                    assetPath);
            }

            return File.ReadAllText(absolutePath);
        }

        /// <summary>
        /// 执行写入文本资源相关逻辑。
        /// </summary>
        private static void WriteTextAsset(
            string assetPath,
            string content)
        {
            assetPath = NormalizeAssetPath(assetPath);
            EnsureAssetFolder(
                Path.GetDirectoryName(assetPath)
                    ?.Replace('\\', '/'));
            File.WriteAllText(
                ToAbsolutePath(assetPath),
                content,
                new UTF8Encoding(false));
        }

        /// <summary>
        /// 确保资源目录。
        /// </summary>
        private static void EnsureAssetFolder(string assetFolder)
        {
            assetFolder = NormalizeAssetPath(assetFolder);
            ValidateAssetFolder(assetFolder, "Asset Folder");
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        /// <summary>
        /// 执行Combine资源路径相关逻辑。
        /// </summary>
        private static string CombineAssetPath(
            string folder,
            string fileName)
        {
            return NormalizeAssetPath(
                (folder ?? string.Empty).TrimEnd('/') +
                "/" +
                (fileName ?? string.Empty).TrimStart('/'));
        }

        /// <summary>
        /// 执行规范化资源路径相关逻辑。
        /// </summary>
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }

    }
}
#endif
