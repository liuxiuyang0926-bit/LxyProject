using UnityEditor;
using UnityEngine;
using System.IO;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(PathBindValue))]
    public class PathBindValueDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var name = property.FindPropertyRelative("name");
            var path = property.FindPropertyRelative("path");

            float y = position.y;
            float x = position.x;
            float height = GetPropertyHeight(property, label);
            float width = position.width - HORIZONTAL_GAP * 2;

            Rect nameRect = new Rect(x, y, Mathf.Min(200, width * 0.4f), height);
            Rect pathRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, Mathf.Max(width - 320, width * 0.4f), height);
            Rect buttonRect = new Rect(pathRect.xMax + HORIZONTAL_GAP, y, Mathf.Min(120, width * 0.2f), height);

            // 绘制名称字段
            EditorGUI.PropertyField(nameRect, name, GUIContent.none);

            // 绘制路径字段
            string pathValue = path.stringValue;
            if (!string.IsNullOrEmpty(pathValue))
            {
                // 检查路径是否存在
                bool pathExists = File.Exists(pathValue) || Directory.Exists(pathValue);
                Color originalColor = GUI.color;
                if (!pathExists)
                {
                    GUI.color = Color.red;
                }

                // 获取文件名和后缀名
                string displayName = Path.GetFileName(pathValue);
                
                // 当有路径时，路径字段占用更多空间
                Rect extendedPathRect = new Rect(pathRect.x, y, position.width - nameRect.width - HORIZONTAL_GAP, height);
                
                // 调整路径按钮的宽度，为删除按钮留出空间
                Rect pathButtonRect = new Rect(extendedPathRect.x, y, extendedPathRect.width - 25, height);
                
                // 使用自定义按钮样式来显示路径
                if (GUI.Button(pathButtonRect, displayName, EditorStyles.textField))
                {
                    // 点击路径时跳转到资源，只选中资源不改变 Inspector 选中状态
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(pathValue);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        // 不设置 Selection.activeObject，保持 Inspector 的选中状态不变
                    }
                }

                GUI.color = originalColor;
                
                // 添加删除按钮
                Rect deleteButtonRect = new Rect(pathButtonRect.xMax + 5, y + (height - 18) / 2, 20, 16);
                if (GUI.Button(deleteButtonRect, "×", EditorStyles.miniButtonRight))
                {
                    path.stringValue = "";
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                // 绘制蓝色背景的拖拽区域
                Color originalColor = GUI.color;
                GUI.color = new Color(0.4f, 0.6f, 1f, 0.8f); // 更深的蓝色，更不透明
                GUI.Box(pathRect, "");
                GUI.color = originalColor;
                
                // 绘制提示文本
                GUI.color = Color.white; // 白色文本，更清晰
                GUI.Label(pathRect, "拖拽资源到这里", EditorStyles.centeredGreyMiniLabel);
                GUI.color = originalColor;
            }

            // 绘制选择按钮
            if (string.IsNullOrEmpty(pathValue))
            {
                if (GUI.Button(buttonRect, "选择资源"))
                {
                    string selectedPath = EditorUtility.OpenFilePanel("选择资源", "Assets", "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        if (selectedPath.StartsWith(Application.dataPath))
                        {
                            string relativePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                            path.stringValue = relativePath;
                            property.serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }

            // 处理拖拽
            HandleDragAndDrop(pathRect, path, property);
        }

        private void HandleDragAndDrop(Rect dropArea, SerializedProperty pathProperty, SerializedProperty property)
        {
            Event current = Event.current;

            switch (current.type)
            {
                case EventType.DragUpdated:
                    if (dropArea.Contains(current.mousePosition))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        current.Use();
                    }
                    break;

                case EventType.DragPerform:
                    if (dropArea.Contains(current.mousePosition))
                    {
                        DragAndDrop.AcceptDrag();
                        
                        // 处理拖拽的路径
                        foreach (string draggedPath in DragAndDrop.paths)
                        {
                            if (draggedPath.StartsWith("Assets/"))
                            {
                                pathProperty.stringValue = draggedPath;
                                property.serializedObject.ApplyModifiedProperties();
                                break;
                            }
                        }
                        
                        // 处理拖拽的对象
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject != null)
                            {
                                string assetPath = AssetDatabase.GetAssetPath(draggedObject);
                                if (!string.IsNullOrEmpty(assetPath))
                                {
                                    pathProperty.stringValue = assetPath;
                                    property.serializedObject.ApplyModifiedProperties();
                                    break;
                                }
                            }
                        }
                        
                        current.Use();
                    }
                    break;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + 4;
        }
    }
} 