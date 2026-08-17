#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LuaObjectBind;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LxyDemo.UIFramework.Editor
{
    [Serializable]
    public sealed class UIEffectPrefabGenerationOptions
    {
        public string panelId = string.Empty;
        public string prefabFolder =
            "Assets/GameResources/Prefabs/UIRes";
        public UIScriptType scriptType = UIScriptType.CSharp;
        public string codeNamespace = "LxyDemo.GameUI";
        public string logicClassName = string.Empty;
        public string scriptFolder = "Assets/Scripts/GameUI";
        public UILayer uiLayer = UILayer.Auto;
        public string[] resourceSearchRoots =
        {
            "Assets/GameResources",
        };
    }

    public sealed class UIEffectPrefabGenerationResult
    {
        public string PrefabPath { get; internal set; }
        public string SchemaName { get; internal set; }
        public IReadOnlyList<string> UsedResources { get; internal set; }
        public IReadOnlyList<string> MissingResources { get; internal set; }
    }

    /// <summary>
    /// Deterministically compiles a reviewed UISchema into the project's
    /// standard UI Prefab shape. Visual interpretation stays outside this
    /// class so regeneration is repeatable and reviewable.
    /// </summary>
    public static class UIEffectPrefabBuilder
    {
        private const string GeneratedRootName = "Generated";

        public static UIEffectPrefabGenerationResult GenerateFromSchemaPath(
            string schemaAssetPath,
            UIEffectPrefabGenerationOptions options = null,
            bool promptForExistingPrefab = true)
        {
            schemaAssetPath = NormalizeAssetPath(schemaAssetPath);
            TextAsset schemaAsset =
                AssetDatabase.LoadAssetAtPath<TextAsset>(schemaAssetPath);
            if (schemaAsset == null)
            {
                throw new InvalidOperationException(
                    $"找不到 UISchema：{schemaAssetPath}");
            }

            return Generate(
                schemaAsset.text,
                options,
                promptForExistingPrefab);
        }

        public static UIEffectPrefabGenerationResult Generate(
            string schemaJson,
            UIEffectPrefabGenerationOptions options = null,
            bool promptForExistingPrefab = true)
        {
            UIEffectSchema compactSchema =
                UIEffectSchemaUtility.Parse(schemaJson);
            UIEffectSchema schema =
                UIEffectSchemaUtility.ExpandRepeats(compactSchema);
            options ??= new UIEffectPrefabGenerationOptions();
            NormalizeOptions(options, schema);

            CSharpUIGenerationOptions projectOptions =
                CreateProjectOptions(options);
            CSharpUIGenerationResult initialResult =
                CSharpUIGenerator.Generate(
                    projectOptions,
                    promptForExistingPrefab);

            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    initialResult.PrefabPath);
            if (prefabAsset == null)
            {
                throw new InvalidOperationException(
                    $"项目 UI 工具未生成 Prefab：{initialResult.PrefabPath}");
            }

            var resolver = new UIEffectResourceResolver(
                options.resourceSearchRoots);
            BuildGeneratedTree(
                initialResult.PrefabPath,
                schema,
                resolver);

            // Re-run the project generator against the finished hierarchy so
            // ObjectBinder and generated fields follow existing conventions.
            projectOptions.existingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    initialResult.PrefabPath);
            CSharpUIGenerator.Generate(projectOptions, false);
            AssetDatabase.SaveAssets();

            return new UIEffectPrefabGenerationResult
            {
                PrefabPath = initialResult.PrefabPath,
                SchemaName = schema.name,
                UsedResources = resolver.UsedResources.ToArray(),
                MissingResources = resolver.MissingResources.ToArray(),
            };
        }

        private static void BuildGeneratedTree(
            string prefabPath,
            UIEffectSchema schema,
            UIEffectResourceResolver resolver)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"无法加载 Prefab 内容：{prefabPath}");
            }

            try
            {
                Transform previous = root.transform.Find(GeneratedRootName);
                if (previous != null)
                {
                    RemoveGeneratedBindings(root, previous);
                    UnityEngine.Object.DestroyImmediate(
                        previous.gameObject);
                }

                RectTransform generated = CreateRectTransform(
                    GeneratedRootName,
                    root.transform);
                StretchToParent(generated);

                var bindingNames = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0;
                     index < schema.children.Count;
                     index++)
                {
                    BuildNode(
                        schema.children[index],
                        generated,
                        schema.designWidth,
                        schema.designHeight,
                        resolver,
                        bindingNames,
                        schema.name);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform BuildNode(
            UIEffectNode node,
            RectTransform parent,
            float parentWidth,
            float parentHeight,
            UIEffectResourceResolver resolver,
            HashSet<string> bindingNames,
            string nodePath)
        {
            string bindingName = GetBindingName(node);
            if (ShouldBind(node) && !bindingNames.Add(bindingName))
            {
                throw new InvalidOperationException(
                    $"节点绑定名重复：{bindingName}。" +
                    "请让 UISchema 中可绑定节点使用唯一名称。");
            }

            RectTransform rect;
            RectTransform childParent;
            float childParentWidth = node.width;
            float childParentHeight = node.height;
            switch (node.type.ToLowerInvariant())
            {
                case "image":
                    rect = CreateImageNode(
                        bindingName,
                        node,
                        parent,
                        resolver,
                        nodePath);
                    childParent = rect;
                    break;
                case "text":
                    rect = CreateTextNode(
                        bindingName,
                        node,
                        parent);
                    childParent = rect;
                    break;
                case "button":
                    rect = CreateButtonNode(
                        bindingName,
                        node,
                        parent,
                        resolver,
                        nodePath);
                    childParent = rect;
                    break;
                case "scrollrect":
                    rect = CreateScrollNode(
                        bindingName,
                        node,
                        parent,
                        out childParent);
                    ConfigureScrollContent(
                        node,
                        childParent,
                        out childParentWidth,
                        out childParentHeight);
                    break;
                default:
                    rect = CreateRectTransform(bindingName, parent);
                    childParent = rect;
                    break;
            }

            ApplyLayout(
                rect,
                node,
                parentWidth,
                parentHeight);

            string currentPath = nodePath + "/" + node.name;
            for (int index = 0;
                 index < node.children.Count;
                 index++)
            {
                BuildNode(
                    node.children[index],
                    childParent,
                    childParentWidth,
                    childParentHeight,
                    resolver,
                    bindingNames,
                    currentPath);
            }

            return rect;
        }

        private static RectTransform CreateImageNode(
            string name,
            UIEffectNode node,
            Transform parent,
            UIEffectResourceResolver resolver,
            string nodePath)
        {
            RectTransform rect = CreateRectTransform(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            ApplyImage(
                image,
                node,
                resolver,
                nodePath + "/" + node.name,
                out bool placeholder);
            if (placeholder)
            {
                AddPlaceholderLabel(rect, node);
            }

            return rect;
        }

        private static RectTransform CreateTextNode(
            string name,
            UIEffectNode node,
            Transform parent)
        {
            RectTransform rect = CreateRectTransform(name, parent);
            TextMeshProUGUI text =
                rect.gameObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(text, node);
            return rect;
        }

        private static RectTransform CreateButtonNode(
            string name,
            UIEffectNode node,
            Transform parent,
            UIEffectResourceResolver resolver,
            string nodePath)
        {
            RectTransform rect = CreateRectTransform(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            ApplyImage(
                image,
                node,
                resolver,
                nodePath + "/" + node.name,
                out bool placeholder);
            Button button = rect.gameObject.AddComponent<Button>();
            image.raycastTarget = true;
            button.targetGraphic = image;

            bool hasTextChild = node.children.Any(child =>
                child != null &&
                string.Equals(
                    child.type,
                    "Text",
                    StringComparison.OrdinalIgnoreCase));
            if (!hasTextChild)
            {
                string label = placeholder
                    ? "[MISSING]\n" + GetSemanticName(node)
                    : node.text;
                if (!string.IsNullOrWhiteSpace(label))
                {
                    AddButtonLabel(rect, node, label);
                }
            }

            return rect;
        }

        private static RectTransform CreateScrollNode(
            string name,
            UIEffectNode node,
            Transform parent,
            out RectTransform content)
        {
            RectTransform rect = CreateRectTransform(name, parent);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = ParseColor(
                node.color,
                new Color(0.08f, 0.1f, 0.13f, 0.35f));
            background.raycastTarget = node.raycastTarget;

            RectTransform viewport = CreateRectTransform(
                "Viewport",
                rect);
            StretchToParent(viewport);
            Image viewportImage =
                viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>()
                .showMaskGraphic = false;

            content = CreateRectTransform("Content", viewport);
            StretchToParent(content);
            content.pivot = new Vector2(0.5f, 1f);

            ScrollRect scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal =
                string.Equals(
                    node.scrollDirection,
                    "Horizontal",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    node.scrollDirection,
                    "Both",
                    StringComparison.OrdinalIgnoreCase);
            scroll.vertical =
                !string.Equals(
                    node.scrollDirection,
                    "Horizontal",
                    StringComparison.OrdinalIgnoreCase);
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return rect;
        }

        private static void ApplyImage(
            Image image,
            UIEffectNode node,
            UIEffectResourceResolver resolver,
            string nodePath,
            out bool placeholder)
        {
            Sprite sprite = node.intentionalColor
                ? null
                : resolver.Resolve(node);
            placeholder = sprite == null && !node.intentionalColor;
            image.sprite = sprite;
            image.preserveAspect = node.preserveAspect;
            image.raycastTarget = node.raycastTarget;
            image.color = sprite != null
                ? ParseColor(node.color, Color.white)
                : node.intentionalColor
                    ? ParseColor(node.color, Color.white)
                    : GetPlaceholderColor(node);
            image.type = node.sliced &&
                         sprite != null &&
                         sprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;

            if (placeholder)
            {
                resolver.RecordMissing(
                    nodePath,
                    GetSemanticName(node));
            }
        }

        private static void ConfigureText(
            TextMeshProUGUI text,
            UIEffectNode node)
        {
            text.text = node.text ?? string.Empty;
            text.fontSize = Mathf.Max(1f, node.fontSize);
            text.color = ParseColor(node.color, Color.white);
            text.alignment = ParseAlignment(node.alignment);
            text.fontStyle = node.bold
                ? FontStyles.Bold
                : FontStyles.Normal;
            text.enableWordWrapping = true;
            text.raycastTarget = node.raycastTarget;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void AddButtonLabel(
            RectTransform parent,
            UIEffectNode source,
            string label)
        {
            RectTransform rect = CreateRectTransform("Label", parent);
            StretchToParent(rect);
            TextMeshProUGUI text =
                rect.gameObject.AddComponent<TextMeshProUGUI>();
            var labelNode = new UIEffectNode
            {
                text = label,
                fontSize = source.fontSize,
                alignment = source.alignment,
                bold = source.bold,
                // Button.color tints its background. Use an explicit Text
                // child when the label needs a non-white text color.
                color = "#FFFFFFFF",
            };
            ConfigureText(text, labelNode);
            text.raycastTarget = false;
        }

        private static void AddPlaceholderLabel(
            RectTransform parent,
            UIEffectNode source)
        {
            RectTransform rect =
                CreateRectTransform("PlaceholderLabel", parent);
            StretchToParent(rect);
            TextMeshProUGUI text =
                rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "[MISSING]\n" + GetSemanticName(source);
            text.fontSize = Mathf.Clamp(
                Mathf.Min(source.width, source.height) * 0.12f,
                14f,
                30f);
            text.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
        }

        private static RectTransform CreateRectTransform(
            string name,
            Transform parent)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform));
            RectTransform rect =
                gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void ApplyLayout(
            RectTransform rect,
            UIEffectNode node,
            float parentWidth,
            float parentHeight)
        {
            float left = node.x;
            float top = node.y;
            float right = parentWidth - node.x - node.width;
            float bottom = parentHeight - node.y - node.height;
            string anchor = ResolveAnchor(
                    node,
                    parentWidth,
                    parentHeight)
                .ToLowerInvariant();

            if (anchor == "stretchall")
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(left, bottom);
                rect.offsetMax = new Vector2(-right, -top);
                return;
            }

            if (anchor == "stretchhorizontal")
            {
                float centerY =
                    parentHeight * 0.5f -
                    (node.y + node.height * 0.5f);
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(
                    left,
                    centerY - node.height * 0.5f);
                rect.offsetMax = new Vector2(
                    -right,
                    centerY + node.height * 0.5f);
                return;
            }

            if (anchor == "stretchvertical")
            {
                float centerX =
                    node.x + node.width * 0.5f -
                    parentWidth * 0.5f;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = new Vector2(
                    centerX - node.width * 0.5f,
                    bottom);
                rect.offsetMax = new Vector2(
                    centerX + node.width * 0.5f,
                    -top);
                return;
            }

            Vector2 anchorPoint = GetAnchorPoint(anchor);
            rect.anchorMin = anchorPoint;
            rect.anchorMax = anchorPoint;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(node.width, node.height);

            Vector2 desiredCenter = new Vector2(
                node.x + node.width * 0.5f - parentWidth * 0.5f,
                parentHeight * 0.5f -
                node.y - node.height * 0.5f);
            Vector2 anchorPosition = new Vector2(
                (anchorPoint.x - 0.5f) * parentWidth,
                (anchorPoint.y - 0.5f) * parentHeight);
            rect.anchoredPosition = desiredCenter - anchorPosition;
        }

        private static Vector2 GetAnchorPoint(string anchor)
        {
            switch (anchor)
            {
                case "topleft":
                    return new Vector2(0f, 1f);
                case "topcenter":
                    return new Vector2(0.5f, 1f);
                case "topright":
                    return new Vector2(1f, 1f);
                case "middleleft":
                    return new Vector2(0f, 0.5f);
                case "middleright":
                    return new Vector2(1f, 0.5f);
                case "bottomleft":
                    return new Vector2(0f, 0f);
                case "bottomcenter":
                    return new Vector2(0.5f, 0f);
                case "bottomright":
                    return new Vector2(1f, 0f);
                default:
                    return new Vector2(0.5f, 0.5f);
            }
        }

        private static string ResolveAnchor(
            UIEffectNode node,
            float parentWidth,
            float parentHeight)
        {
            if (!string.IsNullOrWhiteSpace(node.anchor) &&
                !string.Equals(
                    node.anchor,
                    "Auto",
                    StringComparison.OrdinalIgnoreCase))
            {
                return node.anchor;
            }

            float horizontalCoverage = node.width / parentWidth;
            float verticalCoverage = node.height / parentHeight;
            if (horizontalCoverage >= 0.9f &&
                verticalCoverage >= 0.9f)
            {
                return "StretchAll";
            }

            if (horizontalCoverage >= 0.8f)
            {
                return "StretchHorizontal";
            }

            if (verticalCoverage >= 0.8f)
            {
                return "StretchVertical";
            }

            float normalizedCenterX =
                (node.x + node.width * 0.5f) / parentWidth;
            float normalizedCenterY =
                (node.y + node.height * 0.5f) / parentHeight;
            string vertical = normalizedCenterY < 0.34f
                ? "Top"
                : normalizedCenterY > 0.66f
                    ? "Bottom"
                    : "Middle";
            string horizontal = normalizedCenterX < 0.34f
                ? "Left"
                : normalizedCenterX > 0.66f
                    ? "Right"
                    : "Center";
            return vertical + horizontal;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureScrollContent(
            UIEffectNode node,
            RectTransform content,
            out float contentWidth,
            out float contentHeight)
        {
            float requiredWidth = node.width;
            float requiredHeight = node.height;
            foreach (UIEffectNode child in node.children)
            {
                if (child == null)
                {
                    continue;
                }

                requiredWidth = Mathf.Max(
                    requiredWidth,
                    child.x + child.width);
                requiredHeight = Mathf.Max(
                    requiredHeight,
                    child.y + child.height);
            }

            bool horizontal = string.Equals(
                                  node.scrollDirection,
                                  "Horizontal",
                                  StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(
                                  node.scrollDirection,
                                  "Both",
                                  StringComparison.OrdinalIgnoreCase);
            bool vertical = !string.Equals(
                node.scrollDirection,
                "Horizontal",
                StringComparison.OrdinalIgnoreCase);
            contentWidth = horizontal
                ? requiredWidth
                : node.width;
            contentHeight = vertical
                ? requiredHeight
                : node.height;

            if (horizontal && vertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(
                    contentWidth,
                    contentHeight);
                return;
            }

            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(contentWidth, 0f);
                return;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, contentHeight);
        }

        private static void RemoveGeneratedBindings(
            GameObject prefabRoot,
            Transform generatedRoot)
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
                    generatedRoot));
            EditorUtility.SetDirty(objectBinder);
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

        private static string GetBindingName(UIEffectNode node)
        {
            string cleanName = CSharpUIGenerator.SanitizeTypeName(
                node.name);
            if (!ShouldBind(node))
            {
                return cleanName;
            }

            string prefix;
            switch (node.type.ToLowerInvariant())
            {
                case "image":
                    prefix = "img_";
                    break;
                case "text":
                    prefix = "txt_";
                    break;
                case "button":
                    prefix = "btn_";
                    break;
                case "scrollrect":
                    prefix = "scroll_";
                    break;
                default:
                    prefix = "rt_";
                    break;
            }

            return prefix + cleanName;
        }

        private static bool ShouldBind(UIEffectNode node)
        {
            if (string.Equals(
                    node.binding,
                    "Yes",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    node.binding,
                    "No",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return node.type.Equals(
                       "Button",
                       StringComparison.OrdinalIgnoreCase) ||
                   node.type.Equals(
                       "ScrollRect",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Color GetPlaceholderColor(UIEffectNode node)
        {
            string semantic = GetSemanticName(node).ToLowerInvariant();
            if (node.type.Equals(
                    "Button",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Color32(92, 191, 114, 255);
            }

            if (semantic.Contains("avatar") ||
                semantic.Contains("portrait") ||
                semantic.Contains("头像"))
            {
                return new Color32(174, 111, 212, 255);
            }

            if (semantic.Contains("icon") ||
                semantic.Contains("图标"))
            {
                return new Color32(239, 203, 77, 255);
            }

            if (semantic.Contains("background") ||
                semantic.Contains("bg") ||
                semantic.Contains("背景"))
            {
                return new Color32(125, 132, 143, 255);
            }

            if (semantic.Contains("unknown") ||
                semantic.Contains("未知"))
            {
                return new Color32(220, 88, 88, 255);
            }

            if (node.type.Equals(
                    "Image",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Color32(86, 160, 221, 255);
            }

            return new Color32(220, 88, 88, 255);
        }

        private static Color ParseColor(
            string value,
            Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                ColorUtility.TryParseHtmlString(
                    value.Trim(),
                    out Color color))
            {
                return color;
            }

            return fallback;
        }

        private static TextAlignmentOptions ParseAlignment(
            string alignment)
        {
            if (Enum.TryParse(
                    alignment,
                    true,
                    out TextAlignmentOptions result))
            {
                return result;
            }

            return TextAlignmentOptions.Center;
        }

        private static string GetSemanticName(UIEffectNode node)
        {
            return string.IsNullOrWhiteSpace(node.semantic)
                ? node.name
                : node.semantic;
        }

        private static CSharpUIGenerationOptions CreateProjectOptions(
            UIEffectPrefabGenerationOptions options)
        {
            return new CSharpUIGenerationOptions
            {
                panelId = options.panelId,
                scriptType = options.scriptType,
                codeNamespace = options.codeNamespace,
                logicClassName = options.logicClassName,
                prefabFolder = options.prefabFolder,
                scriptFolder = options.scriptFolder,
                autoCollectBindings = true,
                uiLayer = options.uiLayer,
            };
        }

        private static void NormalizeOptions(
            UIEffectPrefabGenerationOptions options,
            UIEffectSchema schema)
        {
            options.panelId = string.IsNullOrWhiteSpace(options.panelId)
                ? schema.name
                : options.panelId.Trim();
            options.logicClassName =
                string.IsNullOrWhiteSpace(options.logicClassName)
                    ? CSharpUIGenerator.SanitizeTypeName(options.panelId)
                    : options.logicClassName.Trim();
            options.prefabFolder =
                NormalizeAssetPath(options.prefabFolder);
            options.scriptFolder =
                NormalizeAssetPath(options.scriptFolder);
            options.codeNamespace =
                options.codeNamespace?.Trim() ?? string.Empty;
            options.resourceSearchRoots =
                (options.resourceSearchRoots ?? Array.Empty<string>())
                .Select(NormalizeAssetPath)
                .Where(path =>
                    !string.IsNullOrWhiteSpace(path) &&
                    AssetDatabase.IsValidFolder(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (options.resourceSearchRoots.Length == 0)
            {
                options.resourceSearchRoots = new[]
                {
                    "Assets/GameResources",
                };
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }

    internal sealed class UIEffectResourceResolver
    {
        private const int MinimumMatchScore = 440;
        private readonly List<SpriteEntry> sprites;
        private readonly List<string> usedResources = new List<string>();
        private readonly List<string> missingResources = new List<string>();

        public UIEffectResourceResolver(string[] searchRoots)
        {
            sprites = BuildSpriteIndex(searchRoots);
        }

        public IReadOnlyList<string> UsedResources => usedResources;
        public IReadOnlyList<string> MissingResources => missingResources;

        public Sprite Resolve(UIEffectNode node)
        {
            List<string> candidates = GetCandidates(node);
            for (int index = 0; index < candidates.Count; index++)
            {
                string candidate = candidates[index];
                if (!candidate.StartsWith(
                        "Assets/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Sprite explicitSprite = LoadSpriteAtPath(candidate);
                if (explicitSprite != null)
                {
                    RecordUsed(candidate, explicitSprite);
                    return explicitSprite;
                }
            }

            SpriteEntry best = null;
            int bestScore = 0;
            foreach (SpriteEntry entry in sprites)
            {
                foreach (string candidate in candidates)
                {
                    int score = GetMatchScore(candidate, entry);
                    if (score > bestScore)
                    {
                        best = entry;
                        bestScore = score;
                    }
                }
            }

            if (best == null || bestScore < MinimumMatchScore)
            {
                return null;
            }

            RecordUsed(best.AssetPath, best.Sprite);
            return best.Sprite;
        }

        public void RecordMissing(string nodePath, string semantic)
        {
            string value = $"{nodePath} -> {semantic}";
            if (!missingResources.Contains(value))
            {
                missingResources.Add(value);
            }
        }

        private void RecordUsed(string assetPath, Sprite sprite)
        {
            string value = assetPath;
            if (!string.Equals(
                    sprite.name,
                    Path.GetFileNameWithoutExtension(assetPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                value += "#" + sprite.name;
            }

            if (!usedResources.Contains(value))
            {
                usedResources.Add(value);
            }
        }

        private static List<SpriteEntry> BuildSpriteIndex(
            string[] searchRoots)
        {
            var result = new List<SpriteEntry>();
            string[] guids = AssetDatabase.FindAssets(
                "t:Sprite",
                searchRoots);
            foreach (string guid in guids.OrderBy(value => value))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Sprite sprite in
                         AssetDatabase.LoadAllAssetsAtPath(path)
                             .OfType<Sprite>()
                             .OrderBy(item => item.name))
                {
                    result.Add(new SpriteEntry(path, sprite));
                }
            }

            return result;
        }

        private static Sprite LoadSpriteAtPath(string assetPath)
        {
            Sprite direct =
                AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (direct != null)
            {
                return direct;
            }

            string spriteName = string.Empty;
            int separator = assetPath.LastIndexOf('#');
            if (separator >= 0)
            {
                spriteName = assetPath.Substring(separator + 1);
                assetPath = assetPath.Substring(0, separator);
            }

            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .FirstOrDefault(sprite =>
                    spriteName.Length == 0 ||
                    string.Equals(
                        sprite.name,
                        spriteName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetCandidates(UIEffectNode node)
        {
            var candidates = new List<string>();
            AddCandidate(candidates, node.resource);
            foreach (string candidate in node.resourceCandidates)
            {
                AddCandidate(candidates, candidate);
            }

            AddCandidate(candidates, node.semantic);
            AddCandidate(candidates, node.name);
            return candidates;
        }

        private static void AddCandidate(
            List<string> candidates,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !candidates.Contains(value.Trim()))
            {
                candidates.Add(value.Trim());
            }
        }

        private static int GetMatchScore(
            string candidate,
            SpriteEntry entry)
        {
            string normalizedCandidate = NormalizeKey(candidate);
            if (normalizedCandidate.Length < 3)
            {
                return 0;
            }

            if (normalizedCandidate == entry.NameKey)
            {
                return 1000;
            }

            if (normalizedCandidate == entry.FileKey)
            {
                return 950;
            }

            int score = 0;
            if (entry.NameKey.Contains(normalizedCandidate) ||
                normalizedCandidate.Contains(entry.NameKey))
            {
                score = Math.Max(score, 650);
            }

            if (entry.PathKey.Contains(normalizedCandidate))
            {
                score = Math.Max(score, 520);
            }

            string[] tokens = Tokenize(candidate);
            if (tokens.Length > 0)
            {
                int matches = tokens.Count(token =>
                    entry.PathKey.Contains(token));
                score = Math.Max(
                    score,
                    matches * 500 / tokens.Length);
            }

            return score;
        }

        private static string[] Tokenize(string value)
        {
            return value
                .Split(
                    new[]
                    {
                        ' ', '_', '-', '.', '/', '\\',
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeKey)
                .Where(token => token.Length >= 3)
                .Distinct()
                .ToArray();
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string fileName = Path.GetFileNameWithoutExtension(value);
            var builder = new StringBuilder(fileName.Length);
            foreach (char character in fileName.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private sealed class SpriteEntry
        {
            public SpriteEntry(string assetPath, Sprite sprite)
            {
                AssetPath = assetPath;
                Sprite = sprite;
                NameKey = NormalizeKey(sprite.name);
                FileKey = NormalizeKey(
                    Path.GetFileNameWithoutExtension(assetPath));
                PathKey = NormalizeKey(assetPath + " " + sprite.name);
            }

            public string AssetPath { get; }
            public Sprite Sprite { get; }
            public string NameKey { get; }
            public string FileKey { get; }
            public string PathKey { get; }
        }
    }
}
#endif
