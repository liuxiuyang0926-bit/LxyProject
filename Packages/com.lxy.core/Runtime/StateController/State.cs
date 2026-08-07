using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class State
    {
        public string Name;

        public string Note;

        public string GetShowName()
        {
            return string.IsNullOrEmpty(Note) ? Name : $"{Note}({Name})";
        }
    }
}