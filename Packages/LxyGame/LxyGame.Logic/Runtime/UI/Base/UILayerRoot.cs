using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LxyDemo.UIFramework
{
    [DisallowMultipleComponent]
    public sealed class UILayerRoot : MonoBehaviour
    {
        [Serializable]
        private sealed class LayerReference
        {
            /// <summary>
            /// 公开的层级数据。
            /// </summary>
            public UILayer layer;
            /// <summary>
            /// 公开的根节点数据。
            /// </summary>
            public RectTransform root;
        }

        [SerializeField]
        private Canvas rootCanvas;

        [SerializeField]
        private CanvasScaler canvasScaler;

        [SerializeField]
        private List<LayerReference> layers =
            new List<LayerReference>();

        private readonly Dictionary<UILayer, RectTransform> layerMap =
            new Dictionary<UILayer, RectTransform>();

        /// <summary>
        /// 向调用方提供RootCanvas。
        /// </summary>
        public Canvas RootCanvas => rootCanvas;

        /// <summary>
        /// 确保Initialized。
        /// </summary>
        public void EnsureInitialized(bool createEventSystem = true)
        {
            ResolveOrCreateCanvas();
            RebuildLayerMap();

            foreach (UILayer layer in GetRuntimeLayers())
            {
                if (!layerMap.ContainsKey(layer) ||
                    layerMap[layer] == null)
                {
                    CreateLayer(layer);
                }
            }

            SortLayers();

            if (createEventSystem)
            {
                Transform eventSystemParent = rootCanvas != null
                    ? rootCanvas.transform.root
                    : transform.root;
                EnsureEventSystem(eventSystemParent);
            }
        }

        /// <summary>
        /// 获取层级。
        /// </summary>
        public RectTransform GetLayer(UILayer layer)
        {
            EnsureInitialized();
            UILayer resolved = layer == UILayer.Auto
                ? UILayer.Stack
                : layer;

            if (!layerMap.TryGetValue(
                    resolved,
                    out RectTransform root) ||
                root == null)
            {
                root = CreateLayer(resolved);
            }

            return root;
        }

        /// <summary>
        /// 创建运行时。
        /// </summary>
        public static UILayerRoot CreateRuntime(
            string objectName = "[UIRoot]")
        {
            var rootObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(UILayerRoot));

            UILayerRoot result =
                rootObject.GetComponent<UILayerRoot>();
            result.EnsureInitialized();
            return result;
        }

        /// <summary>
        /// 解析Or创建画布。
        /// </summary>
        private void ResolveOrCreateCanvas()
        {
            bool createdCanvas = false;
            if (rootCanvas == null)
            {
                Transform namedLayerRoot =
                    transform.Find("LayerRoot");
                rootCanvas = GetComponent<Canvas>() ??
                             (namedLayerRoot == null
                                 ? null
                                 : namedLayerRoot
                                     .GetComponent<Canvas>()) ??
                             GetComponentInChildren<Canvas>(true);
            }

            if (rootCanvas == null)
            {
                rootCanvas = gameObject.AddComponent<Canvas>();
                createdCanvas = true;
            }

            if (createdCanvas)
            {
                rootCanvas.renderMode =
                    RenderMode.ScreenSpaceOverlay;
            }

            if (rootCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                rootCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (canvasScaler == null)
            {
                canvasScaler =
                    rootCanvas.GetComponent<CanvasScaler>();
            }

            if (canvasScaler == null)
            {
                canvasScaler =
                    rootCanvas.gameObject.AddComponent<CanvasScaler>();
            }

            if (canvasScaler.uiScaleMode ==
                CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                canvasScaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution =
                    new Vector2(1920f, 1080f);
                canvasScaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        /// <summary>
        /// 执行重建层级映射相关逻辑。
        /// </summary>
        private void RebuildLayerMap()
        {
            layerMap.Clear();
            if (layers == null)
            {
                layers = new List<LayerReference>();
            }

            foreach (LayerReference reference in layers)
            {
                if (reference != null &&
                    reference.layer != UILayer.Auto &&
                    reference.root != null)
                {
                    layerMap[reference.layer] = reference.root;
                }
            }

            RectTransform[] rects =
                rootCanvas.GetComponentsInChildren<RectTransform>(
                    true);
            foreach (RectTransform rect in rects)
            {
                if (rect == null ||
                    !TryResolveLayer(
                        rect.name,
                        out UILayer layer))
                {
                    continue;
                }

                if (!layerMap.ContainsKey(layer))
                {
                    layerMap[layer] = rect;
                }

                AddOrUpdateLayerReference(layer, rect);
            }
        }

        /// <summary>
        /// 创建层级。
        /// </summary>
        private RectTransform CreateLayer(UILayer layer)
        {
            RectTransform layerContainer = GetLayerContainer();
            var layerObject = new GameObject(
                GetLayerNodeName(layer),
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            var rect =
                layerObject.GetComponent<RectTransform>();
            rect.SetParent(layerContainer, false);
            SetLayerRecursively(
                layerObject,
                layerContainer.gameObject.layer);
            Stretch(rect);

            Canvas canvas = layerObject.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder =
                Mathf.Max(0, (int)layer / 10 - 1) * 1000;

            layerMap[layer] = rect;
            AddOrUpdateLayerReference(layer, rect);
            return rect;
        }

        /// <summary>
        /// 获取层级Container。
        /// </summary>
        private RectTransform GetLayerContainer()
        {
            Transform namedRoot =
                rootCanvas.transform.Find("Root");
            return namedRoot as RectTransform ??
                   rootCanvas.transform as RectTransform;
        }

        /// <summary>
        /// 添加OrUpdate层级引用。
        /// </summary>
        private void AddOrUpdateLayerReference(
            UILayer layer,
            RectTransform root)
        {
            foreach (LayerReference reference in layers)
            {
                if (reference != null &&
                    reference.layer == layer)
                {
                    if (reference.root == null)
                    {
                        reference.root = root;
                    }

                    return;
                }
            }

            layers.Add(new LayerReference
            {
                layer = layer,
                root = root
            });
        }

        /// <summary>
        /// 尝试解析层级，并返回是否成功。
        /// </summary>
        private static bool TryResolveLayer(
            string objectName,
            out UILayer layer)
        {
            layer = UILayer.Auto;
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            string token = objectName.Trim();
            int markerIndex = token.IndexOf(
                '#');
            if (markerIndex >= 0)
            {
                token = token.Substring(0, markerIndex);
            }

            if (token.EndsWith(
                    "Layer",
                    StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(
                    0,
                    token.Length - "Layer".Length);
            }

            switch (token.ToLowerInvariant())
            {
                case "bottom":
                    layer = UILayer.Bottom;
                    return true;
                case "stack":
                    layer = UILayer.Stack;
                    return true;
                case "popup":
                    layer = UILayer.Popup;
                    return true;
                case "guide":
                    layer = UILayer.Guide;
                    return true;
                case "top":
                    layer = UILayer.Top;
                    return true;
                case "loading":
                    layer = UILayer.Loading;
                    return true;
                case "tips":
                    layer = UILayer.Tips;
                    return true;
                case "debug":
                    layer = UILayer.Debug;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 获取层级Node名称。
        /// </summary>
        private static string GetLayerNodeName(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Popup:
                    return "PopUp#Rtf#Canvas";
                case UILayer.Guide:
                    return "GuideLayer#Rtf#Canvas";
                case UILayer.Top:
                    return "TopLayer#Rtf#Canvas";
                default:
                    return $"{layer}#Rtf#Canvas";
            }
        }

        /// <summary>
        /// 执行SortLayers相关逻辑。
        /// </summary>
        private void SortLayers()
        {
            var ordered =
                new List<KeyValuePair<UILayer, RectTransform>>(
                    layerMap);
            ordered.Sort(
                (left, right) =>
                    ((int)left.Key).CompareTo((int)right.Key));

            foreach (KeyValuePair<UILayer, RectTransform> item in
                     ordered)
            {
                if (item.Value != null)
                {
                    item.Value.SetAsLastSibling();
                }
            }
        }

        /// <summary>
        /// 获取运行时层级。
        /// </summary>
        private static IEnumerable<UILayer> GetRuntimeLayers()
        {
            yield return UILayer.Bottom;
            yield return UILayer.Stack;
            yield return UILayer.Popup;
            yield return UILayer.Guide;
            yield return UILayer.Top;
            yield return UILayer.Loading;
            yield return UILayer.Tips;
            yield return UILayer.Debug;
        }

        /// <summary>
        /// 确保事件System。
        /// </summary>
        private static void EnsureEventSystem(
            Transform parent)
        {
            EventSystem eventSystem = EventSystem.current ??
                                      FindObjectOfType<EventSystem>();
            GameObject eventSystemObject;
            if (eventSystem == null)
            {
                eventSystemObject = new GameObject(
                    "[EventSystem]",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
            }
            else
            {
                eventSystemObject = eventSystem.gameObject;
            }

            eventSystemObject.name = "[EventSystem]";
            if (parent != null &&
                eventSystemObject.transform != parent &&
                !parent.IsChildOf(eventSystemObject.transform))
            {
                eventSystemObject.transform.SetParent(parent, false);
            }

            eventSystemObject.transform.SetSiblingIndex(0);
        }

        /// <summary>
        /// 设置UI层级Recursively。
        /// </summary>
        public static void SetUILayerRecursively(
            GameObject root)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
            {
                throw new InvalidOperationException(
                    "项目中不存在名为 UI 的 Layer。");
            }

            SetLayerRecursively(root, uiLayer);
        }

        /// <summary>
        /// 设置层级Recursively。
        /// </summary>
        public static void SetLayerRecursively(
            GameObject root,
            int layer)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            root.layer = layer;
            Transform rootTransform = root.transform;
            for (int index = 0;
                 index < rootTransform.childCount;
                 index++)
            {
                SetLayerRecursively(
                    rootTransform.GetChild(index).gameObject,
                    layer);
            }
        }

        /// <summary>
        /// 执行Stretch相关逻辑。
        /// </summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
