using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common.Utility
{
    [Serializable]
    public sealed class SerializedDictionary<TKey, TValue>
    {
        [Serializable]
        public struct Item
        {
            [VerticalGroup("键"), HideLabel]
            /// <summary>
            /// 公开的键数据。
            /// </summary>
            public TKey Key;

            [VerticalGroup("值"), HideLabel]
            /// <summary>
            /// 公开的值数据。
            /// </summary>
            public TValue Value;
        }

        /// <summary>
        /// 在 Inspector 中序列化的键值对列表，是运行时字典的数据来源。
        /// </summary>
        [TableList(AlwaysExpanded = true)]
        [LabelText("@title")]
        [SerializeField, OnValueChanged("@dirty = true")]
        [InfoBox("数据太多了!!!", InfoMessageType.Warning, "countTooMuch")]
        [InfoBox("@error", InfoMessageType.Error, "HasDuplicateKey")]
        private List<Item> items = new List<Item>();

        private Dictionary<TKey, TValue> data;

        private bool dirty = false;

#if UNITY_EDITOR
        [NonSerialized]
        /// <summary>
        /// 公开的title数据。
        /// </summary>
        public string title;

        [NonSerialized]
        /// <summary>
        /// 公开的warning数量数据。
        /// </summary>
        public int warningCount = 100000;

        private bool countTooMuch => items.Count >= warningCount;

        private HashSet<TKey> keys = new HashSet<TKey>();
        private string error;

        private bool HasDuplicateKey
        {
            get
            {
                keys.Clear();
                foreach (var item in items)
                {
                    if (keys.Add(item.Key))
                        continue;
                    error = $"Exist Duplicate Key({item.Key})";
                    return true;
                }

                return false;
            }
        }
#endif

        public Dictionary<TKey, TValue> Data
        {
            get
            {
                if (data == null)
                {
                    dirty = true;
                    data = new Dictionary<TKey, TValue>();
                }

                if (!dirty)
                    return data;

                dirty = false;
                data.Clear();
                foreach (var item in items)
                {
                    data[item.Key] = item.Value;
                }

                return data;
            }
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys => Data.Keys;

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => Data.GetEnumerator();

        /// <summary>
        /// 尝试获取值，并返回是否成功。
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value) => Data.TryGetValue(key, out value);

        /// <summary>
        /// 获取值Or默认值。
        /// </summary>
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default) => Data.GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// 执行清空相关逻辑。
        /// </summary>
        public void Clear()
        {
            items.Clear();
            Data.Clear();
        }

        /// <summary>
        /// 执行添加相关逻辑。
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            Data.Add(key, value);
            items.Add(new Item() { Key = key, Value = value });
        }

        /// <summary>
        /// 执行初始化从Dictionary相关逻辑。
        /// </summary>
        public void InitFromDictionary(Dictionary<TKey, TValue> dictionary)
        {
            dirty = true;
            items.Clear();
            foreach (var (key, value) in dictionary)
            {
                items.Add(new Item() { Key = key, Value = value });
            }
        }
    }
}
