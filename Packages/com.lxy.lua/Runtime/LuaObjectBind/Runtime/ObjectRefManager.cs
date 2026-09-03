using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;


namespace LuaObjectBind
{
    public class ObjectRefManager
    {
        static Dictionary<int, WeakReference<Object>> _objects = new Dictionary<int, WeakReference<Object>>();

        /// <summary>
        /// 获取对象Id。
        /// </summary>
        public static int GetObjectId(Object obj)
        {
            return obj.GetHashCode();
        }

        /// <summary>
        /// 执行记录对象相关逻辑。
        /// </summary>
        public static int RecordObject(Object obj)
        {
            if (obj == null) return 0;
            int hash = GetObjectId(obj);
            if (_objects.ContainsKey(hash))
            {
                _objects[hash] = new WeakReference<Object>(obj);
            }
            else
            {
                _objects.Add(hash, new WeakReference<Object>(obj));
            }

            return hash;
        }

        /// <summary>
        /// 获取对象。
        /// </summary>
        public static Object GetObject(int hash)
        {
            if (_objects.ContainsKey(hash))
            {
                WeakReference<Object> weakRef = _objects[hash];
                if (weakRef.TryGetTarget(out Object obj))
                {
                    return obj;
                }
                else
                {
                    _objects.Remove(hash);
                }
            }

            return null;
        }
        
        /// <summary>
        /// 移除对象。
        /// </summary>
        public static void RemoveObject(int hash)
        {
            if (_objects.ContainsKey(hash))
            {
                _objects.Remove(hash);
            }
        }
        
        /// <summary>
        /// 清空全部Empty。
        /// </summary>
        public static void ClearAllEmpty()
        {
            List<int> keysToRemove = ListPool<int>.Get();
            foreach (var kvp in _objects)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _objects.Remove(key);
            }
            ListPool<int>.Release(keysToRemove);
        }

        // Roslyn Auto Gen - ResetHandler
#if UNITY_EDITOR
        /// <summary>
        /// 执行对象Ref管理器重置处理器相关逻辑。
        /// </summary>
        [UnityEditor.InitializeOnEnterPlayMode]
        static void ObjectRefManager_ResetHandler(UnityEditor.EnterPlayModeOptions options)
        {
            if (!options.HasFlag(UnityEditor.EnterPlayModeOptions.DisableDomainReload))
                return;
            _objects = new Dictionary<int, WeakReference<Object>>();
        }
#endif
    }
}