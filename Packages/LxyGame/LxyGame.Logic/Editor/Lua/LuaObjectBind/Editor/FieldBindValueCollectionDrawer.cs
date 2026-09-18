using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LuaObjectBind.Editor
{ 
    [CustomPropertyDrawer(typeof(FieldBindValueCollection))]
    public class FieldBindValueCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;

        private ReorderableList list;

        // 静态字典：用于存储每个ObjectBinder的ReorderableList，便于外部访问
        private static Dictionary<int, ReorderableList> _listCache = new Dictionary<int, ReorderableList>();

        /// <summary>
        /// 获取列表。
        /// </summary>
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
        /// 静态方法：设置指定ObjectBinder的FieldBindValues列表的选中索引
        /// 复用 BindValueCollectionDrawer 的高亮逻辑
        /// </summary>
        public static void SetSelectedIndex(ObjectBinder binder, int index)
        {
            if (binder == null) return;

            // 调用 BindValueCollectionDrawer 的高亮逻辑
            BindValueCollectionDrawer.SetSelectedIndex(binder, true, index);

            // 同时设置本地的 ReorderableList 选中状态
            int cacheKey = binder.GetInstanceID();
            string propertyPath = "fieldBindValues.binds";
            int fullKey = cacheKey.GetHashCode() ^ propertyPath.GetHashCode();

            if (_listCache.TryGetValue(fullKey, out ReorderableList list))
            {
                list.Select(index);
            }
        }

        /// <summary>
        /// 获取属性高度。
        /// </summary>
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

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.HelpBox(position,"不可多选", MessageType.Warning);
                return;
            }

            var list = GetList(property.FindPropertyRelative("binds"));
            list.DoList(position);
        }

        /// <summary>
        /// 响应加法Element事件。
        /// </summary>
        private void OnAddElement(Rect rect, ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            int index = bindValues.arraySize > 0 ? bindValues.arraySize : 0;
            bindValues.arraySize++;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 响应移除Element事件。
        /// </summary>
        private void OnRemoveElement(ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            AskRemoveVariable(bindValues, list.index);
        }

        /// <summary>
        /// 绘制Header。
        /// </summary>
        private void DrawHeader(Rect rect)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width - 120, rect.height);
            Rect buttonRect = new Rect(rect.x + rect.width - 120, rect.y, 100, rect.height);

            GUI.Label(labelRect, "字段绑定");
            if (GUI.Button(buttonRect, "自动@绑定"))
            {
                AutoBindComponents();
            }

            Rect clearButtonRect = new Rect(rect.x + rect.width - 230, rect.y, 100, rect.height);
            if (GUI.Button(clearButtonRect, "清空所有绑定"))
            {
                ClearAllBindings();
            }
        }

        /// <summary>
        /// 绘制Element。
        /// </summary>
        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var bindValues = list.serializedProperty;
            if (index < 0 || index >= bindValues.arraySize)
                return;

            var variable = bindValues.GetArrayElementAtIndex(index);

            // 检查是否需要显示高亮标记（复用 BindValueCollectionDrawer 的高亮逻辑）
            bool shouldShowHighlight = ShouldShowHighlight(bindValues, index);

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
        /// 检查当前元素是否应该显示高亮标记（复用 BindValueCollectionDrawer 的高亮状态）
        /// </summary>
        private bool ShouldShowHighlight(SerializedProperty bindValues, int index)
        {
            // 通过反射或直接访问 BindValueCollectionDrawer 的高亮状态
            // 这里使用间接方法：检查是否是 fieldBindValues 列表
            var currentBinder = bindValues.serializedObject.targetObject as ObjectBinder;
            if (currentBinder == null)
                return false;

            // 检查 propertyPath 是否包含 fieldBindValues
            string propertyPath = bindValues.propertyPath;
            bool isFieldBind = propertyPath.Contains("fieldBindValues");

            // 只有 fieldBindValues 才在这里高亮
            if (!isFieldBind)
                return false;

            // 调用 BindValueCollectionDrawer 的公共接口来检查高亮状态
            // 由于没有公共接口，我们通过检查时间和静态变量来判断
            // 这里我们需要访问 BindValueCollectionDrawer 的高亮状态
            return BindValueCollectionDrawer.ShouldShowHighlightForFieldBind(currentBinder, index);
        }

        /// <summary>
        /// 执行请求移除变量相关逻辑。
        /// </summary>
        protected virtual void AskRemoveVariable(SerializedProperty bindValues, int index)
        {
            if (EditorUtility.DisplayDialog("删除绑定", "确定要删除这个绑定吗？", "确定", "取消"))
            {
                RemoveVariable(bindValues, index);
            }
        }

        /// <summary>
        /// 移除Variable。
        /// </summary>
        protected virtual void RemoveVariable(SerializedProperty bindValues, int index)
        {
            bindValues.DeleteArrayElementAtIndex(index);
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 执行复制变量相关逻辑。
        /// </summary>
        protected virtual void DuplicateVariable(SerializedProperty bindValues, int index)
        {
            bindValues.arraySize++;
            var source = bindValues.GetArrayElementAtIndex(index);
            var target = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);
            
            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue + "_Copy";
            target.FindPropertyRelative("objectValue").objectReferenceValue = source.FindPropertyRelative("objectValue").objectReferenceValue;
            target.FindPropertyRelative("fieldBindType").enumValueIndex = source.FindPropertyRelative("fieldBindType").enumValueIndex;
            
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        struct BindEnumAndObject
        {
            /// <summary>
            /// 公开的绑定Enum数据。
            /// </summary>
            public FieldBindEnum  BindEnum;
            /// <summary>
            /// 公开的对象值数据。
            /// </summary>
            public GameObject ObjectValue;
        }
        
        /// <summary>
        /// 执行Auto绑定组件相关逻辑。
        /// </summary>
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

            ObjectBinder objectBinder = targetObject.GetComponent<ObjectBinder>();
            var bindEnumAndObjects = new Dictionary<BindEnumAndObject, string>();
            FindBindEnumWithPrefix(targetObject.transform, bindEnumAndObjects, objectBinder);

            bindValues.serializedObject.Update();
            foreach (var kvp in bindEnumAndObjects)
            {
                if (existingNames.Contains(kvp.Value)) continue;
                GameObject curObject = kvp.Key.ObjectValue;
                Object bindObject = null;
                FieldBindEnum fieldBindEnum = kvp.Key.BindEnum;
                object[] attributes = fieldBindEnum.GetType().GetField(fieldBindEnum.ToString()).GetCustomAttributes(typeof(FieldBindTypeAttribute), true);
                if (attributes.Length > 0)
                {
                    FieldBindTypeAttribute filedBindTypeAttribute = attributes[0] as FieldBindTypeAttribute;
                    if (filedBindTypeAttribute != null)
                    {
                        Type canBindType = filedBindTypeAttribute.CanBindObjectType;
                        if (canBindType == typeof(GameObject))
                        {
                            bindObject = curObject.gameObject;
                        }
                        else
                        {
                            //如果canBindType是继承自MonoBehavior
                            if (canBindType.IsSubclassOf(typeof(MonoBehaviour)))
                            {
                                bindObject = curObject.GetComponent(canBindType);
                            }
                        }
                    }
                }
                if(bindObject == null)
                    continue;

                bindValues.arraySize++;
                var variableProperty = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);

                variableProperty.FindPropertyRelative("name").stringValue = kvp.Value;
                variableProperty.FindPropertyRelative("fieldBindType").enumValueIndex = (int)fieldBindEnum;
                variableProperty.FindPropertyRelative("objectValue").objectReferenceValue = bindObject;
            }
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 清空全部绑定。
        /// </summary>
        private void ClearAllBindings()
        {
            var bindValues = list.serializedProperty;
            bindValues.arraySize = 0;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 查找绑定EnumWithPrefix。
        /// </summary>
        private void FindBindEnumWithPrefix(Transform currentTransform, Dictionary<BindEnumAndObject, string> fieldBindEnums, ObjectBinder curObjectBinder)
        {
            // Process the current transform's GameObject if its name matches the pattern
            string[] parts = currentTransform.name.Split('@');
            if (parts.Length > 1)
            {
                string baseName = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string enumName = parts[i];
                    Object foundObject = null;

                    FieldBindEnum fieldBindEnum = LuaObjectBindEditorProxy.GetFieldBindEnumAbbr(enumName);
                    bool hasEnum = fieldBindEnum != FieldBindEnum.None;
                    if (!hasEnum)
                        hasEnum = FieldBindEnum.TryParse(enumName, out fieldBindEnum);
                    if(!hasEnum)
                        continue;
                    fieldBindEnums.Add(new BindEnumAndObject()  { BindEnum = fieldBindEnum, ObjectValue = currentTransform.gameObject }, enumName + baseName);
                }
            }

            foreach (Transform child in currentTransform)
            {
                //没有ObjectBinder，才继续查找子物体，避免父子的绑定混在一起
                child.parent.TryGetComponent<ObjectBinder>(out var objBinder);
                if (objBinder != null && objBinder != curObjectBinder)
                {
                    continue;
                }
                FindBindEnumWithPrefix(child, fieldBindEnums, curObjectBinder);
            }
        }
    }
} 