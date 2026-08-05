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

        public UIManager Owner => owner;
        public UIPanelConfig Config { get; private set; }
        public GameObject GameObject => gameObject;
        public Transform Transform =>
            gameObject == null ? null : gameObject.transform;
        public RectTransform RectTransform =>
            gameObject == null
                ? null
                : gameObject.transform as RectTransform;
        public bool IsLoaded => gameObject != null;
        public bool IsVisible => visible && gameObject != null;
        public bool IsDisposed => disposed;

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

        public Transform Find(string path)
        {
            return Transform == null ? null : Transform.Find(path);
        }

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

        protected virtual void OnInitialize(object userData)
        {
        }

        protected virtual void OnBind()
        {
        }

        protected virtual void OnShow(object userData)
        {
        }

        protected virtual void OnHide(bool skipAnimation)
        {
        }

        protected virtual void OnUnbind()
        {
        }

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
