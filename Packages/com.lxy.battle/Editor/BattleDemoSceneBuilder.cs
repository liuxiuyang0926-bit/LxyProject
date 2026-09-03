using System.Linq;
using Game.Battle.Config;
using Game.Battle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Battle.Editor
{
    public static class BattleDemoSceneBuilder
    {
        private const string DemoScenePath =
            "Assets/Scenes/BattleDemo.unity";

        /// <summary>
        /// 创建Demo场景。
        /// </summary>
        [MenuItem("工具/战斗/创建本地帧同步 Demo 场景", false, 20)]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BattleConfigDatabase database =
                BattleConfigEditorUtility.LoadOrCreateDatabase();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var driverObject = new GameObject("[BattleDriver]");
            BattleDriver driver =
                driverObject.AddComponent<BattleDriver>();
            var serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("database")
                .objectReferenceValue = database;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();

            GameObject ground = GameObject.CreatePrimitive(
                PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera targetCamera = cameraObject.AddComponent<Camera>();
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cameraObject.transform.position = new Vector3(0f, 8f, -8f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                Vector3.zero - cameraObject.transform.position,
                Vector3.up);

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation =
                Quaternion.Euler(45f, -30f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DemoScenePath);
            AddSceneToBuildSettings(DemoScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = driverObject;
            EditorGUIUtility.PingObject(driverObject);
            Debug.Log(
                "[Battle] 已创建本地帧同步 Demo：" + DemoScenePath,
                driverObject);
        }

        /// <summary>
        /// 添加场景To构建设置。
        /// </summary>
        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            if (scenes.Any(item => item.path == scenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = scenes
                .Concat(
                    new[]
                    {
                        new EditorBuildSettingsScene(scenePath, true),
                    })
                .ToArray();
        }
    }
}
