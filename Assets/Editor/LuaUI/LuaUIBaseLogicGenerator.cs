using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using LuaObjectBind;

namespace VGame.GameLogic.Editor.LuaUI
{
    /// <summary>
    /// Lua UI Base Logic Generator - 用于生成Lua UI逻辑脚本的编辑器窗口
    /// </summary>

    public class LuaUIBaseLogicGeneratorWindow : EditorWindow
    {
        private readonly LuaUIBaseLogicGenerator generator = new();
        
        #region Menu Items
        [MenuItem("Assets/生成Lua脚本文件", false, 10)]
        private static void ShowWindow()
        {
            var window = GetWindow<LuaUIBaseLogicGeneratorWindow>("Lua脚本生成器");
            window.generator.InitializeFromSelection();
            window.Show();
        }

        [MenuItem("Tools/Lua Logic Creator", false, 10)]
        private static void ShowWindowFromTools()
        {
            var window = GetWindow<LuaUIBaseLogicGeneratorWindow>("Lua脚本生成器");
            window.generator.InitializeDefaultValues();
            window.Show();
        }
        #endregion

        private void OnGUI()
        {
            generator.OnGUI(this);
        }
    }
    
    public class LuaUIBaseLogicGenerator
    {
        #region Constants
        private const string TEMPLATE_PATH = "Assets/Editor/LuaUI/BaseLogicCodeTemplate.txt";
        private const string TEMPLATE_AUTO_PATH = "Assets/Editor/LuaUI/BaseLogicCodeTemplate_Auto.txt";
        private const string TEMPLATE_BIND_ASSET_PATH = "Assets/Editor/Assets/BindTemplateList.asset";
        private const string UI_PREFIX = "UI.";
        private const string UI_RES_PREFIX = "UIRes/Prefabs/";
        private const string LUA_UI_ROOT = "Lua/UI";
        private const string LUA_EXTENSION = ".lua";
        private const string PREFAB_EXTENSION = ".prefab";
        #endregion

        #region Fields
        private string username = string.Empty;
        private string luaclassname = string.Empty;
        private string className = string.Empty;
        private string resPath = string.Empty;
        private string outputPath = string.Empty;
        private GameObject selectedPrefab;
        private string previewContent = string.Empty;
        private Vector2 previewScrollPosition;
        private bool showPreview = true;
        #endregion

        // 绑定模板
        private BindTemplateList genBindTemplateList = null;

        #region Initialization
        public void InitializeFromSelection()
        {
            // Always initialize default values first
            InitializeDefaultValues();

            var selectedObject = Selection.activeObject as GameObject;
            if (selectedObject != null)
            {
                selectedPrefab = selectedObject;
                UpdateFromPrefab(selectedPrefab);
                UpdatePreview(); // 确保预览被更新
            }
            else
            {
                Debug.LogWarning("请选择一个GameObject预制体");
            }
        }
        
        public void InitializeWithTarget(GameObject prefab)
        {
            // Always initialize default values first
            InitializeDefaultValues();
            
            selectedPrefab = prefab;
            UpdateFromPrefab(selectedPrefab);
            UpdatePreview(); // 确保预览被更新
        }

        public void InitializeDefaultValues()
        {
            username = System.Environment.UserName;

            // Only set default className if it's empty
            if (string.IsNullOrEmpty(className))
            {
                className = "UI.XX.YY";
                UpdateFromClassName(className);
            }
        }

        private void UpdateFromPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                resPath = string.Empty;
                return;
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            resPath = GenerateResPath(prefabPath);
            outputPath = GenerateLuaOutputPath(prefabPath, prefab.name);
            className = GenerateClassNameFromPrefabPath(prefabPath, prefab.name);
            luaclassname = ExtractLuaClassName(className);

            // 确保预览被更新
            UpdatePreview();
        }

        /// <summary>
        /// 直接设置类名（用于非prefab对象）
        /// </summary>
        public void SetClassName(string newClassName)
        {
            UpdateFromClassName(newClassName);
        }

