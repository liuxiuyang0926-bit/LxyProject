using System;
using System.Collections.Generic;
using LuaObjectBind;
using UnityEngine;

namespace LxyDemo.UIFramework
{
    /// <summary>
    /// 面板逻辑与 Prefab 解耦。通过 UIManager.Register&lt;TLogic&gt; 注册具体逻辑类型。
    /// </summary>
    public abstract class UIPanelLogic
    {
        private UIManager owner;
        private GameObject gameObject;
        private bool initialized;
        private bool disposed;
        private bool visible;
        private readonly List<UIPanelLogic> embeddedLogics =
            new List<UIPanelLogic>();

        /// <summary>
        /// 向调用方提供Owner。
        /// </summary>
        public UIManager Owner => owner;
        /// <summary>
        /// 向调用方提供配置。
        /// </summary>
        public UIPanelConfig Config { get; private set; }
        /// <summary>
        /// 向调用方提供GameObject。
        /// </summary>
        public GameObject GameObject => gameObject;
        /// <summary>
        /// 向调用方提供Transform。
        /// </summary>
        public Transform Transform =>
            gameObject == null ? null : gameObject.transform;
        /// <summary>
        /// 向调用方提供RectTransform。
        /// </summary>
        public RectTransform RectTransform =>
            gameObject == null
                ? null
                : gameObject.transform as RectTransform;
        /// <summary>
        /// 指示当前对象是否已加载。
        /// </summary>
        public bool IsLoaded => gameObject != null;
        /// <summary>
        /// 指示Visible是否成立。
        /// </summary>
        public bool IsVisible => visible && gameObject != null;
        /// <summary>
        /// 指示Disposed是否成立。
        /// </summary>
        public bool IsDisposed => disposed;

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        internal void Initialize(
            UIManager manager,
            UIPanelConfig config,
            object userData)
        {
            if (initialized)
            {
                return;
            }

            owner = manager;
            Config = config;
            initialized = true;
            OnInitialize(userData);
        }

        /// <summary>
        /// 执行绑定游戏对象相关逻辑。
        /// </summary>
        internal void BindGameObject(GameObject instance)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            gameObject = instance != null
                ? instance
                : throw new ArgumentNullException(nameof(instance));
            OnBind();
        }

        /// <summary>
        /// 显示目标界面。
        /// </summary>
        internal void Show(object userData)
        {
            if (disposed || gameObject == null)
            {
                throw new InvalidOperationException(
                    $"面板逻辑 {GetType().Name} 尚未绑定或已销毁。");
            }

            visible = true;
            ShowEmbeddedLogics(userData);
            OnShow(userData);
        }

        /// <summary>
        /// 隐藏目标界面。
        /// </summary>
        internal void Hide(bool skipAnimation)
        {
            if (disposed || gameObject == null || !visible)
            {
                return;
            }

            OnHide(skipAnimation);
            HideEmbeddedLogics(skipAnimation);
            visible = false;
        }

