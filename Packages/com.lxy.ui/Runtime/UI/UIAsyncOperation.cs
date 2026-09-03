using System;
using UnityEngine;

namespace LxyDemo.UIFramework
{
    public sealed class UIAsyncOperation<TResult> :
        CustomYieldInstruction,
        IDisposable
    {
        private TResult result;
        private Exception exception;
        private bool isDone;
        private bool cancellationRequested;
        private float progress;
        private Action<UIAsyncOperation<TResult>> completed;

        /// <summary>
        /// 向调用方提供keepWaiting。
        /// </summary>
        public override bool keepWaiting => !isDone;
        /// <summary>
        /// 指示当前操作是否已完成。
        /// </summary>
        public bool IsDone => isDone;
        /// <summary>
        /// 指示Succeeded是否成立。
        /// </summary>
        public bool IsSucceeded => isDone && exception == null;
        /// <summary>
        /// 指示CancellationRequested是否成立。
        /// </summary>
        public bool IsCancellationRequested => cancellationRequested;
        /// <summary>
        /// 指示Cancelled是否成立。
        /// </summary>
        public bool IsCancelled =>
            exception is UIFrameworkException frameworkException &&
            frameworkException.ErrorCode ==
            UIFrameworkErrorCode.OperationCancelled;
        /// <summary>
        /// 当前操作的归一化进度，取值范围为 0 到 1。
        /// </summary>
        public float Progress => isDone ? 1f : progress;
        /// <summary>
        /// 向调用方提供Exception。
        /// </summary>
        public Exception Exception => exception;

        public TResult Result
        {
            get
            {
                if (!isDone)
                {
                    throw new InvalidOperationException(
                        "UI 异步操作尚未完成。");
                }

                if (exception != null)
                {
                    throw exception;
                }

                return result;
            }
        }

        public event Action<UIAsyncOperation<TResult>> Completed
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                if (isDone)
                {
                    InvokeCallback(value);
                }
                else
                {
                    completed += value;
                }
            }
            remove => completed -= value;
        }

        /// <summary>
        /// 执行取消相关逻辑。
        /// </summary>
        public void Cancel()
        {
            if (isDone)
            {
                return;
            }

            cancellationRequested = true;
            Fail(new UIFrameworkException(
                UIFrameworkErrorCode.OperationCancelled,
                "UI 操作已取消。"));
        }

        /// <summary>
        /// 释放当前实例持有的资源。
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }

        /// <summary>
        /// 尝试获取结果，并返回是否成功。
        /// </summary>
        public bool TryGetResult(out TResult value)
        {
            value = IsSucceeded ? result : default;
            return IsSucceeded;
        }

        /// <summary>
        /// 设置进度。
        /// </summary>
        internal void SetProgress(float value)
        {
            if (!isDone)
            {
                progress = Mathf.Clamp01(value);
            }
        }

        /// <summary>
        /// 标记当前操作成功。
        /// </summary>
        internal void Succeed(TResult value)
        {
            if (isDone)
            {
                return;
            }

            result = value;
            Finish(null);
        }

        /// <summary>
        /// 执行标记失败相关逻辑。
        /// </summary>
        internal void Fail(Exception error)
        {
            if (isDone)
            {
                return;
            }

            Finish(error ?? new Exception("未知 UI 框架错误。"));
        }

        /// <summary>
        /// 执行Finish相关逻辑。
        /// </summary>
        private void Finish(Exception error)
        {
            exception = error;
            progress = 1f;
            isDone = true;

            Action<UIAsyncOperation<TResult>> callbacks = completed;
            completed = null;
            if (callbacks == null)
            {
                return;
            }

            foreach (Delegate callback in callbacks.GetInvocationList())
            {
                InvokeCallback(
                    (Action<UIAsyncOperation<TResult>>)callback);
            }
        }

        /// <summary>
        /// 调用回调。
        /// </summary>
        private void InvokeCallback(
            Action<UIAsyncOperation<TResult>> callback)
        {
            try
            {
                callback(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
