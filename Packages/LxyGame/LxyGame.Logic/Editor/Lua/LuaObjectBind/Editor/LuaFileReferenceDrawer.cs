using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
//using Game.Lockstep.Runtime;
using Unity.CodeEditor;

namespace LuaObjectBind.Editor
{

    [CustomPropertyDrawer(typeof(LuaFileReference))]
    public class LuaFileReferenceDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private bool isDragging = false;
        private bool isValidLuaFile = false;
        private static LuaFileBrowserWindow fileBrowserWindow;

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var filenameProperty = property.FindPropertyRelative("filePath");

            float y = position.y;
            float x = position.x;
            float height = GetPropertyHeight(property, label);

            Rect nameRect = new Rect(x, y, 60, height);
            Rect valueRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, position.xMax - nameRect.xMax - HORIZONTAL_GAP, height);

            // 显示当前选择的文件路径或选择按钮
            if (string.IsNullOrEmpty(filenameProperty.stringValue))
            {
                // 显示标签
                EditorGUI.LabelField(nameRect, "目标Lua：");

                // 选择按钮占满整个宽度
                Rect selectButtonRect = new Rect(valueRect.x, valueRect.y, valueRect.width, valueRect.height);
                if (GUI.Button(selectButtonRect, "选择Lua文件"))
                {
                    ShowLuaFileBrowser(filenameProperty);
                }
            }
            else
            {
                // 不显示标签，文件路径占满整个宽度
                Rect fullRect = new Rect(x, y, position.width, height);
                EditorGUI.LabelField(fullRect, filenameProperty.stringValue);

                // 添加打开文件按钮
                Rect openButtonRect = new Rect(fullRect.xMax - 200, y, 80, height);
                if (GUI.Button(openButtonRect, "打开文件"))
                {
                    OpenLuaFile(filenameProperty.stringValue);
                }

                // 添加导出按钮
                Rect exportButtonRect = new Rect(fullRect.xMax - 110, y, 90, height);
                GUI.color = Color.cyan;
                if (GUI.Button(exportButtonRect, "导出绑定注释"))
                {
                    ExportBindingComment(filenameProperty.stringValue);
                }
                GUI.color = Color.white;

                // 添加一个按钮来清除当前值
                Rect clearButtonRect = new Rect(fullRect.xMax - 20, y, 20, height);
                if (GUI.Button(clearButtonRect, "×"))
                {
                    filenameProperty.stringValue = null;
                    GUI.changed = true;
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 执行显示Lua文件Browser相关逻辑。
        /// </summary>
        private void ShowLuaFileBrowser(SerializedProperty property)
        {
            if (fileBrowserWindow == null)
            {
                fileBrowserWindow = ScriptableObject.CreateInstance<LuaFileBrowserWindow>();
                fileBrowserWindow.Initialize(property);
            }

            fileBrowserWindow.ShowAsDropDown(
                new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0),
                new Vector2(300, 400)
            );
        }

        /// <summary>
        /// 打开Lua文件。
        /// </summary>
        private void OpenLuaFile(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;
            string luaPath = LuaObjectBindProxy.GetLuaPath();
            Debug.Log("OpenLuaFile: " + luaPath + " " + relativePath);
            if (string.IsNullOrEmpty(luaPath) || !Directory.Exists(luaPath))
                return;

            string fullPath = Path.Combine(luaPath, relativePath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", "文件不存在: " + fullPath, "确定");
                return;
            }

            CodeEditor.CurrentEditor.OpenProject(fullPath);
        }

        /// <summary>
        /// 执行Export绑定注释相关逻辑。
        /// </summary>
        private void ExportBindingComment(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            string luaPath = LuaObjectBindProxy.GetLuaPath();
            if (string.IsNullOrEmpty(luaPath) || !Directory.Exists(luaPath))
            {
                EditorUtility.DisplayDialog("错误", "Lua目录不存在: " + luaPath, "确定");
                return;
            }

            string fullPath = Path.Combine(luaPath, relativePath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", "文件不存在: " + fullPath, "确定");
                return;
            }

            // 获取当前选中的GameObject
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个包含ObjectBinder组件的GameObject", "确定");
                return;
            }

            // 调用只导出现有绑定的方法
            ExportExistingBindingCommentToLuaFile(selectedObject, fullPath);
        }

        /// <summary>
        /// 导出ObjectBinder内容到指定的Lua文件
        /// </summary>
        /// <param name="inputGameObject">包含ObjectBinder组件的GameObject</param>
        /// <param name="saveLuaFile">要保存的Lua文件完整路径</param>
        /// <param name="insertLine">要插入到Object Binder区域第一行的额外内容（可选）</param>
        /// <returns>是否成功导出</returns>
        public static bool ExportBindingCommentToLuaFile(GameObject inputGameObject, string saveLuaFile, string insertLine = null)
        {
            if (inputGameObject == null)
            {
                Debug.LogError("ExportBindingCommentToLuaFile: inputGameObject is null");
                return false;
            }

            if (string.IsNullOrEmpty(saveLuaFile))
            {
                Debug.LogError("ExportBindingCommentToLuaFile: saveLuaFile is null or empty");
                return false;
            }

            // 检查是否有ObjectBinder组件，如果没有则自动添加
            ObjectBinder objectBinder = inputGameObject.GetComponent<ObjectBinder>();
            if (objectBinder == null)
            {
                Debug.Log($"ExportBindingCommentToLuaFile: GameObject {inputGameObject.name} 没有ObjectBinder组件，正在自动添加...");
                objectBinder = inputGameObject.AddComponent<ObjectBinder>();
                if (objectBinder == null)
                {
                    Debug.LogError($"ExportBindingCommentToLuaFile: 无法为GameObject {inputGameObject.name} 添加ObjectBinder组件");
                    return false;
                }
                Debug.Log($"ExportBindingCommentToLuaFile: 成功为GameObject {inputGameObject.name} 添加ObjectBinder组件");

                // 标记为已修改并保存
                EditorUtility.SetDirty(inputGameObject);
                AssetDatabase.SaveAssets();

                // 等待一帧，确保组件完全初始化
                EditorApplication.delayCall += () =>
                {
                    // 重新获取ObjectBinder组件，确保获取到最新的实例
                    var refreshedObjectBinder = inputGameObject.GetComponent<ObjectBinder>();
                    if (refreshedObjectBinder != null)
                    {
                        Debug.Log("ExportBindingCommentToLuaFile: ObjectBinder组件已完全初始化，开始执行自动绑定");

                        // 执行自动绑定，传递Lua文件路径
                        bool autoBindSuccess = BindValueCollectionDrawer.ExportAutoBindComponents(inputGameObject, saveLuaFile);
                        if (!autoBindSuccess)
                        {
                            Debug.LogWarning("ExportBindingCommentToLuaFile: 自动绑定失败，但继续执行导出操作");
                        }
                        else
                        {
                            Debug.Log("ExportBindingCommentToLuaFile: 自动绑定执行成功");
                        }

                        // 重新执行导出操作
                        ExportBindingCommentToLuaFileInternal(inputGameObject, saveLuaFile, insertLine);
                    }
                };

                // 先返回true，让延迟调用处理后续操作
                return true;
            }

            // 如果ObjectBinder已存在，直接执行自动绑定，传递Lua文件路径
            bool autoBindSuccess = BindValueCollectionDrawer.ExportAutoBindComponents(inputGameObject, saveLuaFile);
            if (!autoBindSuccess)
            {
                Debug.LogWarning("ExportBindingCommentToLuaFile: 自动绑定失败，但继续执行导出操作");
            }
            else
            {
                Debug.Log("ExportBindingCommentToLuaFile: 自动绑定执行成功");
            }

            // 执行实际的导出操作
            return ExportBindingCommentToLuaFileInternal(inputGameObject, saveLuaFile, insertLine);
        }

        /// <summary>
        /// 只导出现有绑定到指定的Lua文件（不执行自动绑定）
        /// </summary>
        /// <param name="inputGameObject">包含ObjectBinder组件的GameObject</param>
        /// <param name="saveLuaFile">要保存的Lua文件完整路径</param>
        /// <param name="insertLine">要插入到Object Binder区域第一行的额外内容（可选）</param>
        /// <returns>是否成功导出</returns>
        public static bool ExportExistingBindingCommentToLuaFile(GameObject inputGameObject, string saveLuaFile, string insertLine = null)
        {
            if (inputGameObject == null)
            {
                Debug.LogError("ExportExistingBindingCommentToLuaFile: inputGameObject is null");
                return false;
            }

            if (string.IsNullOrEmpty(saveLuaFile))
            {
                Debug.LogError("ExportExistingBindingCommentToLuaFile: saveLuaFile is null or empty");
                return false;
            }

            // 检查ObjectBinder组件
            ObjectBinder objectBinder = inputGameObject.GetComponent<ObjectBinder>();
            if (objectBinder == null)
            {
                Debug.LogError("ExportExistingBindingCommentToLuaFile: GameObject没有ObjectBinder组件");
                return false;
            }

            // 直接执行导出操作，不进行自动绑定
            return ExportBindingCommentToLuaFileInternal(inputGameObject, saveLuaFile, insertLine);
        }

        /// <summary>
        /// 内部方法：执行实际的导出操作
        /// </summary>
        private static bool ExportBindingCommentToLuaFileInternal(GameObject inputGameObject, string saveLuaFile, string insertLine = null)
        {
            // 确保目录存在
            string directory = Path.GetDirectoryName(saveLuaFile);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 读取文件内容并清理旧的Object Binder内容
            string[] originalLines = new string[0];
            if (File.Exists(saveLuaFile))
            {
                originalLines = File.ReadAllLines(saveLuaFile);
            }

            string[] lines = CleanOldObjectBinderContent(originalLines);
            List<string> newLines = new List<string>();

            // 生成field注释
            List<string> fieldComments = GenerateFieldComments(inputGameObject);

            // 生成要插入的内容（只包含field注释，不包含class定义）
            List<string> generatedContent = new List<string>();

            // 如果有额外的插入行，添加到第一行
            if (!string.IsNullOrEmpty(insertLine))
            {
                generatedContent.Add(insertLine);
                // 不添加空行，直接添加field注释
            }

            generatedContent.AddRange(fieldComments);

            // 查找插入位置
            int insertIndex = FindInsertPosition(lines);

            // 添加插入位置之前的所有行
            for (int i = 0; i < insertIndex; i++)
            {
                newLines.Add(lines[i]);
            }

            // 添加开始标记
            newLines.Add(startMarker);

            // 添加生成的内容
            newLines.AddRange(generatedContent);

            // 添加结束标记
            newLines.Add(endMarker);

            // 添加插入位置之后的所有行
            for (int i = insertIndex; i < lines.Length; i++)
            {
                newLines.Add(lines[i]);
            }

            Debug.Log($"已插入Object Binder内容到文件: {saveLuaFile}，插入位置: 第{insertIndex + 1}行");

            // 写入文件
            File.WriteAllLines(saveLuaFile, newLines);

            // 刷新资源
            AssetDatabase.Refresh();

            return true;
        }
        private static string startMarker = "-------------------------------Object Binder Generated（请勿修改）-------------------------------";
        private static string logicBindMarker = "\n--------------------------------Logic Bind Generated（请勿修改）---------------------------------";
        private static string endMarker = "-----------------------------------Object Binder Generated End----------------------------------";
        /// <summary>
        /// 执行CleanOld对象绑定器Content相关逻辑。
        /// </summary>
        private static string[] CleanOldObjectBinderContent(string[] lines)
        {
            List<string> cleanedLines = new List<string>();
            bool insideBinderBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Contains(startMarker))
                {
                    insideBinderBlock = true;
                    continue; // 跳过开始标记
                }

                if (line.Contains(endMarker))
                {
                    insideBinderBlock = false;
                    continue; // 跳过结束标记
                }

                if (!insideBinderBlock)
                {
                    cleanedLines.Add(line);
                }
                // 如果在标记块内，跳过所有内容
            }

            return cleanedLines.ToArray();
        }

        /// <summary>
        /// 查找Insert位置。
        /// </summary>
        private static int FindInsertPosition(string[] lines)
        {
            // 查找local UIBaseLogic = require("Lua.Framework.UI.UIBaseLogic")
            int uiBaseLogicIndex = -1;
            // 查找---@class标签
            int classTagIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // 查找UIBaseLogic require语句
                if (line.StartsWith("local UIBaseLogic") && line.Contains("require") && line.Contains("UIBaseLogic"))
                {
                    uiBaseLogicIndex = i;
                }

                // 查找---@class标签
                if (line.StartsWith("---@class"))
                {
                    classTagIndex = i;
                }
            }

            // 优先级：UIBaseLogic require语句 > ---@class标签 > 文件开头
            if (uiBaseLogicIndex != -1)
            {
                // 在UIBaseLogic require语句之后插入
                return uiBaseLogicIndex + 1;
            }
            else if (classTagIndex != -1)
            {
                // 在---@class标签之前插入
                return classTagIndex;
            }
            else
            {
                // 在文件开头插入
                return 0;
            }
        }

        /// <summary>
        /// 转换CSharp类型ToLua类型。
        /// </summary>
        private static string ConvertCSharpTypeToLuaType(System.Type type)
        {
            if (type == null)
                return "any";

            if (type.IsPrimitive)
            {
                if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(short) || type == typeof(long))
                    return "number";
                if (type == typeof(bool))
                    return "boolean";
                if (type == typeof(string))
                    return "string";
            }
            else if (type.IsArray)
            {
                string elementType = ConvertCSharpTypeToLuaType(type.GetElementType());
                return $"{elementType}[]";
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                string elementType = ConvertCSharpTypeToLuaType(type.GetGenericArguments()[0]);
                return $"{elementType}[]";
            }
            else if (type == typeof(GameObject))
            {
                return "UnityEngine.GameObject";
            }
            else if (type == typeof(Transform))
            {
                return "UnityEngine.Transform";
            }
            else if (type == typeof(Vector3))
            {
                return "UnityEngine.Vector3";
            }
            else if (type == typeof(Vector2))
            {
                return "UnityEngine.Vector2";
            }
            else if (type == typeof(Quaternion))
            {
                return "UnityEngine.Quaternion";
            }
            // 处理Unity UI组件
            else if (type.Namespace == "UnityEngine.UI")
            {
                return $"UnityEngine.UI.{type.Name}";
            }
            // 处理TextMeshPro组件
            else if (type.Namespace == "TMPro")
            {
                return $"TMPro.{type.Name}";
            }
            // 对于其他类型，返回完整的命名空间
            else if (type.Namespace != null)
            {
                return $"{type.Namespace}.{type.Name}";
            }

            return type.Name;
        }

        /// <summary>
        /// 转换Field绑定EnumToLua类型。
        /// </summary>
        private static string ConvertFieldBindEnumToLuaType(FieldBindEnum bindType)
        {
            // 获取枚举值的 FieldInfo
            FieldInfo fieldInfo = bindType.GetType().GetField(bindType.ToString());
            if (fieldInfo == null)
                return "any";

            // 获取 FieldBindTypeAttribute
            FieldBindTypeAttribute attribute = fieldInfo.GetCustomAttribute<FieldBindTypeAttribute>();
            if (attribute == null)
                return "any";

            // 根据 FieldBindTypeEnum 返回对应的 Lua 类型
            switch (attribute.FieldBindType)
            {
                case FieldBindTypeEnum.Int:
                case FieldBindTypeEnum.Float:
                    return "number";
                case FieldBindTypeEnum.String:
                    return "string";
                case FieldBindTypeEnum.Bool:
                    return "boolean";
                case FieldBindTypeEnum.Vector2:
                    return "UnityEngine.Vector2";
                case FieldBindTypeEnum.Vector3:
                    return "table";
                default:
                    return "any";
            }
        }

        /// <summary>
        /// 执行Generate字段注释相关逻辑。
        /// </summary>
        private static List<string> GenerateFieldComments(GameObject selectedObject, string luaClassName = "")
        {
            List<string> fieldComments = new List<string>();
            ObjectBinder objectBinder = selectedObject.GetComponent<ObjectBinder>();
            // 插入logic集合
            string requireLuaClassName = objectBinder.LuaClassName;
            string[] strs = requireLuaClassName.Split('.');
            string realClassName = strs[strs.Length - 1].Replace("_Auto", "");
            string insertClass = $"---@class {realClassName}.Widget";
            fieldComments.Add(insertClass);
            if (objectBinder == null)
            {
                return fieldComments;
            }

            // 生成field注释
            foreach (var bindValue in objectBinder.bindValues.Binds)
            {
                if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                    continue;

                // 获取绑定值的类型
                System.Type valueType = bindValue.ValueType;
                if (valueType == null)
                    continue;

                // 转换C#类型到Lua类型
                string luaType = ConvertCSharpTypeToLuaType(valueType);

                // 生成field注释
                string fieldComment = $"---@field {bindValue.Name} {luaType}";
                fieldComments.Add(fieldComment);
            }

            // 处理fieldBindValues
            if (objectBinder.fieldBindValues != null && objectBinder.fieldBindValues.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.fieldBindValues.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;

                    // 根据FieldBindEnum生成对应的Lua类型
                    string luaType = ConvertFieldBindEnumToLuaType(bindValue.FieldBindType);

                    // 生成field注释
                    string fieldComment = $"---@field {bindValue.Name} {luaType}";
                    fieldComments.Add(fieldComment);
                }
            }

            // 处理pathBindValues
            if (objectBinder.pathBindValues != null && objectBinder.pathBindValues.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.pathBindValues.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;

                    // 路径绑定返回字符串类型
                    string fieldComment = $"---@field {bindValue.Name} string";
                    fieldComments.Add(fieldComment);
                }
            }

            // 处理staticTextBindValues - 静态文本绑定
            if (objectBinder.staticTextBindValues != null && objectBinder.staticTextBindValues.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.staticTextBindValues.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;

                    // 静态文本绑定返回字符串类型，并添加注释说明实际值
                    string textValue = bindValue.GetValue<string>();
                    string displayValue = string.IsNullOrEmpty(textValue) ? "" : $" -- \"{textValue}\"";
                    string fieldComment = $"---@field {bindValue.Name} string{displayValue}";
                    fieldComments.Add(fieldComment);
                }
            }
            
            if (objectBinder.stateControlBindValues != null && objectBinder.stateControlBindValues.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.stateControlBindValues.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;

                    // 生成状态table类名：去掉SG_前缀中的G，变成S_XXX_States
                    string stateTableClassName = GenerateStateTableClassName(realClassName,bindValue.Name);

                    // 状态控制绑定field注释指向生成的States类
                    string fieldComment = $"---@field {bindValue.Name} {stateTableClassName}";
                    fieldComments.Add(fieldComment);
                }
            }

            fieldComments.Add(logicBindMarker);
            fieldComments.Add($"---@class {realClassName}.Logic");
            // 处理Logic注释
            if (objectBinder.binderElements != null && objectBinder.binderElements.Binds.Count > 0)
            {
                foreach (var bindValue in objectBinder.binderElements.Binds)
                {
                    if (bindValue == null || string.IsNullOrEmpty(bindValue.Name))
                        continue;
                    string className = bindValue.Value.LuaClassName;
                    strs = className.Split('.');
                    realClassName = strs[strs.Length - 1].Replace("_Auto", "");

                    if (string.IsNullOrEmpty(className))
                    {
                        Debug.LogError($"ExportFieldComments: 无法获取{bindValue.Name}的LuaClassName");
                        continue;
                    }
                    // 生成注释
                    string fieldComment = $"---@field {bindValue.Name} {realClassName}";
                    fieldComments.Add(fieldComment);
                }
            }
            return fieldComments;
        }
        
        /// <summary>
        /// 执行Generate状态Table类型名称相关逻辑。
        /// </summary>
        private static string GenerateStateTableClassName(string fileName, string stateGroupName)
        {
            // 格式：S_文件名_状态组名
            return $"S_{fileName}_{stateGroupName}";
        }
        
    }

    public class LuaFileBrowserWindow : EditorWindow
    {
        private SerializedProperty targetProperty;
        private Vector2 scrollPosition;
        private Dictionary<string, bool> folderFoldout = new Dictionary<string, bool>();
        private string luaPath;

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        public void Initialize(SerializedProperty property)
        {
            targetProperty = property;
            luaPath = LuaObjectBindProxy.GetLuaPath();
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        private void OnGUI()
        {
            if (targetProperty == null)
            {
                EditorGUILayout.LabelField("未初始化");
                return;
            }

            if (string.IsNullOrEmpty(luaPath) || !Directory.Exists(luaPath))
            {
                EditorGUILayout.LabelField("Lua目录不存在: " + luaPath);
                return;
            }

            // 文件树
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawLuaFileTree(luaPath, "");

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制Lua文件Tree。
        /// </summary>
        private void DrawLuaFileTree(string rootPath, string relativePath)
        {
            string fullPath = string.IsNullOrEmpty(relativePath) ? rootPath : Path.Combine(rootPath, relativePath);

            if (!Directory.Exists(fullPath))
                return;

            // 获取所有子目录和文件
            string[] directories = Directory.GetDirectories(fullPath);
            string[] files = Directory.GetFiles(fullPath, "*.lua");

            // 显示目录
            foreach (string dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                string newRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);

                // 检查是否已经展开
                if (!folderFoldout.ContainsKey(newRelativePath))
                    folderFoldout[newRelativePath] = false;

                // 显示文件夹图标和名称
                folderFoldout[newRelativePath] = EditorGUILayout.Foldout(folderFoldout[newRelativePath], dirName);

                // 如果展开，显示子目录和文件
                if (folderFoldout[newRelativePath])
                {
                    EditorGUI.indentLevel++;
                    DrawLuaFileTree(rootPath, newRelativePath);
                    EditorGUI.indentLevel--;
                }
            }

            // 显示文件
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileRelativePath = string.IsNullOrEmpty(relativePath) ? fileName : Path.Combine(relativePath, fileName);

                // 检查是否已经选中
                bool isSelected = targetProperty.stringValue == fileRelativePath;

                // 显示文件图标和名称
                EditorGUILayout.BeginHorizontal();

                // 使用按钮来选择文件
                if (GUILayout.Button(fileName, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    targetProperty.stringValue = fileRelativePath;
                    targetProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}