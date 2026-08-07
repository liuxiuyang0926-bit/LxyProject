using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object; // For Type
using System.Linq;
using Game;
using StateControl.Runtime; // Added for .Skip() and .Select()

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(BindValueCollection))]
    public class BindValueCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;

        private ReorderableList list;

        // 静态字典：用于存储每个ObjectBinder的ReorderableList，便于外部访问
        private static Dictionary<int, ReorderableList> _listCache = new Dictionary<int, ReorderableList>();

        // 高亮标记相关
        private static ObjectBinder _highlightBinder;
        private static bool _highlightIsFieldBind;
        private static int _highlightIndex = -1;
        private static double _highlightTime;
        private const double HIGHLIGHT_DURATION = 2.0; // 高亮持续2秒

        // 用于控制重绘的标记
        private static bool _isRepaintScheduled = false;

        // 清理缓存的回调
        static BindValueCollectionDrawer()
        {
            // 编辑器进入播放模式或退出时清理缓存
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            {
                CleanupCache();
            }
        }

        private static void CleanupCache()
        {
            // 清理无效的缓存条目
            _listCache.Clear();
        }

        private ReorderableList GetList(SerializedProperty property)
        {
            // 获取缓存key（使用targetObject的instanceID + property path）
            int cacheKey = property.serializedObject.targetObject.GetInstanceID();
            string propertyPath = property.propertyPath;
            int fullKey = cacheKey.GetHashCode() ^ propertyPath.GetHashCode();

            if (list == null)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.elementHeight = 21;
                list.drawElementCallback = DrawElement;
                list.drawHeaderCallback = DrawHeader;
                list.onAddDropdownCallback = OnAddElement;
                list.onRemoveCallback = OnRemoveElement;

                // 存储到静态字典
                _listCache[fullKey] = list;
            }
            else
            {
                list.serializedProperty = property;
                _listCache[fullKey] = list;
            }
            return list;
        }

        /// <summary>
        /// 静态方法：设置指定ObjectBinder的指定列表的选中索引
        /// </summary>
        /// <param name="binder">ObjectBinder实例</param>
        /// <param name="isFieldBind">true为fieldBindValues，false为bindValues</param>
        /// <param name="index">要选中的索引</param>
        public static void SetSelectedIndex(ObjectBinder binder, bool isFieldBind, int index)
        {
            if (binder == null) return;

            // 记录高亮信息
            _highlightBinder = binder;
            _highlightIsFieldBind = isFieldBind;
            _highlightIndex = index;
            _highlightTime = EditorApplication.timeSinceStartup;

            int cacheKey = binder.GetInstanceID();
            string propertyPath = isFieldBind ? "fieldBindValues.binds" : "bindValues.binds";
            int fullKey = cacheKey.GetHashCode() ^ propertyPath.GetHashCode();

            if (_listCache.TryGetValue(fullKey, out ReorderableList list))
            {
                list.Select(index);
            }

            // 安排单次重绘（避免无限循环）
            ScheduleRepaint();

            // 使用 EditorApplication.update 而不是递归的 delayCall
            EditorApplication.update -= CheckClearHighlight;
            EditorApplication.update += CheckClearHighlight;
        }

        private static void CheckClearHighlight()
        {
            if (_highlightBinder == null)
            {
                EditorApplication.update -= CheckClearHighlight;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - _highlightTime;
            if (elapsed >= HIGHLIGHT_DURATION)
            {
                ClearHighlight();
                EditorApplication.update -= CheckClearHighlight;

                // 清除后触发重绘，让箭头消失（不使用SetDirty）
                RepaintInspector();
            }
            else
            {
                // 在高亮期间，触发重绘以保持箭头显示（但不标记为已修改）
                RepaintInspector();
            }
        }

        private static void ClearHighlight()
        {
            _highlightBinder = null;
            _highlightIndex = -1;
            _isRepaintScheduled = false;
        }

        private static void ScheduleRepaint()
        {
            if (_isRepaintScheduled) return;
            _isRepaintScheduled = true;

            EditorApplication.delayCall += () =>
            {
                RepaintInspector();
                _isRepaintScheduled = false;
            };
        }

        /// <summary>
        /// 触发 Inspector 重绘，但不标记对象为已修改
        /// </summary>
        private static void RepaintInspector()
        {
            // 使用 Unity 内部 API 重绘所有视图，不会标记对象为已修改
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
                return 40;

            float height = base.GetPropertyHeight(property, label) + 60;
            var bindValues = property.FindPropertyRelative("binds");
            for (int i = 0; i < bindValues.arraySize; i++)
                height += EditorGUI.GetPropertyHeight(bindValues.GetArrayElementAtIndex(i)) + VERTICAL_GAP;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.HelpBox(position, "不可多选", MessageType.Warning);
                return;
            }

            var list = GetList(property.FindPropertyRelative("binds"));
            list.DoList(position);
        }

        private void OnAddElement(Rect rect, ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            int index = bindValues.arraySize > 0 ? bindValues.arraySize : 0;
            bindValues.arraySize++;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void OnRemoveElement(ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            AskRemoveVariable(bindValues, list.index);
        }

        private void DrawHeader(Rect rect)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width - 120, rect.height);
            Rect buttonRect = new Rect(rect.x + rect.width - 120, rect.y, 100, rect.height);

            // 显示引导节点计数（仅 bindValues 列表）
            string headerText = "绑定";
            if (list != null && list.serializedProperty != null && list.serializedProperty.propertyPath == "bindValues.binds")
            {
                var binder = list.serializedProperty.serializedObject.targetObject as ObjectBinder;
                if (binder != null && binder.guideNodeKeys != null && binder.guideNodeKeys.Count > 0)
                {
                    headerText = $"绑定  <color=#33CC33>[引导节点: {binder.guideNodeKeys.Count}]</color>";
                    GUIStyle richStyle = new GUIStyle(GUI.skin.label) { richText = true };
                    GUI.Label(labelRect, headerText, richStyle);
                }
                else
                {
                    GUI.Label(labelRect, headerText);
                }
            }
            else
            {
                GUI.Label(labelRect, headerText);
            }

            if (GUI.Button(buttonRect, "自动#绑定"))
            {
                AutoBindComponents();
            }

            Rect clearButtonRect = new Rect(rect.x + rect.width - 230, rect.y, 100, rect.height);
            if (GUI.Button(clearButtonRect, "清空所有绑定"))
            {
                ClearAllBindings();
            }
        }

        private void AutoBindComponents()
        {
            var bindValues = list.serializedProperty;
            var targetObject = bindValues.serializedObject.targetObject as MonoBehaviour;
            if (targetObject == null) return;

            var existingNames = new HashSet<string>();
            for (int i = 0; i < bindValues.arraySize; i++)
            {
                var variable = bindValues.GetArrayElementAtIndex(i);
                var name = variable.FindPropertyRelative("name").stringValue;
                if (!string.IsNullOrEmpty(name))
                {
                    existingNames.Add(name);
                }
            }

            var components = new Dictionary<string, Object>();
            ObjectBinder curObjectBinder = targetObject.GetComponent<ObjectBinder>();
            FindComponentsWithPrefix(targetObject.transform, components, curObjectBinder);
            // 默认绑定上根节点的状态控制器
            var stateController = targetObject.GetComponent<StateController>();
            if (stateController != null && components.Values.All(c => c != stateController))
            {
                components["RootStateController"] = stateController;
            }
            
            // 默认绑定上根节点的动画控制器
            //var animController = targetObject.GetComponent<AnimationController>();
            //if (animController != null && components.Values.All(c => c != animController))
            //{
            //    components["RootAnimationController"] = animController;
            //}

            bindValues.serializedObject.Update();
            foreach (var kvp in components)
            {
                if (existingNames.Contains(kvp.Key)) continue;

                bindValues.arraySize++;
                var variableProperty = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);

                variableProperty.FindPropertyRelative("name").stringValue = kvp.Key;
                variableProperty.FindPropertyRelative("objectValue").objectReferenceValue = kvp.Value;
                variableProperty.FindPropertyRelative("bindValueType").enumValueIndex = (int)BindValueType.Object;
            }
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 对外全局方法：为指定的GameObject执行自动绑定
        /// </summary>
        /// <param name="inputGameObject">要执行自动绑定的GameObject</param>
        /// <returns>是否成功执行自动绑定</returns>
        /// 
        private static ReorderableList AutoGetList(SerializedProperty property)
        {
            ReorderableList list = null;
            if (list == null)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.elementHeight = 21;
            }
            else
            {
                list.serializedProperty = property;
            }
            return list;
        }
        public static bool ExportAutoBindComponents(GameObject inputGameObject, string luaFilePath = null)
        {
            if (inputGameObject == null)
            {
                Debug.LogError("ExportAutoBindComponents: inputGameObject is null");
                return false;
            }

            var objectBinder = inputGameObject.GetComponent<ObjectBinder>();
            if (objectBinder == null)
            {
                Debug.LogError($"ExportAutoBindComponents: GameObject {inputGameObject.name} 没有ObjectBinder组件");
                return false;
            }

            try
            {
                // 自动填充LuaFileReference
                if (objectBinder.lua == null)
                {
                    objectBinder.lua = new LuaFileReference();
                }

                if (string.IsNullOrEmpty(objectBinder.lua.FilePath))
                {
                    // 如果传入了luaFilePath参数，使用传入的路径
                    if (!string.IsNullOrEmpty(luaFilePath))
                    {
                        // 将完整路径转换为相对路径
                        string relativePath = ConvertToRelativePath(luaFilePath);
                        objectBinder.lua.FilePath = relativePath.Replace("Lua\\", "");
                        Debug.Log($"ExportAutoBindComponents: 使用传入的Lua文件路径: {relativePath}");
                    }
                }

                // 获取bindValues的SerializedProperty
                var serializedObject = new SerializedObject(objectBinder);
                var bindValuesProperty = serializedObject.FindProperty("bindValues");
                if (bindValuesProperty == null)
                {
                    Debug.LogError("ExportAutoBindComponents: 无法找到bindValues属性");
                    return false;
                }

                // 获取binds数组的SerializedProperty
                var bindsProperty = bindValuesProperty.FindPropertyRelative("binds");
                if (bindsProperty == null)
                {
                    Debug.LogError("ExportAutoBindComponents: 无法找到bindValues.binds属性");
                    return false;
                }

                // 获取现有的绑定名称
                var existingNames = new HashSet<string>();
                var existingObjects = new List<UnityEngine.Object>();
                for (int i = 0; i < bindsProperty.arraySize; i++)
                {
                    var variable = bindsProperty.GetArrayElementAtIndex(i);
                    var name = variable.FindPropertyRelative("name").stringValue;
                    var unityObject = variable.FindPropertyRelative("objectValue").objectReferenceValue;
                    if (unityObject != null)
                        existingObjects.Add(unityObject);

                    if (!string.IsNullOrEmpty(name))
                        existingNames.Add(name);
                }

                // 查找组件
                var components = new Dictionary<string, Object>();
                FindComponentsWithPrefixStatic(inputGameObject.transform, components, objectBinder);

                // 更新绑定
                serializedObject.Update();
                foreach (var kvp in components)
                {
                    if(existingObjects.Contains(kvp.Value))
                        continue;
                    if (existingNames.Contains(kvp.Key)) 
                        continue;
                    bindsProperty.arraySize++;
                    var variableProperty = bindsProperty.GetArrayElementAtIndex(bindsProperty.arraySize - 1);
                    variableProperty.FindPropertyRelative("name").stringValue = kvp.Key;
                    variableProperty.FindPropertyRelative("objectValue").objectReferenceValue = kvp.Value;
                    variableProperty.FindPropertyRelative("bindValueType").enumValueIndex = (int)BindValueType.Object;
                }
                serializedObject.ApplyModifiedProperties();

                // 标记为已修改
                EditorUtility.SetDirty(objectBinder);
                AssetDatabase.SaveAssets();
                // Debug.Log($"ExportAutoBindComponents: 成功为 {inputGameObject.name} 执行自动绑定，添加了 {components.Count - existingNames.Count} 个新绑定");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ExportAutoBindComponents: 执行自动绑定时发生错误: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将完整路径转换为相对路径
        /// </summary>
        private static string ConvertToRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "";

            // 获取项目根目录
            string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);

            // 如果路径是绝对路径且在项目内
            if (fullPath.StartsWith(projectRoot))
            {
                string relativePath = fullPath.Substring(projectRoot.Length + 1);

                // 如果路径以Lua/UI开头，提取UI后面的部分
                if (relativePath.StartsWith("Lua/"))
                {
                    return relativePath.Substring(4); // 移除"Lua/"前缀，返回如"UI/Main/UIMainMenu.lua"
                }

                return relativePath;
            }

            // 如果已经是相对路径，直接返回
            return fullPath;
        }

        /// <summary>
        /// 静态版本的FindComponentsWithPrefix方法
        /// </summary>
        private static void FindComponentsWithPrefixStatic(Transform currentTransform, Dictionary<string, Object> components, ObjectBinder curObjectBinder)
        {
            // Process the current transform's GameObject if its name matches the pattern
            string[] parts = ParseTags(currentTransform.name);
            if (parts.Length > 1)
            {
                string baseName = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string componentTypeName = parts[i];
                    Object foundObject = null;
                    foundObject = LuaObjectBindEditorProxy.GetComponentAbbr(currentTransform.gameObject, componentTypeName);
                    if (foundObject != null)
                    {
                        string newKey = $"{componentTypeName}{baseName}";
                        if (!components.ContainsKey(newKey))
                        {
                            components[newKey] = foundObject;
                        }
                        continue;
                    }

                    if (componentTypeName.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                    {
                        foundObject = currentTransform.gameObject;
                    }
                    else if (componentTypeName.Equals("Transform", StringComparison.OrdinalIgnoreCase))
                    {
                        foundObject = currentTransform;
                    }
                    else
                    {
                        // Try to get the type using reflection
                        Type componentType = Type.GetType($"UnityEngine.UI.{componentTypeName}, UnityEngine.UI");
                        if (componentType == null)
                        {
                            componentType = Type.GetType($"TMPro.{componentTypeName}, Unity.TextMeshPro");
                        }
                        if (componentType == null)
                        {
                            componentType = Type.GetType($"UnityEngine.{componentTypeName}, UnityEngine");
                        }
                        if (componentType == null)
                        {
                            // Fallback for custom components or components in other assemblies
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                componentType = assembly.GetType(componentTypeName);
                                if (componentType != null)
                                {
                                    break;
                                }
                            }
                        }

                        if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                        {
                            foundObject = currentTransform.GetComponent(componentType);
                            // Debug.Log($"FindComponentsWithPrefixStatic: 通过反射找到组件类型 {componentType.Name}");
                        }
                        else
                        {
                            Debug.Log($"FindComponentsWithPrefixStatic: 无法找到组件类型 '{componentTypeName}'");
                        }
                    }

                    if (foundObject != null)
                    {
                        string newKey = $"{componentTypeName}{baseName}";
                        if (!components.ContainsKey(newKey))
                        {
                            components[newKey] = foundObject;
                            Debug.Log($"FindComponentsWithPrefixStatic: 添加组件绑定 '{newKey}' -> {foundObject.GetType().Name}");
                        }
                    }
                    else
                    {
                        Debug.Log($"FindComponentsWithPrefixStatic: 未找到组件 '{componentTypeName}'");
                    }
                }
            }
            else
            {
                // 显示所有GameObject名称，帮助用户了解需要重命名的对象
                var allComponents = currentTransform.gameObject.GetComponents<Component>();
                var componentNames = string.Join(", ", allComponents.Select(c => c.GetType().Name));
                // Debug.Log($"FindComponentsWithPrefixStatic: GameObject '{currentTransform.name}' 不符合绑定模式，包含组件: [{componentNames}]");
            }

            foreach (Transform child in currentTransform)
            {
                //没有ObjectBinder，才继续查找子物体，避免父子的绑定混在一起
                child.parent.TryGetComponent<ObjectBinder>(out var objBinder);
                if (objBinder != null && objBinder != curObjectBinder)
                    continue;
                FindComponentsWithPrefixStatic(child, components, curObjectBinder);
            }
        }

        /// <summary>
        /// 根据标签查找组件
        /// </summary>
        private static Object FindComponentByTag(GameObject gameObject, string tag)
        {
            // 这里可以根据需要扩展查找逻辑
            // 目前实现基础的组件查找
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component.GetType().Name.ToLower().Contains(tag.ToLower()))
                {
                    return component;
                }
            }
            return null;
        }

        private void ClearAllBindings()
        {
            var bindValues = list.serializedProperty;
            bindValues.arraySize = 0;
            bindValues.serializedObject.ApplyModifiedProperties();
        }
        public static string[] ParseTags(string input)
        {
            List<string> tags = new List<string>();
            HashSet<string> uniqueTags = new HashSet<string>();
            string pattern = @"<([^>]+)>|#([^#]+)";
            MatchCollection matches = Regex.Matches(input, pattern);

            int lastMatchEnd = 0;
            bool firstNonMatchingAdded = false;

            foreach (Match match in matches)
            {
                // Check for a non-matching segment before the current match
                if (!firstNonMatchingAdded && match.Index > lastMatchEnd)
                {
                    string nonMatchingSegment = input.Substring(lastMatchEnd, match.Index - lastMatchEnd);
                    tags.Add(nonMatchingSegment);
                    firstNonMatchingAdded = true;
                }

                string tag = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                if (uniqueTags.Add(tag)) // Add to HashSet and check if it was added
                {
                    tags.Add(tag);
                }

                lastMatchEnd = match.Index + match.Length;
            }

            // Check for any remaining non-matching segment after the last match
            if (!firstNonMatchingAdded && lastMatchEnd < input.Length)
            {
                string nonMatchingSegment = input.Substring(lastMatchEnd);
                tags.Insert(0, nonMatchingSegment); // Ensure it's added first
            }

            return tags.ToArray();
        }

        private void FindComponentsWithPrefix(Transform currentTransform, Dictionary<string, Object> components, ObjectBinder curObjectBinder)
        {
            // Process the current transform's GameObject if its name matches the pattern
            //string[] parts = currentTransform.name.Split('#');
            string[] parts = ParseTags(currentTransform.name);
            // Debug.Log("name: " + currentTransform.name);
            // Debug.Log(string.Join(':', parts));
            if (parts.Length > 1)
            {
                string baseName = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string componentTypeName = parts[i];
                    Object foundObject = null;

                    foundObject = LuaObjectBindEditorProxy.GetComponentAbbr(currentTransform.gameObject, componentTypeName);
                    if (foundObject != null)
                    {
                        string newKey = $"{componentTypeName}{baseName}";
                        if (!components.ContainsKey(newKey))
                        {
                            components[newKey] = foundObject;
                        }
                        continue;
                    }

                    if (componentTypeName.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
                    {
                        foundObject = currentTransform.gameObject;
                    }
                    else if (componentTypeName.Equals("Transform", StringComparison.OrdinalIgnoreCase))
                    {
                        foundObject = currentTransform;
                    }
                    else
                    {
                        // Try to get the type using reflection
                        Type componentType = Type.GetType($"UnityEngine.UI.{componentTypeName}, UnityEngine.UI");
                        if (componentType == null)
                        {
                            componentType = Type.GetType($"TMPro.{componentTypeName}, Unity.TextMeshPro");
                        }
                        if (componentType == null)
                        {
                            componentType = Type.GetType($"UnityEngine.{componentTypeName}, UnityEngine");
                        }
                        if (componentType == null)
                        {
                            // Fallback for custom components or components in other assemblies
                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                componentType = assembly.GetType(componentTypeName);
                                if (componentType != null)
                                {
                                    break;
                                }
                            }
                        }

                        if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                        {
                            foundObject = currentTransform.GetComponent(componentType);
                        }
                    }

                    if (foundObject != null)
                    {
                        string newKey = $"{componentTypeName}{baseName}";
                        if (!components.ContainsKey(newKey))
                        {
                            components[newKey] = foundObject;
                        }
                    }
                }
            }

            foreach (Transform child in currentTransform)
            {
                //没有ObjectBinder，才继续查找子物体，避免父子的绑定混在一起
                child.parent.TryGetComponent<ObjectBinder>(out var objBinder);
                if (objBinder != null && objBinder != curObjectBinder)
                    continue;
                FindComponentsWithPrefix(child, components, curObjectBinder);
            }
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var bindValues = list.serializedProperty;
            if (index < 0 || index >= bindValues.arraySize)
                return;

            var variable = bindValues.GetArrayElementAtIndex(index);

            // 检查是否需要显示高亮标记
            bool shouldShowHighlight = ShouldShowHighlight(bindValues, index);

            // 检查是否为引导节点（仅 bindValues 列表）
            bool isGuideNode = false;
            ObjectBinder currentBinder = bindValues.serializedObject.targetObject as ObjectBinder;
            string bindName = variable.FindPropertyRelative("name")?.stringValue;
            bool isBindValuesList = bindValues.propertyPath == "bindValues.binds";
            if (isBindValuesList && currentBinder != null && !string.IsNullOrEmpty(bindName))
            {
                isGuideNode = currentBinder.guideNodeKeys != null && currentBinder.guideNodeKeys.Contains(bindName);
            }

            float x = rect.x;
            float y = rect.y + 2;
            float width = rect.width - 40;
            float height = rect.height;

            // 如果需要高亮，在左侧预留空间显示箭头
            if (shouldShowHighlight)
            {
                Rect arrowRect = new Rect(x, y, 20, height);
                GUIStyle arrowStyle = new GUIStyle(EditorStyles.boldLabel);
                arrowStyle.normal.textColor = new Color(1f, 0.5f, 0f); // 橙色
                arrowStyle.fontSize = 14;
                GUI.Label(arrowRect, "→", arrowStyle);

                x += 20; // 向右偏移，为箭头腾出空间
                width -= 20;

                // 移除了导致无限循环的重绘逻辑
                // 现在重绘由 CheckClearHighlight 在 EditorApplication.update 中控制
            }

            // 引导节点：左侧显示绿色标记
            if (isGuideNode)
            {
                Rect guideIconRect = new Rect(x, y, 16, height);
                GUIStyle guideStyle = new GUIStyle(EditorStyles.boldLabel);
                guideStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f); // 绿色
                guideStyle.fontSize = 12;
                GUI.Label(guideIconRect, "G", guideStyle);

                x += 16;
                width -= 16;
            }

            // 右键菜单：引导节点标记（仅 bindValues 列表）
            if (isBindValuesList && Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                GenericMenu menu = new GenericMenu();

                if (!string.IsNullOrEmpty(bindName))
                {
                    if (isGuideNode)
                    {
                        menu.AddItem(new GUIContent("取消引导节点标记"), false, () =>
                        {
                            Undo.RecordObject(currentBinder, "Remove Guide Node");
                            currentBinder.guideNodeKeys.Remove(bindName);
                            EditorUtility.SetDirty(currentBinder);
                        });
                    }
                    else
                    {
                        menu.AddItem(new GUIContent("标记为引导节点"), false, () =>
                        {
                            Undo.RecordObject(currentBinder, "Add Guide Node");
                            if (currentBinder.guideNodeKeys == null)
                                currentBinder.guideNodeKeys = new List<string>();
                            if (!currentBinder.guideNodeKeys.Contains(bindName))
                                currentBinder.guideNodeKeys.Add(bindName);
                            EditorUtility.SetDirty(currentBinder);
                        });
                    }
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("请先填写绑定名称"));
                }

                menu.ShowAsContext();
            }

            Rect variableRect = new Rect(x, y, width, height);
            EditorGUI.PropertyField(variableRect, variable, GUIContent.none);

            var buttonLeftRect = new Rect(variableRect.xMax + HORIZONTAL_GAP, y - 1, 18, 18);
            var buttonRightRect = new Rect(buttonLeftRect.xMax, y - 1, 18, 18);

            if (GUI.Button(buttonLeftRect, new GUIContent("+"), EditorStyles.miniButtonLeft))
            {
                DuplicateVariable(bindValues, index);
            }
            if (GUI.Button(buttonRightRect, new GUIContent("-"), EditorStyles.miniButtonRight))
            {
                AskRemoveVariable(bindValues, index);
            }
        }

        /// <summary>
        /// 检查当前元素是否应该显示高亮标记
        /// </summary>
        private bool ShouldShowHighlight(SerializedProperty bindValues, int index)
        {
            if (_highlightBinder == null || _highlightIndex != index)
                return false;

            // 检查时间是否过期
            if (EditorApplication.timeSinceStartup - _highlightTime >= HIGHLIGHT_DURATION)
                return false;

            // 检查是否是同一个binder
            var currentBinder = bindValues.serializedObject.targetObject as ObjectBinder;
            if (currentBinder != _highlightBinder)
                return false;

            // 检查是否是正确的列表类型（bindValues 或 fieldBindValues）
            string propertyPath = bindValues.propertyPath;
            bool isFieldBind = propertyPath.Contains("fieldBindValues");

            return isFieldBind == _highlightIsFieldBind;
        }

        /// <summary>
        /// 公共方法：供 FieldBindValueCollectionDrawer 调用，检查是否应该高亮
        /// </summary>
        public static bool ShouldShowHighlightForFieldBind(ObjectBinder binder, int index)
        {
            if (_highlightBinder == null || _highlightIndex != index)
                return false;

            // 检查时间是否过期
            if (EditorApplication.timeSinceStartup - _highlightTime >= HIGHLIGHT_DURATION)
                return false;

            // 检查是否是同一个binder
            if (binder != _highlightBinder)
                return false;

            // 检查是否是 fieldBindValues
            return _highlightIsFieldBind;
        }

        protected virtual void AskRemoveVariable(SerializedProperty bindValues, int index)
        {
            if (EditorUtility.DisplayDialog("删除绑定", "确定要删除这个绑定吗？", "确定", "取消"))
            {
                RemoveVariable(bindValues, index);
            }
        }

        protected virtual void RemoveVariable(SerializedProperty bindValues, int index)
        {
            // 删除绑定时，同步清理 guideNodeKeys
            if (bindValues.propertyPath == "bindValues.binds")
            {
                var binder = bindValues.serializedObject.targetObject as ObjectBinder;
                if (binder != null && binder.guideNodeKeys != null)
                {
                    var variable = bindValues.GetArrayElementAtIndex(index);
                    string name = variable.FindPropertyRelative("name")?.stringValue;
                    if (!string.IsNullOrEmpty(name) && binder.guideNodeKeys.Contains(name))
                    {
                        Undo.RecordObject(binder, "Remove Guide Node on Delete");
                        binder.guideNodeKeys.Remove(name);
                    }
                }
            }

            bindValues.DeleteArrayElementAtIndex(index);
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DuplicateVariable(SerializedProperty bindValues, int index)
        {
            bindValues.arraySize++;
            var source = bindValues.GetArrayElementAtIndex(index);
            var target = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);

            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue + "_Copy";
            target.FindPropertyRelative("value").stringValue = source.FindPropertyRelative("value").stringValue;

            bindValues.serializedObject.ApplyModifiedProperties();
        }
    }
}