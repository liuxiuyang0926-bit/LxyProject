using Game.Battle.Config;
using Game.Battle.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public sealed class BattleSkillEditorWindow : EditorWindow
    {
        private BattleConfigDatabase database;
        private SerializedObject serializedDatabase;
        private Vector2 scroll;
        private int selectedIndex;

        /// <summary>
        /// 执行打开相关逻辑。
        /// </summary>
        [MenuItem("工具/战斗/技能编辑器", false, 10)]
        private static void Open()
        {
            GetWindow<BattleSkillEditorWindow>("技能编辑器")
                .Show();
        }

        /// <summary>
        /// 在组件启用时建立运行时关联。
        /// </summary>
        private void OnEnable()
        {
            SetDatabase(
                BattleConfigEditorUtility.LoadOrCreateDatabase());
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        private void OnGUI()
        {
            DrawDatabaseField();
            if (database == null || serializedDatabase == null)
            {
                EditorGUILayout.HelpBox(
                    "请选择或创建 BattleConfigDatabase。",
                    MessageType.Info);
                return;
            }

            serializedDatabase.Update();
            SerializedProperty skills =
                serializedDatabase.FindProperty("skills");

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            DrawSkillList(skills);
            DrawSelectedSkill(skills);
            EditorGUILayout.EndHorizontal();

            if (serializedDatabase.ApplyModifiedProperties())
            {
                BattleConfigEditorUtility.Save(database);
            }

            DrawBottomButtons(skills);
        }

        /// <summary>
        /// 绘制数据库Field。
        /// </summary>
        private void DrawDatabaseField()
        {
            EditorGUI.BeginChangeCheck();
            BattleConfigDatabase next =
                (BattleConfigDatabase)EditorGUILayout.ObjectField(
                    "配置数据库",
                    database,
                    typeof(BattleConfigDatabase),
                    false);
            if (EditorGUI.EndChangeCheck())
            {
                SetDatabase(next);
            }
        }

        /// <summary>
        /// 绘制技能列表。
        /// </summary>
        private void DrawSkillList(SerializedProperty skills)
        {
            EditorGUILayout.BeginVertical(
                GUILayout.Width(210f));
            EditorGUILayout.LabelField(
                $"技能列表（{skills.arraySize}）",
                EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < skills.arraySize; index++)
            {
                SerializedProperty item =
                    skills.GetArrayElementAtIndex(index);
                int id = item.FindPropertyRelative("id").intValue;
                string displayName = item
                    .FindPropertyRelative("displayName")
                    .stringValue;
                bool selected = selectedIndex == index;
                if (GUILayout.Toggle(
                        selected,
                        $"{id}  {displayName}",
                        "Button"))
                {
                    selectedIndex = index;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制选中项技能。
        /// </summary>
        private void DrawSelectedSkill(SerializedProperty skills)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(
                "技能参数",
                EditorStyles.boldLabel);
            if (skills.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "点击下方“新增技能”创建配置。",
                    MessageType.Info);
            }
            else
            {
                selectedIndex = Mathf.Clamp(
                    selectedIndex,
                    0,
                    skills.arraySize - 1);
                DrawSkill(
                    skills.GetArrayElementAtIndex(selectedIndex));
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制技能。
        /// </summary>
        private static void DrawSkill(SerializedProperty skill)
        {
            EditorGUILayout.PropertyField(
                skill.FindPropertyRelative("id"),
                new GUIContent("技能 ID"));
            EditorGUILayout.PropertyField(
                skill.FindPropertyRelative("displayName"),
                new GUIContent("显示名称"));
            SerializedProperty totalFrames =
                skill.FindPropertyRelative("totalFrames");
            EditorGUILayout.PropertyField(
                totalFrames,
                new GUIContent("总帧数"));
            EditorGUILayout.PropertyField(
                skill.FindPropertyRelative("cooldownFrames"),
                new GUIContent("冷却帧数"));

            EditorGUILayout.Space(8f);
            DrawFrameOperations(
                skill.FindPropertyRelative("frameOperations"),
                Mathf.Max(1, totalFrames.intValue));
        }

        /// <summary>
        /// 绘制帧操作。
        /// </summary>
        private static void DrawFrameOperations(
            SerializedProperty operations,
            int totalFrames)
        {
            EditorGUILayout.LabelField(
                $"帧操作时间轴（{operations.arraySize}）",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "同一帧按列表顺序执行。伤害盒使用角色施法朝向作为本地 +X；" +
                "AABB 始终轴对齐，OBB 会叠加角色朝向和配置角度。",
                MessageType.None);

            for (int index = 0;
                 index < operations.arraySize;
                 index++)
            {
                SerializedProperty operation =
                    operations.GetArrayElementAtIndex(index);
                if (DrawFrameOperation(
                        operations,
                        operation,
                        index,
                        totalFrames))
                {
                    return;
                }
            }

            if (GUILayout.Button("＋ 新增帧操作"))
            {
                int index = operations.arraySize;
                operations.InsertArrayElementAtIndex(index);
                ResetFrameOperation(
                    operations.GetArrayElementAtIndex(index),
                    Mathf.Min(4, totalFrames - 1));
            }
        }

        /// <summary>
        /// 绘制帧操作。
        /// </summary>
        private static bool DrawFrameOperation(
            SerializedProperty operations,
            SerializedProperty operation,
            int index,
            int totalFrames)
        {
            SerializedProperty frame =
                operation.FindPropertyRelative("frame");
            SerializedProperty operationType =
                operation.FindPropertyRelative("operationType");
            var type = (BattleSkillOperationType)
                operationType.intValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"#{index + 1}  Frame {frame.intValue}  {type}",
                EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(28f)))
                {
                    operations.MoveArrayElement(index, index - 1);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return true;
                }
            }
            using (new EditorGUI.DisabledScope(
                       index >= operations.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(28f)))
                {
                    operations.MoveArrayElement(index, index + 1);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return true;
                }
            }
            if (GUILayout.Button("×", GUILayout.Width(28f)))
            {
                operations.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }
            EditorGUILayout.EndHorizontal();

            frame.intValue = EditorGUILayout.IntSlider(
                "执行帧",
                frame.intValue,
                0,
                totalFrames - 1);
            EditorGUILayout.PropertyField(
                operationType,
                new GUIContent("操作类型"));
            type = (BattleSkillOperationType)
                operationType.intValue;
            switch (type)
            {
                case BattleSkillOperationType.DamageBox:
                    DrawDamageBox(operation);
                    break;

                case BattleSkillOperationType.ApplyBuffToTarget:
                    EditorGUILayout.PropertyField(
                        operation.FindPropertyRelative("buffId"),
                        new GUIContent("目标 Buff ID"));
                    break;

                case BattleSkillOperationType.DisplaceCaster:
                    DrawRawVector2(
                        operation,
                        "displacementXRaw",
                        "displacementYRaw",
                        "施法者本地位移");
                    break;
            }

            EditorGUILayout.EndVertical();
            return false;
        }

        /// <summary>
        /// 绘制DamageBox。
        /// </summary>
        private static void DrawDamageBox(SerializedProperty operation)
        {
            SerializedProperty hitShape =
                operation.FindPropertyRelative("hitShape");
            EditorGUILayout.PropertyField(
                hitShape,
                new GUIContent("判定形状"));
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative("damage"),
                new GUIContent("伤害"));
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative("buffId"),
                new GUIContent("命中附加 Buff ID", "0 表示不附加"));
            DrawRawVector2(
                operation,
                "offsetXRaw",
                "offsetYRaw",
                "伤害盒本地中心偏移");
            DrawRawVector2(
                operation,
                "halfWidthRaw",
                "halfHeightRaw",
                "伤害盒半尺寸");
            if ((BattleCollisionShape)hitShape.intValue ==
                BattleCollisionShape.Obb)
            {
                EditorGUILayout.PropertyField(
                    operation.FindPropertyRelative(
                        "rotationDegreesRaw"),
                    new GUIContent(
                        "相对旋转 Raw",
                        "10000 = 1 度"));
            }
        }

        /// <summary>
        /// 绘制原始值Vector2。
        /// </summary>
        private static void DrawRawVector2(
            SerializedProperty parent,
            string xName,
            string yName,
            string label)
        {
            EditorGUILayout.LabelField(
                label + "（Raw，10000 = 1）",
                EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                parent.FindPropertyRelative(xName),
                new GUIContent("X"));
            EditorGUILayout.PropertyField(
                parent.FindPropertyRelative(yName),
                new GUIContent("Y"));
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 重置帧操作。
        /// </summary>
        private static void ResetFrameOperation(
            SerializedProperty operation,
            int frame)
        {
            operation.FindPropertyRelative("frame").intValue = frame;
            operation.FindPropertyRelative("operationType").intValue =
                (int)BattleSkillOperationType.DamageBox;
            operation.FindPropertyRelative("hitShape").intValue =
                (int)BattleCollisionShape.Aabb;
            operation.FindPropertyRelative("damage").intValue = 100;
            operation.FindPropertyRelative("buffId").intValue = 0;
            operation.FindPropertyRelative("offsetXRaw").longValue =
                17500L;
            operation.FindPropertyRelative("offsetYRaw").longValue = 0L;
            operation.FindPropertyRelative("halfWidthRaw").longValue =
                17500L;
            operation.FindPropertyRelative("halfHeightRaw").longValue =
                10000L;
            operation.FindPropertyRelative("rotationDegreesRaw").longValue =
                0L;
            operation.FindPropertyRelative("displacementXRaw").longValue =
                0L;
            operation.FindPropertyRelative("displacementYRaw").longValue =
                0L;
        }

        /// <summary>
        /// 绘制BottomButtons。
        /// </summary>
        private void DrawBottomButtons(SerializedProperty skills)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增技能"))
            {
                Undo.RecordObject(database, "Add Battle Skill");
                database.AddDefaultSkill();
                selectedIndex = database.Skills.Count - 1;
                BattleConfigEditorUtility.Save(database);
                serializedDatabase.Update();
            }

            using (new EditorGUI.DisabledScope(skills.arraySize == 0))
            {
                if (GUILayout.Button("删除当前技能"))
                {
                    Undo.RecordObject(database, "Remove Battle Skill");
                    database.RemoveSkillAt(selectedIndex);
                    selectedIndex = Mathf.Max(0, selectedIndex - 1);
                    BattleConfigEditorUtility.Save(database);
                    serializedDatabase.Update();
                }
            }

            if (GUILayout.Button("校验全部配置"))
            {
                ValidateDatabase();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 校验数据库。
        /// </summary>
        private void ValidateDatabase()
        {
            if (database.TryBuildCatalog(out _, out string error))
            {
                Debug.Log("[Battle] 技能与 Buff 配置校验通过。", database);
                ShowNotification(new GUIContent("配置校验通过"));
            }
            else
            {
                Debug.LogError("[Battle] 配置校验失败：" + error, database);
                ShowNotification(new GUIContent("配置校验失败"));
            }
        }

        /// <summary>
        /// 设置数据库。
        /// </summary>
        private void SetDatabase(BattleConfigDatabase value)
        {
            database = value;
            serializedDatabase = database != null
                ? new SerializedObject(database)
                : null;
            selectedIndex = 0;
        }
    }
}