        /// <summary>
        /// 执行DisposeLogic相关逻辑。
        /// </summary>
        internal void DisposeLogic()
        {
            if (disposed)
            {
                return;
            }

            if (visible)
            {
                try
                {
                    OnHide(true);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                HideEmbeddedLogics(true);

                visible = false;
            }

            if (gameObject != null)
            {
                try
                {
                    OnUnbind();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            DisposeEmbeddedLogics();

            gameObject = null;

            try
            {
                OnDispose();
            }
            finally
            {
                disposed = true;
                owner = null;
            }
        }

        public T GetComponent<T>() where T : Component
        {
            return gameObject == null
                ? null
                : gameObject.GetComponent<T>();
        }

        public T GetComponentInChildren<T>(bool includeInactive = true)
            where T : Component
        {
            return gameObject == null
                ? null
                : gameObject.GetComponentInChildren<T>(
                    includeInactive);
        }

        /// <summary>
        /// 执行查找相关逻辑。
        /// </summary>
        public Transform Find(string path)
        {
            return Transform == null ? null : Transform.Find(path);
        }

        /// <summary>
        /// 关闭Self。
        /// </summary>
        protected void CloseSelf(bool forceDestroy = false)
        {
            owner?.ClosePanel(Config.Id, forceDestroy);
        }

        protected TLogic CreateEmbeddedLogic<TLogic>(
            ObjectBinder objectBinder)
            where TLogic : UIPanelLogic, new()
        {
            if (objectBinder == null)
            {
                throw new MissingReferenceException(
                    $"{GetType().Name} 的子 Logic ObjectBinder 为空。");
            }

            if (!initialized || Config == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} 尚未初始化，无法创建子 Logic。");
            }

            var logic = new TLogic();
            try
            {
                logic.Initialize(owner, Config, null);
                logic.BindGameObject(objectBinder.gameObject);
                embeddedLogics.Add(logic);
                return logic;
            }
            catch
            {
                if (logic.initialized)
                {
                    logic.DisposeLogic();
                }

                throw;
            }
        }

        /// <summary>
        /// 释放EmbeddedLogic。
        /// </summary>
        protected void ReleaseEmbeddedLogic(
            UIPanelLogic logic)
        {
            if (logic == null)
            {
                return;
            }

            embeddedLogics.Remove(logic);
            logic.DisposeLogic();
        }

        /// <summary>
        /// 执行显示EmbeddedLogics相关逻辑。
        /// </summary>
        private void ShowEmbeddedLogics(object userData)
        {
            for (int index = 0;
                 index < embeddedLogics.Count;
                 index++)
            {
                UIPanelLogic logic = embeddedLogics[index];
                if (logic != null && !logic.IsDisposed)
                {
                    logic.Show(userData);
                }
            }
        }

        /// <summary>
        /// 执行隐藏EmbeddedLogics相关逻辑。
        /// </summary>
        private void HideEmbeddedLogics(bool skipAnimation)
        {
            for (int index = embeddedLogics.Count - 1;
                 index >= 0;
                 index--)
            {
                UIPanelLogic logic = embeddedLogics[index];
                if (logic == null || logic.IsDisposed)
                {
                    continue;
                }

                try
                {
                    logic.Hide(skipAnimation);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>
        /// 执行DisposeEmbeddedLogics相关逻辑。
        /// </summary>
        private void DisposeEmbeddedLogics()
        {
            for (int index = embeddedLogics.Count - 1;
                 index >= 0;
                 index--)
            {
                UIPanelLogic logic = embeddedLogics[index];
                try
                {
                    logic?.DisposeLogic();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            embeddedLogics.Clear();
        }

        /// <summary>
        /// 响应Initialize事件。
        /// </summary>
        protected virtual void OnInitialize(object userData)
        {
        }

        /// <summary>
        /// 响应绑定事件。
        /// </summary>
        protected virtual void OnBind()
        {
        }

        /// <summary>
        /// 响应Show事件。
        /// </summary>
        protected virtual void OnShow(object userData)
        {
        }

        /// <summary>
        /// 响应Hide事件。
        /// </summary>
        protected virtual void OnHide(bool skipAnimation)
        {
        }

        /// <summary>
        /// 响应Unbind事件。
        /// </summary>
        protected virtual void OnUnbind()
        {
        }

        /// <summary>
        /// 响应Dispose事件。
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        /// <summary>
        /// UICodeBinder 为 Button 等无参事件生成的统一入口。
        /// </summary>
        protected virtual void OnGeneratedClick(string bindingName)
        {
        }

        /// <summary>
        /// UICodeBinder 为 Toggle、Slider、InputField 等带值事件生成的统一入口。
        /// </summary>
        protected virtual void OnGeneratedValueChanged(
            string bindingName,
            object value)
        {
        }
    }

    public sealed class UIDefaultPanelLogic : UIPanelLogic
    {
    }
}
