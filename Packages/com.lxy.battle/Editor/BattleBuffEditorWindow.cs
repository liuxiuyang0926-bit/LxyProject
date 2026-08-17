using Game.Battle.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public sealed class BattleBuffEditorWindow : EditorWindow
    {
        private BattleConfigDatabase database;
        private SerializedObject serializedDatabase;
        private Vector2 scroll;
        private int selectedIndex;

        [MenuItem("工具/战斗/Buff编辑器", false, 11)]
        private static void Open()
        {
            GetWindow<BattleBuffEditorWindow>("Buff编辑器")
                .Show();
        }

        private void OnEnable()
        {
            SetDatabase(
                BattleConfigEditorUtility.LoadOrCreateDatabase());
        }

        private void OnGUI()
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

            if (database == null || serializedDatabase == null)
            {
                EditorGUILayout.HelpBox(
                    "请选择或创建 BattleConfigDatabase。",
                    MessageType.Info);
                return;
            }

            serializedDatabase.Update();
            SerializedProperty buffs =
                serializedDatabase.FindProperty("buffs");
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            DrawBuffList(buffs);
            DrawSelectedBuff(buffs);
            EditorGUILayout.EndHorizontal();
            if (serializedDatabase.ApplyModifiedProperties())
            {
                BattleConfigEditorUtility.Save(database);
            }

            DrawBottomButtons(buffs);
        }

        private void DrawBuffList(SerializedProperty buffs)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            EditorGUILayout.LabelField(
                $"Buff列表（{buffs.arraySize}）",
                EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < buffs.arraySize; index++)
            {
                SerializedProperty item =
                    buffs.GetArrayElementAtIndex(index);
                int id = item.FindPropertyRelative("id").intValue;
                string displayName = item
                    .FindPropertyRelative("displayName")
                    .stringValue;
                if (GUILayout.Toggle(
                        selectedIndex == index,
                        $"{id}  {displayName}",
                        "Button"))
                {
                    selectedIndex = index;
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedBuff(SerializedProperty buffs)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(
                "Buff参数",
                EditorStyles.boldLabel);
            if (buffs.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "点击下方“新增 Buff”创建配置。",
                    MessageType.Info);
            }
            else
            {
                selectedIndex = Mathf.Clamp(
                    selectedIndex,
                    0,
                    buffs.arraySize - 1);
                EditorGUILayout.PropertyField(
                    buffs.GetArrayElementAtIndex(selectedIndex),
                    true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawBottomButtons(SerializedProperty buffs)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增 Buff"))
            {
                Undo.RecordObject(database, "Add Battle Buff");
                database.AddDefaultBuff();
                selectedIndex = database.Buffs.Count - 1;
                BattleConfigEditorUtility.Save(database);
                serializedDatabase.Update();
            }

            using (new EditorGUI.DisabledScope(buffs.arraySize == 0))
            {
                if (GUILayout.Button("删除当前 Buff"))
                {
                    Undo.RecordObject(database, "Remove Battle Buff");
                    database.RemoveBuffAt(selectedIndex);
                    selectedIndex = Mathf.Max(0, selectedIndex - 1);
                    BattleConfigEditorUtility.Save(database);
                    serializedDatabase.Update();
                }
            }

            if (GUILayout.Button("校验全部配置"))
            {
                if (database.TryBuildCatalog(out _, out string error))
                {
                    Debug.Log(
                        "[Battle] 技能与 Buff 配置校验通过。",
                        database);
                    ShowNotification(new GUIContent("配置校验通过"));
                }
                else
                {
                    Debug.LogError(
                        "[Battle] 配置校验失败：" + error,
                        database);
                    ShowNotification(new GUIContent("配置校验失败"));
                }
            }
            EditorGUILayout.EndHorizontal();
        }

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
