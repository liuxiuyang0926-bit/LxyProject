using System;
using System.Collections.Generic;

namespace LxyDemo.UIFramework
{
    internal sealed class UIStackEntry
    {
        public UIPanelConfig Config;
        public object UserData;
    }

    internal readonly struct UIStackPushResult
    {
        public UIStackPushResult(UIPanelConfig previousTop)
        {
            PreviousTop = previousTop;
        }

        public UIPanelConfig PreviousTop { get; }
    }

    internal readonly struct UIStackPopResult
    {
        public UIStackPopResult(
            bool found,
            bool wasTop,
            UIStackEntry newTop)
        {
            Found = found;
            WasTop = wasTop;
            NewTop = newTop;
        }

        public bool Found { get; }
        public bool WasTop { get; }
        public UIStackEntry NewTop { get; }
    }

    internal sealed class UIStackCoordinator
    {
        public UIStackPushResult Push(
            List<UIStackEntry> stack,
            UIPanelConfig config,
            object userData)
        {
            if (config == null || config.IgnoreStack)
            {
                return new UIStackPushResult(null);
            }

            UIPanelConfig previousTop =
                stack.Count > 0
                    ? stack[stack.Count - 1].Config
                    : null;

            int existingIndex = FindIndex(stack, config.Id);
            if (existingIndex >= 0 &&
                existingIndex == stack.Count - 1)
            {
                stack[existingIndex].UserData = userData;
                return new UIStackPushResult(null);
            }

            if (existingIndex >= 0)
            {
                stack.RemoveAt(existingIndex);
            }

            stack.Add(new UIStackEntry
            {
                Config = config,
                UserData = userData
            });

            return new UIStackPushResult(
                previousTop != null &&
                !string.Equals(
                    previousTop.Id,
                    config.Id,
                    StringComparison.Ordinal)
                    ? previousTop
                    : null);
        }

        public UIStackPopResult Pop(
            List<UIStackEntry> stack,
            UIPanelConfig config,
            bool isClear)
        {
            if (config == null ||
                config.IgnoreStack ||
                isClear ||
                stack.Count == 0)
            {
                return new UIStackPopResult(false, false, null);
            }

            int index = FindIndex(stack, config.Id);
            if (index < 0)
            {
                return new UIStackPopResult(false, false, null);
            }

            bool wasTop = index == stack.Count - 1;
            stack.RemoveAt(index);
            UIStackEntry newTop =
                wasTop && stack.Count > 0
                    ? stack[stack.Count - 1]
                    : null;
            return new UIStackPopResult(true, wasTop, newTop);
        }

        public bool Remove(
            List<UIStackEntry> stack,
            string panelId)
        {
            int index = FindIndex(stack, panelId);
            if (index < 0)
            {
                return false;
            }

            stack.RemoveAt(index);
            return true;
        }

        public bool Contains(
            List<UIStackEntry> stack,
            string panelId)
        {
            return FindIndex(stack, panelId) >= 0;
        }

        public UIStackEntry GetTop(List<UIStackEntry> stack)
        {
            return stack.Count == 0
                ? null
                : stack[stack.Count - 1];
        }

        private static int FindIndex(
            List<UIStackEntry> stack,
            string panelId)
        {
            for (int index = 0; index < stack.Count; index++)
            {
                if (string.Equals(
                        stack[index].Config.Id,
                        panelId,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
