using System;
using UnityEngine;
using UnityEngine.UI;

namespace LxyDemo.UIFramework
{
    /// <summary>
    /// 项目可替换此服务接入 URP/自研的截图模糊。默认实现提供可点击的半透明遮罩。
    /// </summary>
    public interface IUIBackdropService
    {
        GameObject CreateBackdrop(
            UIPanelConfig config,
            RectTransform parent,
            Action closeRequested);

        /// <summary>
        /// 释放Backdrop。
        /// </summary>
        void ReleaseBackdrop(GameObject backdrop);
    }

    public sealed class DefaultUIBackdropService :
        IUIBackdropService
    {
        /// <summary>
        /// 创建Backdrop。
        /// </summary>
        public GameObject CreateBackdrop(
            UIPanelConfig config,
            RectTransform parent,
            Action closeRequested)
        {
            var backdropObject = new GameObject(
                $"{config.Id}_Backdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect =
                backdropObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            UILayerRoot.SetLayerRecursively(
                backdropObject,
                parent.gameObject.layer);
            UILayerRoot.Stretch(rect);

            Image image = backdropObject.GetComponent<Image>();
            image.color = config.BackdropColor;
            image.raycastTarget = true;

            if (config.BlurCloseOnClick)
            {
                Button button =
                    backdropObject.AddComponent<Button>();
                button.transition =
                    Selectable.Transition.None;
                button.onClick.AddListener(
                    () => closeRequested?.Invoke());
            }

            backdropObject.transform.SetAsLastSibling();
            return backdropObject;
        }

        /// <summary>
        /// 释放Backdrop。
        /// </summary>
        public void ReleaseBackdrop(GameObject backdrop)
        {
            if (backdrop != null)
            {
                UnityEngine.Object.Destroy(backdrop);
            }
        }
    }
}