        public void SetOutputFolder(string relativeFolder)
        {
            if (string.IsNullOrWhiteSpace(relativeFolder) ||
                string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            string projectRoot =
                Path.GetDirectoryName(Application.dataPath);
            string outputRoot = Path.Combine(
                projectRoot,
                relativeFolder
                    .Trim()
                    .Trim('/', '\\'));
            outputPath = Path.Combine(
                outputRoot,
                luaclassname + LUA_EXTENSION);
            UpdatePreview();
        }

        private void UpdateFromClassName(string newClassName)
        {
            if (string.IsNullOrEmpty(newClassName)) return;

            className = newClassName;
            luaclassname = ExtractLuaClassName(className);
            outputPath = GenerateOutputPathFromClassName(className);

            // 确保预览被更新
            UpdatePreview();
        }
        #endregion

        #region UI Rendering
        public void OnGUI(EditorWindow window)
        {
            RenderHeader();
            RenderPrefabSelection(window);
            RenderUserInputs();
            RenderValidation();
            RenderOutputInfo();
            RenderGenerateButton();
            RenderPreview();
        }

        private void RenderHeader()
        {
            GUILayout.Label("Generate UIBaseLogic Lua File", EditorStyles.boldLabel);
            EditorGUILayout.Space();
        }

        private void RenderPrefabSelection(EditorWindow window)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected Prefab:", GUILayout.Width(100));
            var newPrefab = (GameObject)EditorGUILayout.ObjectField(selectedPrefab, typeof(GameObject), false);

            if (newPrefab != selectedPrefab)
            {
                selectedPrefab = newPrefab;
                UpdateFromPrefab(selectedPrefab);
                window.Repaint(); // 强制刷新窗口
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void RenderUserInputs()
        {
            // Username field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Username:", GUILayout.Width(100));
            var newUsername = EditorGUILayout.TextField(username);
            if (newUsername != username)
            {
                username = newUsername;
                UpdatePreview();
            }
            EditorGUILayout.EndHorizontal();

            // Res Path field (only show when prefab is selected)
            if (selectedPrefab != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Res Path:", GUILayout.Width(100));
                var newResPath = EditorGUILayout.TextField(resPath);
                if (newResPath != resPath)
                {
                    resPath = newResPath;
                    UpdatePreview();
                }
                EditorGUILayout.EndHorizontal();
            }

            // Class Name field (primary input)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Class Name:", GUILayout.Width(100));
            var newClassName = EditorGUILayout.TextField(className);
            if (newClassName != className)
            {
                // 确保类名始终以UI.开头
                if (!newClassName.StartsWith(UI_PREFIX))
                {
                    newClassName = UI_PREFIX + newClassName;
                }
                UpdateFromClassName(newClassName);
            }
            EditorGUILayout.EndHorizontal();

            // Lua class name field (read-only, auto-generated)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Lua Class Name:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(luaclassname);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Output path field (read-only, auto-generated)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Path:", GUILayout.Width(100));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(outputPath);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void RenderValidation()
        {
            if (string.IsNullOrEmpty(className)) return;

            var isValid = IsValidClassName(className);
            var message = GetClassNameValidationMessage(className);
            var messageType = isValid ? MessageType.Info : MessageType.Error;
            EditorGUILayout.HelpBox(message, messageType);
        }

        private void RenderOutputInfo()
        {
            RenderFileExistenceStatus();
            RenderPrefabInfo();
        }

        private void RenderFileExistenceStatus()
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                EditorGUILayout.HelpBox("输出路径为空，请填写类名", MessageType.Warning);
                return;
            }

            var mainFilePath = outputPath;
            var autoFilePath = GenerateAutoFilePath(outputPath);
            var mainFileExists = File.Exists(mainFilePath);
            var autoFileExists = File.Exists(autoFilePath);

            var statusMessage = "";
            var messageType = MessageType.Info;

            if (mainFileExists && autoFileExists)
            {
                statusMessage = "主文件和Auto文件都已存在";
                messageType = MessageType.Warning;
            }
            else if (mainFileExists)
            {
                statusMessage = "主文件已存在，Auto文件将创建";
                messageType = MessageType.Info;
            }
            else if (autoFileExists)
            {
                statusMessage = "主文件将创建，Auto文件已存在";
                messageType = MessageType.Info;
            }
            else
            {
                statusMessage = "主文件和Auto文件都将创建";
                messageType = MessageType.Info;
            }

            EditorGUILayout.HelpBox(statusMessage, messageType);
        }

        private void RenderPrefabInfo()
        {
            EditorGUILayout.Space();
            if (selectedPrefab != null)
            {
                EditorGUILayout.HelpBox($"选中预设资源: {selectedPrefab.name}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("未选中预设资源. 直接创建Lua脚本.", MessageType.Info);
            }
            EditorGUILayout.Space();
        }

        private void RenderGenerateButton()
        {
            var canGenerate = !string.IsNullOrEmpty(className) && IsValidClassName(className);
            GUI.enabled = canGenerate;
            if (GUILayout.Button("生成Lua文件", GUILayout.Height(30)))
            {
                GenerateLuaFile();
            }
            GUI.enabled = true;
            EditorGUILayout.Space();
        }

        private void RenderPreview()
        {
            showPreview = EditorGUILayout.Foldout(showPreview, "预览生成的内容", true);
            if (!showPreview) return;

            EditorGUILayout.BeginVertical("box");
            if (string.IsNullOrEmpty(previewContent))
            {
                EditorGUILayout.HelpBox("暂无预览内容。请填写类名以生成预览。", MessageType.Info);
            }
            else
            {
                RenderPreviewContent();
            }
            EditorGUILayout.EndVertical();
        }

        private void RenderPreviewContent()
        {
            EditorGUILayout.LabelField("生成的Lua内容:", EditorStyles.boldLabel);
            previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(200));
            EditorGUILayout.TextArea(previewContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("复制预览内容到剪贴板", GUILayout.Height(25)))
            {
                CopyToClipboard(previewContent, "预览内容");
            }
        }
        #endregion

        #region Utility Methods
        private void CopyToClipboard(string content, string description)
        {
            EditorGUIUtility.systemCopyBuffer = content;
            Debug.Log($"{description} copied to clipboard!");
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(className))
            {
                previewContent = string.Empty;
                return;
            }

            try
            {
                var mainContent = GenerateLuaFileContent();
                var autoContent = GenerateAutoLuaFileContent();

                previewContent = $"=== 主文件内容 ===\n{mainContent}\n\n=== Auto文件内容 ===\n{autoContent}";
            }
            catch (System.Exception e)
            {
                previewContent = $"生成预览时出错: {e.Message}";
            }
        }
        #endregion

