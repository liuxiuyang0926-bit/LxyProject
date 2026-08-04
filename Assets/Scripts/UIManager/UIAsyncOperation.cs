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

        public override bool keepWaiting => !isDone;
        public bool IsDone => isDone;
        public bool IsSucceeded => isDone && exception == null;
        public bool IsCancellationRequested => cancellationRequested;
        public bool IsCancelled =>
            exception is UIFrameworkException frameworkException &&
            frameworkException.ErrorCode ==
            UIFrameworkErrorCode.OperationCancelled;
        public float Progress => isDone ? 1f : progress;
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

        public void Dispose()
        {
            Cancel();
        }

        public bool TryGetResult(out TResult value)
        {
            value = IsSucceeded ? result : default;
            return IsSucceeded;
        }

        internal void SetProgress(float value)
        {
            if (!isDone)
            {
                progress = Mathf.Clamp01(value);
            }
        }

        internal void Succeed(TResult value)
        {
            if (isDone)
            {
                return;
            }

            result = value;
            Finish(null);
        }

        internal void Fail(Exception error)
        {
            if (isDone)
            {
                return;
            }

            Finish(error ?? new Exception("未知 UI 框架错误。"));
        }

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
