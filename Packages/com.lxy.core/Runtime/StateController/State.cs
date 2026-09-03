using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class State
    {
        /// <summary>
        /// 公开的名称数据。
        /// </summary>
        public string Name;

        /// <summary>
        /// 公开的Note数据。
        /// </summary>
        public string Note;

        /// <summary>
        /// 获取Show名称。
        /// </summary>
        public string GetShowName()
        {
            return string.IsNullOrEmpty(Note) ? Name : $"{Note}({Name})";
        }
    }
}