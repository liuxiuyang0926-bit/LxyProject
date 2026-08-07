using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Game.Common.Utility;
namespace Game
{
    public sealed class BindParam : MonoBehaviour
    {
        [SerializeField]
        [InlineProperty, HideLabel]
        private SerializedDictionary<string, string> bindParams = new()
        {
#if UNITY_EDITOR
            title = "绑定字典数据",
#endif
        };
        
        [LabelText("绑定独立数据")]
        [SerializeField]
        private List<string> BindParamList = new List<string>();

        public string GetBindData(string key)
        {
            return bindParams.GetValueOrDefault(key, "");
        }

        public string GetBindListData(int index)
        {
            if (BindParamList.Count >= index)
            {
                return BindParamList[index];
            }

            return "";
        }
    }
}