        #region Path Generation
        private string GenerateResPath(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath)) return string.Empty;

            if (prefabPath.StartsWith("Assets/"))
            {
                var relativePath = prefabPath.Replace("Assets/GameResources/UIRes/Prefabs/", "");
                return $"{UI_RES_PREFIX}{relativePath}";
            }

            return $"{UI_RES_PREFIX}{Path.GetFileName(prefabPath)}";
        }

        private string GenerateAutoFilePath(string mainFilePath)
        {
            var directory = Path.GetDirectoryName(mainFilePath);
            var fileName = Path.GetFileNameWithoutExtension(mainFilePath);
            var extension = Path.GetExtension(mainFilePath);
            return Path.Combine(directory, $"{fileName}_Auto{extension}");
        }

        private string GenerateLuaOutputPath(string prefabPath, string prefabName)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var relativePath = ExtractRelativePath(prefabPath);
            var pathParts = relativePath.Split('/');
            var fileName = pathParts[pathParts.Length - 1];

            // 移除.prefab扩展名
            fileName = fileName.Replace(PREFAB_EXTENSION, "");

            var directoryPath = pathParts.Length > 1
                ? string.Join("/", pathParts, 0, pathParts.Length - 1)
                : string.Empty;

            var luaDirectory = Path.Combine(projectRoot, LUA_UI_ROOT);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                luaDirectory = Path.Combine(luaDirectory, directoryPath);
            }

            return Path.Combine(luaDirectory, $"{fileName}{LUA_EXTENSION}");
        }

        private string ExtractRelativePath(string prefabPath)
        {
            if (prefabPath.Contains("UIRes/Prefabs/View/"))
            {
                var viewIndex = prefabPath.IndexOf("UIRes/Prefabs/View/");
                return prefabPath.Substring(viewIndex + "UIRes/Prefabs/View/".Length);
            }

            if (prefabPath.Contains("UIRes/Prefabs/"))
            {
                var prefabsIndex = prefabPath.IndexOf("UIRes/Prefabs/");
                return prefabPath.Substring(prefabsIndex + "UIRes/Prefabs/".Length);
            }

            return Path.GetFileNameWithoutExtension(prefabPath);
        }

        private string GenerateClassNameFromPrefabPath(string prefabPath, string prefabName)
        {
            // Extract the relative path from UIRes/Prefabs/View/...
            var relativePath = ExtractRelativePath(prefabPath);

            // Remove the .prefab extension
            relativePath = relativePath.Replace(PREFAB_EXTENSION, "");

            // Split the path to get directory and filename
            var pathParts = relativePath.Split('/');
            var fileName = pathParts[pathParts.Length - 1];
            var directoryPath = pathParts.Length > 1
                ? string.Join("/", pathParts, 0, pathParts.Length - 1)
                : string.Empty;

            // Convert path to class name format
            var className = string.Empty;
            if (!string.IsNullOrEmpty(directoryPath))
            {
                className = $"{UI_PREFIX}{directoryPath.Replace('/', '.')}.{fileName}";
            }
            else
            {
                className = $"{UI_PREFIX}{fileName}";
            }

            return className;
        }

        private string GenerateClassNameFromPath(string luaFilePath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var luaUIRoot = Path.Combine(projectRoot, LUA_UI_ROOT);

            if (luaFilePath.StartsWith(luaUIRoot))
            {
                var relativePath = luaFilePath.Substring(luaUIRoot.Length + 1);
                var className = relativePath.Replace(LUA_EXTENSION, "").Replace('\\', '.').Replace('/', '.');
                return className.StartsWith(UI_PREFIX) ? className : $"{UI_PREFIX}{className}";
            }

            var fileName = Path.GetFileNameWithoutExtension(luaFilePath);
            return fileName.StartsWith(UI_PREFIX) ? fileName : $"{UI_PREFIX}{fileName}";
        }

        private string ExtractLuaClassName(string className)
        {
            if (string.IsNullOrEmpty(className)) return string.Empty;
            var parts = className.Split('.');
            return parts[parts.Length - 1];
        }

        private string GenerateOutputPathFromClassName(string className)
        {
            var parts = className.Split('.');
            if (parts.Length > 1 && parts[0] == "UI")
            {
                parts = parts.Skip(1).ToArray();
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var luaDirectory = Path.Combine(projectRoot, LUA_UI_ROOT);

            if (parts.Length > 1)
            {
                var directoryParts = parts.Take(parts.Length - 1).ToArray();
                var directoryPath = string.Join("/", directoryParts);
                luaDirectory = Path.Combine(luaDirectory, directoryPath);
            }

            var fileName = parts[parts.Length - 1] + LUA_EXTENSION;
            return Path.Combine(luaDirectory, fileName);
        }
        #endregion

        #region Validation
        private bool IsValidClassName(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            if (!className.StartsWith(UI_PREFIX)) return false;

            var parts = className.Split('.');
            if (parts.Length < 2) return false;

            return parts.All(IsValidIdentifier);
        }

        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return false;
            if (!char.IsLetter(identifier[0]) && identifier[0] != '_') return false;
            return identifier.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private string GetClassNameValidationMessage(string className)
        {
            if (string.IsNullOrEmpty(className)) return "类名不能为空";
            if (!className.StartsWith(UI_PREFIX)) return "类名必须以'UI.'开头";

            var parts = className.Split('.');
            if (parts.Length < 2) return "类名至少需要2个部分 (例如: UI.Battle)";

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part)) return $"第{i + 1}个部分不能为空";
                if (!char.IsLetter(part[0]) && part[0] != '_') return $"第{i + 1}个部分必须以字母或下划线开头";
                if (part.Any(c => !char.IsLetterOrDigit(c) && c != '_')) return $"第{i + 1}个部分包含无效字符";
            }

            return "有效";
        }
        #endregion

        #region File Operations
        public void GenerateLuaFile(
            bool showCompletionDialog = true)
        {
            if (selectedPrefab == null)
            {
                EditorUtility.DisplayDialog("提示", "请选择Prefab", "确定");
                return;
            }
            if (!ValidateGeneration()) return;

            // 生成主文件和Auto文件的路径
            string mainFilePath = outputPath;
            string autoFilePath = GenerateAutoFilePath(outputPath);

            // 检查主文件是否已存在
            bool mainFileExists = File.Exists(mainFilePath);
            bool autoFileExists = File.Exists(autoFilePath);

            // 如果主文件已存在，不覆盖也不提示，只处理Auto文件
            if (mainFileExists)
            {
                Debug.Log($"主文件 {Path.GetFileName(mainFilePath)} 已存在，跳过生成");
            }

            // 检查Auto文件是否需要覆盖
            if (autoFileExists)
            {
                var result = EditorUtility.DisplayDialog(
                    "Auto文件已存在",
                    $"Auto文件 {Path.GetFileName(autoFilePath)} 已经存在，是否要覆盖？",
                    "覆盖",
                    "取消"
                );

                if (!result) return;
            }

            try
            {
                // 生成主文件（如果不存在）
                if (!mainFileExists)
                {
                    EnsureDirectoryExists(mainFilePath);
                    var mainContent = GenerateLuaFileContent();
                    File.WriteAllText(mainFilePath, mainContent);
                    Debug.Log($"主文件生成成功: {mainFilePath}");
                }

                // 生成Auto文件
                EnsureDirectoryExists(autoFilePath);
                var autoContent = GenerateAutoLuaFileContent();
                File.WriteAllText(autoFilePath, autoContent);

                AssetDatabase.Refresh();

                // 如果指定了Prefab，添加ObjectBinder组件并导出绑定注释
                if (selectedPrefab != null)
                {
                    AddObjectBinderComponent(selectedPrefab);
                    ExportBindingComments(selectedPrefab, string.Empty);
                }

                // 处理_Auto.lua文件，插入绑定函数
                var objectBinder = selectedPrefab.GetComponent<ObjectBinder>();
                GenerateBindInformation(TEMPLATE_BIND_ASSET_PATH, autoFilePath, objectBinder);
                Debug.Log($"Auto文件生成成功: {autoFilePath}");

                if (showCompletionDialog)
                {
                    ShowSuccessMessage();
                }
            }
            catch (System.Exception e)
            {
                if (showCompletionDialog)
                {
                    ShowErrorMessage(
                        e.Message + "\n" + e.StackTrace);
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 读取BindTemplateList.asset，生成绑定函数内容并写入_Auto.lua文件
        /// </summary>
        /// <param name="bindTemplateListAssetPath">BindTemplateList.asset的路径</param>
        /// <param name="autoLuaFilePath">目标_Auto.lua文件路径</param>
        public static void GenerateBindInformation(string bindTemplateListAssetPath, string autoLuaFilePath, ObjectBinder objectBinder)
        {
            // 1. 读取BindTemplateList.asset
            var bindTemplateList = UnityEditor.AssetDatabase.LoadAssetAtPath<VGame.GameLogic.Editor.LuaUI.BindTemplateList>(bindTemplateListAssetPath);
            if (bindTemplateList == null)
            {
                Debug.LogError($"未找到BindTemplateList: {bindTemplateListAssetPath}");
                return;
            }

            // 2. 解析_Auto.lua文件中的---@field注释，提取字段名和类型
            if (!System.IO.File.Exists(autoLuaFilePath))
            {
                Debug.LogError($"未找到_Auto.lua文件: {autoLuaFilePath}");
                return;
            }
            string luaContent = System.IO.File.ReadAllText(autoLuaFilePath);
            var fieldDict = new Dictionary<string, string>(); // 字段名->类型
            var fieldLines = System.Text.RegularExpressions.Regex.Matches(luaContent, @"^\s*---@field\s+(\w+)\s+([^\s]+)", System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in fieldLines)
            {
                if (match.Groups.Count == 3)
                {
                    string name = match.Groups[1].Value;
                    string type = match.Groups[2].Value;
                    fieldDict[name] = type;
                }
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(autoLuaFilePath);
            string luaclassname = fileName.EndsWith("_Auto") ? fileName.Substring(0, fileName.Length - 5) : fileName;

            // 3. 分别生成绑定语句和绑定函数
            var bindInfoBuilder = new System.Text.StringBuilder();
            var bindFuncBuilder = new System.Text.StringBuilder();
            var releaseBindBuilder = new System.Text.StringBuilder();

            int lineIndex = 0;
            foreach (var template in bindTemplateList.bindTemplates)
            {
                string widgetTypeName = template.widgetType;
                foreach (var kv in fieldDict)
                {
                    if (kv.Value == widgetTypeName)
                    {
                        string widgetName = kv.Key;
                        foreach (var func in template.funcInfos)
                        {
                            // 生成绑定语句
                            string prefix = lineIndex == 0 ? "" : "\t";
                            bindInfoBuilder.AppendLine($"{prefix}self.Widget.{widgetName}.{func.funName}:AddListener(Func(self, self.{func.funName}_{widgetName}))");
                            // 生成函数原型
                            foreach (var parameter in func.parameters)
                            {
                                bindFuncBuilder.AppendLine($"---@param {parameter.paramName} {parameter.typeName}");
                            }
                            var paramList = string.Join(", ", func.parameters.Select(p => p.paramName));
                            bindFuncBuilder.AppendLine($"function $luaclassName$:{func.funName}_{widgetName}({paramList}) end");
                            // 生成销毁绑定语句
                            releaseBindBuilder.AppendLine($"{prefix}if self.Widget.{widgetName} then self.Widget.{widgetName}.{func.funName}:RemoveAllListeners() end");
                            lineIndex++;
                        }
                    }
                }
            }

            // 4.1 生成子逻辑绑定信息
            var bindLogicBuilder = new System.Text.StringBuilder();
            string autoLogic = "self.Logic.$logic_name$ = self:CreateLogicInstance(self._staticObjectBinders.$logic_name$.LuaClassName, self._staticObjectBinders.$logic_name$.gameObject)";
            if (objectBinder.binderElements != null && objectBinder.binderElements.Binds.Count > 0)
            {
                bool isFirst = true;
                foreach (var bindValue in objectBinder.binderElements.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;
                    string className = bindValue.Value.LuaClassName;
                    if (string.IsNullOrEmpty(className))
                    {
                        Debug.LogError($"ExportFieldComments: 无法获取{bindValue.Name}的LuaClassName");
                        continue;
                    }
                    string logicName = bindValue.Name;
                    string input = autoLogic.Replace("$logic_name$", logicName);
                    if (isFirst) { bindLogicBuilder.AppendLine(input); isFirst = false; }
                    else bindLogicBuilder.AppendLine("\t" + input);
                }
            }

            // 4.2 分别替换$auto_bind_information$和$auto_bind_functions$占位符
            var auto_bind_information = bindInfoBuilder.ToString().TrimEnd();
            var auto_bind_binder_logic_information = bindLogicBuilder.ToString().Trim();
            if (!string.IsNullOrEmpty(auto_bind_binder_logic_information))
            {
                auto_bind_information = $"{auto_bind_information}\n\n    {auto_bind_binder_logic_information}";
            }
            
            luaContent = luaContent.Replace("$auto_bind_information$", auto_bind_information);
            
            string funcCode = bindFuncBuilder.ToString().Replace("$luaclassName$", luaclassname);
            luaContent = luaContent.Replace("$auto_bind_functions$", funcCode);
            luaContent = luaContent.Replace("$auto_release_information$", releaseBindBuilder.ToString().TrimEnd());
            luaContent = luaContent.Replace("$auto_state_control$", GenerateLuaStateContent(objectBinder, luaclassname));

            // 5. 生成 GuideNodeMap 引导节点注册代码
            string guideNodeMapCode = GenerateGuideNodeMapCode(objectBinder, luaclassname);
            if (!string.IsNullOrEmpty(guideNodeMapCode))
            {
                // 在 return 语句之前插入 GuideNodeMap 代码
                string returnStatement = $"return {luaclassname}";
                int returnIndex = luaContent.LastIndexOf(returnStatement);
                if (returnIndex >= 0)
                {
                    luaContent = luaContent.Insert(returnIndex, guideNodeMapCode + "\n");
                }
                else
                {
                    // 如果找不到 return 语句，追加到末尾
                    luaContent += "\n" + guideNodeMapCode;
                }
            }

            // 6. 写回_Auto.lua文件
            System.IO.File.WriteAllText(autoLuaFilePath, luaContent);
            Debug.Log($"已写入绑定信息和函数到: {autoLuaFilePath}");
        }

        private bool ValidateGeneration()
        {
            if (string.IsNullOrEmpty(className))
            {
                EditorUtility.DisplayDialog("错误", "类名不能为空!", "确定");
                return false;
            }

            if (!IsValidClassName(className))
            {
                var errorMessage = GetClassNameValidationMessage(className);
                EditorUtility.DisplayDialog("错误", $"无效的类名: {errorMessage}", "确定");
                return false;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                EditorUtility.DisplayDialog("错误", "输出路径不能为空!", "确定");
                return false;
            }

            return true;
        }

        private void EnsureDirectoryExists(string filePath)
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private string ReadAllTextFromFile(string filePath)
        {
            var templatePath = Path.Combine(Application.dataPath, "..", filePath);
            if (!File.Exists(templatePath))
            {
                throw new System.Exception($"Template file not found at: {templatePath}");
            }

            var templateContent = File.ReadAllText(templatePath);
            return templateContent;
        }

        private string GenerateLuaFileContent()
        {
            string templateContent = ReadAllTextFromFile(TEMPLATE_PATH);
            var luaContent = templateContent
                .Replace("$luaclassName$", luaclassname)
                .Replace("$className$", className)
                .Replace("$datetime$", System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$username$", username)
                .Replace("$autoClassPath$", className + "_Auto");

            // 总是包含ResPath行，如果为空则显示"Empty"
            var resPathValue = string.IsNullOrEmpty(resPath) ? "Empty" : resPath;
            luaContent = luaContent.Replace("$resPath$", resPathValue);

            return luaContent;
        }

        private string GenerateAutoLuaFileContent()
        {
            string templateContent = ReadAllTextFromFile(TEMPLATE_AUTO_PATH);
            var luaContent = templateContent
                .Replace("$luaclassName$", luaclassname)
                .Replace("$className$", className)
                .Replace("$datetime$", System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"))
                .Replace("$username$", username)
                .Replace("$autoClassPath$", className + "_Auto");

            // 总是包含ResPath行，如果为空则显示"Empty"
            var resPathValue = string.IsNullOrEmpty(resPath) ? "Empty" : resPath;
            luaContent = luaContent.Replace("$resPath$", resPathValue);

            return luaContent;
        }

        private static string GenerateLuaStateContent(ObjectBinder objectBinder, string realClassName)
        {
            StringBuilder sb = new StringBuilder();
            // 处理stateControlBindValues
            if (objectBinder.stateControlBindValues != null && objectBinder.stateControlBindValues.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.stateControlBindValues.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;

                    // 生成状态table定义
                    var controller = bindValue.GetStateController();
                    if (controller != null && !string.IsNullOrEmpty(bindValue.StateGroupName))
                    {
                        var stateGroup = controller.FindStateGroup(bindValue.StateGroupName);
                        if (stateGroup != null && stateGroup.States != null && stateGroup.States.Count > 0)
                        {
                            string stateTableDef = GenerateStateTableDefinition(bindValue.Name, stateGroup, realClassName);
                            if (!string.IsNullOrEmpty(stateTableDef))
                            {
                                sb.AppendLine(stateTableDef);
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }
        private static string GenerateStateTableClassName(string fileName, string stateGroupName)
        {
            // 格式：S_文件名_状态组名
            return $"S_{fileName}_{stateGroupName}";
        }

        private static string GenerateStateTableDefinition(string bindingName, StateControl.Runtime.StateGroup stateGroup, string className)
        {
            if (stateGroup == null || stateGroup.States == null || stateGroup.States.Count == 0)
                return null;

            List<string> lines = new List<string>();

            // 生成状态table名称：去掉SG_前缀中的G
            string tableName = bindingName.StartsWith("SG_") ? "S_" + bindingName.Substring(3) : bindingName;

            // 生成table定义，作为类的成员
            lines.Add($"{className}.{tableName} = {{");

            // 添加每个状态的键值对
            for (int i = 0; i < stateGroup.States.Count; i++)
            {
                var state = stateGroup.States[i];
                if (state == null || string.IsNullOrEmpty(state.Name))
                    continue;

                string note = string.IsNullOrEmpty(state.Note) ? "" : "--" + state.Note;
                // 检查状态名是否是纯数字或以数字开头，使用字符串键
                if (char.IsDigit(state.Name[0]))
                {
                    lines.Add($"    [\"s{state.Name}\"] = {i},{note}");
                }
                else
                {
                    lines.Add($"    [\"{state.Name}\"] = {i},{note}");
                }
            }

            lines.Add("}");

            return string.Join("\n", lines);
        }
    
        /// <summary>
        /// 生成 GuideNodeMap 引导节点注册代码
        /// 读取 ObjectBinder 的 guideNodeKeys，按 UIViewName 分组注册
        /// </summary>
        private static string GenerateGuideNodeMapCode(ObjectBinder objectBinder, string uiViewName)
        {
            if (objectBinder == null || objectBinder.guideNodeKeys == null || objectBinder.guideNodeKeys.Count == 0)
                return null;

            // 过滤掉空的 key
            var validKeys = objectBinder.guideNodeKeys.Where(k => !string.IsNullOrEmpty(k)).ToList();
            if (validKeys.Count == 0)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("--- 引导节点注册（自动生成，勿手动修改）");
            sb.AppendLine("GuideNodeMap = GuideNodeMap or {}");
            sb.AppendLine($"GuideNodeMap[\"{uiViewName}\"] = {{");
            foreach (var key in validKeys)
            {
                sb.AppendLine($"    {key} = true,");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }

        private void ShowSuccessMessage()
        {
            Debug.Log($"Lua文件生成成功: {outputPath}");
            EditorUtility.DisplayDialog("成功", $"Lua文件已生成:\n{outputPath}", "确定");
        }

        private void ShowErrorMessage(string message)
        {
            Debug.LogError($"生成Lua文件失败: {message}");
            EditorUtility.DisplayDialog("错误", $"生成Lua文件失败:\n{message}", "确定");
        }

        private void AddObjectBinderComponent(GameObject prefab)
        {
            if (prefab == null) return;

            // 检查是否已经存在ObjectBinder组件
            var existingObjectBinder = prefab.GetComponent<ObjectBinder>();
            if (existingObjectBinder != null)
            {
                Debug.Log($"Prefab {prefab.name} 已存在ObjectBinder组件，跳过添加");
                return;
            }

            // 添加ObjectBinder组件
            var objectBinder = prefab.AddComponent<ObjectBinder>();
            if (objectBinder != null)
            {
                Debug.Log($"已为Prefab {prefab.name} 添加ObjectBinder组件");

                // 标记为已修改，确保保存
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogError($"为Prefab {prefab.name} 添加ObjectBinder组件失败");
            }
        }

        private void ExportBindingComments(GameObject prefab, string insertLine)
        {
            if (prefab == null) return;

            try
            {
                // 使用Auto文件路径进行导出
                string autoFilePath = GenerateAutoFilePath(outputPath);
                // 调用ExportBindingCommentToLuaFile方法
                bool success = LuaObjectBind.Editor.LuaFileReferenceDrawer.ExportBindingCommentToLuaFile(prefab, autoFilePath, insertLine);

                if (success)
                {
                    Debug.Log($"已成功导出ObjectBinder绑定注释到: {autoFilePath}");
                }
                else
                {
                    Debug.LogWarning($"导出ObjectBinder绑定注释失败: {autoFilePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"导出ObjectBinder绑定注释时发生错误: {e.Message}");
            }
        }
        #endregion
    }
}
