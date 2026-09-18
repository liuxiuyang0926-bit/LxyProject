using System.IO;
using Game.Battle.TurnBased.Authoring;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    [CustomEditor(typeof(TurnBasedSkillAsset))]
    public sealed class TurnBasedSkillAssetOdinEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            var asset = (TurnBasedSkillAsset)target;
            SirenixEditorGUI.Title(
                "回合制技能定义",
                "逻辑负责结果，表现负责播放；本资产负责组合二者。",
                TextAlignment.Left,
                true,
                true);

            base.OnInspectorGUI();

            EditorGUILayout.Space(6f);
            DrawValidation(asset);
            DrawWorkflow(asset);
        }

        private static void DrawValidation(TurnBasedSkillAsset asset)
        {
            if (asset.TryValidate(out string error))
            {
                SirenixEditorGUI.InfoMessageBox("技能定义校验通过。", true);
            }
            else
            {
                SirenixEditorGUI.WarningMessageBox(error, true);
            }
        }

        private void DrawWorkflow(TurnBasedSkillAsset asset)
        {
            SirenixEditorGUI.BeginBox("策划工作流", false);
            try
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(asset.Logic == null))
                    {
                        if (GUILayout.Button("打开技能逻辑"))
                        {
                            TurnBasedLogicEditorWindow.Open(asset.Logic);
                        }
                    }
                    using (new EditorGUI.DisabledScope(asset.Expression == null))
                    {
                        if (GUILayout.Button("打开技能表现"))
                        {
                            TurnBasedExpressionEditorWindow.Open(asset.Expression);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("保存并校验"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(asset);
                        AssetDatabase.SaveAssets();
                        if (!asset.TryValidate(out string error))
                        {
                            Debug.LogError("[TurnBasedBattle] 技能定义校验失败：" + error, asset);
                        }
                        else
                        {
                            Debug.Log("[TurnBasedBattle] 技能定义校验通过。", asset);
                        }
                    }

                    if (GUILayout.Button("定位默认战斗定义"))
                    {
                        Object definition = AssetDatabase.LoadAssetAtPath<Object>(
                            TurnBasedSkillAssetGenerator.DefinitionPath);
                        Selection.activeObject = definition;
                        if (definition != null)
                        {
                            EditorGUIUtility.PingObject(definition);
                        }
                    }

                    if (GUILayout.Button("查看策划手册"))
                    {
                        string path = Path.GetFullPath(
                            "Packages/LxyGame/LxyGame.Logic/Documentation~/" +
                            "TurnBasedSkillAuthoringGuide.md");
                        EditorUtility.RevealInFinder(path);
                    }
                }
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }
        }
    }
}
