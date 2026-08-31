#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LuaObjectBind;
using Lxy.UIEffectGenerator.Editor;
using UnityEditor;
using UnityEngine;

namespace LxyDemo.UIFramework.Editor
{
    [InitializeOnLoad]
    internal static class UIEffectLxyProjectAdapterRegistration
    {
        private static readonly IUIEffectProjectAdapter Adapter =
            new UIEffectLxyProjectAdapter();

        static UIEffectLxyProjectAdapterRegistration()
        {
            UIEffectProjectAdapterRegistry.Register(Adapter, 100);
        }
    }

    internal sealed class UIEffectLxyProjectAdapter :
        IUIEffectProjectAdapter
    {
        public string Id => "lxy-ui-framework";
        public string DisplayName => "Lxy UI Framework";
        public bool SupportsScriptGeneration => true;
        public bool SupportsLayerSelection => true;

        public UIEffectProjectDefaults CreateDefaults()
        {
            return new UIEffectProjectDefaults
            {
                prefabFolder = "Assets/GameResources/Prefabs/UIRes",
                codeNamespace = "LxyDemo.GameUI",
                scriptFolder = "Assets/Scripts/GameUI",
                resourceSearchRoot =
                    "Assets/GameResources/UIAtlas/AtlasScr",
                referenceFolder = "Assets/Editor/UIReferences",
                schemaFolder = "Assets/Editor/UISchemas",
                figmaSpriteFolder =
                    "Assets/GameResources/UIAtlas/FigmaGenerated",
                scriptType = UIEffectScriptType.CSharp,
                uiLayer = UIEffectLayer.Auto,
            };
        }

        public UIEffectPrefabHostResult CreateOrUpdatePrefab(
            UIEffectPrefabGenerationOptions options,
            bool promptForExistingPrefab)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var projectOptions = new CSharpUIGenerationOptions
            {
                panelId = options.panelId,
                scriptType = options.scriptType == UIEffectScriptType.Lua
                    ? UIScriptType.Lua
                    : UIScriptType.CSharp,
                codeNamespace = options.codeNamespace,
                logicClassName = options.logicClassName,
                prefabFolder = options.prefabFolder,
                scriptFolder = options.scriptFolder,
                autoCollectBindings = true,
                uiLayer = (UILayer)(int)options.uiLayer,
            };
            CSharpUIGenerationResult result =
                CSharpUIGenerator.Generate(
                    projectOptions,
                    promptForExistingPrefab);
            return new UIEffectPrefabHostResult(result.PrefabPath);
        }

        public void BeforeReplaceGeneratedTree(
            GameObject prefabRoot,
            Transform previousGeneratedRoot)
        {
            ObjectBinder objectBinder =
                prefabRoot.GetComponent<ObjectBinder>();
            if (objectBinder?.bindValues == null)
            {
                return;
            }

            List<BindValue> bindings = objectBinder.bindValues;
            bindings.RemoveAll(binding =>
                binding != null &&
                IsInsideGeneratedRoot(
                    binding.GetObjectValue,
                    previousGeneratedRoot));
            EditorUtility.SetDirty(objectBinder);
        }

        public void AfterBuildGeneratedTree(
            GameObject prefabRoot,
            UIEffectPrefabGenerationOptions options)
        {
            if (options.scriptType != UIEffectScriptType.CSharp)
            {
                return;
            }

            ObjectBinder objectBinder =
                prefabRoot.GetComponent<ObjectBinder>();
            CSharpUIGenerator.AutoCollectObjectBindings(objectBinder);
        }

        private static bool IsInsideGeneratedRoot(
            UnityEngine.Object target,
            Transform generatedRoot)
        {
            Transform transform = null;
            if (target is GameObject gameObject)
            {
                transform = gameObject.transform;
            }
            else if (target is Component component)
            {
                transform = component.transform;
            }

            return transform != null &&
                   (transform == generatedRoot ||
                    transform.IsChildOf(generatedRoot));
        }
    }
}
#endif
